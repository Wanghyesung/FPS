using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
               TraceMoveNode
기능 : 사거리 밖의 타겟에게 접근하는 노드.
       Sequence가 매 틱 재평가하므로, 타겟이 실제로 움직였을 때만 경로를 다시 계산한다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_TraceMoveNode", menuName = "Game/Monster/ActionNode/TraceMoveNode")]
public class SOTraceMoveNode : SONode
{
    [Tooltip("타겟이 이 거리 이상 움직였을 때만 경로를 다시 계산한다")]
    [SerializeField] private float m_fRepathDistance = 1.5f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return eNodeState.Failure;

        if (refAgent.isStopped == true)
            refAgent.isStopped = false;

        Vector3 vTargetPos = _refBB.TargetTr.position;

        // 매 틱 SetDestination을 부르면 경로를 매 프레임 다시 계산한다
        if (refAgent.hasPath == false
            || (refAgent.destination - vTargetPos).sqrMagnitude > (m_fRepathDistance * m_fRepathDistance))
        {
            if (refAgent.SetDestination(vTargetPos) == false)
                return eNodeState.Failure;
        }

        // 접근은 계속 진행 중인 행동이므로 Running — 사거리에 들어가면 Fire 시퀀스가 가져간다
        return eNodeState.Running;
    }

    public override void Abort(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return;

        if (refAgent.hasPath == true)
            refAgent.ResetPath();
    }
}
