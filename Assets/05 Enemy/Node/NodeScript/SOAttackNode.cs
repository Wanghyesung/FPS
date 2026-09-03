using System.Collections;
using System.Collections.Generic;
using UnityEditor;
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
        if (_refBB == null || _refBB.Weapon == null || _refBB.TargetTr == null)
            return eNodeState.Failure;

        // 쿨다운 중이면 Sequence를 Running으로 붙잡아둔다 — Failure로 반환하면
        // Selector가 곧장 Chase/Patrol로 넘어가 버려 사격 텀마다 상태가 튄다
        if (_refBB.Weapon.CheckTime() == false)
            return eNodeState.Running;

        _refBB.Weapon.RequestFire(_refBB.TargetTr.position);

        return eNodeState.Success;
    }
}
