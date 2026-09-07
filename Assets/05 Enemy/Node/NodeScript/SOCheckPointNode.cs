using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
             SOCheckPointNode
기능 : 플레이어의 히트맵을 보고 가중치가 가장 높은 곳으로 이동
       목표 지점과 진행 여부는 전부 BlackBoard에 둔다. leaf 노드는 클론되지 않아
       상태를 들면 같은 SO 에셋을 공유하는 모든 몬스터가 즉시 오염된다
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckPointNode", menuName = "Game/Monster/ActionNode/CheckPointNode")]
public class SOCheckPointNode : SONode
{
    [Header("Move")]
    [Tooltip("히트맵 셀 중앙이 NavMesh 밖일 때 주변에서 갈 수 있는 곳을 찾아줄 반경")]
    [SerializeField] private float m_fSampleRadius = 5.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return eNodeState.Failure;

        // Sequence가 매 틱 0번부터 재평가하므로, 이미 목표를 잡아뒀으면 다시 뽑지 않는다
        if (_refBB.HasCheckPoint == true)
        {
            // hasPath는 SetDestination 직후 한두 프레임 false다 - 그걸로 목표를 버리면
            // 매 틱 새 셀을 뽑아 제자리에서 떨게 되므로, 경로가 무효로 확정됐을 때만 버린다
            if (refAgent.pathPending == true || refAgent.pathStatus != NavMeshPathStatus.PathInvalid)
                return eNodeState.Success;

            _refBB.HasCheckPoint = false;
        }

        if (PlayerHeatMapLoader.m_Instance == null || PlayerHeatmapRecorder.m_Instance == null)
            return eNodeState.Failure;

        // 워커 스레드가 배열을 통째로 교체하므로 스냅샷은 반드시 한 번만 읽어 지역 변수로 고정한다
        Vector2Int[] arrTopPos = PlayerHeatMapLoader.m_Instance.TopPositions;

        if (arrTopPos.Length == 0)
            return eNodeState.Failure;

        int iIdx = Random.Range(0, arrTopPos.Length);
        Vector2Int vCell = arrTopPos[iIdx];

        Vector3 vMovePoint = PlayerHeatmapRecorder.m_Instance.CellToWorld(vCell);
        vMovePoint.y = refAgent.transform.position.y;

        // 셀 중앙이 벽이나 낭떠러지일 수 있으므로 실제로 갈 수 있는 지점으로 스냅한다
        if (NavMesh.SamplePosition(vMovePoint, out NavMeshHit tHit, m_fSampleRadius, refAgent.areaMask) == false)
            return eNodeState.Failure;

        refAgent.isStopped = false;

        //갈수없는 지역이면 버리기
        if (refAgent.SetDestination(tHit.position) == false)
            return eNodeState.Failure;

        _refBB.CheckPointPos = tHit.position;
        _refBB.HasCheckPoint = true;

        return eNodeState.Success;
    }

    public override void Abort(BlackBoard _refBB)
    {
        _refBB.HasCheckPoint = false;
    }
}
