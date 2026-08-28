using UnityEngine;

/*///////////////////////////////////////////
                WeaponPickup
목적 : 월드에 놓인 무기에 플레이어가 접촉하면 Player.PickupWeapon으로 넘겨준다.
       이 GameObject의 Collider는 Is Trigger로 설정돼야 한다(수동 설정 필요).
 *///////////////////////////////////////////

[RequireComponent(typeof(Weapon))]
public sealed class WeaponPickup : MonoBehaviour
{
    private Weapon m_refWeapon;
    private bool m_bPicked = false;

    private void Awake()
    {
        m_refWeapon = GetComponent<Weapon>();
    }

    private void OnTriggerEnter(Collider _tOther)
    {
        if (m_bPicked)
            return;

        Player refPlayer = _tOther.GetComponentInParent<Player>();
        if (refPlayer == null)
            return;

        m_bPicked = true;
        refPlayer.PickupWeapon(m_refWeapon);
    }
}
