using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                Sequence
기능 : 지정된 노드가 모두 성공해야 성공, 하나라도 실패하면 실패.
       매 틱 0번부터 다시 평가하므로 앞쪽 조건 노드가 계속 가드로 동작한다.
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SequenceNode", menuName = "Game/Monster/SequenceNode")]
public class SOSequenceNode : SOListNode
{
    private int iRanIdx = -1;

    private void OnEnable()
    {
        iRanIdx = -1;
    }

    public override eNodeState Execute(BlackBoard _refBB)
    {
        for (int i = 0; i < listNode.Count; ++i)
        {
            eNodeState eState = listNode[i].Execute(_refBB);

            if (eState == eNodeState.Failure)
            {
                // 이번 틱에 이미 실행된 앞 자식(i - 1)과 직전 틱까지 진행됐던 범위 중 넓은 쪽을 되돌린다
                Unwind(_refBB, iRanIdx > i - 1 ? iRanIdx : i - 1);
                return eNodeState.Failure;
            }

            if (eState == eNodeState.Running)
            {
                iRanIdx = i;
                return eNodeState.Running;
            }
        }

        iRanIdx = listNode.Count - 1;
        return eNodeState.Success;
    }

    public override void Abort(BlackBoard _refBB)
    {
        Unwind(_refBB, iRanIdx);
    }

    // Zoom처럼 Success를 반환하고 끝나는 자식도 상태를 되돌려야 하므로 참여한 범위 전체를 역순으로 정리
    private void Unwind(BlackBoard _refBB, int _iFromIdx)
    {
        for (int i = _iFromIdx; i >= 0; --i)
            listNode[i].Abort(_refBB);

        iRanIdx = -1;
    }
}
