using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
              SOZoomNode
기능 : 적 AI에 장착된 무기의 공격 준비를 하는 모션
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_ZoomNode", menuName = "Game/Monster/ActionNode/ZoomNode")]

public class SOZoomNode : SONode
{
    public override eNodeState Execute(BlackBoard _refBB)
    {
        if(_refBB.Weapon == null)
            return eNodeState.Failure;

        _refBB.Weapon.Zoom();
        return eNodeState.Success;
    }
}
