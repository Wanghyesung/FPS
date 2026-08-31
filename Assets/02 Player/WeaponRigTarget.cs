using UnityEngine;
using UnityEngine.Animations.Rigging;

/*///////////////////////////////////////////
                WeaponRigTarget
목적 : Animation Rigging의 Two Bone IK Constraint(왼손/오른손)가 참조할 target/hint를
       현재 장착된 무기(Weapon)의 그립 트랜스폼으로 갈아끼운다. Rig 계층(Constraint
       컴포넌트 자체)은 씬에 미리 배치되어 있지만, target/hint Transform 참조는
       RigBuilder.Build() 시점에 PlayableGraph의 TransformStreamHandle로 한 번
       바인딩된다 — 이후 data.target/hint를 코드로만 바꿔서는 이미 빌드된 job이
       그 변경을 반영하지 않으므로, 참조를 바꿀 때마다 Build()를 다시 호출해야 한다.
 *///////////////////////////////////////////

public sealed class WeaponRigTarget : MonoBehaviour
{
    [SerializeField] private RigBuilder m_refRigBuilder;

    [SerializeField] private TwoBoneIKConstraint m_refLeftHandIK;
    [SerializeField] private TwoBoneIKConstraint m_refRightHandIK;

    [SerializeField] private Transform m_refLeftHintTr;
    [SerializeField] private Transform m_refRightHintTr;

    public Transform LeftHint => m_refLeftHintTr;
    public Transform RightHint => m_refRightHintTr;

    // Player.EquipWeapon()이 무기 장착 시점에 호출한다.
    public void SetWeapon(Transform _refLeftGrip,Transform _refLeftHint,
        Transform _refRightGrip, Transform _refRightHint)
    {
        SetHand(m_refLeftHandIK, _refLeftGrip, _refLeftHint);
        SetHand(m_refRightHandIK, _refRightGrip, _refRightHint);

        Rebuild();
    }

    // 현재 코드베이스에 무기 해제 로직은 없지만, 추후 드롭 기능 추가 시 IK를 끄기 위해 대비해 둔다.
    public void ClearWeapon()
    {
        SetHand(m_refLeftHandIK, null, null);
        SetHand(m_refRightHandIK, null, null);

        Rebuild();
    }

    // target/hint 참조 변경을 실제 PlayableGraph에 반영하기 위해 재빌드한다.
    private void Rebuild()
    {
        if (m_refRigBuilder != null)
            m_refRigBuilder.Build();
    }

    private void SetHand(TwoBoneIKConstraint _refConstraint, Transform _refGrip, Transform _refHint)
    {
        if (_refConstraint == null)
            return;

        if (_refGrip == null)
        {
            _refConstraint.weight = 0f;
            return;
        }

        _refConstraint.data.target = _refGrip;
        _refConstraint.data.hint = _refHint;
        _refConstraint.data.targetPositionWeight = 1f;
        _refConstraint.weight = 1f;
    }

}
