using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/*///////////////////////////////////////////
             SORotateNode
기능 : 적 AI가 플레이어의 방향으로 회전하도록 하는 노드
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_RotateNode", menuName = "Game/Monster/ActionNode/RotateNode")]

public class SORotateNode : SONode
{
    [SerializeField] private float m_fRotateSpeed = 30.0f;
    public override eNodeState Execute(BlackBoard _refBB)
    {
        _refBB.Agent.updateRotation = false;
        // 이동하려는 방향
        Vector3 vTargetPos = _refBB.TargetTr.position;

        if (vTargetPos.sqrMagnitude > 0.001f)
        {
            Quaternion qTargetRotation = Quaternion.LookRotation(vTargetPos);

            Transform refOwnerTr = _refBB.Owner.transform;

            refOwnerTr.rotation =
                Quaternion.Slerp(refOwnerTr.rotation, qTargetRotation, Time.deltaTime * m_fRotateSpeed);

            // _refBB.Agent.updateRotation = false;
            return eNodeState.Success;
        }
        return eNodeState.Failure;
    }
}
