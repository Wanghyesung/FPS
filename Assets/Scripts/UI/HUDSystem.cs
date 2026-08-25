using UnityEngine;

/*///////////////////////////////////////////
                HUDSystem
목적 : PlayerSystem/WeaponSystem/ItemSystem/ZoneSystem가 발행하는 C# 이벤트를
       구독해 HUDView를 갱신한다. 구독은 OnEnable, 해제는 OnDisable에서 짝을 맞춘다
       (정적 이벤트 누수 방지). 이벤트 발행 시점보다 늦게 활성화될 수 있으므로
       Start에서 1회 초기 동기화를 한다.
       자기장 표시는 1초 틱으로만 들어오므로 거리 계산도 그 시점에만 수행한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class HUDSystem : MonoBehaviour
{
    [SerializeField] private HUDView m_refView;
    [SerializeField] private PlayerSystem m_refPlayerSystem;
    [SerializeField] private WeaponSystem m_refWeaponSystem;
    [SerializeField] private ItemSystem m_refItemSystem;
    [SerializeField] private ZoneSystem m_refZoneSystem;

    private void OnEnable()
    {
        PlayerSystem.OnHealthChanged += HandleHealthChanged;
        WeaponSystem.OnAmmoChanged += HandleAmmoChanged;
        ItemSystem.OnInventoryChanged += HandleInventoryChanged;
        ZoneSystem.OnPhaseChanged += HandleZonePhaseChanged;
    }

    private void OnDisable()
    {
        PlayerSystem.OnHealthChanged -= HandleHealthChanged;
        WeaponSystem.OnAmmoChanged -= HandleAmmoChanged;
        ItemSystem.OnInventoryChanged -= HandleInventoryChanged;
        ZoneSystem.OnPhaseChanged -= HandleZonePhaseChanged;
    }

    private void Start()
    {
        if (m_refPlayerSystem != null)
        {
            HandleHealthChanged(m_refPlayerSystem.CurrentHP, m_refPlayerSystem.MaxHP);
        }

        if (m_refWeaponSystem != null)
        {
            HandleAmmoChanged(m_refWeaponSystem.CurrentAmmoInMag, m_refWeaponSystem.ReserveAmmo);
        }

        if (m_refItemSystem != null)
        {
            HandleInventoryChanged(m_refItemSystem.BandageCount, m_refItemSystem.MedkitCount, m_refItemSystem.HasVest);
        }
    }

    private void HandleHealthChanged(int _iCurrent, int _iMax)
    {
        if (m_refView == null)
        {
            return;
        }

        m_refView.RefreshHealth(_iCurrent, _iMax);
    }

    private void HandleAmmoChanged(int _iMagazine, int _iReserve)
    {
        if (m_refView == null)
        {
            return;
        }

        string strName = m_refWeaponSystem != null ? m_refWeaponSystem.ActiveWeaponName : "None";
        m_refView.RefreshAmmo(strName, _iMagazine, _iReserve);
    }

    private void HandleInventoryChanged(int _iBandageCount, int _iMedkitCount, bool _bHasVest)
    {
        if (m_refView == null)
        {
            return;
        }

        m_refView.RefreshInventory(_iBandageCount, _iMedkitCount, _bHasVest);
    }

    private void HandleZonePhaseChanged(int _iPhase, float _fRemaining, float _fRadius)
    {
        if (m_refView == null)
        {
            return;
        }

        int iDistanceOutside = 0;

        if (m_refZoneSystem != null && m_refPlayerSystem != null)
        {
            Vector3 vDelta = m_refPlayerSystem.Position - m_refZoneSystem.CurrentCenter;
            vDelta.y = 0f;
            iDistanceOutside = Mathf.CeilToInt(vDelta.magnitude - _fRadius);
        }

        m_refView.RefreshZone(_iPhase, Mathf.CeilToInt(_fRemaining), iDistanceOutside);
    }
}
