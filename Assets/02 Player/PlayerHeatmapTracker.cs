using UnityEngine;

/*///////////////////////////////////////////
               PlayerHeatmapTracker
목적 : 일정 주기로 플레이어 좌표를 샘플링해 PlayerHeatmapRecorder(워커 스레드)로 넘기는 컴포넌트.
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
