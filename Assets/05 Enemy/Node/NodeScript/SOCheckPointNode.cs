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

        // SORotateNode가 교전 중 꺼둔 updateRotation을 여기서 되돌린다 — 안 그러면
        // 순찰로 복귀해도 마지막 조준 방향을 향한 채 옆걸음으로 이동해버린다
        _refBB.Agent.updateRotation = true;
        _refBB.Agent.SetDestination(refMovePoint.position);
        return eNodeState.Success;
    }
}
