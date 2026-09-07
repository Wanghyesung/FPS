using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

/*///////////////////////////////////////////
               PlayerHeatmapRecorder
목적 : 플레이어 위치를 별도 스레드에서 그리드 가중치로 누적하고 CSV로 저장하는 매니저.
       
 *///////////////////////////////////////////
public sealed class PlayerHeatmapRecorder : MonoBehaviour
{
    public static PlayerHeatmapRecorder m_Instance { get; private set; }

    [SerializeField] private float m_fCellSize = 1.0f;
    [SerializeField] private float m_fSaveInterval = 5.0f;
    [SerializeField] private string m_strFileName = "PlayerHeatmap.csv";

    private ConcurrentQueue<Vector3> m_quePendingPositions;
    private Dictionary<Vector2Int, float> m_hashWeight;

    //리코더가 데이터를 모으고 Loader가 데이터를 읽는 구조 (이벤트로 알림)
    //주의: 아래 이벤트는 워커 스레드에서 Invoke된다 - 구독자 안에서 Unity API를 호출하면 안 된다
    public event Action<Dictionary<Vector2Int, float>> OnHeatmapSaved;
    private Thread m_refWorkerThread;
    private volatile bool m_bRunning;
    private readonly ManualResetEventSlim m_refSignal = new ManualResetEventSlim(false); //데이터가 들어올 때만 신호를 주어 워커 스레드가 깨어나게

    private string m_strFilePath;

    // 누적 파일을 아직 못 읽었는데 저장하면 이전 기록을 통째로 덮어써 날린다 - 로드 성공 전엔 저장을 막는다
    // 워커가 쓰고 StopWorker(메인 스레드)가 읽으므로 volatile
    private volatile bool m_bLoadedFromFile;

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return; // 없으면 아래 두 줄이 파괴 예정 인스턴스에서도 실행된다
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);

        m_quePendingPositions = new ConcurrentQueue<Vector3>();
        m_hashWeight = new Dictionary<Vector2Int, float>();

        // Application.persistentDataPath는 메인 스레드 전용 API이므로 워커 시작 전에 미리 캐싱해둔다
        m_strFilePath = Path.Combine(Directory.GetCurrentDirectory(), m_strFileName);
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

    // WorldToCell의 역변환 - 셀 인덱스를 다시 월드 좌표로 되돌린다 (메인 스레드에서 호출, 순수 계산이라 Unity API 의존 없음)
    // FloorToInt로 소수부를 버렸으므로 그대로 곱하면 셀의 좌하단 모서리가 된다 - 0.5를 더해 셀 중앙을 반환
    public Vector3 CellToWorld(Vector2Int _vCell)
    {
        float fX = (_vCell.x + 0.5f) * m_fCellSize;
        float fZ = (_vCell.y + 0.5f) * m_fCellSize;
        return new Vector3(fX, 0f, fZ);
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
        // 이전 실행까지 쌓인 가중치를 먼저 복원한 뒤 그 위에 이어서 누적한다.
        // 메인 스레드가 아니라 여기서 읽어야 씬 로드가 파일 IO만큼 멈추지 않는다
        LoadFromFile();

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

                //m_hashWeight를 그대로 넘긴다 - 구독자가 이 워커 스레드에서 동기로 실행되므로 락이 필요 없다
                //단, 구독자는 콜백 안에서 읽고 끝내야 한다 (참조를 보관하면 다음 주기에 깨진다)
                OnHeatmapSaved?.Invoke(m_hashWeight);
            }
        }
    }

    // SaveToFile이 쓴 CSV를 그대로 역파싱해 m_hashWeight를 채운다. 최초 실행이라 파일이 없으면 빈 상태로 시작
    private void LoadFromFile()
    {
        if (m_bLoadedFromFile == true)
            return;

        m_bLoadedFromFile = true;

        if (File.Exists(m_strFilePath) == false)
            return;

        try
        {
            using (StreamReader tReader = new StreamReader(m_strFilePath))
            {
                tReader.ReadLine(); // 헤더(cellX,cellZ,weight) 한 줄 버리기

                string strLine;
                while ((strLine = tReader.ReadLine()) != null)
                {
                    int iFirst = strLine.IndexOf(',');
                    if (iFirst < 0)
                        continue;

                    int iSecond = strLine.IndexOf(',', iFirst + 1);
                    if (iSecond < 0)
                        continue;

                    // 손상된 줄 하나 때문에 나머지 기록을 통째로 버리지 않도록 줄 단위로 건너뛴다
                    if (int.TryParse(strLine.Substring(0, iFirst), NumberStyles.Integer, CultureInfo.InvariantCulture, out int iX) == false)
                        continue;

                    if (int.TryParse(strLine.Substring(iFirst + 1, iSecond - iFirst - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int iZ) == false)
                        continue;

                    if (float.TryParse(strLine.Substring(iSecond + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float fWeight) == false)
                        continue;

                    m_hashWeight[new Vector2Int(iX, iZ)] = fWeight;
                }
            }
        }
        catch (IOException)
        {
            // 못 읽으면 이번 실행은 빈 히트맵으로 시작하는 수밖에 없다
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
        // 아직 이전 기록을 못 읽었다면 지금 쓰는 순간 그 기록이 사라진다
        if (m_bLoadedFromFile == false)
            return;

        var tBuilder = new StringBuilder();
        tBuilder.Append("cellX,cellZ,weight\n");

        foreach (var tPair in m_hashWeight)
        {
            tBuilder.Append(tPair.Key.x);
            tBuilder.Append(',');
            tBuilder.Append(tPair.Key.y);
            tBuilder.Append(',');
            // 파싱은 InvariantCulture로 하므로 쓸 때도 맞춰야 한다 - 소수점이 쉼표인 로캘에서 CSV가 깨진다
            tBuilder.Append(tPair.Value.ToString(CultureInfo.InvariantCulture));
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
