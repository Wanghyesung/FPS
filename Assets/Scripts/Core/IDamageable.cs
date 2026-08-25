/*///////////////////////////////////////////
                IDamageable
목적 : 히트스캔 사격이 명중했을 때 데미지를 받을 수 있는 대상의 공통 계약.
       PlayerSystem이 구현하며, MonsterAgent도 동일 인터페이스로 히트스캔을 받는다.
 *///////////////////////////////////////////

public interface IDamageable
{
    void TakeDamage(int _iAmount, bool _bIsHeadshot);
}
