using UnityEngine;

/*///////////////////////////////////////////
                WeaponAimAlign
목적 : 줌(조준) 중일 때만, 개머리판 맨 끝점(m_refPivotTr)을 축으로 총 전체를 회전시켜
       총구(Weapon.FireTr)가 화면 중앙 크로스헤어가 가리키는 지점(Aim.TargetPosition)을
       향하도록 정렬한다. 줌 상태의 조준 애니메이션이 잡아준 포즈 위에 얹는 보정 회전이라,
       애니메이션 자체를 대체하지 않고 그 결과(총구 방향)만 살짝 틀어 맞춘다.

       WeaponRecoilKick과 같은 GameObject(Weapon)의 로컬 Transform을 함께 건드리는데,
       RecoilKick은 매 프레임 로컬 포즈를 절대값으로 새로 써버리므로(스프링 오프셋 재계산),
       이 컴포넌트가 그보다 먼저 실행되면 이번 프레임의 정렬 회전이 RecoilKick에 덮여
       사라진다. DefaultExecutionOrder로 RecoilKick(기본값 0)보다 반드시 나중에 실행되도록
       강제해서, RecoilKick이 확정한 포즈 위에 정렬 회전이 얹히게 한다.

       Update에서 실행하는 이유는 WeaponRecoilKick과 동일 — Two Bone IK(왼손)는 Update와
       LateUpdate 사이의 Animation 단계에서 평가되므로, 그 전에 이번 프레임의 최종 회전을
       확정해야 왼손 IK가 같은 프레임의 보정된 LeftHandGrip 위치를 잡는다.
 *///////////////////////////////////////////

[DefaultExecutionOrder(50)]
public sealed class WeaponAimAlign : MonoBehaviour
{
    [SerializeField] private Transform m_refPivotTr;
    [SerializeField] private Aim m_refAim;
    [SerializeField] private float m_fBlendSpeed = 8f; // 초당 가중치 변화량 — 줌 On/Off 시 즉시 스냅되지 않고 이 속도로 부드럽게 붙었다 떨어짐

    private Weapon m_refWeapon;
    private float m_fWeight; // 0 = 정렬 회전 미적용(원래 포즈), 1 = 완전 정렬 — Zoom을 향해 매 프레임 보간됨

    public bool Zoom { get; set; }

    private void Awake()
    {
        m_refWeapon = GetComponent<Weapon>();
    }

    private void Update()
    {
        float fTargetWeight = Zoom ? 1f : 0f;
        m_fWeight = Mathf.MoveTowards(m_fWeight, fTargetWeight, m_fBlendSpeed * Time.deltaTime);

        if (m_fWeight <= 0f)
            return; // 가중치 0 — 정렬 회전 없음, WeaponRecoilKick이 확정한 원래 포즈 그대로 유지

        if (m_refPivotTr == null || m_refAim == null || m_refWeapon == null)
            return;

        Transform refFireTr = m_refWeapon.FireTr;
        if (refFireTr == null)
            return;

        Vector3 vDesiredDir = m_refAim.TargetPosition - m_refPivotTr.position;
        if (vDesiredDir.sqrMagnitude < 0.0001f)
            return;
        vDesiredDir.Normalize();

        Quaternion qDelta = Quaternion.FromToRotation(refFireTr.forward, vDesiredDir);
        qDelta.ToAngleAxis(out float fAngle, out Vector3 vAxis);

        if (Mathf.Approximately(fAngle, 0f))
            return;

        transform.RotateAround(m_refPivotTr.position, vAxis, fAngle * m_fWeight);
    }
}
