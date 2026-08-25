using System.Collections.Generic;
using UnityEngine;

/*///////////////////////////////////////////
                LootPickupSystem
목적 : 맵에 배치된 개별 루팅 오브젝트(무기 1종 또는 아이템 1종)를 담당하는 System.
       기획 §4.3대로 근접(SphereCollider isTrigger) 시 후보로 등록되고 Interact(E)로 획득하며,
       이미 같은 무기를 소지 중이면 예비탄만 흡수한다.
       무기/아이템 참조가 모두 비어 있고 LootTableSO가 지정돼 있으면 가중치로 내용물을 추첨한다.
       1회성 월드 오브젝트이므로 획득 시 풀 반납이 아니라 Destroy로 제거한다.
       CharacterController와 "정적" 트리거 사이의 OnTriggerEnter는 Unity에서 누락될 수 있으므로
       키네마틱 Rigidbody를 필수로 요구한다(RequireComponent) — 물리 시뮬레이션은 하지 않고
       트리거 이벤트 발생만 보장하기 위한 것이라 Awake에서 강제로 kinematic으로 고정한다.
 *///////////////////////////////////////////

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public sealed class LootPickupSystem : MonoBehaviour
{
    private static readonly List<LootPickupSystem> s_listActive = new List<LootPickupSystem>(16);

    [SerializeField] private WeaponDefinition m_refWeaponDefinition;
    [SerializeField] private ItemDefinition m_refItemDefinition;
    [SerializeField] private LootTableSO m_refLootTable;
    [SerializeField] private Transform m_refVisualRoot;

    private Transform m_refTransform;

    /// <summary>봇의 "가장 가까운 루팅 포인트" 탐색용. FindObjectOfType 대신 자기 등록 방식을 쓴다.</summary>
    public static IReadOnlyList<LootPickupSystem> ActivePickups => s_listActive;

    public Vector3 Position => m_refTransform != null ? m_refTransform.position : transform.position;
    public bool IsWeapon => m_refWeaponDefinition != null;

    private void Awake()
    {
        m_refTransform = transform;

        Rigidbody refBody = GetComponent<Rigidbody>();
        if (refBody != null)
        {
            refBody.isKinematic = true; // 트리거 이벤트 보장용 — 낙하/충돌 시뮬레이션은 하지 않는다
            refBody.useGravity = false;
        }

        RollFromTableIfEmpty();
    }

    private void OnEnable()
    {
        s_listActive.Add(this);
    }

    private void OnDisable()
    {
        s_listActive.Remove(this);
    }

    private void OnTriggerEnter(Collider _refOther)
    {
        PlayerSystem refPlayer = _refOther.GetComponentInParent<PlayerSystem>();
        if (refPlayer != null)
        {
            refPlayer.SetInteractable(this);
        }
    }

    private void OnTriggerExit(Collider _refOther)
    {
        PlayerSystem refPlayer = _refOther.GetComponentInParent<PlayerSystem>();
        if (refPlayer != null)
        {
            refPlayer.ClearInteractable(this);
        }
    }

    /// <summary>획득 시도. 흡수된 게 있으면 픽업을 제거하고 true를 반환한다.</summary>
    public bool TryPickup(ItemSystem _refInventory, WeaponSystem _refWeapons)
    {
        bool bConsumed = false;

        if (m_refWeaponDefinition != null)
        {
            bConsumed = TryPickupWeapon(_refWeapons);
        }
        else if (m_refItemDefinition != null)
        {
            bConsumed = TryPickupItem(_refInventory, _refWeapons);
        }

        if (!bConsumed)
        {
            return false;
        }

        Destroy(gameObject);
        return true;
    }

    private bool TryPickupWeapon(WeaponSystem _refWeapons)
    {
        if (_refWeapons == null)
        {
            return false;
        }

        WeaponSlot eSlot = m_refWeaponDefinition.Slot;

        if (_refWeapons.HasWeapon(eSlot))
        {
            // 기획 §4.3 — 같은 무기를 이미 들고 있으면 예비탄만 흡수
            return _refWeapons.AddReserveAmmo(eSlot, m_refWeaponDefinition.MagazineSize);
        }

        return _refWeapons.GiveWeapon(eSlot, m_refWeaponDefinition);
    }

    private bool TryPickupItem(ItemSystem _refInventory, WeaponSystem _refWeapons)
    {
        if (_refInventory != null)
        {
            return _refInventory.TryCollect(m_refItemDefinition);
        }

        // 봇은 인벤토리(소모품)를 갖지 않는다 — 탄약만 직접 흡수한다
        if (m_refItemDefinition.IsAmmo && _refWeapons != null)
        {
            return _refWeapons.AddReserveAmmo(m_refItemDefinition.AmmoSlot, m_refItemDefinition.AmmoAmount);
        }

        return false;
    }

    private void RollFromTableIfEmpty()
    {
        if (m_refWeaponDefinition != null || m_refItemDefinition != null || m_refLootTable == null)
        {
            return;
        }

        if (!m_refLootTable.TryRoll(out WeaponDefinition refWeapon, out ItemDefinition refItem))
        {
            return;
        }

        m_refWeaponDefinition = refWeapon;
        m_refItemDefinition = refItem;
        SpawnRolledVisual();
    }

    private void SpawnRolledVisual()
    {
        Transform refParent = m_refVisualRoot != null ? m_refVisualRoot : m_refTransform;
        GameObject refPrefab = m_refWeaponDefinition != null
            ? m_refWeaponDefinition.WorldModelPrefab
            : (m_refItemDefinition != null ? m_refItemDefinition.PickupPrefab : null);

        if (refPrefab == null)
        {
            return;
        }

        GameObject refGo = Instantiate(refPrefab, refParent, false);
        refGo.transform.localPosition = Vector3.zero;
        refGo.transform.localRotation = Quaternion.identity;
    }
}
