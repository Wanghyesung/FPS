/*///////////////////////////////////////////
                PlayerModel
목적 : 플레이어의 런타임 상태(체력, 조준 여부)만 보유하는 순수 C# 모델.
       Unity API 의존이 전혀 없어야 하며, View/System를 절대 참조하지 않는다.
 *///////////////////////////////////////////

public sealed class PlayerModel
{
    public const int MAX_HP = 100;

    public int HP = MAX_HP;
    public bool IsAiming;

    public bool IsDead => HP <= 0;
}
