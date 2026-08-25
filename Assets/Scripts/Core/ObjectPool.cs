using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                ObjectPool
목적 : 총구 이펙트/예광탄/탄피처럼 초당 수 회 생성·소멸되는 오브젝트의
       Instantiate/Destroy 비용과 GC 스파이크를 제거하기 위한 매니저급 싱글톤.
       SOPoolData를 키로 Queue<PoolObject>를 보관하고 SetActive 토글로 재사용한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance { get; private set; }

    [SerializeField] private SOPoolData[] m_refPrewarmTargets;

    private readonly Dictionary<SOPoolData, Queue<PoolObject>> m_hashPools = new Dictionary<SOPoolData, Queue<PoolObject>>();
    private readonly Dictionary<SOPoolData, Transform> m_hashRoots = new Dictionary<SOPoolData, Transform>();

    private CancellationTokenSource m_cts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return; // 필수 — Destroy는 프레임 끝에 처리되므로 return 없이는 아래가 실행된다
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        m_cts = new CancellationTokenSource();
        PrewarmAll();
    }

    private void OnDestroy()
    {
        if (m_cts != null)
        {
            m_cts.Cancel();
            m_cts.Dispose();
            m_cts = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public PoolObject Rent(SOPoolData _data, Vector3 _vPos, Quaternion _tRot)
    {
        if (_data == null || _data.Prefab == null)
        {
            return null;
        }

        Queue<PoolObject> queFree = GetQueue(_data);

        PoolObject refObj = null;
        while (queFree.Count > 0)
        {
            PoolObject refCandidate = queFree.Dequeue();
            if (refCandidate != null) // 씬 전환 등으로 파괴된 인스턴스는 건너뛴다
            {
                refObj = refCandidate;
                break;
            }
        }

        if (refObj == null)
        {
            refObj = CreateInstance(_data);
            if (refObj == null)
            {
                return null;
            }
        }

        Transform refTr = refObj.transform;
        refTr.SetPositionAndRotation(_vPos, _tRot);
        refObj.gameObject.SetActive(true);
        refObj.OnSpawned();
        return refObj;
    }

    public void Return(SOPoolData _data, PoolObject _obj)
    {
        if (_data == null || _obj == null)
        {
            return;
        }

        Queue<PoolObject> queFree = GetQueue(_data);
        if (!_obj.gameObject.activeSelf)
        {
            return; // 이미 반납된 인스턴스 — 중복 Enqueue 방지
        }

        _obj.OnDespawned();
        _obj.gameObject.SetActive(false);
        _obj.transform.SetParent(GetRoot(_data), false);
        queFree.Enqueue(_obj);
    }

    public async UniTaskVoid ReturnAfter(SOPoolData _data, PoolObject _obj, float _fSeconds)
    {
        if (_data == null || _obj == null)
        {
            return;
        }

        CancellationToken token = m_cts != null ? m_cts.Token : this.GetCancellationTokenOnDestroy();

        bool bCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_fSeconds), cancellationToken: token)
            .SuppressCancellationThrow();

        if (bCanceled || _obj == null)
        {
            return;
        }

        Return(_data, _obj);
    }

    private void PrewarmAll()
    {
        if (m_refPrewarmTargets == null)
        {
            return;
        }

        for (int i = 0; i < m_refPrewarmTargets.Length; i++)
        {
            SOPoolData data = m_refPrewarmTargets[i];
            if (data == null || data.Prefab == null)
            {
                continue;
            }

            Queue<PoolObject> queFree = GetQueue(data);
            for (int j = 0; j < data.PrewarmCount; j++)
            {
                PoolObject refObj = CreateInstance(data);
                if (refObj == null)
                {
                    break;
                }

                refObj.gameObject.SetActive(false);
                queFree.Enqueue(refObj);
            }
        }
    }

    private PoolObject CreateInstance(SOPoolData _data)
    {
        GameObject refGo = Instantiate(_data.Prefab, GetRoot(_data));
        PoolObject refObj = refGo.GetComponent<PoolObject>();
        if (refObj == null)
        {
            // 프리팹이 Synty 원본처럼 PoolObject를 갖고 있지 않은 경우 자동 부착
            refObj = refGo.AddComponent<PoolObject>();
        }

        return refObj;
    }

    private Queue<PoolObject> GetQueue(SOPoolData _data)
    {
        if (!m_hashPools.TryGetValue(_data, out Queue<PoolObject> queFree))
        {
            queFree = new Queue<PoolObject>(Mathf.Max(1, _data.PrewarmCount));
            m_hashPools.Add(_data, queFree);
        }

        return queFree;
    }

    private Transform GetRoot(SOPoolData _data)
    {
        if (!m_hashRoots.TryGetValue(_data, out Transform refRoot) || refRoot == null)
        {
            GameObject refGo = new GameObject(_data.name);
            refRoot = refGo.transform;
            refRoot.SetParent(transform, false);
            m_hashRoots[_data] = refRoot;
        }

        return refRoot;
    }
}
