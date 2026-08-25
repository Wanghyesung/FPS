using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                WeaponSystem
목적 : AK/TRG 2슬롯의 발사·재장전·탄퍼짐 계산을 담당하는 System.
       기획 §4.2대로 히트스캔(Raycast)이며, 미조준 시에는 조준점 기준 무기별 콘
       안에서 매 발마다 방향을 재추첨하고, 조준+정지 시에는 카메라 정면 그대로 쏜다.
       입력/카메라를 전혀 알지 못하고 InputView가 호출한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class WeaponSystem : MonoBehaviour
{
    private const int INSTANT_KILL_DAMAGE = 9999;

    public static event Action<int, int> OnAmmoChanged;
    public static event Action<Vector3, Vector3> OnMuzzleFlash;

    [SerializeField] private WeaponDefinition m_refAkDefinition;
    [SerializeField] private WeaponDefinition m_refTrgDefinition;
    [SerializeField] private Camera m_refAimCamera;
    [SerializeField] private Transform m_refWeaponSocket;
    [SerializeField] private Transform m_refMuzzleTransform;
    [SerializeField] private LayerMask m_maskHittable = ~0;

    // 나중에 MonsterAgent가 같은 컴포넌트를 재사용할 때를 위한 설정 —
    // 카메라 대신 자기 조준 트랜스폼을 쓰고, 플레이어 HUD용 정적 이벤트는 발행하지 않는다.
    [SerializeField] private Transform m_refAimSourceOverride;
    [SerializeField] private bool m_bBroadcastAmmoEvents = true;
    [SerializeField] private bool m_bOwnsAkAtStart = true;
    [SerializeField] private bool m_bOwnsTrgAtStart = true;

    private readonly WeaponModel m_modelAk = new WeaponModel();
    private readonly WeaponModel m_modelTrg = new WeaponModel();

    // 3인칭에서는 카메라가 몸 뒤에 있어 자기 자신이 먼저 맞는다.
    // RaycastNonAlloc + 자기 계층 스킵으로 처리하기 위한 사전 할당 버퍼(performance.md).
    private readonly RaycastHit[] m_tHitBuffer = new RaycastHit[8];

    private Transform m_refSelfRoot;
    private PlayerSystem m_refPlayer;
    private GameObject m_refAkWorldModel;
    private GameObject m_refTrgWorldModel;
    private CancellationTokenSource m_ctsReload;

    private WeaponSlot m_eActiveSlot = WeaponSlot.AK;
    private int m_iHeadLayer = -1;
    private bool m_bIsAiming;
    private bool m_bFireHeld;
    private bool m_bFireTriggered;
    private bool m_bOwnsAk;
    private bool m_bOwnsTrg;

    public WeaponSlot ActiveSlot => m_eActiveSlot;
    public string ActiveWeaponName => ActiveDefinition != null ? ActiveDefinition.DisplayName : "None";
    public int CurrentAmmoInMag => ActiveModel.CurrentAmmoInMag;
    public int ReserveAmmo => ActiveModel.ReserveAmmo;
    public bool HasAnyWeapon => m_bOwnsAk || m_bOwnsTrg;

    // 미소지 슬롯은 정의를 null로 취급해 발사/월드모델/HUD 표기를 한 곳에서 차단한다
    private WeaponDefinition ActiveDefinition => GetDefinition(m_eActiveSlot);
    private WeaponModel ActiveModel => m_eActiveSlot == WeaponSlot.AK ? m_modelAk : m_modelTrg;

    private void Awake()
    {
        m_refSelfRoot = transform;
        m_refPlayer = GetComponent<PlayerSystem>();

        if (m_refAimCamera == null)
        {
            m_refAimCamera = Camera.main;
        }

        m_iHeadLayer = LayerMask.NameToLayer("Head");

        m_bOwnsAk = m_bOwnsAkAtStart && m_refAkDefinition != null;
        m_bOwnsTrg = m_bOwnsTrgAtStart && m_refTrgDefinition != null;

        if (m_bOwnsAk)
        {
            FillMagazine(m_refAkDefinition, m_modelAk, m_refAkDefinition != null ? m_refAkDefinition.MaxReserveAmmo : 0);
        }

        if (m_bOwnsTrg)
        {
            FillMagazine(m_refTrgDefinition, m_modelTrg, m_refTrgDefinition != null ? m_refTrgDefinition.MaxReserveAmmo : 0);
        }

        if (!m_bOwnsAk && m_bOwnsTrg)
        {
            m_eActiveSlot = WeaponSlot.TRG;
        }

        SpawnWorldModels();
        ApplyWorldModelVisibility();
    }

    private void Start()
    {
        RaiseAmmoChanged();
    }

    private void Update()
    {
        // 사망한 소유자는 발사할 수 없다 — InputView는 사망 후에도 활성 상태로 남아
        // Fire를 누르고 있으면 m_bFireHeld가 true로 유지되기 때문(매치 종료 후 사격 방지)
        if (m_refPlayer != null && m_refPlayer.IsDead)
        {
            m_bFireTriggered = false;
            return;
        }

        WeaponDefinition def = ActiveDefinition;
        if (def == null)
        {
            m_bFireTriggered = false;
            return;
        }

        bool bWantsFire = def.IsFullAuto ? m_bFireHeld : m_bFireTriggered;
        if (bWantsFire)
        {
            bool bIsMoving = m_refPlayer != null && m_refPlayer.IsMoving;
            bool bIsJumping = m_refPlayer != null && !m_refPlayer.IsGrounded;
            TryFire(m_refAimCamera, m_bIsAiming, bIsMoving, bIsJumping);
        }

        m_bFireTriggered = false;
    }

    private void OnDestroy()
    {
        CancelReload();
    }

    public void SetActiveSlot(WeaponSlot _eSlot)
    {
        if (m_eActiveSlot == _eSlot || !HasWeapon(_eSlot))
        {
            return; // 소지하지 않은 슬롯으로는 전환하지 않는다(루팅 전 빈손 상태)
        }

        CancelReload();
        ActiveModel.IsReloading = false;

        m_eActiveSlot = _eSlot;
        m_bFireTriggered = false;

        ApplyWorldModelVisibility();
        RaiseAmmoChanged();
    }

    public void SetAiming(bool _bIsAiming)
    {
        m_bIsAiming = _bIsAiming;
    }

    public void SetFireHeld(bool _bHeld)
    {
        if (_bHeld && !m_bFireHeld)
        {
            m_bFireTriggered = true;
        }

        m_bFireHeld = _bHeld;
    }

    /// <summary>이미 소지 중인 무기인지 — 루팅 시 "예비탄만 흡수" 분기에 사용(기획 §4.3).</summary>
    public bool HasWeapon(WeaponSlot _eSlot)
    {
        return _eSlot == WeaponSlot.AK ? m_bOwnsAk : m_bOwnsTrg;
    }

    /// <summary>루팅으로 새 무기를 획득한다. 이미 소지 중이면 false.</summary>
    public bool GiveWeapon(WeaponSlot _eSlot, WeaponDefinition _refDefinition)
    {
        if (HasWeapon(_eSlot))
        {
            return false;
        }

        if (_eSlot == WeaponSlot.AK)
        {
            if (_refDefinition != null)
            {
                m_refAkDefinition = _refDefinition;
            }

            if (m_refAkDefinition == null)
            {
                return false;
            }

            m_bOwnsAk = true;
            FillMagazine(m_refAkDefinition, m_modelAk, m_refAkDefinition.MagazineSize);
        }
        else
        {
            if (_refDefinition != null)
            {
                m_refTrgDefinition = _refDefinition;
            }

            if (m_refTrgDefinition == null)
            {
                return false;
            }

            m_bOwnsTrg = true;
            FillMagazine(m_refTrgDefinition, m_modelTrg, m_refTrgDefinition.MagazineSize);
        }

        if (GetDefinition(m_eActiveSlot) == null)
        {
            m_eActiveSlot = _eSlot; // 빈손이었다면 주운 무기를 바로 든다
        }

        SpawnWorldModels();
        ApplyWorldModelVisibility();
        RaiseAmmoChanged();
        return true;
    }

    /// <summary>탄약 픽업 — 예비탄 상한 초과분은 버려진다(기획 §5.2). 이미 상한이면 false.</summary>
    public bool AddReserveAmmo(WeaponSlot _eSlot, int _iAmount)
    {
        if (_iAmount <= 0)
        {
            return false;
        }

        WeaponDefinition def = _eSlot == WeaponSlot.AK ? m_refAkDefinition : m_refTrgDefinition;
        WeaponModel model = _eSlot == WeaponSlot.AK ? m_modelAk : m_modelTrg;

        if (def == null || model.ReserveAmmo >= def.MaxReserveAmmo)
        {
            return false;
        }

        model.ReserveAmmo = Mathf.Min(def.MaxReserveAmmo, model.ReserveAmmo + _iAmount);

        if (_eSlot == m_eActiveSlot)
        {
            RaiseAmmoChanged();
        }

        return true;
    }

    public bool TryFire(Camera _refAimCamera, bool _bIsAiming, bool _bIsMoving, bool _bIsJumping)
    {
        Camera refCam = _refAimCamera != null ? _refAimCamera : m_refAimCamera;
        Transform refSource = m_refAimSourceOverride != null ? m_refAimSourceOverride : (refCam != null ? refCam.transform : null);
        return TryFire(refSource, _bIsAiming, _bIsMoving, _bIsJumping);
    }

    /// <summary>봇/플레이어 공용 발사 진입점 — 조준 원점과 방향을 Transform 하나로 받는다.</summary>
    public bool TryFire(Transform _refAimSource, bool _bIsAiming, bool _bIsMoving, bool _bIsJumping)
    {
        WeaponDefinition def = ActiveDefinition;
        WeaponModel model = ActiveModel;

        if (def == null || _refAimSource == null)
        {
            return false;
        }

        if (model.IsReloading)
        {
            return false;
        }

        if (model.CurrentAmmoInMag <= 0)
        {
            return false; // 인수조건 #12 — 빈 탄창은 발사 입력을 받아도 발사되지 않는다
        }

        if (Time.time < model.NextFireReadyTime)
        {
            return false;
        }

        Vector3 vOrigin = _refAimSource.position;
        Vector3 vDir = ResolveShotDirection(_refAimSource, def, _bIsAiming, _bIsMoving, _bIsJumping);

        model.CurrentAmmoInMag--;
        model.NextFireReadyTime = Time.time + def.GetFireInterval();
        RaiseAmmoChanged();

        Vector3 vMuzzlePos = m_refMuzzleTransform != null ? m_refMuzzleTransform.position : vOrigin + vDir * 0.5f;
        OnMuzzleFlash?.Invoke(vMuzzlePos, vDir);

        ResolveHit(vOrigin, vDir, def);
        return true;
    }

    private void ResolveHit(Vector3 _vOrigin, Vector3 _vDir, WeaponDefinition _def)
    {
        int iCount = Physics.RaycastNonAlloc(_vOrigin, _vDir, m_tHitBuffer, _def.EffectiveRange, m_maskHittable, QueryTriggerInteraction.Ignore);

        int iBestIndex = -1;
        float fBestDistance = float.MaxValue;

        for (int i = 0; i < iCount; i++)
        {
            Transform refHitTr = m_tHitBuffer[i].collider.transform;
            if (m_refSelfRoot != null && refHitTr.IsChildOf(m_refSelfRoot))
            {
                continue; // 사수 본인의 캡슐/머리 히트박스는 무시
            }

            if (m_tHitBuffer[i].distance < fBestDistance)
            {
                fBestDistance = m_tHitBuffer[i].distance;
                iBestIndex = i;
            }
        }

        if (iBestIndex < 0)
        {
            return;
        }

        Collider refCollider = m_tHitBuffer[iBestIndex].collider;
        bool bIsHeadshot = m_iHeadLayer >= 0 && refCollider.gameObject.layer == m_iHeadLayer;

        IDamageable refDamageable = refCollider.GetComponentInParent<IDamageable>();
        if (refDamageable != null)
        {
            refDamageable.TakeDamage(ResolveDamage(_def, bIsHeadshot), bIsHeadshot);
        }
    }

    public void Reload()
    {
        WeaponDefinition def = ActiveDefinition;
        WeaponModel model = ActiveModel;

        if (def == null || model.IsReloading)
        {
            return;
        }

        if (model.CurrentAmmoInMag >= def.MagazineSize || model.ReserveAmmo <= 0)
        {
            return;
        }

        ReloadAsync(m_eActiveSlot, def, model).Forget();
    }

    private async UniTaskVoid ReloadAsync(WeaponSlot _eSlot, WeaponDefinition _def, WeaponModel _model)
    {
        CancelReload();
        m_ctsReload = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        CancellationToken token = m_ctsReload.Token;

        _model.IsReloading = true;

        bool bCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_def.ReloadTime), cancellationToken: token)
            .SuppressCancellationThrow();

        _model.IsReloading = false;

        if (bCanceled)
        {
            return;
        }

        int iNeeded = _def.MagazineSize - _model.CurrentAmmoInMag;
        int iLoaded = Mathf.Min(iNeeded, _model.ReserveAmmo);
        _model.CurrentAmmoInMag += iLoaded;
        _model.ReserveAmmo -= iLoaded;

        if (_eSlot == m_eActiveSlot)
        {
            RaiseAmmoChanged();
        }
    }

    private Vector3 ResolveShotDirection(Transform _refCamTr, WeaponDefinition _def, bool _bIsAiming, bool _bIsMoving, bool _bIsJumping)
    {
        float fSpread = ResolveSpread(_def, _bIsAiming, _bIsMoving, _bIsJumping);
        Vector3 vForward = _refCamTr.forward;

        if (fSpread <= 0f)
        {
            return vForward; // 인수조건 #3 — TRG 조준+정지는 분산 0, 정중앙 명중
        }

        // 콘 반경을 화면 평면상의 반지름으로 환산해 무작위 방향을 재추첨한다 (구조체 연산, 힙 할당 없음)
        float fRadius = Mathf.Tan(fSpread * Mathf.Deg2Rad);
        Vector2 vRand = UnityEngine.Random.insideUnitCircle * fRadius;
        Vector3 vDir = vForward + _refCamTr.right * vRand.x + _refCamTr.up * vRand.y;
        return vDir.normalized;
    }

    private float ResolveSpread(WeaponDefinition _def, bool _bIsAiming, bool _bIsMoving, bool _bIsJumping)
    {
        if (_bIsAiming)
        {
            return _bIsMoving || _bIsJumping ? _def.SpreadAdsMove : _def.SpreadAdsStand;
        }

        if (_bIsJumping)
        {
            return _def.SpreadHipJump;
        }

        return _bIsMoving ? _def.SpreadHipMove : _def.SpreadHipStand;
    }

    private int ResolveDamage(WeaponDefinition _def, bool _bIsHeadshot)
    {
        if (!_bIsHeadshot)
        {
            return _def.BodyDamage;
        }

        if (_def.HeadshotIsInstantKill)
        {
            return INSTANT_KILL_DAMAGE;
        }

        return _def.HeadshotDamage > 0 ? _def.HeadshotDamage : _def.BodyDamage;
    }

    private void CancelReload()
    {
        if (m_ctsReload == null)
        {
            return;
        }

        m_ctsReload.Cancel();
        m_ctsReload.Dispose();
        m_ctsReload = null;
    }

    private void FillMagazine(WeaponDefinition _def, WeaponModel _model, int _iReserve)
    {
        if (_def == null)
        {
            return;
        }

        _model.CurrentAmmoInMag = _def.MagazineSize;
        _model.ReserveAmmo = Mathf.Clamp(_iReserve, 0, _def.MaxReserveAmmo);
        _model.IsReloading = false;
        _model.NextFireReadyTime = 0f;
    }

    private void SpawnWorldModels()
    {
        if (m_refWeaponSocket == null)
        {
            return;
        }

        // 소지 중인 슬롯만, 그리고 아직 만들지 않았을 때만 생성한다(GiveWeapon에서 재호출되므로 멱등)
        if (m_bOwnsAk && m_refAkWorldModel == null)
        {
            m_refAkWorldModel = SpawnWorldModel(m_refAkDefinition);
        }

        if (m_bOwnsTrg && m_refTrgWorldModel == null)
        {
            m_refTrgWorldModel = SpawnWorldModel(m_refTrgDefinition);
        }
    }

    private GameObject SpawnWorldModel(WeaponDefinition _def)
    {
        if (_def == null || _def.WorldModelPrefab == null)
        {
            return null;
        }

        GameObject refGo = Instantiate(_def.WorldModelPrefab, m_refWeaponSocket, false);
        refGo.transform.localPosition = Vector3.zero;
        refGo.transform.localRotation = Quaternion.identity;
        return refGo;
    }

    private void ApplyWorldModelVisibility()
    {
        if (m_refAkWorldModel != null)
        {
            m_refAkWorldModel.SetActive(m_bOwnsAk && m_eActiveSlot == WeaponSlot.AK);
        }

        if (m_refTrgWorldModel != null)
        {
            m_refTrgWorldModel.SetActive(m_bOwnsTrg && m_eActiveSlot == WeaponSlot.TRG);
        }
    }

    private void RaiseAmmoChanged()
    {
        if (!m_bBroadcastAmmoEvents)
        {
            return; // 봇의 탄약 변화가 플레이어 HUD를 덮어쓰지 않도록 차단
        }

        WeaponModel model = ActiveModel;
        OnAmmoChanged?.Invoke(model.CurrentAmmoInMag, model.ReserveAmmo);
    }

    private WeaponDefinition GetDefinition(WeaponSlot _eSlot)
    {
        if (_eSlot == WeaponSlot.AK)
        {
            return m_bOwnsAk ? m_refAkDefinition : null;
        }

        return m_bOwnsTrg ? m_refTrgDefinition : null;
    }
}
