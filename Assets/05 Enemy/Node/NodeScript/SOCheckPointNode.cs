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
        Vector3 vMovePoint = _refBB.PatrolList[_refBB.PatrolIdx].position;

        // Sequence가 매 틱 재평가하므로, 목표가 그대로면 경로를 다시 계산하지 않는다
        if (_refBB.Agent.hasPath == true
            && (_refBB.Agent.destination - vMovePoint).sqrMagnitude < 0.01f)
            return eNodeState.Success;

        // 반환값을 안 보면 목표 지점이 NavMesh 밖이라 실패해도 Success로 보고돼, 다음
        // WaitCheckPointNode가 절대 줄지 않는 remainingDistance를 기다리며 영원히 Running에 갇힌다
        if (_refBB.Agent.SetDestination(vMovePoint) == false)
            return eNodeState.Failure;

        return eNodeState.Success;
    }
}
