using UnityEngine;

/*///////////////////////////////////////////
                IZoneTarget
목적 : 자기장 피해 판정을 받을 수 있는 대상(플레이어/봇)의 공통 계약.
       ZoneSystem는 이 인터페이스만 알고 있으며, 구현체는 스스로 Register/Unregister한다
       (FindObjectOfType 금지 — performance.md).
       IDamageable을 상속하지만 자기장 피해는 별도 메서드(ApplyZoneDamage)로 받는다 —
       방탄조끼 감쇠가 "총알 피해"에만 적용돼야 하기 때문(기획 §5.2).
 *///////////////////////////////////////////

public interface IZoneTarget : IDamageable
{
    Vector3 Position { get; }
    bool IsDead { get; }
    void ApplyZoneDamage(int _iAmount);
}
