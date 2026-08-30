using UnityEngine;
using UnityEngine.Animations.Rigging;

/*///////////////////////////////////////////
                WeaponRigTarget
목적 : 왼손/오른손을 현재 장착된 무기(Weapon)의 그립 트랜스폼에 매 프레임 IK로 맞춘다.
       TwoBoneIKConstraint(Animation Rigging)의 root/mid/tip/target/hint "데이터"는
       씬에 미리 배치된 구성을 그대로 재사용하지만, 실제 풀이는 Animation Rigging의
       PlayableGraph가 아니라 AnimationRuntimeUtils.SolveTwoBoneIK()로 이 스크립트가
       LateUpdate에서 직접 수행한다(그래서 각 constraint의 weight는 0으로 꺼둔다 —
       Rigging 쪽 평가가 남아있어도 어차피 이 스크립트가 덮어쓰지만, 혼란을 피하려고
       아예 꺼둔다).

       이렇게 우회하는 이유: 무기는 더 이상 팔 뼈에 매달려 있지 않고 WeaponAimDriver가
       매 프레임 LateUpdate에서 회전시킨다(그래야 하는 이유는 WeaponAimDriver 주석 참고).
       그런데 Animation Rigging의 TwoBoneIKConstraint는 Update와 LateUpdate 사이의
       애니메이션 단계에서 딱 한 번만 풀리므로, WeaponAimDriver가 "그 다음"(LateUpdate)에
       무기를 움직이면 이번 프레임 IK는 항상 무기가 아직 안 움직인 이전 프레임 기준으로
       풀린 것이 된다. 무기가 계속 회전하는 한(피벗이 아닌 쪽 그립은 회전할 때마다
       위치가 계속 바뀐다) 이 한 프레임 지연은 절대 따라잡히지 않는다(실측 확인됨).
       그래서 WeaponAimDriver([DefaultExecutionOrder(150)])가 무기 회전을 끝낸
       "바로 다음"(같은 LateUpdate 큐의 더 늦은 순서)에 손 IK를 다시 풀어서 완전히
       같은 프레임에서 동기화되게 한다.
 *///////////////////////////////////////////

[DefaultExecutionOrder(200)]
public sealed class WeaponRigTarget : MonoBehaviour
{
    [SerializeField] private TwoBoneIKConstraint m_refLeftHandIK;
    [SerializeField] private TwoBoneIKConstraint m_refRightHandIK;

    [SerializeField] private Transform m_refLeftElbowHintTr;
    [SerializeField] private Transform m_refRightElbowHintTr;

    public Transform LeftHint => m_refLeftElbowHintTr;
    public Transform RightHint => m_refRightElbowHintTr;

    // Player.EquipWeapon()이 무기 장착 시점에 호출한다.
    public void SetWeapon(Transform _refLeftGrip, Transform _refLeftHint, Transform _refRightGrip, Transform _refRightHint)
    {
        SetHand(m_refLeftHandIK, _refLeftGrip, _refLeftHint);
        SetHand(m_refRightHandIK, _refRightGrip, _refRightHint);
    }

    // 현재 코드베이스에 무기 해제 로직은 없지만, 추후 드롭 기능 추가 시 IK를 끄기 위해 대비해 둔다.
    public void ClearWeapon()
    {
        SetHand(m_refLeftHandIK, null, null);
        SetHand(m_refRightHandIK, null, null);
    }

    private void LateUpdate()
    {
        SolveHand(m_refLeftHandIK);
        SolveHand(m_refRightHandIK);
    }

    private void SolveHand(TwoBoneIKConstraint _refConstraint)
    {
        if (_refConstraint == null || _refConstraint.data.target == null)
            return;

        var refData = _refConstraint.data;
        SolveTwoBoneIK(refData.root, refData.mid, refData.tip, refData.target, refData.hint, refData.hintWeight, refData.targetRotationWeight);
    }

