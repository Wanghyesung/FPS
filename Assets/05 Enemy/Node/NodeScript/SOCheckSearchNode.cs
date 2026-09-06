using UnityEngine;

/*///////////////////////////////////////////
            SOCheckSearchNode
기능 : 수색할 목격 기억이 남아 있는지 확인하는 조건 노드.

       기억의 만료(일정 시간이 지나면 HasLastSeen = false)는 SOPerceptionNode가
       매 틱 처리하므로 여기서는 결과만 읽는다 — 만료 판정을 두 곳에 두면
       시간 기준이 어긋났을 때 원인을 찾기 어려워진다.

       진행 중인 수색은 무조건 통과시킨다. SOSearchNode가 도착 후 두리번거리기를
       마치며 HasLastSeen을 지우는데, 그 전에 이 노드가 먼저 실패해버리면
       수색이 매번 중간에 끊긴다.
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
