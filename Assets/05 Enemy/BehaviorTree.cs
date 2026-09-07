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

    // 상위 우선순위에 밀려 중단될 때, 이 노드가 바꿔둔 상태를 되돌리는 자리
    public virtual void Abort(BlackBoard _refBB) { }
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


// 도주는 "이동 → 은신" 2단으로 진행된다. Sequence가 매 틱 0번부터 재평가하므로
// 어느 단계인지 남겨두지 않으면 SOEscapeNode가 매 틱 목표를 새로 뽑아버린다
public enum eEscapePhase
{
    None,   // 도주 안 함
    Moving, // 엄폐 지점으로 이동 중
    Hiding, // 도착해서 은신 중
}


// 수색도 "이동 → 두리번" 2단이라 도주와 같은 이유로 단계를 남겨야 한다
public enum eSearchPhase
{
    None,    // 수색 안 함
    Moving,  // 마지막 목격 위치로 이동 중
    Looking, // 도착해서 주변을 둘러보는 중
}


[Serializable]
public class BlackBoard
{
    [Header("Component")]
    public Enemy Owner;
    public Transform OwnerOffset;
    public Transform TargetTr;
    public Transform TargetRoot;   // 히트한 콜라이더가 타겟 소유인지 판정하는 기준 (root는 다른 적과 공유됨)
    public NavMeshAgent Agent;

    [Header("Weapon")]
    public Weapon Weapon;
    public Aim Aim;

    [Header("EntityInfo")]
    public ObjectInfo ObjInfo;

    //[Header("PatrolIdx")]
    //public int PatrolIdx; //임시로 넣은 데이터 (나중에 가중치 맵을 읽고 찾는 구조로 변경)
    //public List<Transform> PatrolList;

    [Header("CheckPoint")]

    public bool HasCheckPoint;    // 이번 순찰 목표를 이미 뽑았는지 - Sequence가 매 틱 재평가해도 목표를 새로 뽑지 않게 한다
    public Vector3 CheckPointPos; // 현재 순찰 목표 (히트맵 셀을 월드로 되돌린 뒤 NavMesh 위로 스냅한 좌표)


    [Header("FindTarget")]
    public bool FindTarget;        // 이번 틱에 실제로 보이는지 - SOPerceptionNode가 매 틱 갱신
    public float POV;
    public bool HasLastSeen;       // 목격 기억이 아직 유효한지
    public Vector3 LastSeenPos;    // 마지막으로 본 위치
    public float LastSeenTime;     // 마지막으로 본 시각
    public float BlockedSinceTime; // 보이다가 시야가 끊긴 시각 (0 = 안 끊김)


    [Header("Combat")]

    public float CombatEndTime; // 이번 교전을 끊고 도주할 시각 (0 = 교전 중이 아님)


    [Header("Search")]

    public eSearchPhase SearchPhase; // 수색의 현재 단계
    public Vector3 SearchPos;        // 수색 목표 (LastSeenPos를 NavMesh 위로 스냅한 좌표)
    public float SearchEndTime;      // 두리번거리기가 끝나는 시각 (0 = 아직 안 잡힘)
    public float SearchBaseYaw;      // 도착했을 때의 방향 — 이 각도를 중심으로 좌우를 훑는다


    [Header("Alert")]

    public bool HasPendingHit;  // 피격했는데 아직 그쪽을 돌아보지 않은 상태
    public Vector3 HitFromDir;  // 공격이 날아온 쪽 (평면 방향, 정규화됨)
    public float AlertEndTime;  // 다 돌아본 뒤 그 방향을 주시할 시각 (0 = 아직 안 잡힘)


    [Header("Escape")]

    public eEscapePhase EscapePhase; // 도주 에피소드의 현재 단계
    public Vector3 EscapePos;    // 현재 도주 목표 지점 (NavMesh 위로 스냅된 좌표)
    public float HideEndTime;    // 은신이 끝나는 시각 (0 = 아직 안 잡힘)
    public float NextEscapeTime; // 이 시각(Time.time) 전까지는 HP가 낮아도 재도주하지 않음 — 은신 직후 한동안 교전하게 함

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
