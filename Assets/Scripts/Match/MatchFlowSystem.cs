using System;
using UnityEngine;

/*///////////////////////////////////////////
                MatchFlowSystem
목적 : 단판 서바이벌의 승패 판정을 담당하는 System(기획 §3/§12).
       PlayerSystem.OnPlayerDied(패배), ZoneSystem.OnFinalGracePeriodExpired(강제 종료
       시점에 생존해 있으면 승리)를 구독해 단 한 번만 매치를 끝낸다.
       몬스터 전멸 승리 조건은 몬스터 시스템 제거로 함께 삭제했다 —
       추후 MonsterAgent(ML-Agents)가 들어오면 다시 추가한다.
       결과 화면은 직접 참조하지 않고 OnMatchEnded 이벤트로 알린다(ResultSystem가 구독).
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class MatchFlowSystem : MonoBehaviour
{
    /// <summary>true = 승리(최종 자기장까지 생존), false = 패배.</summary>
    public static event Action<bool> OnMatchEnded;

    [SerializeField] private PlayerSystem m_refPlayer;

    private bool m_bIsMatchEnded;

    private void OnEnable()
    {
        PlayerSystem.OnPlayerDied += HandlePlayerDied;
        ZoneSystem.OnFinalGracePeriodExpired += HandleFinalGraceExpired;
    }

    private void OnDisable()
    {
        PlayerSystem.OnPlayerDied -= HandlePlayerDied;
        ZoneSystem.OnFinalGracePeriodExpired -= HandleFinalGraceExpired;
    }

    private void HandlePlayerDied()
    {
        EndMatch(false);
    }

    private void HandleFinalGraceExpired()
    {
        // 기획 §10 — 최종 자기장 강제 종료 시점. 플레이어가 살아있으면 승리 처리
        bool bIsVictory = m_refPlayer == null || !m_refPlayer.IsDead;
        EndMatch(bIsVictory);
    }

    private void EndMatch(bool _bIsVictory)
    {
        if (m_bIsMatchEnded)
        {
            return; // 재진입 가드 — 같은 프레임에 여러 종료 조건이 겹쳐도 한 번만 처리
        }

        m_bIsMatchEnded = true;
        OnMatchEnded?.Invoke(_bIsVictory);
    }
}
