/*///////////////////////////////////////////
                IDamageable
목적 : Bullet의 히트 판정이 데미지를 전달할 수 있는 대상의 공통 계약.
 *///////////////////////////////////////////

public interface IDamageable
{
    void TakeDamage(AttackInfo _refAttackInfo, tShotInfo _tShotInfo);
}
