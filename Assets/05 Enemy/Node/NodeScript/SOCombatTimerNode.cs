using UnityEngine;

/*///////////////////////////////////////////
            SOCombatTimerNode
기능 : 교전을 시작할 때 "이번엔 몇 초나 싸울지"를 무작위로 굴려 BlackBoard에 적어두는 노드.
       판정만 하고 행동은 하지 않으므로 항상 Success — 뒤의 Sel_Engage로 그대로 넘어간다.

       시간이 다 되면 SOCheckEscapeNode가 이 값을 보고 도주를 발동시킨다.
       즉 "적당히 쏘다가 빠진다"는 리듬이 도주 브랜치를 그대로 재사용해서 나온다 —
       도주 → 은신 → 기습 복귀 → 쿨다운 흐름이 이미 있으므로 새로 만들 게 없다.

       교전이 끊기면(플레이어를 놓치거나 도주로 밀려나면) Abort에서 0으로 지운다.
       그래야 다음 교전이 새 시간을 다시 굴린다.
 *///////////////////////////////////////////
[CreateAssetMenu(fileName = "SO_CombatTimerNode", menuName = "Game/Monster/ActionNode/CombatTimerNode")]

public class SOCombatTimerNode : SONode
{
    [Tooltip("한 번의 교전을 유지할 최소 시간(초)")]
    [SerializeField] private float m_fMinCombatTime = 4.0f;

    [Tooltip("한 번의 교전을 유지할 최대 시간(초)")]
    [SerializeField] private float m_fMaxCombatTime = 9.0f;

    public override eNodeState Execute(BlackBoard _refBB)
    {
        // Sequence가 매 틱 0번부터 재평가하므로, 이미 굴려둔 값이 있으면 건드리지 않는다.
        // 매 틱 다시 굴리면 시간이 계속 미뤄져서 영원히 도주하지 않는다
        if (_refBB.CombatEndTime <= 0.0f)
            _refBB.CombatEndTime = Time.time + Random.Range(m_fMinCombatTime, m_fMaxCombatTime);

        return eNodeState.Success;
    }

    public override void Abort(BlackBoard _refBB)
    {
        _refBB.CombatEndTime = 0.0f;
    }
}
