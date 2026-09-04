using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
             SOIsArrivePointNode
기능 :다음으로 가야할 위치까지 도착했는지 확인하는 노드 
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_IsArrivePointNode", menuName = "Game/Monster/ActionNode/IsArrivePointNode")]
public class SOIsArrivePointNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.Agent.isStopped == true)
            return eNodeState.Failure;

        // SetDestination 직후 경로 계산이 끝나기 전(pathPending)엔 remainingDistance가
        // 아직 갱신되지 않아 0으로 읽혀, 도착하지도 않았는데 도착으로 오판한다
        if (_refBB.Agent.pathPending == true)
            return eNodeState.Failure;

        if (_refBB.Agent.remainingDistance <= 1.0f)
        {
            _refBB.PatrolIdx += 1;
            return eNodeState.Success;
        }

        return eNodeState.Failure;
    }
}
