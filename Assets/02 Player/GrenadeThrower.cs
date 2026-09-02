using UnityEngine;

/*///////////////////////////////////////////
                GrenadeThrower
목적 : 장착 무기(Weapon)와는 완전히 독립된 수류탄 투척 슬롯. 던지기 버튼을 누르고
       있는 동안 고정 비행시간(T) 방식으로 초기 속도를 역산해 포물선 예고선을
       그리고, 버튼을 떼는 순간 같은 속도로 실제 Grenade를 투척한다.

       고정 비행시간 방식을 쓰는 이유: "고정 속도로 던지고 각도를 계산"하는 방식은
       목표가 사거리 밖이면 해가 없어 실패할 수 있다. 비행시간을 먼저 정하고
       속도를 역산하면 항상 해가 존재해서 예고선이 항상 목표 지점에 도달한다.

       예고선과 실제 투척이 완전히 같은 공식을 쓰기 때문에(CalcThrowVelocity 재사용),
       화면에 보이는 선과 실제 낙하 지점이 사실상 일치한다.
 *///////////////////////////////////////////

public sealed class GrenadeThrower : MonoBehaviour
{
    [SerializeField] private Aim m_refAim;
    [SerializeField] private Transform m_refThrowOrigin; // CameraPivot3D 재사용 — Aim의 레이캐스트 원점과 동일
    [SerializeField] private LineRenderer m_refTrajectoryLine;
    [SerializeField] private PoolObject m_refGrenadePrefab;
    [SerializeField] private SOAttackInfo m_SOAttackInfo;

    [SerializeField] private int m_iLineSegments = 30;
    [SerializeField] private float m_fBaseTime = 0.3f;       // 최소 비행시간 베이스
    [SerializeField] private float m_fTimePerDistance = 0.05f; // 거리 1유닛당 추가되는 비행시간
    [SerializeField] private float m_fMinTime = 0.4f;
    [SerializeField] private float m_fMaxTime = 1.6f;
    [SerializeField] private LayerMask m_tLineBlockMask = ~0; // 예고선이 지형에 막힐 때 쓰는 레이어

    private AttackInfo m_refAttackInfo;
    private bool m_bWasHeld;
    private Vector3[] m_arrLinePoints;

    private void Awake()
    {
        m_refAttackInfo = m_SOAttackInfo != null ? m_SOAttackInfo.MakeAttackInfo() : null;
        m_arrLinePoints = new Vector3[m_iLineSegments + 1];

        if (m_refTrajectoryLine != null)
            m_refTrajectoryLine.positionCount = 0;
    }

    private void Update()
    {
        if (InputManager.m_Instance == null || m_refThrowOrigin == null || m_refAim == null)
            return;

        bool bHeld = InputManager.m_Instance.InputInfo.OnThrow;

        if (bHeld == true)
        {
            Vector3 vInitialVelocity = CalcThrowVelocity(out float fFlightTime);
            DrawTrajectory(vInitialVelocity, fFlightTime);
        }
        else if (m_bWasHeld == true) // 이번 프레임에 막 뗀 순간
        {
            Vector3 vInitialVelocity = CalcThrowVelocity(out _);
            Throw(vInitialVelocity);
            HideTrajectory();
        }

        m_bWasHeld = bHeld;
    }

    // 고정 비행시간(T)을 먼저 정하고, 목표 지점에 정확히 도달하는 데 필요한 초기 속도를 역산한다.
    private Vector3 CalcThrowVelocity(out float _fFlightTime)
    {
        Vector3 vStart = m_refThrowOrigin.position;
        Vector3 vTarget = m_refAim.TargetPosition;
        Vector3 vDelta = vTarget - vStart;

        float fDistance = new Vector2(vDelta.x, vDelta.z).magnitude;
        _fFlightTime = Mathf.Clamp(m_fBaseTime + fDistance * m_fTimePerDistance, m_fMinTime, m_fMaxTime);

        float fGravity = Physics.gravity.magnitude;

        Vector3 vDeltaXZ = new Vector3(vDelta.x, 0f, vDelta.z);
        Vector3 vVelXZ = vDeltaXZ / _fFlightTime;
        float fVelY = vDelta.y / _fFlightTime + 0.5f * fGravity * _fFlightTime;

        return vVelXZ + Vector3.up * fVelY;
    }

    // CalcThrowVelocity와 같은 포물선 공식으로 점을 찍는다 — 도중에 지형에 막히면 그 지점에서 끊는다.
    private void DrawTrajectory(Vector3 _vInitialVelocity, float _fFlightTime)
    {
        if (m_refTrajectoryLine == null)
            return;

        Vector3 vStart = m_refThrowOrigin.position;
        float fGravity = Physics.gravity.magnitude;

        int iPointCount = 1;
        m_arrLinePoints[0] = vStart;

        for (int i = 1; i <= m_iLineSegments; ++i)
        {
            float fT = (_fFlightTime * i) / m_iLineSegments;
            Vector3 vPoint = vStart + _vInitialVelocity * fT + Vector3.down * (0.5f * fGravity * fT * fT);

            Vector3 vPrev = m_arrLinePoints[iPointCount - 1];
            if (Physics.Linecast(vPrev, vPoint, out RaycastHit tHit, m_tLineBlockMask) == true)
            {
                m_arrLinePoints[iPointCount] = tHit.point;
                ++iPointCount;
                break;
            }

            m_arrLinePoints[iPointCount] = vPoint;
            ++iPointCount;
        }

        m_refTrajectoryLine.positionCount = iPointCount;
        m_refTrajectoryLine.SetPositions(m_arrLinePoints);
    }

    private void HideTrajectory()
    {
        if (m_refTrajectoryLine != null)
            m_refTrajectoryLine.positionCount = 0;
    }

    private void Throw(Vector3 _vInitialVelocity)
    {
        if (m_refGrenadePrefab == null || m_refAttackInfo == null)
            return;

        Grenade.SpawnAttackObject(m_refGrenadePrefab, m_refThrowOrigin.position, m_refAttackInfo, _vInitialVelocity);
    }
}
