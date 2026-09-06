using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
                SOHideNode
기능 : 엄폐 지점에 도착한 뒤 몸을 숨기고 버티는 노드.
       은신 시간이 지나거나 플레이어가 기습 사거리 안으로 들어오면 종료
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_HideNode", menuName = "Game/Monster/ActionNode/HideNode")]

public class SOHideNode : SONode
{
    [Tooltip("숨어서 버티는 시간(초). 지나면 스스로 뛰쳐나온다")]
    [SerializeField] private float m_fHideDuration = 4.0f;

    [Tooltip("플레이어가 이 거리 안으로 들어오면 시간이 남아도 기습으로 전환한다")]
    [SerializeField] private float m_fAmbushRange = 12.0f;

    [Tooltip("은신을 마친 뒤 재도주를 금지할 시간(초)")]
    [SerializeField] private float m_fEscapeCooldown = 5.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.EscapePhase != eEscapePhase.Hiding)
            return eNodeState.Failure;

        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent.isOnNavMesh == true && refAgent.isStopped == false)
            refAgent.isStopped = true;

        // 도착 첫 틱에만 종료 시각을 잡는다 — Sequence가 매 틱 재평가하므로 갱신하면 영원히 안 끝난다
        if (_refBB.HideEndTime <= 0.0f)
            _refBB.HideEndTime = Time.time + m_fHideDuration;

        if (IsAmbushReady(_refBB) == true || Time.time >= _refBB.HideEndTime)
        {
            EndHide(_refBB);
            return eNodeState.Success;
        }

        return eNodeState.Running;
    }

    private bool IsAmbushReady(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null || _refBB.Owner == null)
            return false;

        Vector3 vDelta = _refBB.TargetTr.position - _refBB.Owner.transform.position;

        return vDelta.sqrMagnitude <= (m_fAmbushRange * m_fAmbushRange);
    }

    // 은신 종료 — 쿨다운을 걸어 다음 틱부터 SOCheckEscapeNode가 실패하게 만든다
    private void EndHide(BlackBoard _refBB)
    {
        _refBB.EscapePhase = eEscapePhase.None;
        _refBB.HideEndTime = 0.0f;
        _refBB.NextEscapeTime = Time.time + m_fEscapeCooldown;

        ReleaseAgent(_refBB);
    }

    public override void Abort(BlackBoard _refBB)
    {
        if (_refBB.EscapePhase != eEscapePhase.Hiding)
            return;

        _refBB.EscapePhase = eEscapePhase.None;
        _refBB.HideEndTime = 0.0f;

        ReleaseAgent(_refBB);
    }

    private void ReleaseAgent(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return;

        refAgent.isStopped = false;
    }
}
