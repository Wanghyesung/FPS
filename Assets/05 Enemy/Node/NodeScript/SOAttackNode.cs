using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
              SOAttackNode
기능 : 적 AI에 장착된 무기를 통해서 플레이어를 공격하는 노드
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_AttackNode", menuName = "Game/Monster/ActionNode/AttackNode")]

public class SOAttackNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if(_refBB == null)
            return eNodeState.Failure;

        _refBB.Weapon.Fire(_refBB.TargetTr.position);
        return eNodeState.Success;
    }
}
