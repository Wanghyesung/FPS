using UnityEngine;
using UnityEngine.AI;

/*///////////////////////////////////////////
              SOResumeMoveNode
기능 : 교전/도주 중에 바뀐 이동 관련 상태를 한 번에 기본값으로 되돌려
       다시 NavMeshAgent로 정상 순찰 이동을 할 수 있게 만드는 노드.

       되돌리는 것 : isStopped(정지) / updateRotation(회전 잠금)
                     Agent.speed(도주 속도 배율) / IsEscaping(도주 플래그) / Zoom(조준 자세)

       순찰 시퀀스 맨 앞에 두어 "순찰 전 이동 복구" 책임을 한 곳으로 모은다 —
       예전엔 SOCheckPointNode가 updateRotation만 슬쩍 되돌리고 있어서
       정지/속도/조준 상태는 교전 때 값 그대로 남는 문제가 있었다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_ResumeMoveNode", menuName = "Game/Monster/ActionNode/ResumeMoveNode")]

public class SOResumeMoveNode : SONode
{
    [Tooltip("도주 상태 플래그(BlackBoard.IsEscaping)를 함께 초기화할지")]
    [SerializeField] private bool m_bResetEscape = true;

    [Tooltip("교전 중 올려둔 조준(Zoom) 자세를 함께 내릴지")]
    [SerializeField] private bool m_bUnZoom = true;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        NavMeshAgent refAgent = _refBB.Agent;

        // NavMesh 밖에서는 isStopped 대입만으로도 매 프레임 에러가 찍히므로 건드리지 않는다
        if (refAgent == null || refAgent.isOnNavMesh == false)
        {
#if UNITY_EDITOR
            Debug.Log($"[ResumeMoveNode] 실패 - isOnNavMesh: {(refAgent == null ? "Agent 없음" : refAgent.isOnNavMesh.ToString())}", _refBB.Owner);
#endif
            return eNodeState.Failure;
        }

        refAgent.isStopped = false;
        refAgent.updateRotation = true;
        refAgent.speed = _refBB.ObjInfo.Speed;

        if (m_bResetEscape == true)
            _refBB.IsEscaping = false;

        if (m_bUnZoom == true && _refBB.Weapon != null)
            _refBB.Weapon.UnZoom();

        return eNodeState.Success;
    }
}
