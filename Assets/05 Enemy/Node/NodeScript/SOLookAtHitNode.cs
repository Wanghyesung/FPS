using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
             SOLookAtHitNode
기능 : 맞은 방향으로 몸을 돌리고 잠시 그쪽을 주시하는 노드.

       핵심은 "돌아보는 것 자체가 탐지"라는 점이다. 이 노드는 플레이어를 찾지 않는다.
       시야각(POV) 콘을 피격 방향으로 돌려놓기만 하면, 매 틱 도는 SOPerceptionNode가
       그 콘 안에 들어온 플레이어를 잡아 FindTarget을 세우고, 우선순위가 높은
       교전 브랜치가 이 브랜치를 밀어낸다(Abort).

       그래서 등 뒤에서 저격당해도 적이 반응할 수 있다 — 예전에는 시야각 밖이라
       영원히 발견하지 못한 채 순찰만 돌았다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_LookAtHitNode", menuName = "Game/Monster/ActionNode/LookAtHitNode")]

public class SOLookAtHitNode : SONode
{
    [Tooltip("돌아보는 속도. SORotateNode(30)보다 낮게 잡아 '멈칫하고 돌아보는' 느낌을 준다")]
    [SerializeField] private float m_fRotateSpeed = 8.0f;

    [Tooltip("이 각도 안으로 들어오면 다 돌아본 것으로 처리")]
    [SerializeField] private float m_fRotateDiff = 5.0f;

    [Tooltip("다 돌아본 뒤 그 방향을 주시하는 시간(초). 이 사이에 플레이어가 콘에 들어오면 교전으로 전환된다")]
    [SerializeField] private float m_fAlertHoldTime = 1.5f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.Owner == null)
            return eNodeState.Failure;

        Vector3 vLookDir = _refBB.HitFromDir;
        vLookDir.y = 0.0f;

        // 방향을 못 구한 피격이면 돌아볼 곳이 없다
        if (vLookDir.sqrMagnitude < 0.0001f)
        {
            ClearAlert(_refBB);
            return eNodeState.Failure;
        }

        HoldAgent(_refBB);

        Transform refOwnerTr = _refBB.Owner.transform;
        Quaternion qTarget = Quaternion.LookRotation(vLookDir.normalized);

        refOwnerTr.rotation =
            Quaternion.Slerp(refOwnerTr.rotation, qTarget, Time.deltaTime * m_fRotateSpeed);

        if (Quaternion.Angle(refOwnerTr.rotation, qTarget) > m_fRotateDiff)
            return eNodeState.Running;

        // 다 돌아봤다 - 첫 틱에만 주시 종료 시각을 잡는다
        // (Sequence가 매 틱 재평가하므로 갱신하면 영원히 안 끝난다)
        if (_refBB.AlertEndTime <= 0.0f)
            _refBB.AlertEndTime = Time.time + m_fAlertHoldTime;

        if (Time.time < _refBB.AlertEndTime)
            return eNodeState.Running;

        // 아무도 안 나타났다 - 경계를 풀고 순찰로 돌려보낸다
        ClearAlert(_refBB);
        ReleaseAgent(_refBB);

        return eNodeState.Success;
    }

    public override void Abort(BlackBoard _refBB)
    {
        // 교전으로 밀려났다면 돌아본 목적을 이룬 것이므로 경계를 지운다
        ClearAlert(_refBB);
        ReleaseAgent(_refBB);
    }

    // 걸어가면서 돌면 이동 방향과 시선이 어긋나 어색하므로 멈춰 세우고 돌린다
    private void HoldAgent(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return;

        if (refAgent.isStopped == false)
            refAgent.isStopped = true;

        if (refAgent.updateRotation == true)
            refAgent.updateRotation = false;
    }

    private void ReleaseAgent(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return;

        refAgent.isStopped = false;
        refAgent.updateRotation = true;
    }

    private void ClearAlert(BlackBoard _refBB)
    {
        _refBB.HasPendingHit = false;
        _refBB.AlertEndTime = 0.0f;
    }
}
