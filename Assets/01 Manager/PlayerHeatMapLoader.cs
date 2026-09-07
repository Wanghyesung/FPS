using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

/*///////////////////////////////////////////
               PlayerHeatMapLoader
목적 : Recorder 워커 스레드가 넘겨준 그리드 가중치 Dictionary에서
       가중치 상위 N개 셀을 뽑아 내림차순 배열로 발행하는 컴포넌트.
       계산은 워커 스레드에서, 소비는 메인 스레드에서 이루어진다.
 *///////////////////////////////////////////
public sealed class PlayerHeatMapLoader : MonoBehaviour
{
    public static PlayerHeatMapLoader m_Instance;

    [FormerlySerializedAs("m_fPickCount")]
    [SerializeField] private int m_iPickCount = 10; //PQ에서 제일 위에 가중치 몇개를 뽑을지

    
    private readonly PriorityQueue<tWeightData> m_PQWeight = new PriorityQueue<tWeightData>(new tWeightComparer());

    private Vector2Int[] m_arrTopPos = Array.Empty<Vector2Int>();

    //Volatile.Read는 캐시에 데이터를 넣는 것을 방지하고 메모리에 넣게 함 가시성O 원자성 X
    //메인 스레드용 스냅샷 - 받은 배열은 지역 변수에 담아두고 쓸 것 (접근할 때마다 다른 배열일 수 있음)
    public Vector2Int[] TopPositions => Volatile.Read(ref m_arrTopPos);


    public struct tWeightData
    {
        public tWeightData(float _fWeight, Vector2Int _vIntPos)
        {
            Weight = _fWeight;
            Position = _vIntPos;
        }
        public float Weight;
        public Vector2Int Position;
    }

    private struct tWeightComparer : IComparer<tWeightData>
    {
        public int Compare(tWeightData x, tWeightData y)
        {
            return x.Weight.CompareTo(y.Weight);
        }
    }

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return; // 없으면 아래 두 줄이 파괴 예정 인스턴스에서도 실행된다
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (PlayerHeatmapRecorder.m_Instance != null)
            PlayerHeatmapRecorder.m_Instance.OnHeatmapSaved += LoadHeatMap;
    }


    private void OnDestroy()
    {
        //Recorder가 먼저 파괴되는 종료 순서에서 NRE가 나지 않도록 가드
        if (PlayerHeatmapRecorder.m_Instance != null)
            PlayerHeatmapRecorder.m_Instance.OnHeatmapSaved -= LoadHeatMap;
    }


    // !!! 워커 스레드에서 호출됨 - Unity API 호출 금지, _hashSaveData 참조를 필드에 저장하지 말 것 !!!
    private void LoadHeatMap(Dictionary<Vector2Int, float> _hashSaveData)
    {
        m_PQWeight.Clear(); //이전 주기 잔여물 제거 - 없으면 낡은 가중치가 계속 누적된다

        //전체를 넣고 N개 뽑는 O(N log N) 대신, 크기 N 힙을 유지하는 O(N log PickCount) Top-K
        foreach (var tKV in _hashSaveData)
        {
            if (m_PQWeight.Count < m_iPickCount)
                m_PQWeight.Enqueue(new tWeightData(tKV.Value, tKV.Key));
            else if (tKV.Value > m_PQWeight.Peek().Weight) //루트 = 현재 커트라인, 그보다 크면 교체
            {
                m_PQWeight.Dequeue();
                m_PQWeight.Enqueue(new tWeightData(tKV.Value, tKV.Key));
            }
        }

        int iCount = m_PQWeight.Count;
        var arrResult = new Vector2Int[iCount];

        //min-heap이라 가중치가 작은 것부터 나온다 - 뒤에서부터 채워 내림차순으로 만든다
        for (int i = iCount - 1; i >= 0; --i)
            arrResult[i] = m_PQWeight.Dequeue().Position;

        Volatile.Write(ref m_arrTopPos, arrResult); //완성한 뒤 한 번에 발행
    }
}
