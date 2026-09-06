using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
              SOEscapeNode
기능 : 플레이어 반대편 엄폐 지점을 골라 거기까지 달리는 "이동 전담" 노드.
       도착하면 EscapePhase를 Hiding으로 넘기고 Success — 그 다음은 SOHideNode가 맡는다.

       진입 조건(HP 임계 · 재도주 쿨다운)은 SOCheckEscapeNode가,
       은신과 쿨다운 설정은 SOHideNode가 가져갔다. 여기는 이동만 책임진다.
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

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.Owner == null || _refBB.TargetTr == null)
            return eNodeState.Failure;

        NavMeshAgent refAgent = _refBB.Agent;

        
        if (refAgent == null || refAgent.isOnNavMesh == false)
            return eNodeState.Failure;

        // 이미 도착해 은신 중이면 목표를 다시 뽑지 않고 그대로 통과시켜 SOHideNode에게 넘긴다.
        if (_refBB.EscapePhase == eEscapePhase.Hiding)
            return eNodeState.Success;

        if (_refBB.EscapePhase == eEscapePhase.Moving)
            return CheckEscaping(_refBB, refAgent);

        return StartEscape(_refBB, refAgent);
    }

    // 엄폐 지점을 새로 골라 이동을 시작한다
    private eNodeState StartEscape(BlackBoard _refBB, NavMeshAgent _refAgent)
    {
        if (TryPickEscapePos(_refBB, _refAgent, out Vector3 vEscapePos) == false)
            return eNodeState.Failure;

        _refAgent.updateRotation = true;
        _refAgent.isStopped = false;

        // 기준값이 항상 ObjInfo.Speed(원본 속도)라서 목표를 여러 번 다시 뽑아도 배율이 누적되지 않는다
        if (_refBB.ObjInfo != null && _refBB.ObjInfo.Speed > 0.0f)
            _refAgent.speed = _refBB.ObjInfo.Speed * m_fSpeedRate;

        if (_refAgent.SetDestination(vEscapePos) == false)
            return eNodeState.Failure;

        _refBB.EscapePos = vEscapePos;
        _refBB.EscapePhase = eEscapePhase.Moving;

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
            // 은신 단계로 넘긴다 — 쿨다운은 SOHideNode가 은신을 마칠 때 건다
            _refBB.EscapePhase = eEscapePhase.Hiding;
            RestoreSpeed(_refBB);
            return eNodeState.Success;
        }

        // 끊긴 구역을 목표로 잡아 경로가 무산된 경우
        if (_refAgent.hasPath == false || _refAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            _refBB.EscapePhase = eEscapePhase.None;
            RestoreSpeed(_refBB);
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

    public override void Abort(BlackBoard _refBB)
    {
        RestoreSpeed(_refBB);

        // 은신 중(Hiding)이라면 SOHideNode.Abort가 먼저 정리한다 — Sequence는 역순으로 되돌리므로
        // 여기서 Hiding까지 지우면 은신 해제 처리(isStopped 복구)를 건너뛰게 된다
        if (_refBB.EscapePhase == eEscapePhase.Moving)
            _refBB.EscapePhase = eEscapePhase.None;
    }

    // 도주 배율(m_fSpeedRate)이 다음 행동까지 남지 않도록 원본 속도로 되돌린다
    private void RestoreSpeed(BlackBoard _refBB)
    {
        if (_refBB.Agent == null || _refBB.ObjInfo == null || _refBB.ObjInfo.Speed <= 0.0f)
            return;

        _refBB.Agent.speed = _refBB.ObjInfo.Speed;
    }
}
