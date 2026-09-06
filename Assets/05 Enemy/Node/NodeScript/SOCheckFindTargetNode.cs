using UnityEngine;

/*///////////////////////////////////////////
           SOCheckFindTargetNode
기능 : SOPerceptionNode가 갱신해둔 BlackBoard.FindTarget을 읽는 조건 노드
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckFindTargetNode", menuName = "Game/Monster/ActionNode/CheckFindTargetNode")]

public class SOCheckFindTargetNode : SONode
{
    [Tooltip("체크를 뒤집어 '안 보일 때' Success로 쓴다")]
    [SerializeField] private bool m_bInvert;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        bool bFind = m_bInvert == true ? !_refBB.FindTarget : _refBB.FindTarget;

        return bFind == true ? eNodeState.Success : eNodeState.Failure;
    }
}
