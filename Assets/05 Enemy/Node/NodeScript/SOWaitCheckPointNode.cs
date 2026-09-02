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

        if(_refBB.Agent.remainingDistance <= 1.0f)
        {
            _refBB.PatrolIdx += 1;
            return eNodeState.Success;
        }

        return eNodeState.Running;
    }
}
