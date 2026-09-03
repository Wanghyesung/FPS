using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;


/*///////////////////////////////////////////
             SOCheckAngleNode
기능 : 플레이어가 바라보는 방향과 내 방향이 얼마나 맞는지 체크하는 노드
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckAngleNode", menuName = "Game/Monster/ActionNode/CheckAngleNode")]

public class SOCheckAngleNode : SONode
{
    [Description("각도 차이가 적어야 하나")]
    [SerializeField] private float m_fRotateDiff = 3.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        Vector3 vTargetPos = _refBB.TargetTr.position;

        if (vTargetPos.sqrMagnitude > 0.001f)
        {
            Quaternion qTargetRotation = Quaternion.LookRotation(vTargetPos);
            Transform refOwnerTr = _refBB.Owner.transform;

            if (Quaternion.Angle(refOwnerTr.rotation, qTargetRotation) < m_fRotateDiff)
                return eNodeState.Success;

            return eNodeState.Failure;

        }
        return eNodeState.Failure;
    }
}
