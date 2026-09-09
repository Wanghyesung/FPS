using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/*///////////////////////////////////////////
                LoadingOverlay
목적 : 로딩 진행률을 슬라이더로 표시하는 오버레이. 실제 진행률이 계단식으로 뛰어도
       슬라이더는 일정 속도로 따라가게 해서 눈에 튀지 않게 한다.
       GameSceneManager 프리팹의 자식으로 함께 DontDestroyOnLoad되므로 씬이 바뀌어도 살아있다.
 *///////////////////////////////////////////

public sealed class LoadingOverlay : MonoBehaviour
{
    [SerializeField] private Slider m_refLoadingSlider;
    [SerializeField] private float m_fFillSpeed = 1.0f;

    private float m_fTargetFill = 0.0f;
    private bool m_bIsFilling = false;

    //돌고 있는 채우기 루프를 무효화하기 위한 세대 값(ObjectPoolManager의 Generation 가드와 같은 방식)
    private int m_iFillGeneration = 0;

    public void SetProgress(float _fValue)
    {
        if (m_refLoadingSlider == null)
            return;

        m_fTargetFill = Mathf.Clamp01(_fValue);

        //이미 도는 루프가 새 target을 따라가면 된다. 매 프레임 새 루프를 띄우면 그만큼 할당이 생긴다
        if (m_bIsFilling)
            return;

        m_bIsFilling = true;
        SmoothFillAsync(++m_iFillGeneration, this.GetCancellationTokenOnDestroy()).Forget();
    }

    //코루틴은 SetActive(false)면 알아서 멈췄지만 UniTask는 멈추지 않는다.
    //오버레이가 DontDestroyOnLoad로 계속 살아있으므로, 꺼질 때 여기서 확실히 끊어준다
    private void OnDisable()
    {
        StopFill();
    }

    public void ShowLoadingImage()
    {
        gameObject.SetActive(true);
    }

    public void CompletedLoading()
    {
        StopFill();

        gameObject.SetActive(false);

        //값까지 되돌려야 다음 로딩이 이전 진행률에서 이어지지 않는다
        m_fTargetFill = 0.0f;
        if (m_refLoadingSlider != null)
            m_refLoadingSlider.value = 0.0f;
    }

    //세대 값을 올려서 돌고 있는 루프를 무효화한다
    private void StopFill()
    {
        m_iFillGeneration++;
        m_bIsFilling = false;
    }

    private async UniTaskVoid SmoothFillAsync(int _iGeneration, CancellationToken _tToken)
    {
        while (_iGeneration == m_iFillGeneration && m_refLoadingSlider != null)
        {
            //지정한 속도로 target 쪽으로 이동
            float fNextAmount = Mathf.MoveTowards(m_refLoadingSlider.value, m_fTargetFill, m_fFillSpeed * Time.unscaledDeltaTime);
            m_refLoadingSlider.value = fNextAmount;

            //target이 1.0이 됐어도 슬라이더가 실제로 다 찰 때까지는 계속 채운다
            if (m_fTargetFill >= 0.99f && Mathf.Approximately(fNextAmount, m_fTargetFill))
                break;

            await UniTask.Yield(PlayerLoopTiming.Update, _tToken);
        }

        //내가 최신 세대일 때만 플래그를 되돌린다(이미 CompletedLoading이 정리했다면 건드리지 않는다)
        if (_iGeneration == m_iFillGeneration)
            m_bIsFilling = false;
    }
}
