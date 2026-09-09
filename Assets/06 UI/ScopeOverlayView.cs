using UnityEngine;
using UnityEngine.UI;

/*///////////////////////////////////////////
                ScopeOverlayView
목적 : 스코프 진행도(0~1)를 받아 전체화면 스코프 오버레이의 알파만 조절하는 순수 View.
 *///////////////////////////////////////////

public sealed class ScopeOverlayView : MonoBehaviour
{
    [SerializeField] private Image m_refCrosshairImage;      
    [SerializeField] private float m_fFadeInStart = 0.6f; // 이 진행도부터 오버레이가 나타난다

    private float m_fAlpha = -1f;

    private void Awake()
    {
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        ScopeController.OnScopeChanged += SetProgress;
    }

    private void OnDisable()
    {
        ScopeController.OnScopeChanged -= SetProgress;
    }

    private void SetProgress(float _fProgress)
    {
        SetAlpha(Mathf.InverseLerp(m_fFadeInStart, 1f, _fProgress));
    }

    private void SetAlpha(float _fAlpha)
    {
        if (m_fAlpha == _fAlpha)
            return;

        m_fAlpha = _fAlpha;
        m_refCrosshairImage.color = new Color(1.0f, 1.0f, 1.0f,_fAlpha);

    }
}
