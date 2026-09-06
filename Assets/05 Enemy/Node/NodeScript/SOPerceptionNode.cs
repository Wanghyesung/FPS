using UnityEngine;

/*///////////////////////////////////////////
             SOPerceptionNode
기능 : 거리 / 시야각 / 시야 차단을 한 번에 판정해 BlackBoard에 기록하는 노드.
       트리 맨 앞에 두어 모든 브랜치가 같은 지각 결과를 공유한다. 항상 Success.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_PerceptionNode", menuName = "Game/Monster/ActionNode/PerceptionNode")]

public class SOPerceptionNode : SONode
{
    [Header("Sight")]
    [Tooltip("이 거리 밖은 보지 못한다")]
    [SerializeField] private float m_fSightRange = 40.0f;
    [Tooltip("BlackBoard.OwnerOffset이 비어 있을 때 쓸 눈높이")]
    [SerializeField] private float m_fEyeHeight = 1.6f;

    [Header("Ray")]
    [Tooltip("시야를 가리는 레이어 + 타겟 레이어를 함께 넣을 것 (자기 자신인 Enemy는 제외)")]
    [SerializeField] private LayerMask m_tCollideMask;
    [SerializeField] private float m_fRayRadius = 0.2f;

    [Header("Memory")]
    [Tooltip("마지막으로 본 뒤 이 시간이 지나면 목격 기억을 지운다")]
    [SerializeField] private float m_fMemoryDuration = 8.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (CanSeeTarget(_refBB) == true)
        {
            _refBB.FindTarget = true;
            _refBB.HasLastSeen = true;
            _refBB.LastSeenPos = _refBB.TargetTr.position;
            _refBB.LastSeenTime = Time.time;
            _refBB.BlockedSinceTime = 0.0f;
            return eNodeState.Success;
        }

        // 보이다가 막 놓친 순간만 기록한다 - 재배치 판정이 이 시각으로 차단 지속시간을 잰다
        if (_refBB.FindTarget == true)
            _refBB.BlockedSinceTime = Time.time;

        _refBB.FindTarget = false;

        if (_refBB.HasLastSeen == true && Time.time - _refBB.LastSeenTime > m_fMemoryDuration)
            _refBB.HasLastSeen = false;

        return eNodeState.Success;
    }

    private bool CanSeeTarget(BlackBoard _refBB)
    {
        if (_refBB.TargetTr == null || _refBB.Owner == null)
            return false;

        Vector3 vEyePos = GetEyePos(_refBB);
        Vector3 vDelta = _refBB.TargetTr.position - vEyePos;

        if (vDelta.sqrMagnitude > m_fSightRange * m_fSightRange)
            return false;

        Vector3 vFlatDir = vDelta;
        vFlatDir.y = 0.0f;

        if (vFlatDir.sqrMagnitude < 0.0001f)
            return true;

        if (Vector3.Angle(_refBB.Owner.transform.forward, vFlatDir.normalized) > _refBB.POV * 0.5f)
            return false;

        float fDistance = vDelta.magnitude;

        // 맨 처음 맞은 게 벽이 아니라 타겟 본인이어야 시야가 트인 것
        if (Physics.SphereCast(vEyePos, m_fRayRadius, vDelta / fDistance, out RaycastHit tHit,
                fDistance, m_tCollideMask, QueryTriggerInteraction.Ignore) == false)
            return false;

        return IsTarget(_refBB, tHit.transform);
    }

    // root 비교는 못 쓴다 - 씬에서 Player와 Enemy가 같은 부모 아래 묶여 있어 다른 적을 맞혀도 통과한다
    private bool IsTarget(BlackBoard _refBB, Transform _refHitTr)
    {
        if (_refBB.TargetRoot != null)
            return _refHitTr.IsChildOf(_refBB.TargetRoot);

        return _refHitTr.root == _refBB.TargetTr.root;
    }

    private Vector3 GetEyePos(BlackBoard _refBB)
    {
        if (_refBB.OwnerOffset != null)
            return _refBB.OwnerOffset.position;

        return _refBB.Owner.transform.position + (Vector3.up * m_fEyeHeight);
    }
}
