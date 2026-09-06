using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*///////////////////////////////////////////
                Weapon
목적 : 장착된 무기 한 자루. 발사 '의도'를 접수(RequestFire)하고, 실제 투사체 스폰은
       LateUpdate에서만 수행한다.
 *///////////////////////////////////////////

public class Weapon : MonoBehaviour
{

    public enum eWeaponType
    {
        AK,
        TRG,
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
    [SerializeField] private Transform m_refZoomTr;          // 왼손 IK가 잡아야 할 그립 포인트 — WeaponRigTarget이 참조
    public Transform RightHandGripTr => m_refRightHandGripTr;
    public Transform LeftHandGripTr => m_refLeftHandGripTr;
    public Transform ZoomTr => m_refZoomTr;
    public Transform FireTr => m_refFireTr;   // WeaponAimAlign이 총구 방향(정렬 기준)을 읽기 위해 참조

    private WeaponRecoilKick m_refRecoilKick; // 사격 시 순수 연출용 스프링 반동 — 없으면 조용히 생략(선택 컴포넌트)
    private WeaponAimAlign m_refAimAlign;     // 리깅 가중치를 높여서 총구가 앞으로 가게

    private Renderer[] m_arrRenderers;        // 스코프 진입 시 렌더를 통째로 끄기 위해
    private bool m_bRenderersVisible = true;
    private bool m_bZoomed;                   // 이 무기 자신의 조준 상태(적 무기도 자기 상태를 쓰게 하기 위함)

    // 조준 설정 — SO는 정적 데이터이므로 Init에서 값만 복사해 둔다
    private eAimMode m_eAimMode = eAimMode.None;
    private float m_fZoomFov;
    private float m_fZoomBlendTime;
    private bool m_bHideWeaponOnAim;

    public eAimMode AimMode => m_eAimMode;
    public float ZoomFov => m_fZoomFov;
    public float ZoomBlendTime => m_fZoomBlendTime;
    public bool HideWeaponOnAim => m_bHideWeaponOnAim;

    private float m_fFireTime = 0.2f;
    private float m_fBaseCooldown = 0.2f;
    private float m_fLastFireTime = -Mathf.Infinity;

    private eWeaponType m_eWeapoonType = eWeaponType.AK;
    public eWeaponType WeaponType => m_eWeapoonType;

    public PoolObject FireBulletPrefab => m_SOAttackInfo.PoolPrefab;

    [Header("Weapon Option")]
    [SerializeField] private bool m_bLookTarget = true;

    [Header("Inaccuracy")]
    [SerializeField] private float m_fInaccuracyAngle = 2f; // 조준 방향에서 좌우/상하로 흔들리는 오차 각도

    [Header("Circular Sector Shot")]
    [SerializeField] private int m_iBulletCount = 1;     // 1이면 기존처럼 단발
    [SerializeField] private float m_fSpreadAngle = 30f; // 부채꼴(원뿔) 전체 각도


    //예약 시스템으로 변경 Update -> 리깅 -> LateUpdate 순서에서 총구 위치가 확정되므로, 발사 요청은 Update에서 받아서 예약만 해두기
    private bool m_bFireRequested;
    private Vector3 m_vRequestedTarget;
    private float m_fRequestTime;

    private const float REQUEST_BUFFER = 0.1f; // 해당 시간이 지나면 예약 철회

    // Animator + RigBuilder 평가가 끝난 뒤 = 총구가 최종 확정된 뒤
    private void LateUpdate()
    {

        if (CheckTime() == false || m_bFireRequested == false)
            return;

        // 너무 오래된 요청은 폐기 — 큐처럼 무한히 쌓이지 않게
        if (Time.time - m_fRequestTime > REQUEST_BUFFER)
        {
            m_bFireRequested = false;
            return;
        }

        m_bFireRequested = false;
        Fire(m_vRequestedTarget);
    }

