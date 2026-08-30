using UnityEngine;

/*///////////////////////////////////////////
                WeaponAimDriver
목적 : 무기 루트의 회전을 매 프레임 조준경/총구(ZoomTr, 없으면 FireTr)에서 조준점
       (Aim.TargetPosition)을 향하도록 직접 계산해서 맞춘다. 줌 여부와 무관하게 항상
       적용한다 — 힙파이어에서도 크로스헤어를 정확히 조준하는 배틀그라운드 스타일.
       실제 탄 퍼짐(부정확도)은 Weapon.m_fInaccuracyAngle이 따로 처리하므로, 이
       컴포넌트는 무기 모델의 시각적 방향만 담당한다.

       매 프레임 "지금 회전에서 얼마나 더 돌릴지"(delta)를 누적 적용하는 방식(이전 구현)은
       Quaternion.FromToRotation을 forward/roll 두 번 겹쳐 적용하다 보니 완전히 수렴하지
       못하고 매 프레임 계속 흔들렸다 — 실측 결과 왼손 그립(RightHandGripTr가 아닌 쪽,
       회전 피벗이 아니라서 회전할 때마다 위치가 계속 바뀜)이 프레임마다 크게 움직여서,
       그 뒤에 도는 손 IK가 한 프레임 뒤처진 채 절대 따라잡지 못하는 문제로 이어졌다.
       그래서 매 프레임 "무기 루트가 최종적으로 가져야 할 회전"을 Quaternion.LookRotation
       으로 한 번에 명시적으로(forward와 up 모두) 계산해서 안정적인 목표점으로 삼고,
       거기로 Slerp만 한다 — 목표점이 프레임마다 흔들리지 않으므로 그립도 흔들리지 않고
       매끄럽게 수렴해서 정지한다.

       오른손 그립(RightHandGripTr) 위치를 고정 피벗으로 삼아 그 지점 기준으로만
       회전시킨다 — 오른손은 IK로 이 그립을 그대로 쫓아가므로, 피벗을 오른손 그립으로
       잡아야 회전할 때 오른손이 크게 튀지 않고 총구 쪽만 조준점으로 돈다.

       반동(WeaponRecoilKick)과는 계층으로 분리되어 있다 — 이 컴포넌트는 무기 루트를,
       반동은 그 자식인 RecoilPivot을 건드리므로 서로 기준 포즈를 덮어쓰며 싸우지 않는다.
 *///////////////////////////////////////////

[DefaultExecutionOrder(150)]
public sealed class WeaponAimDriver : MonoBehaviour
{
    [SerializeField] private float m_fTurnSpeed = 25f; // 조준점의 프레임 간 노이즈를 완충하는 추적 속도(초당 보간 계수)
    private Weapon m_refWeapon;

    private Vector3 m_vTargetPosition;
    private bool m_bHasTarget;
    private bool m_bSnapPending = true; // 장착 직후 첫 보정은 감쇠 없이 즉시 스냅해서 잘못된 시작 자세가 눈에 보이지 않게 한다

    private void Awake()
    {
        m_refWeapon = GetComponent<Weapon>();
    }

    // Weapon.SetAimCorrection()이 Player.Update()로부터 받은 조준점을 그대로 넘겨준다.
    public void SetTarget(Vector3 _vTargetPos)
    {
        m_vTargetPosition = _vTargetPos;
        m_bHasTarget = true;
    }

    // Weapon.Init()이 장착 시점에 호출한다 — 다음 보정을 감쇠 없이 즉시 스냅시켜서
    // 편집 중 임의로 잡혀있던 시작 자세가 한 프레임이라도 화면에 보이지 않게 한다.
    public void SnapNextCorrection()
    {
        m_bSnapPending = true;
    }

    // 반드시 LateUpdate여야 한다 — 무기는 Spine_03 아래 WeaponHoldAnchor의 자식이고, Spine_03은
    // Animator + Animation Rigging이 움직인다. 애니메이션 평가는 Update 이후 LateUpdate 이전에
    // 일어나므로, Update에서 월드 회전을 써봐야 그 직후 부모 본이 다시 움직이며 무효화된다
    // (실측 결과 조준 방향과 20도가량 어긋난 채 수렴하지 못했다).
    private void LateUpdate()
    {
        if (m_bHasTarget == false || m_refWeapon == null)
            return;

        Transform refSight = m_refWeapon.ZoomTr != null ? m_refWeapon.ZoomTr : m_refWeapon.FireTr;
        Transform refPivot = m_refWeapon.RightHandGripTr;
        if (refSight == null || refPivot == null)
            return;

        Vector3 vDesiredDir = m_vTargetPosition - refSight.position;
        if (vDesiredDir.sqrMagnitude < 0.0001f)
            return;

        // refSight가 무기 루트의 자식으로서 갖는 "고유" 로컬 회전(메시 지오메트리상 고정 오프셋)을
        // 역산해서, refSight의 목표 월드 회전(LookRotation으로 forward+up 둘 다 명시)을 만족시키는
        // 무기 루트의 목표 회전을 구한다. forward만 맞추고 roll은 방치하는 방식이 아니라서
        // 총이 뒤집힌 채로 수렴하는 일도, 매 프레임 델타가 누적되며 발산하는 일도 없다.
        Quaternion qSightLocalRot = Quaternion.Inverse(transform.rotation) * refSight.rotation;
        Quaternion qDesiredSightWorldRot = Quaternion.LookRotation(vDesiredDir.normalized, Vector3.up);
        Quaternion qDesiredRootRot = qDesiredSightWorldRot * Quaternion.Inverse(qSightLocalRot);

        float fT = m_bSnapPending ? 1f : Mathf.Clamp01(Time.deltaTime * m_fTurnSpeed);
        m_bSnapPending = false;

        Quaternion qNewRootRot = Quaternion.Slerp(transform.rotation, qDesiredRootRot, fT);
        Quaternion qDelta = qNewRootRot * Quaternion.Inverse(transform.rotation);

        // 오른손 그립 위치(vPivot)를 고정점으로 삼아 그 지점 기준으로만 회전시킨다 —
        // 그래야 오른손 그립 위치는 전혀 움직이지 않고, 총구 쪽만 조준점으로 돈다.
        Vector3 vPivot = refPivot.position;
        Vector3 vOffset = transform.position - vPivot;

        transform.rotation = qNewRootRot;
        transform.position = vPivot + qDelta * vOffset;
    }
}
