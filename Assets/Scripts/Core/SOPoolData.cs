using UnityEngine;

/*///////////////////////////////////////////
                SOPoolData
목적 : 풀 하나를 식별하는 키이자 설정 데이터(프리팹 + 프리웜 개수).
       ObjectPool의 Dictionary 키로 쓰이므로 런타임 상태를 절대 갖지 않는다
       (architecture.md '데이터 분리 규칙').
 *///////////////////////////////////////////

[CreateAssetMenu(menuName = "Game/Pool Data", fileName = "SOPoolData")]
public sealed class SOPoolData : ScriptableObject
{
    [SerializeField] private GameObject m_refPrefab;
    [SerializeField] private int m_iPrewarmCount = 8;

    public GameObject Prefab => m_refPrefab;
    public int PrewarmCount => m_iPrewarmCount;
}