    public void Init()
    {

        m_refAttackInfo = m_SOAttackInfo.MakeAttackInfo();
        m_refAttackInfo.Owner = gameObject.transform;
        m_eWeapoonType = m_SOAttackInfo.WeaponType;
        m_fBaseCooldown = m_refAttackInfo.CoolDown;

        m_fFireTime = m_refAttackInfo.CoolDown;
        m_fLastFireTime = Time.time;

        m_refRecoilKick = GetComponent<WeaponRecoilKick>();
        m_refAimAlign = GetComponent<WeaponAimAlign>();
        if (m_refRecoilKick != null)
            m_refRecoilKick.CaptureBasePose(); //처음 위치를 캐싱해두기 (총을 다 쏘고 원래 위치로 돌아오게)

        m_eAimMode = m_SOAttackInfo.AimMode;
        m_fZoomFov = m_SOAttackInfo.ZoomFov;
        m_fZoomBlendTime = m_SOAttackInfo.ZoomBlendTime;
        m_bHideWeaponOnAim = m_SOAttackInfo.HideWeaponOnAim;

        // 비활성 자식(머즐 파티클 등)까지 포함해서 잡아둔다 — 스코프 중에는 총구 화염도 같이 숨어야 한다
        m_arrRenderers = GetComponentsInChildren<Renderer>(true);
        m_bRenderersVisible = true;
    }

    // 무기 루트를 SetActive(false)로 끄면 LateUpdate가 멈춰 발사가 안 되고,
    // WeaponRigTarget이 넘긴 IK 타겟이 비활성 트랜스폼이 되어 리그가 깨진다. 렌더링만 끈다.
    public void SetRenderersVisible(bool _bVisible)
    {
        if (m_bRenderersVisible == _bVisible || m_arrRenderers == null)
            return;

        m_bRenderersVisible = _bVisible;

        for (int i = 0; i < m_arrRenderers.Length; ++i)
        {
            if (m_arrRenderers[i] == null)
                continue;

            m_arrRenderers[i].enabled = _bVisible;
        }
    }

    public void RequestFire(Vector3 _vTargetPos)
    {
        m_bFireRequested = true;
        m_vRequestedTarget = _vTargetPos;  // 최신 요청이 덮어씀
        m_fRequestTime = Time.time;
    }

    

    private void Fire(Vector3 _vTargetPos)
    {
        tShotInfo refShotInfo = new tShotInfo();
        refShotInfo.TargetPos = _vTargetPos;
        refShotInfo.Speed = RollSpeed();

        Vector3 vLookDir = _vTargetPos - m_refFireTr.position;
        Quaternion qRot = (m_bLookTarget == true && vLookDir.sqrMagnitude > 0.0001f)
            ? Quaternion.LookRotation(vLookDir) : m_refFireTr.rotation;
        qRot = ApplyInaccuracy(qRot);


        GameObject refObj = Bullet.SpawnAttackObject(m_SOAttackInfo.PoolPrefab, m_refFireTr.position, qRot, m_refAttackInfo, refShotInfo);
        if (refObj == null)
            return;

        
        OnBulletFired();
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


    private Quaternion ApplyInaccuracy(Quaternion _qBase)
    {
        // 이 무기 자신의 조준 상태를 본다 — 예전처럼 InputManager를 직접 읽으면
        // 플레이어가 우클릭을 누르는 동안 모든 적의 사격까지 100% 정확해진다.
        float fAngle = m_bZoomed ? 0f : m_fInaccuracyAngle;

        if (fAngle <= 0f)
            return _qBase;

        Quaternion qJitter = Quaternion.Euler(
            UnityEngine.Random.Range(-fAngle, fAngle),
            UnityEngine.Random.Range(-fAngle, fAngle),
            0f);

        return qJitter * _qBase;
    }


    // 무기 모델(transform)에만 스프링 오프셋을 얹는다
    private void OnBulletFired()
    {
        if (m_refEffectObject != null)
            m_refEffectObject.Play();

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

    public void Zoom()
    {
        m_bZoomed = true;

        if (m_refAimAlign != null)
            m_refAimAlign.Zoom = true;
    }

    public void UnZoom()
    {
        m_bZoomed = false;

        if (m_refAimAlign != null)
            m_refAimAlign.Zoom = false;
    }
}

