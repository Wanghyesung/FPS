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
        // 순찰 지점이 하나도 없으면 아래 % 연산이 0으로 나눠 예외가 난다
        if (_refBB.PatrolList == null || _refBB.PatrolList.Count == 0)
            return eNodeState.Failure;

        _refBB.PatrolIdx %= _refBB.PatrolList.Count;
        Transform refMovePoint = _refBB.PatrolList[_refBB.PatrolIdx];

        // 이동 상태 복구(updateRotation / isStopped / speed / Zoom)는 순찰 시퀀스 맨 앞의
        // SOResumeMoveNode가 전담한다 — 이 노드는 목표 지점 지정만 담당
        _refBB.Agent.SetDestination(refMovePoint.position);
        return eNodeState.Success;
    }
}
