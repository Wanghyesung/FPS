using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/*///////////////////////////////////////////
                ItemSystem
목적 : 플레이어의 소모품 인벤토리(붕대/구급상자)와 방탄조끼 착용 상태를 관리하는 System.
       기획 §5.2 그대로 — 붕대는 3초 후 +25(이동 가능), 구급상자는 6초 후 완전 회복이며
       사용 중 이동 불가 + 피격 시 취소된다. 대기는 코루틴이 아니라 UniTask로 처리하고,
       취소는 PlayerSystem.OnDamaged 구독으로 트리거한다(unity-specifics.md).
       방탄조끼 감쇠 자체는 PlayerSystem.TakeDamage가 HasVest를 읽어 적용한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class ItemSystem : MonoBehaviour
{
    public static event Action<int, int, bool> OnInventoryChanged;

    [SerializeField] private ItemDefinition m_refBandageDefinition;
    [SerializeField] private ItemDefinition m_refMedkitDefinition;
    [SerializeField] private int m_iStartingBandageCount;
    [SerializeField] private int m_iStartingMedkitCount;
    [SerializeField] private int m_iMaxStackCount = 5;
    [SerializeField] private bool m_bStartsWithVest;

    private PlayerSystem m_refPlayer;
    private WeaponSystem m_refWeapon;
    private CancellationTokenSource m_ctsUse;

    private int m_iBandageCount;
    private int m_iMedkitCount;
    private bool m_bHasVest;
    private bool m_bIsBusy;
    private bool m_bIsUsingMedkit;

    public bool HasVest => m_bHasVest;
    public bool IsBusy => m_bIsBusy;
    public int BandageCount => m_iBandageCount;
    public int MedkitCount => m_iMedkitCount;

    private void Awake()
    {
        m_refPlayer = GetComponent<PlayerSystem>();
        m_refWeapon = GetComponent<WeaponSystem>();

        m_iBandageCount = m_iStartingBandageCount;
        m_iMedkitCount = m_iStartingMedkitCount;
        m_bHasVest = m_bStartsWithVest;
    }

    private void OnEnable()
    {
        PlayerSystem.OnDamaged += HandleOwnerDamaged;
    }

    private void OnDisable()
    {
        PlayerSystem.OnDamaged -= HandleOwnerDamaged;
        CancelUse();
    }

    private void Start()
    {
        RaiseInventoryChanged();
    }

    private void OnDestroy()
    {
        CancelUse();
    }

    /// <summary>루팅 픽업이 호출하는 단일 진입점. 흡수되지 않으면 false(픽업은 그대로 남는다).</summary>
    public bool TryCollect(ItemDefinition _refDefinition)
    {
        if (_refDefinition == null)
        {
            return false;
        }

        switch (_refDefinition.Type)
        {
            case ItemType.Bandage:
                return TryAddStack(ref m_iBandageCount);

            case ItemType.Medkit:
                return TryAddStack(ref m_iMedkitCount);

            case ItemType.Vest:
                return EquipVest();

            case ItemType.AmmoAK:
            case ItemType.AmmoTRG:
                return m_refWeapon != null && m_refWeapon.AddReserveAmmo(_refDefinition.AmmoSlot, _refDefinition.AmmoAmount);

            default:
                return false;
        }
    }

    public bool EquipVest()
    {
        if (m_bHasVest)
        {
            return false; // 등급/내구도가 없으므로 중복 착용은 의미 없음(기획 §5.2)
        }

        m_bHasVest = true;
        RaiseInventoryChanged();
        return true;
    }

    public void TryUseBandage()
    {
        TryUse(m_refBandageDefinition, false);
    }

    public void TryUseMedkit()
    {
        TryUse(m_refMedkitDefinition, true);
    }

    private void TryUse(ItemDefinition _refDefinition, bool _bIsMedkit)
    {
        if (m_bIsBusy || _refDefinition == null || m_refPlayer == null || m_refPlayer.IsDead)
        {
            return;
        }

        int iCount = _bIsMedkit ? m_iMedkitCount : m_iBandageCount;
        if (iCount <= 0)
        {
            return;
        }

        if (m_refPlayer.CurrentHP >= m_refPlayer.MaxHP)
        {
            return; // 이미 만피면 소모하지 않는다
        }

        UseAsync(_refDefinition, _bIsMedkit).Forget();
    }

    private async UniTaskVoid UseAsync(ItemDefinition _refDefinition, bool _bIsMedkit)
    {
        CancelUse();
        m_ctsUse = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        CancellationToken token = m_ctsUse.Token;

        m_bIsBusy = true;
        m_bIsUsingMedkit = _bIsMedkit;

        if (_bIsMedkit && m_refPlayer != null)
        {
            m_refPlayer.SetMovementLocked(true); // 기획 §5.2 — 구급상자는 사용 중 이동 불가
        }

        bool bCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_refDefinition.UseDuration), cancellationToken: token)
            .SuppressCancellationThrow();

        m_bIsBusy = false;
        m_bIsUsingMedkit = false;

        if (_bIsMedkit && m_refPlayer != null)
        {
            m_refPlayer.SetMovementLocked(false);
        }

        if (bCanceled)
        {
            return; // 피격/파괴로 취소 — 아이템도 소모하지 않는다
        }

        if (_bIsMedkit)
        {
            m_iMedkitCount = Mathf.Max(0, m_iMedkitCount - 1);
        }
        else
        {
            m_iBandageCount = Mathf.Max(0, m_iBandageCount - 1);
        }

        if (m_refPlayer != null)
        {
            m_refPlayer.Heal(_refDefinition.HealAmount);
        }

        RaiseInventoryChanged();
    }

    private void HandleOwnerDamaged(int _iAmount)
    {
        if (!m_bIsBusy || !m_bIsUsingMedkit)
        {
            return; // 붕대는 피격으로 취소되지 않는다(기획 §5.2)
        }

        CancelUse();
    }

    private bool TryAddStack(ref int _iCount)
    {
        if (_iCount >= m_iMaxStackCount)
        {
            return false;
        }

        _iCount++;
        RaiseInventoryChanged();
        return true;
    }

    private void CancelUse()
    {
        if (m_ctsUse == null)
        {
            return;
        }

        m_ctsUse.Cancel();
        m_ctsUse.Dispose();
        m_ctsUse = null;
    }

    private void RaiseInventoryChanged()
    {
        OnInventoryChanged?.Invoke(m_iBandageCount, m_iMedkitCount, m_bHasVest);
    }
}
