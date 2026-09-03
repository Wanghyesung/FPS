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
    [SerializeField] private float m_fRotateDiff = 3.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null)
            return eNodeState.Failure;

        Transform refOwnerTr = _refBB.Owner.transform;

        // 방향(타겟 좌표 - 내 좌표)을 써야 하는데 기존엔 타겟의 월드 좌표를 그대로
        // LookRotation에 넣고 있었다 — 원점 근처가 아니면 완전히 엉뚱한 방향을 봄
        Vector3 vDir = _refBB.TargetTr.position - refOwnerTr.position;
        vDir.y = 0f;

        if (vDir.sqrMagnitude < 0.001f)
            return eNodeState.Failure;

        _refBB.Agent.updateRotation = false;

        Quaternion qTargetRotation = Quaternion.LookRotation(vDir);
        refOwnerTr.rotation =
            Quaternion.Slerp(refOwnerTr.rotation, qTargetRotation, Time.deltaTime * m_fRotateSpeed);

        // 목표 각도까지 덜 돌았으면 Running으로 Sequence를 붙잡아둔다 — Success를 바로
        // 반환하면 다음 노드(Zoom/Attack)가 덜 돈 상태에서 바로 실행돼 버린다
        if (Quaternion.Angle(refOwnerTr.rotation, qTargetRotation) > m_fRotateDiff)
            return eNodeState.Running;

        return eNodeState.Success;
    }
}
