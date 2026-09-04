using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
             SOWaitCheckPointNode
기능 :다음으로 가야할 위치까지 기다리는 노드 
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_WaitCheckPointNode", menuName = "Game/Monster/ActionNode/WaitCheckPointNode")]
public class SOWaitCheckPointNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.Agent.isStopped == true)
            return eNodeState.Failure;

        // SetDestination 직후 경로 계산이 끝나기 전(pathPending)엔 remainingDistance가
        // 아직 갱신되지 않아 0으로 읽혀, 도착하지도 않았는데 바로 Success로 오판한다
        if (_refBB.Agent.pathPending == true)
            return eNodeState.Running;

        if(_refBB.Agent.remainingDistance <= 1.0f)
        {
            _refBB.PatrolIdx += 1;
            return eNodeState.Success;
        }

        return eNodeState.Running;
    }
}
