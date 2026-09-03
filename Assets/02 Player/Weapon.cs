using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/*///////////////////////////////////////////
                Weapon
목적 : 장착된 무기 한 자루. 발사 '의도'를 접수(RequestFire)하고, 실제 투사체 스폰은
       LateUpdate에서만 수행한다.

       총구(FireTr)는 Spine_02/Spine_03의 MultiAimConstraint와 Clavicle_R 아래 소켓에
       매달려 있어서, Animator + RigBuilder가 평가된 뒤에야(모든 Update 이후 ~ LateUpdate
       직전) 최종 위치가 확정된다. 그래서 Update에서 FireTr을 읽으면 한 프레임 전 포즈의
       총구를 읽게 된다. 이 규칙을 발사자(Player 입력 / Enemy BT)마다 지키게 하는 대신
       Weapon이 혼자 책임지도록 모아서, 누가 언제 요청하든 총구는 항상 LateUpdate에서만 읽는다.
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

    private float m_fFireTime = 0.2f;
    private float m_fBaseCooldown = 0.2f;
    private float m_fLastFireTime = -Mathf.Infinity;

    private eWeaponType m_eWeapoonType = eWeaponType.AK;
    public eWeaponType WeaponType => m_eWeapoonType;

    public PoolObject FireBulletPrefab => m_SOAttackInfo.PoolPrefab;

    [Header("Weapon Option")]
    [SerializeField] private bool m_bLookTarget = true;

    [Header("Inaccuracy")]
    [SerializeField] private float m_fInaccuracyAngle = 0f; // 조준 방향에서 좌우/상하로 흔들리는 오차 각도

    [Header("Circular Sector Shot")]
    [SerializeField] private int m_iBulletCount = 1;     // 1이면 기존처럼 단발
    [SerializeField] private float m_fSpreadAngle = 30f; // 부채꼴(원뿔) 전체 각도

    private const float GOLDEN_ANGLE_DEG = 137.50776f;


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
            m_refRecoilKick.CaptureBasePose(); //처음 위치를 캐싱해두기 (총을 다 쏘고 원래 위치로 돌아오게_
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

    public void SetCooldown(float _fValue)
    {
        float fClamped = Mathf.Max(_fValue, 0.1f);

        m_refAttackInfo.CoolDown = m_fBaseCooldown * fClamped;
        m_fFireTime = m_refAttackInfo.CoolDown;
    }


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


    public void Zoom()
    {
        if (m_refAimAlign != null)
            m_refAimAlign.Zoom = true;
    }

    public void UnZoom()
    {
        if (m_refAimAlign != null)
            m_refAimAlign.Zoom = false;
    }
}

