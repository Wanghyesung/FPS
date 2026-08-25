/*///////////////////////////////////////////
                ItemType
목적 : 기획 §5.2 아이템 표의 종류 식별자. ItemDefinition(SO)이 어떤 효과 경로로
       처리될지를 이 값 하나로 분기한다(회복 / 장착 / 탄약 합산).
 *///////////////////////////////////////////

public enum ItemType
{
    Bandage = 0,
    Medkit = 1,
    Vest = 2,
    AmmoAK = 3,
    AmmoTRG = 4
}
