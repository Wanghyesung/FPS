using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

/*///////////////////////////////////////////
               PlayerHeatmapRecorder
목적 : 플레이어 위치를 별도 스레드에서 그리드 가중치로 누적하고 CSV로 저장하는 매니저.
       메인 스레드(PlayerHeatmapTracker)는 좌표를 큐에 넣기만 하고,
       그리드 갱신 + 파일 IO는 워커 스레드가 전담한다.
       플레이어가 한 번도 가지 않은 셀은 딕셔너리에 아예 안 생기므로 가중치는 자동으로 0이다.
 *///////////////////////////////////////////
public sealed class PlayerHeatmapRecorder : MonoBehaviour
{
    public static PlayerHeatmapRecorder Instance { get; private set; }

    [SerializeField] private float m_fCellSize = 1.0f;
    [SerializeField] private float m_fSaveInterval = 5.0f;
    [SerializeField] private string m_strFileName = "PlayerHeatmap.csv";

    private ConcurrentQueue<Vector3> m_quePendingPositions;
    private Dictionary<Vector2Int, float> m_hashWeight;

    private Thread m_refWorkerThread;
    private volatile bool m_bRunning;
    private readonly ManualResetEventSlim m_refSignal = new ManualResetEventSlim(false);

    private string m_strFilePath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return; // 없으면 아래 두 줄이 파괴 예정 인스턴스에서도 실행된다
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        m_quePendingPositions = new ConcurrentQueue<Vector3>();
        m_hashWeight = new Dictionary<Vector2Int, float>();

        // Application.persistentDataPath는 메인 스레드 전용 API이므로 워커 시작 전에 미리 캐싱해둔다
        m_strFilePath = Path.Combine(Application.persistentDataPath, m_strFileName);
    }

    private void OnEnable()
    {
        m_bRunning = true;
        m_refWorkerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "PlayerHeatmapWorker"
        };
        m_refWorkerThread.Start();
    }

    private void OnDisable()
    {
        StopWorker();
    }

    private void OnApplicationQuit()
    {
        StopWorker();
    }

    // 메인 스레드에서 호출 - 좌표 하나를 큐에 넣기만 하는 가벼운 호출
    public void EnqueuePosition(Vector3 _vWorldPos)
    {
        if (m_bRunning == false)
            return;

        m_quePendingPositions.Enqueue(_vWorldPos);
        m_refSignal.Set();
    }

    private void StopWorker()
    {
        if (m_bRunning == false)
            return;

        m_bRunning = false;
        m_refSignal.Set(); // 대기 중인 워커를 깨워서 종료 루프로 바로 진입시킴

        m_refWorkerThread?.Join(1000);
        m_refWorkerThread = null;

        // 워커가 완전히 멈춘 뒤이므로 메인 스레드에서 마지막 상태를 동기적으로 한 번 더 저장
        SaveToFile();
    }

    // ---- 아래부터는 워커 스레드에서만 실행됨 - Unity API(Transform, Application 등) 호출 금지 ----

    private void WorkerLoop()
    {
        var tStopwatch = System.Diagnostics.Stopwatch.StartNew();
        double dLastSaveSeconds = 0.0;

        while (m_bRunning)
        {
            m_refSignal.Wait(200);
            m_refSignal.Reset();

            while (m_quePendingPositions.TryDequeue(out Vector3 vPos))
            {
                Vector2Int vCell = WorldToCell(vPos);

                if (m_hashWeight.TryGetValue(vCell, out float fWeight))
                    m_hashWeight[vCell] = fWeight + 1f;
                else
                    m_hashWeight[vCell] = 1f;
            }

            double dElapsed = tStopwatch.Elapsed.TotalSeconds;
            if (dElapsed - dLastSaveSeconds >= m_fSaveInterval)
            {
                dLastSaveSeconds = dElapsed;
                SaveToFile();
            }
        }
    }

    private Vector2Int WorldToCell(Vector3 _vWorldPos)
    {
        int iX = Mathf.FloorToInt(_vWorldPos.x / m_fCellSize);
        int iZ = Mathf.FloorToInt(_vWorldPos.z / m_fCellSize);
        return new Vector2Int(iX, iZ);
    }

    private void SaveToFile()
    {
        var tBuilder = new StringBuilder();
        tBuilder.Append("cellX,cellZ,weight\n");

        foreach (var tPair in m_hashWeight)
        {
            tBuilder.Append(tPair.Key.x);
            tBuilder.Append(',');
            tBuilder.Append(tPair.Key.y);
            tBuilder.Append(',');
            tBuilder.Append(tPair.Value);
            tBuilder.Append('\n');
        }

        try
        {
            File.WriteAllText(m_strFilePath, tBuilder.ToString());
        }
        catch (IOException)
        {
            // 다음 저장 주기에 다시 시도 - 워커 스레드에서 매 프레임 로그를 남기지 않기 위해 조용히 무시
        }
    }
}
