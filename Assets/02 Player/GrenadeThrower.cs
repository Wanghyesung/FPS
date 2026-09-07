using UnityEngine;
using UnityEngine.Serialization;

/*///////////////////////////////////////////
                GrenadeThrower
목적 : 장착 무기(Weapon)와는 완전히 독립된 수류탄 투척 슬롯. 던지기 버튼을 누르고
       있는 동안 포물선 예고선과 착지 마커를 그리고, 버튼을 떼는 순간 같은 초기
       속도로 실제 Grenade를 투척한다.

       배틀그라운드 방식을 따른다 — 던지는 세기(m_fThrowPower)는 상수이고 방향만
       카메라를 따라간다. 따라서 '목표 지점'이라는 개념이 없고, 사거리 밖을 조준하면
       그냥 못 미쳐서 떨어진다. 착지점은 계산으로 정하는 게 아니라 시뮬레이션이
       지형에 처음 부딪히는 지점으로 결정된다.

       예고선은 해석해(P = P0 + Vt + ½gt²)를 쓰지 않고 PhysX와 동일한
       semi-implicit Euler(v += g·dt; p += v·dt)로 적분한다. Rigidbody는 이산
       적분이라 해석해보다 ½·g·Δt·t 만큼 더 떨어지는데, 같은 방식으로 스텝을 돌면
       그 오차가 애초에 생기지 않아 예고선과 실제 낙하 지점이 일치한다. 어차피
       구간별 충돌 검사 때문에 스텝을 끊어야 하므로 추가 비용도 없다.
 *///////////////////////////////////////////

