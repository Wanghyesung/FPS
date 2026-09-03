using UnityEngine;

/*///////////////////////////////////////////
                WeaponPickup
목적 : 월드에 놓인 무기에 플레이어가 접촉하면 Player.PickupWeapon으로 넘겨준다.
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

    //TODO : 나중에 통합된 구조로 잡기
    private void OnTriggerEnter(Collider _refOther)
    {
        if (m_bPicked)
            return;

        if(_refOther.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            m_bPicked = true;
            Player refPlayer = _refOther.GetComponentInParent<Player>();
            refPlayer.PickupWeapon(m_refWeapon);
        }
        else if(_refOther.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            m_bPicked = true;
            Enemy refEnemy = _refOther.GetComponentInParent<Enemy>();
            refEnemy.PickupWeapon(m_refWeapon);
        }
    }
}
