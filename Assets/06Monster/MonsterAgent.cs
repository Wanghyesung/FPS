using UnityEngine;

/*///////////////////////////////////////////
                MonsterAgent
목적 : AI 봇 자리표시자. 폴백 FSM은 전부 제거했고, 지금은 로직 없이
       히트스캔에 맞을 수 있는 최소 상태(HP)만 가진 빈 껍데기다.
       나중에 이 클래스를 Unity ML-Agents의 Agent로 교체해 게임 디자인
       §7의 관찰/행동/보상을 구현할 예정이다(.claude/docs/game-design.md).
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class MonsterAgent : MonoBehaviour, IDamageable
{
    [SerializeField] private int m_iMaxHealth = 100;

    private int m_iHealth;

    private void Awake()
    {
        m_iHealth = m_iMaxHealth;
    }

    public void TakeDamage(int _iAmount, bool _bIsHeadshot)
    {
        m_iHealth = Mathf.Max(0, m_iHealth - _iAmount);
    }
}
