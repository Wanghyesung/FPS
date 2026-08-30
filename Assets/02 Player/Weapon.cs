using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class Weapon : MonoBehaviour
{

    public enum eWeaponType
    {
        None,
        Bullet,
        Trace,
        MissileBullet,
        Missile,
        Laser,
        ShotGun,
        End,
    }


    [SerializeField] private SOAttackInfo m_SOAttackInfo;

    private AttackInfo m_refAttackInfo;

    public int SetMaxAttackCount
    {
        get { return m_refAttackInfo.MaxHitCount; }
        set { m_refAttackInfo.MaxHitCount = value; }
    }

    [SerializeField] private Transform m_refFireTr = null;
    [SerializeField] private ParticleSystem m_refEffectObject;

    [SerializeField] private Transform m_refRightHandGripTr; // 오른손이 닿아야 할 그립 포인트 — Player가 무기를 소켓에 배치할 때 참조
    [SerializeField] private Transform m_refLeftHandGripTr;  // 왼손 IK가 잡아야 할 그립 포인트 — WeaponRigTarget이 참조
    [SerializeField] private Transform m_refZoomTr;  // 조준경(ADS) 지점 — WeaponAimDriver가 조준점 정렬의 기준으로 참조
    public Transform RightHandGripTr => m_refRightHandGripTr;
    public Transform LeftHandGripTr => m_refLeftHandGripTr;
    public Transform ZoomTr => m_refZoomTr;
    public Transform FireTr => m_refFireTr; // WeaponAimDriver가 ZoomTr 없는 무기에 대해 대신 참조하는 폴백


    private WeaponRecoilKick m_refRecoilKick; // 사격 시 순수 연출용 스프링 반동 — RecoilPivot(자식)에 붙어있음, 없으면 조용히 생략(선택 컴포넌트)
    private WeaponAimDriver m_refAimDriver;   // 조준경/총구를 조준점으로 상시 정렬 — 없으면 조용히 생략(선택 컴포넌트)

    private float m_fFireTime = 0.2f;
    private float m_fBaseCooldown = 0.2f;
    private float m_fLastFireTime = -Mathf.Infinity;

    private eWeaponType m_eWeapoonType = eWeaponType.None;
    public eWeaponType WeaponType => m_eWeapoonType;

    public PoolObject FireBulletPrefab => m_SOAttackInfo.PoolPrefab;

    [Header("Weapon Option")]
    [SerializeField] private bool m_bLookTarget = true;

    [Header("Inaccuracy")]
    [SerializeField] private float m_fInaccuracyAngle = 0f; // 조준 방향에서 좌우/상하로 흔들리는 오차 각도

    [Header("Circular Sector Shot")]
    [SerializeField] private int m_iBulletCount = 1; // 1이면 기존처럼 단발
    [SerializeField] private float m_fSpreadAngle = 30f; // 부채꼴(원뿔) 전체 각도

    private const float GOLDEN_ANGLE_DEG = 137.50776f;

    // Player.PickupWeapon()이 무기를 손에 넣는 시점에 호출한다 — 무기는 기본적으로
    // 아무도 소지하지 않은 상태(월드에 놓인 상태)로 존재하므로, Awake/Start가 아니라
    // 픽업 시점에만 초기화된다.
    public void Init()
    {

        m_refAttackInfo = m_SOAttackInfo.MakeAttackInfo();
        m_refAttackInfo.Owner = gameObject.transform;
        m_eWeapoonType = m_SOAttackInfo.WeaponType;
        m_fBaseCooldown = m_refAttackInfo.CoolDown;

        m_fFireTime = m_refAttackInfo.CoolDown;
        m_fLastFireTime = Time.time;

        // TakeWeapon()이 그립 정렬을 이미 마친 뒤(Player.EquipWeapon()에서 Init()을 그
        // 다음에 호출함) — 이 시점의 로컬 포즈를 반동 스프링의 "원점"으로 캡처해야 한다.
        // WeaponRecoilKick은 무기 루트가 아니라 자식인 RecoilPivot에 붙어있다 —
        // WeaponAimDriver가 매 프레임 덮어쓰는 루트 회전과 반동이 서로 기준 포즈를
        // 덮어쓰며 싸우지 않도록 계층으로 분리했기 때문에 GetComponentInChildren로 찾는다.
        // includeInactive: true — RecoilPivot이나 무기 오브젝트가 아직 비활성인 상태로 픽업/초기화되는
        // 경우(월드에 비활성으로 놓여있다가 주워지는 무기)에도 반동이 조용히 사라지지 않도록 한다.
        m_refRecoilKick = GetComponentInChildren<WeaponRecoilKick>(true);
        if (m_refRecoilKick != null)
            m_refRecoilKick.CaptureBasePose();

        m_refAimDriver = GetComponent<WeaponAimDriver>();
    }

    // Player.Update()가 Aim의 탄착점(Hit Point)을 매 프레임 넘겨준다. 줌 여부와 무관하게
    // 항상 정밀 조준한다(상세 이유는 WeaponAimDriver 참고).
    public void SetAimCorrection(Vector3 _vTargetPos)
    {
        if (m_refAimDriver != null)
            m_refAimDriver.SetTarget(_vTargetPos);
    }

    public void Fire(Vector3 _vTargetPos)
    {
        tShotInfo refShotInfo = new tShotInfo();
        refShotInfo.TargetPos = _vTargetPos;
        refShotInfo.Speed = RollSpeed();

        if (m_iBulletCount > 1)
        {
            FireCircularSector(_vTargetPos, refShotInfo);
            return;
        }

        Vector3 vLookDir = _vTargetPos - m_refFireTr.position;
        Quaternion qRot = (m_bLookTarget == true && vLookDir.sqrMagnitude > 0.0001f)
            ? Quaternion.LookRotation(vLookDir) : m_refFireTr.rotation;
        qRot = ApplyInaccuracy(qRot);

        GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, m_refFireTr.position, qRot, m_refAttackInfo, refShotInfo);
        if (refObj == null)
            return;

        OnBulletFired();
    }

    // 조준 방향(_vTargetPos)을 중심축으로, 반각 m_fSpreadAngle/2인 원뿔 단면에 m_iBulletCount발을
    // 골든 앵글 스파이럴로 균등 분포시켜 3D 부채꼴(샷건 콘) 형태로 발사
    private void FireCircularSector(Vector3 _vTargetPos, tShotInfo _refShotInfo)
    {
        Vector3 vBaseDir = (_vTargetPos - m_refFireTr.position).normalized;
        Vector3 vSpokeAxis = Vector3.Cross(vBaseDir, m_refFireTr.up);
        vSpokeAxis.Normalize();

        float fHalfAngle = m_fSpreadAngle * 0.5f;

        for (int i = 0; i < m_iBulletCount; ++i)
        {
            // fConeAngle: 중심축에서 얼마나 벌어지는지 (sqrt 분포로 원뿔 단면에 균등하게 채움)
            // fSpinAngle: 중심축을 기준으로 몇 도 회전한 스포크에 놓을지 (골든 앵글로 겹치지 않게 배치)
            float fRatio = (i + 0.5f) / m_iBulletCount;
            float fConeAngle = Mathf.Sqrt(fRatio) * fHalfAngle;
            float fSpinAngle = i * GOLDEN_ANGLE_DEG; //i가 증가할 때마다 황금각만큼 계속 회전시키기

            Vector3 vAxis = Quaternion.AngleAxis(fSpinAngle, vBaseDir) * vSpokeAxis;//실제로 회전시킬 대상인 3D 화살표
            Vector3 vDir = Quaternion.AngleAxis(fConeAngle, vAxis) * vBaseDir;

            if (vDir.sqrMagnitude < 0.0001f)
                vDir = vBaseDir;

            Quaternion qRot = ApplyInaccuracy(Quaternion.LookRotation(vDir));

            tShotInfo refPelletShotInfo = _refShotInfo;
            refPelletShotInfo.Speed = RollSpeed();

            GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, m_refFireTr.position, qRot, m_refAttackInfo, refPelletShotInfo);
            if (refObj == null)
                continue;

            OnBulletFired();
        }
    }


    public void FireAndRotate(Vector3 _vDir, float _fFowardOffset)
    {
        if (_vDir.sqrMagnitude < 0.0001f)
            _vDir = m_refFireTr.forward;

        Vector3 vSpawnPos = m_refFireTr.position + (_vDir * _fFowardOffset);
        Quaternion qRot = ApplyInaccuracy(Quaternion.LookRotation(_vDir));

        tShotInfo refShotInfo = new tShotInfo();
        refShotInfo.Speed = RollSpeed();

        GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, vSpawnPos, qRot, m_refAttackInfo, refShotInfo);
        if (refObj == null)
            return;

        OnBulletFired();
    }

    private float RollSpeed()
    {
        float fSpeed = m_refAttackInfo.Speed;
        return UnityEngine.Random.Range(fSpeed - m_SOAttackInfo.SpeedOffset, fSpeed + m_SOAttackInfo.SpeedOffset);
    }

    // 줌(우클릭 ADS) 중에는 정확히 조준점으로 나가고, 3인칭(줌 아님)일 때만 무기의
    // m_fInaccuracyAngle만큼 랜덤하게 흩어진다.
    private Quaternion ApplyInaccuracy(Quaternion _qBase)
    {
        bool bZoomed = InputManager.m_Instance != null && InputManager.m_Instance.InputInfo.OnRButton;
        float fAngle = bZoomed ? 0f : m_fInaccuracyAngle;

        if (fAngle <= 0f)
            return _qBase;

        Quaternion qJitter = Quaternion.Euler(
            UnityEngine.Random.Range(-fAngle, fAngle),
            UnityEngine.Random.Range(-fAngle, fAngle),
            0f);

        return qJitter * _qBase;
    }


    private void OnBulletFired()
    {
        if (m_refEffectObject != null)
            m_refEffectObject.Play();

        if (GameCameraManager.m_Instance != null)
            GameCameraManager.m_Instance.Shake(m_refAttackInfo.RecoilAmount);

        // 순수 연출용 반동 — 조준(pitch/yaw)이나 실제 탄 퍼짐(m_fInaccuracyAngle)과는
        // 완전히 별개로, 무기 모델(transform)에만 스프링 오프셋을 얹는다.
        if (m_refRecoilKick != null)
        {
            Vector3 vRotKick = m_SOAttackInfo.VisualRotKick;
            vRotKick.y += UnityEngine.Random.Range(-m_SOAttackInfo.VisualRotKickRandomYaw, m_SOAttackInfo.VisualRotKickRandomYaw);

            m_refRecoilKick.Kick(m_SOAttackInfo.VisualKickback, vRotKick,
                m_SOAttackInfo.VisualSpringStiffness, m_SOAttackInfo.VisualSpringDamping);
        }

        m_fLastFireTime = Time.time;
        m_fFireTime = m_refAttackInfo.CoolDown;
    }


    public bool CheckTime()
    {
        return (Time.time - m_fLastFireTime) > m_fFireTime;
    }

    // 기존 배율에 누적 곱하지 않고 매번 기본 쿨다운 기준으로 재계산 (Repeatable 기능 재적용 시 드리프트 방지)
    public void SetCooldown(float _fValue)
    {
        float fClamped = Mathf.Max(_fValue, 0.1f);

        m_refAttackInfo.CoolDown = m_fBaseCooldown * fClamped;
        m_fFireTime = m_refAttackInfo.CoolDown;
    }

    // Player.UpAttack()에서 레벨업 시점에 호출. m_refAttackInfo는 이 무기가 만든 모든 총알이

    public void AddAttackDamage(int _iValue)
    {
        m_refAttackInfo.Damage += _iValue;
    }

    public void AddBulletSpeed(float _fValue)
    {
        m_refAttackInfo.Speed += _fValue;
    }
    public void DownBulletSpeed(float _fValue)
    {
        m_refAttackInfo.Speed -= _fValue;
    }


  
}

