using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
             SOWaitCheckPointNode
기능 :다음으로 가야할 위치까지 기다리는 노드
      도착 판정은 에이전트 내부 상태가 아니라 BlackBoard에 저장해둔 목표 좌표와의
      실제 거리로 한다 
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_WaitCheckPointNode", menuName = "Game/Monster/ActionNode/WaitCheckPointNode")]
public class SOWaitCheckPointNode : SONode
{
    [Tooltip("목표에 이만큼 가까워지면 도착으로 처리")]
    [SerializeField] private float m_fArriveDistance = 1.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        if (refAgent == null || refAgent.isOnNavMesh == false)
            return eNodeState.Failure;

        if (refAgent.isStopped == true)
        {
            _refBB.HasCheckPoint = false;
            return eNodeState.Failure;
        }

        // SetDestination은 비동기라, 호출한 그 프레임엔 pathPending이 아직 false이고
        // 이전 경로도 이미 지워져 remainingDistance가 0으로 읽힌다. Sequence가 같은 틱에
        // 이 노드를 실행하므로 그 값을 믿으면 매번 즉시 도착으로 오판해 목표를 다시 뽑는다.
        // 그래서 목표 좌표와의 실제 평면 거리로 판정한다
        Vector3 vToPoint = _refBB.CheckPointPos - refAgent.transform.position;
        vToPoint.y = 0.0f;

        if (vToPoint.sqrMagnitude <= (m_fArriveDistance * m_fArriveDistance))
        {
            _refBB.HasCheckPoint = false;
            return eNodeState.Success;
        }

        if (refAgent.pathPending == false && refAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            _refBB.HasCheckPoint = false;
            return eNodeState.Failure;
        }

        return eNodeState.Running;
    }
}
