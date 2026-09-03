using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
              SOEscapeNode
기능 : HP가 낮을 때 플레이어의 반대 방향으로 도망가는 노드.
      쿨다운이 끝났는데도 HP가 낮으면 그때 새
      지점으로 다시 도망간다 → "한동안 쏘다가 다시 도망" 패턴이 된다.

 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_EscapeNode", menuName = "Game/Monster/ActionNode/EscapeNode")]

public class SOEscapeNode : SONode
{
    [Header("Escape Range")]
    [Tooltip("도주 목표를 뽑을 최소 거리")]
    [SerializeField] private float m_fMinDistance = 8.0f;
    [Tooltip("도주 목표를 뽑을 최대 거리")]
    [SerializeField] private float m_fMaxDistance = 16.0f;
    [Tooltip("플레이어 반대 방향을 중심으로 좌우로 벌어질 부채꼴 전체 각도")]
    [SerializeField] private float m_fSpreadAngle = 120.0f;

    [Header("NavMesh Sample")]
    [Tooltip("이동 가능한 지점을 찾기 위해 각도/거리를 몇 번까지 다시 굴려볼지")]
    [SerializeField] private int m_iSampleCount = 8;
    [Tooltip("뽑은 좌표 주변에서 NavMesh를 찾아줄 허용 반경")]
    [SerializeField] private float m_fSampleRadius = 2.0f;

    [Header("Move")]
    [Tooltip("도주 중 이동 속도 배율")]
    [SerializeField] private float m_fSpeedRate = 1.3f;
    [Tooltip("도주 목표에 이만큼 가까워지면 도착으로 처리")]
    [SerializeField] private float m_fArriveDistance = 1.0f;

    [Header("Cooldown")]
    [Tooltip("도주 도착 후, HP가 낮아도 재도주하지 않고 교전하게 둘 시간(초)")]
    [SerializeField] private float m_fEscapeCooldown = 5.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.Owner == null || _refBB.TargetTr == null)
            return eNodeState.Failure;

        NavMeshAgent refAgent = _refBB.Agent;

        // NavMesh 밖(스폰 직후·추락 등)에서는 isStopped 대입과 SetDestination이 매 프레임
        // 에러를 뱉으므로 아예 건드리지 않고 실패로 넘긴다
        if (refAgent == null || refAgent.isOnNavMesh == false)
            return eNodeState.Failure;

        if (_refBB.IsEscaping == true)
            return CheckEscaping(_refBB, refAgent);

        // 방금 도주를 마쳤다면 쿨다운이 끝나기 전까지는 실패로 돌려보내 Attack이 대신 돌게 한다
        if (Time.time < _refBB.NextEscapeTime)
            return eNodeState.Failure;

        if (TryPickEscapePos(_refBB, refAgent, out Vector3 vEscapePos) == false)
            return eNodeState.Failure;

       
        refAgent.updateRotation = true;
        refAgent.isStopped = false;

        // 기준값이 항상 ObjInfo.Speed(원본 속도)라서 목표를 여러 번 다시 뽑아도 배율이 누적되지 않는다
        if (_refBB.ObjInfo != null && _refBB.ObjInfo.Speed > 0.0f)
            refAgent.speed = _refBB.ObjInfo.Speed * m_fSpeedRate;

        if (refAgent.SetDestination(vEscapePos) == false)
            return eNodeState.Failure;

        _refBB.EscapePos = vEscapePos;
        _refBB.IsEscaping = true;

        if (_refBB.Weapon != null)
            _refBB.Weapon.UnZoom();

        return eNodeState.Running;
    }

    // 이미 잡아둔 목표로 달리는 중
    private eNodeState CheckEscaping(BlackBoard _refBB, NavMeshAgent _refAgent)
    {
        if (_refAgent.pathPending == true)
            return eNodeState.Running;

      
        // BlackBoard에 저장해둔 목표 좌표와의 실제 거리로도 같이 도착을 확인한다
        Vector3 vToEscapePos = _refBB.EscapePos - _refBB.Owner.transform.position;
        vToEscapePos.y = 0f;

        bool bArrived = _refAgent.remainingDistance <= m_fArriveDistance
            || vToEscapePos.sqrMagnitude <= (m_fArriveDistance * m_fArriveDistance);

        if (bArrived == true)
        {
            _refBB.IsEscaping = false;
            _refBB.NextEscapeTime = Time.time + m_fEscapeCooldown; // 쿨다운 동안은 Attack이 우선권을 가짐
            return eNodeState.Success;
        }

        // 끊긴 구역을 목표로 잡아 경로가 무산된 경우
        if (_refAgent.hasPath == false || _refAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            _refBB.IsEscaping = false;
            return eNodeState.Failure;
        }

        return eNodeState.Running;
    }

    // 플레이어 → 나 방향(vAwayDir)을 중심축으로 부채꼴 안에서 각도·거리를 무작위로 굴리기
    private bool TryPickEscapePos(BlackBoard _refBB, NavMeshAgent _refAgent, out Vector3 _vResult)
    {
        Vector3 vOwnerPos = _refBB.Owner.transform.position;

        Vector3 vAwayDir = vOwnerPos - _refBB.TargetTr.position;
        vAwayDir.y = 0f;

        // 플레이어와 좌표가 거의 겹쳐 방향이 안 나오면 지금 등지고 있는 쪽을 기준으로 삼는다
        if (vAwayDir.sqrMagnitude < 0.0001f)
            vAwayDir = -_refBB.Owner.transform.forward;

        vAwayDir.Normalize();

        float fHalfAngle = m_fSpreadAngle * 0.5f;

        for (int i = 0; i < m_iSampleCount; ++i)
        {
            float fYaw = UnityEngine.Random.Range(-fHalfAngle, fHalfAngle);
            float fDistance = UnityEngine.Random.Range(m_fMinDistance, m_fMaxDistance);

            Vector3 vDir = Quaternion.AngleAxis(fYaw, Vector3.up) * vAwayDir;
            Vector3 vCandidate = vOwnerPos + (vDir * fDistance);

            // NavMeshHit은 struct + out이라 이 루프는 힙 할당이 없다
            if (NavMesh.SamplePosition(vCandidate, out NavMeshHit tHit, m_fSampleRadius, _refAgent.areaMask) == false)
                continue;

            _vResult = tHit.position;
            return true;
        }

        _vResult = vOwnerPos;
        return false;
    }
}
