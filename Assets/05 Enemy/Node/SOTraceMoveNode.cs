using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*///////////////////////////////////////////
               TraceMoveNode
기능 : 적을 추적하는 기능
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_TraceMoveNode", menuName = "Game/Monster/ActionNode/TraceMoveNode")]
public class SOTraceMoveNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        Vector3 vTargetPos = _refBB.TargetTr.position;

        _refBB.Agent.SetDestination(vTargetPos);
        _refBB.Agent.isStopped = false;

        return eNodeState.Success;
    }
}
