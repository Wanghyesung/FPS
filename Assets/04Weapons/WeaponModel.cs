/*///////////////////////////////////////////
                WeaponModel
목적 : 무기 슬롯 하나의 런타임 상태(탄창/예비탄/재장전 여부/다음 발사 가능 시각).
       WeaponDefinition(SO)은 여러 인스턴스가 공유하므로 변동 상태는 전부 여기에 둔다.
 *///////////////////////////////////////////

public sealed class WeaponModel
{
    public int CurrentAmmoInMag;
    public int ReserveAmmo;
    public bool IsReloading;
    public float NextFireReadyTime;
}
