using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
               SOSearchNode
기능 : 마지막으로 본 위치까지 가서 주변을 둘러보는 노드. "어디 갔지" 행동.

       이동 → 도착 → 좌우로 시야를 훑기 → 기억 소거 후 순찰 복귀 순으로 진행한다.
       SOLookAtHitNode와 같은 원리로, 두리번거리는 것 자체가 탐지 수단이다 —
       훑는 동안 시야 콘에 플레이어가 들어오면 SOPerceptionNode가 잡아내고
       우선순위가 높은 교전 브랜치가 이 브랜치를 밀어낸다(Abort).

       단계와 타이머는 전부 BlackBoard에 둔다. leaf 노드는 클론되지 않아
       모든 몬스터가 원본 에셋을 공유하므로 여기에 상태를 두면 즉시 오염된다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_SearchNode", menuName = "Game/Monster/ActionNode/SearchNode")]

public class SOSearchNode : SONode
{
    [Header("Move")]
    [Tooltip("마지막 목격 위치가 NavMesh 밖일 때 주변에서 갈 수 있는 곳을 찾아줄 반경")]
    [SerializeField] private float m_fSampleRadius = 2.0f;
    [Tooltip("목표에 이만큼 가까워지면 도착으로 처리")]
    [SerializeField] private float m_fArriveDistance = 1.5f;

    [Header("Look Around")]
    [Tooltip("도착 후 주변을 둘러보는 시간(초)")]
    [SerializeField] private float m_fLookDuration = 3.0f;
    [Tooltip("도착 방향을 중심으로 좌우로 훑을 각도")]
    [SerializeField] private float m_fLookAngle = 70.0f;
    [Tooltip("좌우로 훑는 속도")]
    [SerializeField] private float m_fLookSpeed = 2.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.Owner == null)
            return eNodeState.Failure;

        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return eNodeState.Failure;

        if (_refBB.SearchPhase == eSearchPhase.Looking)
            return LookAround(_refBB);

        if (_refBB.SearchPhase == eSearchPhase.Moving)
            return CheckArrived(_refBB, refAgent);

        return StartSearch(_refBB, refAgent);
    }

    // 마지막 목격 위치를 NavMesh 위로 스냅해서 이동을 시작한다
    private eNodeState StartSearch(BlackBoard _refBB, NavMeshAgent _refAgent)
    {
        if (NavMesh.SamplePosition(_refBB.LastSeenPos, out NavMeshHit tHit,
                m_fSampleRadius, _refAgent.areaMask) == false)
        {
            // 갈 수 없는 곳을 봤다면 그 기억으로는 더 할 게 없다
            EndSearch(_refBB, _refAgent);
            return eNodeState.Failure;
        }

        _refAgent.isStopped = false;
        _refAgent.updateRotation = true;

        if (_refBB.ObjInfo != null && _refBB.ObjInfo.Speed > 0.0f)
            _refAgent.speed = _refBB.ObjInfo.Speed;

        if (_refAgent.SetDestination(tHit.position) == false)
        {
            EndSearch(_refBB, _refAgent);
            return eNodeState.Failure;
        }

        _refBB.SearchPos = tHit.position;
        _refBB.SearchPhase = eSearchPhase.Moving;

        return eNodeState.Running;
    }

    private eNodeState CheckArrived(BlackBoard _refBB, NavMeshAgent _refAgent)
    {
        if (_refAgent.pathPending == true)
            return eNodeState.Running;

        Vector3 vToSearchPos = _refBB.SearchPos - _refBB.Owner.transform.position;
        vToSearchPos.y = 0.0f;

        bool bArrived = _refAgent.remainingDistance <= m_fArriveDistance
            || vToSearchPos.sqrMagnitude <= (m_fArriveDistance * m_fArriveDistance);

        if (bArrived == true)
        {
            _refAgent.isStopped = true;
            _refAgent.updateRotation = false;

            // 도착한 방향을 기준으로 좌우를 훑는다
            _refBB.SearchBaseYaw = _refBB.Owner.transform.eulerAngles.y;
            _refBB.SearchEndTime = Time.time + m_fLookDuration;
            _refBB.SearchPhase = eSearchPhase.Looking;

            return eNodeState.Running;
        }

        if (_refAgent.hasPath == false || _refAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            EndSearch(_refBB, _refAgent);
            return eNodeState.Failure;
        }

        return eNodeState.Running;
    }

    // 좌우로 시야를 훑는다. sin으로 흔들면 왕복 상태를 따로 들고 있지 않아도 된다
    private eNodeState LookAround(BlackBoard _refBB)
    {
        float fElapsed = m_fLookDuration - (_refBB.SearchEndTime - Time.time);
        float fYaw = _refBB.SearchBaseYaw + (Mathf.Sin(fElapsed * m_fLookSpeed) * m_fLookAngle);

        _refBB.Owner.transform.rotation = Quaternion.Euler(0.0f, fYaw, 0.0f);

        if (Time.time < _refBB.SearchEndTime)
            return eNodeState.Running;

        // 다 둘러봤는데 없다 - 기억을 지워야 다음 틱에 또 같은 곳을 수색하지 않는다
        _refBB.HasLastSeen = false;
        EndSearch(_refBB, _refBB.Agent);

        return eNodeState.Success;
    }

    public override void Abort(BlackBoard _refBB)
    {
        // HasLastSeen은 건드리지 않는다 — 도주 등으로 밀려난 것뿐이라면
        // 상황이 끝난 뒤 남은 기억으로 수색을 이어가는 게 맞다
        EndSearch(_refBB, _refBB.Agent);
    }

    private void EndSearch(BlackBoard _refBB, NavMeshAgent _refAgent)
    {
        _refBB.SearchPhase = eSearchPhase.None;
        _refBB.SearchEndTime = 0.0f;

        if (_refAgent == null || _refAgent.isOnNavMesh == false)
            return;

        _refAgent.isStopped = false;
        _refAgent.updateRotation = true;
    }
}