public sealed class GrenadeThrower : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform m_refThrowOrigin;   // CameraPivot3D 재사용 — 시선 방향이 곧 투척 방향
    [SerializeField] private LineRenderer m_refTrajectoryLine;
    [SerializeField] private Transform m_refLandingMarker; // 착지 예상 지점 데칼 — 이 Transform의 +Z가 지면 노멀에 정렬된다
    [SerializeField] private PoolObject m_refGrenadePrefab;
    [SerializeField] private SOAttackInfo m_SOAttackInfo;

    [Header("투척")]
    [SerializeField] private float m_fThrowPower = 18f;
    [SerializeField] private float m_fUpBias = 0.1f; // forward에 섞는 위쪽 비율 — 정면을 봐도 살짝 던져 올린다

    [Header("예고선")]
    [FormerlySerializedAs("m_iLineSegments")]
    [Min(2)] [SerializeField] private int m_iMaxLinePoints = 64;
    [Min(1)] [SerializeField] private int m_iSubStepsPerPoint = 2; // 점 하나당 물리 스텝 수 — 적분 정확도는 그대로 두고 캐스트 횟수만 줄인다
    [SerializeField] private float m_fMaxSimTime = 2.5f;           // 허공으로 던졌을 때 예고선을 무한정 늘리지 않기 위한 상한
    [SerializeField] private float m_fCastRadius = 0.05f;          // 수류탄 콜라이더 반지름. 0 이하면 Raycast로 대체
    [SerializeField] private LayerMask m_tLineBlockMask = ~0;      // 예고선이 막히는 레이어 — 플레이어 자신은 반드시 제외할 것

    private AttackInfo m_refAttackInfo;
    private bool m_bWasHeld;
    private Vector3[] m_arrLinePoints;
    private float m_fGrenadeDrag;

    private void Awake()
    {
        m_refAttackInfo = m_SOAttackInfo != null ? m_SOAttackInfo.MakeAttackInfo() : null;
        m_arrLinePoints = new Vector3[m_iMaxLinePoints];

        CacheGrenadeDrag();

        SetLineVisible(false);
        HideLandingMarker();
    }

    // Drag를 인스펙터에 따로 복제해두면 프리팹 값과 어긋나는 순간 예고선이 조용히 틀어진다.
    // 값의 출처를 프리팹 하나로 두기 위해 실제 던질 Rigidbody에서 직접 읽어온다.
    private void CacheGrenadeDrag()
    {
        if (m_refGrenadePrefab == null)
            return;

        Rigidbody refBody = m_refGrenadePrefab.GetComponent<Rigidbody>();
        if (refBody != null)
            m_fGrenadeDrag = refBody.drag;
    }

    private void Update()
    {
        if (InputManager.m_Instance == null || m_refThrowOrigin == null)
            return;

        bool bHeld = InputManager.m_Instance.InputInfo.OnThrow;

        if (bHeld == true)
        {
            DrawTrajectory(CalcThrowVelocity());
        }
        else if (m_bWasHeld == true) // 이번 프레임에 막 뗀 순간
        {
            Throw(CalcThrowVelocity()); // 예고선과 완전히 같은 함수 — 보이는 선과 실제 궤적이 어긋날 여지가 없다
            HideTrajectory();
        }

        m_bWasHeld = bHeld;
    }

    // 세기는 상수, 방향만 카메라를 따라간다. 목표 지점을 받지 않으므로 역산이 없다.
    private Vector3 CalcThrowVelocity()
    {
        Vector3 vDir = (m_refThrowOrigin.forward + Vector3.up * m_fUpBias).normalized;
        return vDir * m_fThrowPower;
    }

    // 실제 Rigidbody와 같은 방식으로 스텝을 굴리며 점을 찍고, 지형에 부딪히면 그 지점에서 끊는다.
    private void DrawTrajectory(Vector3 _vInitialVelocity)
    {
        if (m_refTrajectoryLine == null)
            return;

        Vector3 vPos = m_refThrowOrigin.position;
        Vector3 vVelocity = _vInitialVelocity;
        float fStep = Time.fixedDeltaTime;
        float fElapsed = 0f;
        float fDragFactor = 1f / (1f + m_fGrenadeDrag * fStep); // PhysX 선형 감쇠와 동일한 식

        m_arrLinePoints[0] = vPos;
        int iPointCount = 1;
        bool bHit = false;

        for (int i = 1; i < m_iMaxLinePoints; ++i)
        {
            Vector3 vPrev = vPos;

            // 적분은 물리 스텝(fixedDeltaTime) 그대로, 캐스트와 점 찍기는 서브스텝을 모아서 한 번.
            // 가속 → 감쇠 → 위치 적분 순서까지 PhysX와 동일하게 맞춘다.
            for (int j = 0; j < m_iSubStepsPerPoint; ++j)
            {
                vVelocity += Physics.gravity * fStep;
                vVelocity *= fDragFactor;
                vPos += vVelocity * fStep;
            }
            fElapsed += fStep * m_iSubStepsPerPoint;

            if (TryHitBetween(vPrev, vPos, out RaycastHit tHit) == true)
            {
                m_arrLinePoints[iPointCount] = tHit.point;
                ++iPointCount;
                ShowLandingMarker(tHit.point, tHit.normal);
                bHit = true;
                break;
            }

            m_arrLinePoints[iPointCount] = vPos;
            ++iPointCount;

            if (fElapsed >= m_fMaxSimTime)
                break;
        }

        if (bHit == false)
            HideLandingMarker(); // 아직 허공 — 착지점을 모르므로 마커를 띄우지 않는다

        m_refTrajectoryLine.positionCount = iPointCount;
        m_refTrajectoryLine.SetPositions(m_arrLinePoints);
        SetLineVisible(true);
    }

    // LineRenderer는 GameObject가 비활성이어도 positionCount/SetPositions가 에러 없이 통과한다.
    // 즉 씬에서 꺼둔 채로 두면 아무 경고 없이 라인만 영영 안 보인다 — 씬 설정에 의존하지 않도록
    // 여기서 직접 켜고 끈다.
    private void SetLineVisible(bool _bVisible)
    {
        if (m_refTrajectoryLine == null)
            return;

        GameObject refLineObj = m_refTrajectoryLine.gameObject;
        if (refLineObj.activeSelf != _bVisible)
            refLineObj.SetActive(_bVisible);
    }

    // 수류탄은 점이 아니라 부피가 있으므로 SphereCast — Linecast로는 실제로 통과 못 하는 좁은 틈을
    // 예고선만 빠져나가서, 화면에 보이는 착지점과 실제 낙하 지점이 어긋난다.
    private bool TryHitBetween(Vector3 _vFrom, Vector3 _vTo, out RaycastHit _tHit)
    {
        Vector3 vDelta = _vTo - _vFrom;
        float fDistance = vDelta.magnitude;

        if (fDistance <= Mathf.Epsilon)
        {
            _tHit = default;
            return false;
        }

        Vector3 vDir = vDelta / fDistance;

        // QueryTriggerInteraction.Ignore — 폭발 판정용 트리거 볼륨 같은 곳에서 예고선이 끊기면 안 된다
        if (m_fCastRadius > 0f)
            return Physics.SphereCast(_vFrom, m_fCastRadius, vDir, out _tHit, fDistance, m_tLineBlockMask, QueryTriggerInteraction.Ignore);

        return Physics.Raycast(_vFrom, vDir, out _tHit, fDistance, m_tLineBlockMask, QueryTriggerInteraction.Ignore);
    }

    private void ShowLandingMarker(Vector3 _vPoint, Vector3 _vNormal)
    {
        if (m_refLandingMarker == null)
            return;

        // 마커의 +Z를 표면 노멀에 맞춘다(메쉬 앞면이 반대면 빈 부모를 끼워 보정하거나 Cull Off 머티리얼 사용).
        // LookRotation은 노멀이 정확히 Vector3.up(평지)일 때 기본 up 벡터와 평행해져 불안정하므로
        // FromToRotation을 쓴다 — 평지가 대부분인 이 게임에선 그 케이스가 상시 발생한다.
        m_refLandingMarker.SetPositionAndRotation(
            _vPoint + _vNormal * 0.02f, // z-fighting 방지 오프셋
            Quaternion.FromToRotation(Vector3.forward, _vNormal));

        if (m_refLandingMarker.gameObject.activeSelf == false)
            m_refLandingMarker.gameObject.SetActive(true);
    }

    private void HideLandingMarker()
    {
        if (m_refLandingMarker == null)
            return;

        if (m_refLandingMarker.gameObject.activeSelf == true)
            m_refLandingMarker.gameObject.SetActive(false);
    }

    private void HideTrajectory()
    {
        SetLineVisible(false);
        HideLandingMarker();
    }

    private void Throw(Vector3 _vInitialVelocity)
    {
        if (m_refGrenadePrefab == null || m_refAttackInfo == null)
            return;

        Grenade.SpawnAttackObject(m_refGrenadePrefab, m_refThrowOrigin.position, m_refAttackInfo, _vInitialVelocity);
    }
}
