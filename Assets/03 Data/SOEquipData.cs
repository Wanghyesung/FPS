using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Weapon;
[CreateAssetMenu(fileName = "SO_EquipData", menuName = "Game/Data/Equip")]



public class SOEquipData : SOData
{
    public eWeaponType WeaponType;

    public override eDataType DataType =>eDataType.Equip;

    public override int SubDataType => 0;

    public override void Use()
    {
        Player.CurrentPlayer.UseWeapon(WeaponType);
    }
}
