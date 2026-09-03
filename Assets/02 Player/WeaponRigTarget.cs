using UnityEngine;
using UnityEngine.Animations.Rigging;

/*///////////////////////////////////////////
                WeaponRigTarget
목적 : Animation Rigging의 Two Bone IK Constraint(왼손/오른손)가 참조할 target/hint를
       현재 장착된 무기(Weapon)의 그립 트랜스폼으로 갈아끼운다.
 *///////////////////////////////////////////

public sealed class WeaponRigTarget : MonoBehaviour
{
    [SerializeField] private RigBuilder m_refRigBuilder;

    [SerializeField] private TwoBoneIKConstraint m_refLeftHandIK;
    [SerializeField] private TwoBoneIKConstraint m_refRightHandIK;
    [SerializeField] private MultiAimConstraint m_refWeaponAimConstraint; // WeaponAimIK — 장착된 무기가 바뀔 때마다 constrainedObject를 갈아끼워 재사용하는 공유 콘스트레인트

    [SerializeField] private Transform m_refLeftHintTr;
    [SerializeField] private Transform m_refRightHintTr;

    public Transform LeftHint => m_refLeftHintTr;
    public Transform RightHint => m_refRightHintTr;

    // Player.EquipWeapon()이 무기 장착 시점에 호출한다.
    public void SetWeapon(Transform _refWeaponRoot, Transform _refLeftGrip, Transform _refLeftHint,
        Transform _refRightGrip, Transform _refRightHint)
    {
        SetHand(m_refLeftHandIK, _refLeftGrip, _refLeftHint);
        SetHand(m_refRightHandIK, _refRightGrip, _refRightHint);
        SetAim(_refWeaponRoot);

        Rebuild();
    }

    // 현재 코드베이스에 무기 해제 로직은 없지만, 추후 드롭 기능 추가 시 IK를 끄기 위해 대비해 둔다.
    public void ClearWeapon()
    {
        SetHand(m_refLeftHandIK, null, null);
        SetHand(m_refRightHandIK, null, null);
        SetAim(null);

        Rebuild();
    }

    // WeaponAimIK(Multi-Aim Constraint)가 회전시킬 대상을 이번에 장착된 무기 루트로 갈아끼우고,
    // 그 무기의 WeaponAimAlign에게 이 콘스트레인트 참조를 넘겨 weight 블렌딩을 맡긴다.
    private void SetAim(Transform _refWeaponRoot)
    {
        if (m_refWeaponAimConstraint == null)
            return;

        m_refWeaponAimConstraint.data.constrainedObject = _refWeaponRoot;

        if (_refWeaponRoot == null)
            return;

        WeaponAimAlign refAimAlign = _refWeaponRoot.GetComponent<WeaponAimAlign>();
        if (refAimAlign != null)
            refAimAlign.SetAimConstraint(m_refWeaponAimConstraint);
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
