using UnityEngine;

/*///////////////////////////////////////////
            SOCheckSearchNode
기능 : 수색할 목격 기억이 남아 있는지 확인하는 조건 노드.

       기억의 만료(일정 시간이 지나면 HasLastSeen = false)는 SOPerceptionNode가
       매 틱 처리하므로 여기서는 결과만 읽는다 
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckSearchNode", menuName = "Game/Monster/ActionNode/CheckSearchNode")]

public class SOCheckSearchNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.SearchPhase != eSearchPhase.None)
            return eNodeState.Success;

        return _refBB.HasLastSeen == true ? eNodeState.Success : eNodeState.Failure;
    }
}
