using System;
using UnityEngine;

/*///////////////////////////////////////////
                LootTableSO
목적 : 루팅 포인트가 "무엇을 스폰할지"를 결정하는 가중치 테이블(SO).
       무기(WeaponDefinition)와 아이템(ItemDefinition) 항목을 한 배열에 섞어 담고
       가중치 누적 방식으로 하나를 뽑는다. 스폰 결과(런타임 상태)는 SO에 저장하지 않고
       호출한 LootPickupSystem가 갖는다 — SO 오염 방지(architecture.md).
 *///////////////////////////////////////////

[CreateAssetMenu(menuName = "Game/Loot Table", fileName = "LootTable")]
public sealed class LootTableSO : ScriptableObject
{
    [SerializeField] private Entry[] m_tEntries;

    /// <summary>가중치 기반으로 1개를 뽑는다. 뽑히지 않으면 false(빈 테이블).</summary>
    public bool TryRoll(out WeaponDefinition _refWeapon, out ItemDefinition _refItem)
    {
        _refWeapon = null;
        _refItem = null;

        if (m_tEntries == null || m_tEntries.Length == 0)
        {
            return false;
        }

        float fTotal = 0f;
        for (int i = 0; i < m_tEntries.Length; i++)
        {
            fTotal += Mathf.Max(0f, m_tEntries[i].Weight);
        }

        if (fTotal <= 0f)
        {
            return false;
        }

        float fPick = UnityEngine.Random.value * fTotal;
        for (int i = 0; i < m_tEntries.Length; i++)
        {
            fPick -= Mathf.Max(0f, m_tEntries[i].Weight);
            if (fPick > 0f)
            {
                continue;
            }

            _refWeapon = m_tEntries[i].WeaponDefinition;
            _refItem = m_tEntries[i].ItemDefinition;
            return _refWeapon != null || _refItem != null;
        }

        return false;
    }

    [Serializable]
    private struct Entry
    {
        [SerializeField] private float m_fWeight;
        [SerializeField] private WeaponDefinition m_refWeaponDefinition;
        [SerializeField] private ItemDefinition m_refItemDefinition;

        public float Weight => m_fWeight;
        public WeaponDefinition WeaponDefinition => m_refWeaponDefinition;
        public ItemDefinition ItemDefinition => m_refItemDefinition;
    }
}
