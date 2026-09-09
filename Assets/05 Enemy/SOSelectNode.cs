using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/*///////////////////////////////////////////
                SelectNode
기능 : 자식을 순차 실행해 Success/Running을 반환한 첫 자식에서 종료.
       매 틱 0번부터 다시 평가하므로 자식 순서가 곧 우선순위가 된다.
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "SO_SelectNode", menuName = "Game/Monster/SelectNode")]

public class SOSelectNode : SOListNode
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
                continue;

            // 우선순위에 밀려난 브랜치가 바꿔둔 에이전트/무기 상태를 되돌린다
            if (iRanIdx != -1 && iRanIdx != i)
                listNode[iRanIdx].Abort(_refBB);

            iRanIdx = i;
            return eState;
        }

        Abort(_refBB);
        return eNodeState.Failure;
    }

    public override void Abort(BlackBoard _refBB)
    {
        if (iRanIdx == -1)
            return;

        //새로운 인덱스로 갱신하기 전 이전의 인덱스의 노드 초기화
        listNode[iRanIdx].Abort(_refBB);
        iRanIdx = -1;
    }
}
