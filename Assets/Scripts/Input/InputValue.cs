using UnityEngine;

/*///////////////////////////////////////////
                tInputValue
목적 : 현재 프레임의 연속 입력값 스냅샷. InputView가 매 프레임 갱신해서
       `InputView.CurrentInput`으로 노출하므로, 다른 System이 InputView에
       직접 메서드를 붙이지 않고도 현재 입력 상태를 읽을 수 있다.
       Jump/Reload/Interact/SwitchWeapon/UseBandage/UseMedkit처럼 단발성
       (edge-triggered) 입력은 여기 포함하지 않는다 — unity-specifics.md 규칙대로
       콜백(performed)으로 각 System에 직접 전달되며, "현재 값"이라는 개념과 맞지 않는다.
 *///////////////////////////////////////////

[System.Serializable]
public struct tInputValue
{
    public Vector2 vMove;
    public Vector2 vLook;
    public bool bSprint;
    public bool bAim;
    public bool bFire;
}
