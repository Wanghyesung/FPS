using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
             SOCheckHPNode
기능 : 적 AI가 현제 체력이 몇 이하로 떨어졌는지 체크하는 노드
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckHPNode", menuName = "Game/Monster/ActionNode/CheckHPNode")]

public class SOCheckHPNode : SONode
{
    [SerializeField] private float m_fCheckHP = 30.0f;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.ObjInfo.CurrentHP <= m_fCheckHP)
            return eNodeState.Success;

        return eNodeState.Failure;
    }
}
