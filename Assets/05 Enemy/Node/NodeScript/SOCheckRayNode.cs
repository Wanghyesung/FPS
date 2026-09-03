using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
/*///////////////////////////////////////////
             SOCheckRayNode
기능 : Owner에서 TargetTr로 시야가 막힘없이 트이는지 체크하는 노드
       (장애물에 가리면 Failure → Attack 시퀀스 실패 → Patrol로 전환)
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckRayNode", menuName = "Game/Monster/ActionNode/CheckRayNode")]

public class SOCheckRayNode : SONode
{
    [Description("레이가 충돌 검사할 레이어 (장애물 + Player 둘 다 포함해야 함)")]
    [SerializeField] private LayerMask m_tCollideMask;
    [SerializeField] private Vector3 m_vEyeOffset = new Vector3(0f, 1.5f, 0f); // 가슴/눈높이
    [SerializeField] private float m_fRayRadius = 0.2f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        Transform refOwnerTr = _refBB.OwnerOffset != null ? _refBB.OwnerOffset : _refBB.Owner.transform;
        Vector3 vOrigin = refOwnerTr.position + m_vEyeOffset;
        Vector3 vDelta = (_refBB.TargetTr.position + m_vEyeOffset) - vOrigin;

        if (Physics.SphereCast(vOrigin, m_fRayRadius, vDelta.normalized, out RaycastHit tHit, vDelta.magnitude, m_tCollideMask) == true)
        {
            // 맨 처음 맞은 게 벽이 아니라 실제로 내가 찾는 타겟인지 확인
            if (tHit.transform == _refBB.TargetTr || tHit.transform.IsChildOf(_refBB.TargetTr))
                return eNodeState.Success;
        }
        return eNodeState.Failure;
    }
}
