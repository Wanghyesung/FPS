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

        // 반환값을 안 보면 목표 지점이 NavMesh 밖이라 실패해도 Success로 보고돼, 다음
        // WaitCheckPointNode가 절대 줄지 않는 remainingDistance를 기다리며 영원히 Running에 갇힌다
        bool bSetOk = _refBB.Agent.SetDestination(refMovePoint.position);

#if UNITY_EDITOR
        Debug.Log($"[CheckPointNode] Idx:{_refBB.PatrolIdx} Target:{refMovePoint.position} SetDestination:{bSetOk} pathStatus:{_refBB.Agent.pathStatus} isStopped:{_refBB.Agent.isStopped} updateRotation:{_refBB.Agent.updateRotation}", _refBB.Owner);
#endif

        if (bSetOk == false)
            return eNodeState.Failure;

        return eNodeState.Success;
    }
}
