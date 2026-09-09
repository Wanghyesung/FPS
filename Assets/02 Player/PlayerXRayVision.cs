using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerXRayVision
목적 : 투시 능력 입력(기본 V키)을 받아 EnemyXRayFeature를 켜고,
       지속시간이 끝나면 자동으로 끄는 능력 컨트롤러.

       EnemyXRayFeature의 on/off는 static이라 씬을 넘어 살아남는다.
       켜진 채로 이 컴포넌트가 꺼지거나 씬이 바뀌면 투시가 영구히 켜진 상태로
       남아버리므로, OnDisable에서 반드시 되돌린다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class PlayerXRayVision : MonoBehaviour
{
    [Tooltip("0 이하면 다시 누를 때까지 유지되는 수동 토글로 동작한다")]
    [SerializeField] private float m_fDuration = 5.0f;

    [SerializeField] private float m_fCoolTime = 10.0f;

    private CancellationTokenSource m_refDurationCts;
    private float m_fNextUsableTime;
    private bool m_bIsSubscribed;

    // OnEnable은 InputManager.Awake보다 먼저 돌 수 있어 구독이 조용히 실패한다.
    // 첫 활성화는 Start가, 이후 재활성화는 OnEnable이 책임진다
    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopXRay();
    }

    private void OnDestroy()
    {
        CancelDurationTask();
    }

    private void TrySubscribe()
    {
        if (m_bIsSubscribed == true)
            return;

        if (InputManager.m_Instance == null)
            return;

        InputManager.m_Instance.OnXRayPressed += OnXRayInput;
        m_bIsSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (m_bIsSubscribed == false)
            return;

        if (InputManager.m_Instance != null)
            InputManager.m_Instance.OnXRayPressed -= OnXRayInput;

        m_bIsSubscribed = false;
    }

    private void OnXRayInput()
    {
        // 켜져 있는 동안 다시 누르면 즉시 해제한다(수동 토글 + 조기 종료 겸용)
        if (EnemyXRayFeature.IsXRayOn == true)
        {
            StopXRay();
            return;
        }

        if (Time.time < m_fNextUsableTime)
            return;

        StartXRay();
    }

    private void StartXRay()
    {
        EnemyXRayFeature.SetXRayEnabled(true);

        if (m_fDuration <= 0.0f)
            return;

        CancelDurationTask();

        // 오브젝트가 파괴되면 대기 중인 태스크도 같이 끊기도록 링크해둔다
        m_refDurationCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());

        RunDurationAsync(m_refDurationCts.Token).Forget();
    }

    private async UniTaskVoid RunDurationAsync(CancellationToken _tToken)
    {
        await UniTask.Delay(TimeSpan.FromSeconds(m_fDuration), cancellationToken: _tToken);
        StopXRay();
    }

    private void StopXRay()
    {
        CancelDurationTask();

        if (EnemyXRayFeature.IsXRayOn == false)
            return;

        EnemyXRayFeature.SetXRayEnabled(false);
        m_fNextUsableTime = Time.time + m_fCoolTime;
    }

    private void CancelDurationTask()
    {
        if (m_refDurationCts == null)
            return;

        m_refDurationCts.Cancel();
        m_refDurationCts.Dispose();
        m_refDurationCts = null;
    }
}
