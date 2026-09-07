using UnityEngine;

/*///////////////////////////////////////////
              SOCheckHitNode
기능 : 피격했는데 아직 그쪽을 돌아보지 않았는지 확인하는 조건 노드.
       Enemy.TakeDamage가 BlackBoard.HasPendingHit을 세워준다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckHitNode", menuName = "Game/Monster/ActionNode/CheckHitNode")]

public class SOCheckHitNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        return _refBB.HasPendingHit == true ? eNodeState.Success : eNodeState.Failure;
    }
}
