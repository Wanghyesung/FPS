using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
             SOStopMoveNode
기능 : 제자리 사격을 위해 에이전트를 세우는 노드.
       다른 브랜치로 넘어갈 때 Abort에서 다시 풀어준다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_StopMoveNode", menuName = "Game/Monster/ActionNode/StopMoveNode")]

public class SOStopMoveNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return eNodeState.Failure;

        if (refAgent.isStopped == false)
            refAgent.isStopped = true;

        return eNodeState.Success;
    }

    public override void Abort(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return;

        refAgent.isStopped = false;
    }
}
