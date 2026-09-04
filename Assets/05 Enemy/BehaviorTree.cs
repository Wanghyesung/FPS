using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using static Weapon;


public enum eNodeState
{
    Success,
    Failure,
    Running,
}


/*///////////////////////////////////////////
                  SONode
기능 : 노드 최상위 클래스 (모든 액션을 처리하는 단위)
 *///////////////////////////////////////////
public abstract class SONode : ScriptableObject
{
    public abstract eNodeState Execute(BlackBoard _refBB);
}

// SOList는 그냥 SONode 모음
public abstract class SOListNode : SONode
{
    [SerializeField] protected List<SONode> listNode = new List<SONode>();

    //SO는 공유 메모리이기 때문에 리프 노드가 들고 있는 캐시(예: SOChargeNode)까지
    //몬스터 인스턴스마다 독립적이어야 한다 → 리스트의 자식은 전부 복제해서 사용

    public void CloneChildren(List<SOListNode> _ListTracker)
    {
        for (int i = 0; i < listNode.Count; i++)
        {
            if (listNode[i] is SOListNode listChild)
            {
                SOListNode clone = Instantiate(listChild);
                _ListTracker.Add(clone);
                listNode[i] = clone;
                clone.CloneChildren(_ListTracker);

            }
        }
    }
}


[Serializable]
public class BlackBoard
{
    [Header("Component")]
    public Enemy Owner;
    public Transform OwnerOffset;
    public Transform TargetTr;
    public NavMeshAgent Agent;

    [Header("Weapon")]
    public Weapon Weapon;
    public Aim Aim;

    [Header("EntityInfo")]
    public ObjectInfo ObjInfo;

    [Header("PatrolIdx")]
    public int PatrolIdx; //임시로 넣은 데이터 (나중에 가중치 맵을 읽고 찾는 구조로 변경)
    public List<Transform> PatrolList;

    [Header("FindTarget")]
    public bool FindTarget; // 시야 내에 플레이어가 있는지
    public float POV;


    [Header("Escape")]
    
    public bool IsEscaping;      // 도주 목표 지점을 이미 잡아둔 상태인지
    public Vector3 EscapePos;    // 현재 도주 목표 지점 (NavMesh 위로 스냅된 좌표)
    public float NextEscapeTime; // 이 시각(Time.time) 전까지는 HP가 낮아도 재도주하지 않음 — 도주 직후 한동안 교전하게 함

}


/*///////////////////////////////////////////
              BehaviorTree
 *///////////////////////////////////////////

public class BehaviorTree : MonoBehaviour
{
    [SerializeField] private SONode m_refRootNode = null;
    [SerializeField] private Enemy m_refOwner;

    [SerializeField] private BlackBoard m_refBB = new();


    public BlackBoard BlackBoard => m_refBB;
    private bool m_bRunning = true;
    private readonly List<SONode> m_listClonedNodes = new List<SONode>();

    private void OnDestroy()
    {
        foreach (SONode node in m_listClonedNodes)
        {
            if (node != null)
                Destroy(node);
        }
        m_listClonedNodes.Clear();
    }

    private void Awake()
    {
        //if (m_refOwner == null)
        //    m_refOwner = GetComponent<Monster>();

        // SO는 공유 메모리이므로, 인스턴스별 상태(iCurrentIdx, m_fTimer 등)를 갖는
        // SOListNode 트리는 몬스터마다 복제해서 사용해야 함
        if (m_refRootNode is SOListNode listRoot)
        {
            SOListNode cloneRoot = Instantiate(listRoot);
            m_listClonedNodes.Add(cloneRoot);

            List<SOListNode> listChildTracker = new List<SOListNode>();
            cloneRoot.CloneChildren(listChildTracker);
            m_listClonedNodes.AddRange(listChildTracker);

            m_refRootNode = cloneRoot;
        }
    }

    public bool StopBT() => m_bRunning = false;
    public bool StartBT() => m_bRunning = true;

    public void Evaluate()
    {
        if (m_bRunning == true)
            m_refRootNode?.Execute(m_refBB);
    }

}
