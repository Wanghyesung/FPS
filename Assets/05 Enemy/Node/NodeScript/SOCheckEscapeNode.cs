using UnityEngine;

/*///////////////////////////////////////////
            SOCheckEscapeNode
기능 : 도주 브랜치의 진입 가드. 서로 의존적인 조건들을 한 노드에서 판정한다.
       (1) 이미 도주/은신이 진행 중이면 무조건 통과 — 에피소드가 중간에 끊기지 않게
       (2) 직전 은신의 재도주 쿨다운이 끝났는지
       (3) HP가 임계 이하이거나, 이번 교전 시간(CombatEndTime)을 다 채웠는지

       조건 노드 3개를 Sequence로 나열하는 방식으로는 표현할 수 없다 —
       (1)이 나머지 둘을 단락(short-circuit)시켜야 하기 때문.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CheckEscapeNode", menuName = "Game/Monster/ActionNode/CheckEscapeNode")]

public class SOCheckEscapeNode : SONode
{
    [Tooltip("이 값 이하로 떨어지면 도주를 시작한다")]
    [SerializeField] private float m_fCheckHP = 30.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        if (_refBB.ObjInfo == null)
            return eNodeState.Failure;

        // 진행 중인 도주 에피소드는 HP/쿨다운을 다시 보지 않고 그대로 이어간다
        if (_refBB.EscapePhase != eEscapePhase.None)
            return eNodeState.Success;

        // 은신을 마친 직후엔 한동안 교전하게 두고, 쿨다운이 끝나야 다시 도주한다.
        // 두 발동 조건에 공통으로 걸리므로 먼저 본다
        if (Time.time < _refBB.NextEscapeTime)
            return eNodeState.Failure;

        // (1) 체력이 바닥났다
        bool bLowHP = _refBB.ObjInfo.CurrentHP <= m_fCheckHP;

        // (2) 이번 교전을 굴린 시간만큼 다 싸웠다 — 계속 제자리에서 쏘기만 하면 단조로우므로
        //     한 번씩 빠져서 자리를 바꾸게 한다. 0이면 아직 교전을 시작하지 않은 것
        bool bCombatOver = _refBB.CombatEndTime > 0.0f && Time.time >= _refBB.CombatEndTime;

        return (bLowHP == true || bCombatOver == true) ? eNodeState.Success : eNodeState.Failure;
    }
}
