using UnityEngine;

/*///////////////////////////////////////////
                WeaponDefinition
목적 : 기획서 §5.1 무기 스탯표를 그대로 담는 정적 설정 데이터(SO).
       런타임에 변하는 값(현재 탄약, 쿨다운)은 절대 여기에 두지 않고
       WeaponModel이 보유한다 — SO 데이터 오염 방지(architecture.md).
 *///////////////////////////////////////////

[CreateAssetMenu(menuName = "Game/Weapon Definition", fileName = "WeaponDefinition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [SerializeField] private string m_strDisplayName = "Weapon";
    [SerializeField] private WeaponSlot m_eSlot = WeaponSlot.AK;
    [SerializeField] private bool m_bIsFullAuto;
    [SerializeField] private float m_fRoundsPerMinute;

    [SerializeField] private int m_iBodyDamage = 24;
    [SerializeField] private int m_iHeadshotDamage = 48;
    [SerializeField] private bool m_bHeadshotIsInstantKill;

    [SerializeField] private int m_iMagazineSize = 30;
    [SerializeField] private int m_iMaxReserveAmmo = 90;
    [SerializeField] private float m_fEffectiveRange = 80f;

    [SerializeField] private float m_fSpreadHipStand = 4f;
    [SerializeField] private float m_fSpreadHipMove = 8f;
    [SerializeField] private float m_fSpreadHipJump = 12f;
    [SerializeField] private float m_fSpreadAdsStand = 0.5f;
    [SerializeField] private float m_fSpreadAdsMove = 3f;

    [SerializeField] private float m_fReloadTime = 2.2f;
    [SerializeField] private float m_fBoltCycleTime;

    [SerializeField] private GameObject m_refWorldModelPrefab;

    public string DisplayName => m_strDisplayName;

    /// <summary>이 무기가 들어가는 2슬롯 중 어느 쪽인지 — 루팅/탄약 흡수 분기의 기준.</summary>
    public WeaponSlot Slot => m_eSlot;

    public bool IsFullAuto => m_bIsFullAuto;
    public float RoundsPerMinute => m_fRoundsPerMinute;

    public int BodyDamage => m_iBodyDamage;
    public int HeadshotDamage => m_iHeadshotDamage;
    public bool HeadshotIsInstantKill => m_bHeadshotIsInstantKill;

    public int MagazineSize => m_iMagazineSize;
    public int MaxReserveAmmo => m_iMaxReserveAmmo;
    public float EffectiveRange => m_fEffectiveRange;

    public float SpreadHipStand => m_fSpreadHipStand;
    public float SpreadHipMove => m_fSpreadHipMove;
    public float SpreadHipJump => m_fSpreadHipJump;
    public float SpreadAdsStand => m_fSpreadAdsStand;
    public float SpreadAdsMove => m_fSpreadAdsMove;

    public float ReloadTime => m_fReloadTime;
    public float BoltCycleTime => m_fBoltCycleTime;

    public GameObject WorldModelPrefab => m_refWorldModelPrefab;

    /// <summary>발사 간 최소 간격(초). 완전자동은 RPM, 볼트액션은 볼트 조작 시간이 기준.</summary>
    public float GetFireInterval()
    {
        if (m_bIsFullAuto)
        {
            return m_fRoundsPerMinute > 0f ? 60f / m_fRoundsPerMinute : 0f;
        }

        return m_fBoltCycleTime;
    }
}
