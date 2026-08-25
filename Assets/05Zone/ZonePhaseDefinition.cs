using UnityEngine;

/*///////////////////////////////////////////
                ZonePhaseDefinition
목적 : 기획 §6 자기장 페이즈 표 한 줄을 담는 정적 설정 데이터(SO).
       매치 시작 기준 경고 시작 시각, 축소 소요 시간, 축소 후 반경, 자기장 밖 초당 피해.
       진행 중 반경/타이머 같은 런타임 상태는 ZoneSystem가 갖는다(SO 오염 방지).
 *///////////////////////////////////////////

[CreateAssetMenu(menuName = "Game/Zone Phase Definition", fileName = "ZonePhase")]
public sealed class ZonePhaseDefinition : ScriptableObject
{
    [SerializeField] private float m_fWarningStartTime;
    [SerializeField] private float m_fShrinkDuration;
    [SerializeField] private float m_fFinalRadius = 200f;
    [SerializeField] private int m_iDamagePerSecond;

    public float WarningStartTime => m_fWarningStartTime;
    public float ShrinkDuration => m_fShrinkDuration;
    public float FinalRadius => m_fFinalRadius;
    public int DamagePerSecond => m_iDamagePerSecond;
}
