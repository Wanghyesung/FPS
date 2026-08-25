using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/*///////////////////////////////////////////
                ResultSystem
목적 : MatchFlowSystem.OnMatchEnded를 구독해 결과 View를 띄우고,
       "다시하기" 요청이 오면 씬을 비동기로 다시 로드한다(코루틴 금지 — UniTask).
       매치 종료 시 마우스 커서를 풀어 버튼을 누를 수 있게 하는 것도 여기서 담당한다.
       결과 화면은 평소 CanvasGroup으로 숨겨져 있으므로 이 System의 GameObject는
       항상 활성 상태여야 이벤트를 놓치지 않는다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class ResultSystem : MonoBehaviour
{
    [SerializeField] private ResultView m_refView;
    [SerializeField] private string m_strSceneName = "BattleScene";

    private bool m_bIsRestarting;

    private void OnEnable()
    {
        MatchFlowSystem.OnMatchEnded += HandleMatchEnded;

        if (m_refView != null)
        {
            m_refView.OnRestartRequested += HandleRestartRequested;
        }
    }

    private void OnDisable()
    {
        MatchFlowSystem.OnMatchEnded -= HandleMatchEnded;

        if (m_refView != null)
        {
            m_refView.OnRestartRequested -= HandleRestartRequested;
        }
    }

    private void HandleMatchEnded(bool _bIsVictory)
    {
        if (m_refView != null)
        {
            m_refView.Show(_bIsVictory);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleRestartRequested()
    {
        if (m_bIsRestarting)
        {
            return;
        }

        m_bIsRestarting = true;
        RestartAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RestartAsync(CancellationToken _token)
    {
        // 씬 로드가 끝나면 이 오브젝트가 파괴되면서 destroy 토큰이 취소된다.
        // 취소 예외를 그대로 던지면 .Forget() 경로에서 미관측 예외로 로그가 남으므로 억제한다.
        await SceneManager.LoadSceneAsync(m_strSceneName)
            .ToUniTask(cancellationToken: _token)
            .SuppressCancellationThrow();
    }
}
