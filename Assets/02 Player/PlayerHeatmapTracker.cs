using UnityEngine;

/*///////////////////////////////////////////
               PlayerHeatmapTracker
목적 : 일정 주기로 플레이어 좌표를 샘플링해 PlayerHeatmapRecorder(워커 스레드)로 넘기는 컴포넌트.
       Unity API 접근은 메인 스레드 전용이라, 이 컴포넌트는 좌표를 읽어 큐에 넣는 역할만 하고
       그리드 누적/파일 저장 같은 무거운 작업은 전부 워커 스레드가 담당한다.
 *///////////////////////////////////////////
public sealed class PlayerHeatmapTracker : MonoBehaviour
{
    [SerializeField] private float m_fSampleInterval = 0.2f;

    private Transform m_refTr;
    private float m_fElapsed;

    private void Awake()
    {
        m_refTr = transform;
    }

    private void Update()
    {
        m_fElapsed += Time.deltaTime;
        if (m_fElapsed < m_fSampleInterval)
            return;

        m_fElapsed = 0f;

        if (PlayerHeatmapRecorder.Instance == null)
            return;

        PlayerHeatmapRecorder.Instance.EnqueuePosition(m_refTr.position);
    }
}
