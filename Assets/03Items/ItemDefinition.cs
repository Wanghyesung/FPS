using UnityEngine;

/*///////////////////////////////////////////
                ItemDefinition
목적 : 기획 §5.2 아이템 스탯표를 담는 정적 설정 데이터(SO).
       보유 개수/사용 진행도 같은 런타임 상태는 절대 여기 두지 않고 ItemSystem가 갖는다
       — SO 데이터 오염 방지(architecture.md).
       HealAmount는 음수(-1)를 "완전 회복" 센티널로 사용한다(구급상자).
 *///////////////////////////////////////////

[CreateAssetMenu(menuName = "Game/Item Definition", fileName = "ItemDefinition")]
public sealed class ItemDefinition : ScriptableObject
{
    [SerializeField] private ItemType m_eType = ItemType.Bandage;
    [SerializeField] private string m_strDisplayName = "Item";
    [SerializeField] private float m_fUseDuration;
    [SerializeField] private int m_iHealAmount;
    [SerializeField] private int m_iAmmoAmount;
    [SerializeField] private GameObject m_refWorldModelPrefab;
    [SerializeField] private GameObject m_refPickupPrefab;

    public ItemType Type => m_eType;
    public string DisplayName => m_strDisplayName;
    public float UseDuration => m_fUseDuration;
    public int HealAmount => m_iHealAmount;
    public int AmmoAmount => m_iAmmoAmount;
    public GameObject WorldModelPrefab => m_refWorldModelPrefab;
    public GameObject PickupPrefab => m_refPickupPrefab;

    public bool IsAmmo => m_eType == ItemType.AmmoAK || m_eType == ItemType.AmmoTRG;

    /// <summary>탄약 아이템이 채워 넣을 무기 슬롯. 탄약이 아니면 의미 없음(AK 반환).</summary>
    public WeaponSlot AmmoSlot => m_eType == ItemType.AmmoTRG ? WeaponSlot.TRG : WeaponSlot.AK;
}