    // AnimationRuntimeUtils.SolveTwoBoneIK은 AnimationStream/TransformHandle 기반 잡 API라
    // 일반 스크립트에서 직접 호출할 수 없다 — 그래서 동일한 표준 해석적(코사인 법칙) 2본 IK를
    // 직접 구현한다. 위치만 맞추고 tip(손) 회전은 그대로 두면, 달리기처럼 팔이 크게 움직이는
    // 애니메이션 중엔 팔꿈치를 굽힌 방향과 원래 애니메이션의 손 회전이 어긋나 손목이 꺾인
    // 것처럼 보인다 — 그래서 위치를 맞춘 뒤 tip의 회전도 target 쪽으로 같이 정렬한다.
    private static void SolveTwoBoneIK(Transform _root, Transform _mid, Transform _tip, Transform _target, Transform _hint, float _fHintWeight, float _fRotWeight)
    {
        Vector3 vRootPos = _root.position;
        Vector3 vMidPos = _mid.position;
        Vector3 vTargetPos = _target.position;

        float fUpperLen = Vector3.Distance(vRootPos, vMidPos);
        float fLowerLen = Vector3.Distance(vMidPos, _tip.position);
        float fMaxLen = fUpperLen + fLowerLen - 0.0001f;
        float fMinLen = Mathf.Abs(fUpperLen - fLowerLen) + 0.0001f;

        Vector3 vToTarget = vTargetPos - vRootPos;
        float fTargetLen = Mathf.Clamp(vToTarget.magnitude, fMinLen, fMaxLen);
        Vector3 vDirToTarget = vToTarget.normalized;

        // 굽힘 평면 법선 — 힌트(폴 벡터)가 있으면 그쪽 방향, 없으면 현재 mid 위치를 기준으로 삼는다.
        Vector3 vBendRef = (_hint != null && _fHintWeight > 0f) ? (_hint.position - vRootPos) : (vMidPos - vRootPos);
        Vector3 vBendNormal = Vector3.Cross(vDirToTarget, vBendRef);
        if (vBendNormal.sqrMagnitude < 0.0001f)
            vBendNormal = Vector3.Cross(vDirToTarget, Vector3.up);
        vBendNormal.Normalize();

        float fCosAngle = (fUpperLen * fUpperLen + fTargetLen * fTargetLen - fLowerLen * fLowerLen) / (2f * fUpperLen * fTargetLen);
        fCosAngle = Mathf.Clamp(fCosAngle, -1f, 1f);
        float fAngleDeg = Mathf.Acos(fCosAngle) * Mathf.Rad2Deg;

        Vector3 vNewMidDir = Quaternion.AngleAxis(fAngleDeg, vBendNormal) * vDirToTarget;

        // root 회전 — root->mid 방향을 새로 구한 방향으로 돌린다.
        Vector3 vOldRootToMidDir = (vMidPos - vRootPos).normalized;
        Quaternion qRootDelta = Quaternion.FromToRotation(vOldRootToMidDir, vNewMidDir);
        _root.rotation = qRootDelta * _root.rotation;

        // mid 회전 — root 회전으로 갱신된 실제 위치 기준, mid->tip 방향이 target을 향하도록 돌린다.
        Vector3 vNewDirMidToTarget = (vTargetPos - _mid.position).normalized;
        Vector3 vCurrentMidToTipDir = (_tip.position - _mid.position).normalized;
        Quaternion qMidDelta = Quaternion.FromToRotation(vCurrentMidToTipDir, vNewDirMidToTarget);
        _mid.rotation = qMidDelta * _mid.rotation;

        // tip(손) 회전 — 그립 지점의 회전으로 맞춰서 애니메이션이 남긴 손 회전과 어긋나
        // 손목이 꺾여 보이는 것을 막는다.
        if (_fRotWeight > 0f)
            _tip.rotation = Quaternion.Slerp(_tip.rotation, _target.rotation, _fRotWeight);
    }

    private void SetHand(TwoBoneIKConstraint _refConstraint, Transform _refGrip, Transform _refHint)
    {
        if (_refConstraint == null)
            return;

        // Animation Rigging 쪽 평가는 꺼서, 이 스크립트의 수동 풀이와 이중으로 겹치지 않게 한다.
        _refConstraint.weight = 0f;

        if (_refGrip == null)
            return;

        _refConstraint.data.target = _refGrip;
        _refConstraint.data.hint = _refHint;
    }
}
