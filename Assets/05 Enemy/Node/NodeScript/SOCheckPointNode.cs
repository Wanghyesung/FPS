using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
             SOCheckPointNode
기능 : Move To CheckPoint in CheckPointList 
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckPointNode", menuName = "Game/Monster/ActionNode/CheckPointNode")]
public class SOCheckPointNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        _refBB.PatrolIdx %= _refBB.PatrolList.Count;
        Transform refMovePoint = _refBB.PatrolList[_refBB.PatrolIdx];

        _refBB.Agent.SetDestination(refMovePoint.position);
        return eNodeState.Success;
    }
}
