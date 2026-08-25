using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/*///////////////////////////////////////////
                ResultView
목적 : 승패 결과 화면을 표시하기만 하는 Passive View.
       판정 로직은 전혀 없고 ResultSystem가 호출하는 Show/Hide만 노출한다.
       SetActive 토글 대신 CanvasGroup(alpha/blocksRaycasts)으로 감춰
       재활성화 시 캔버스 재구축 비용을 피한다(performance.md).
       "다시하기" 버튼 클릭은 로직 없이 OnRestartRequested 이벤트로 그대로 전달한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class ResultView : MonoBehaviour
{
    private const string TEXT_VICTORY = "YOU WIN";
    private const string TEXT_DEFEAT = "YOU DIED";

    public event Action OnRestartRequested;

    [SerializeField] private CanvasGroup m_refGroup;
    [SerializeField] private TMP_Text m_refTitleText;
    [SerializeField] private Button m_refRestartButton;

    private void Awake()
    {
        Hide();

        if (m_refRestartButton != null)
        {
            m_refRestartButton.onClick.AddListener(RaiseRestart);
        }
    }

    private void OnDestroy()
    {
        if (m_refRestartButton != null)
        {
            m_refRestartButton.onClick.RemoveListener(RaiseRestart);
        }
    }

    public void Show(bool _bIsVictory)
    {
        if (m_refTitleText != null)
        {
            m_refTitleText.SetText(_bIsVictory ? TEXT_VICTORY : TEXT_DEFEAT);
        }

        if (m_refGroup == null)
        {
            return;
        }

        m_refGroup.alpha = 1f;
        m_refGroup.interactable = true;
        m_refGroup.blocksRaycasts = true;
    }

    public void Hide()
    {
        if (m_refGroup == null)
        {
            return;
        }

        m_refGroup.alpha = 0f;
        m_refGroup.interactable = false;
        m_refGroup.blocksRaycasts = false;
    }

    private void RaiseRestart()
    {
        OnRestartRequested?.Invoke();
    }
}
