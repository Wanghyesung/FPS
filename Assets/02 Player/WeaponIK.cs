using UnityEngine;

/*///////////////////////////////////////////
                WeaponIK
목적 : 왼손이 현재 장착된 무기의 그립 포인트(Weapon.LeftHandGripTr)를
       애니메이션 종류와 상관없이 항상 정확히 잡도록 휴머노이드 IK로
       왼손 위치/회전을 매 프레임 보정한다.

       오른손은 IK로 처리하지 않는다 — 오른손이 잡는 지점(그립)은 무기 자체에
       리지드 페어런팅되어 있어서(WeaponSocket이 Hand_R의 자식), 오른손을 무기 위의
       한 점으로 IK 이동시키려 해도 그 점이 오른손과 함께 그대로 따라 움직이는
       자기참조 관계라 수학적으로 절대 수렴하지 않는다(항상 같은 고정 오프셋만큼
       어긋난 채로 멈춤). 오른손 그립 위치는 대신 WeaponSocket의 로컬 오프셋 자체를
       정확히 맞춰서 고정한다(Player.cs 및 WeaponSocket 참고).
 *///////////////////////////////////////////

[RequireComponent(typeof(Animator))]
public sealed class WeaponIK : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float m_fRotationWeight = 1f; // 튜닝용 — 관절 가동범위 한계로 손목이 부자연스럽게 꺾일 때 낮춰서 원래 애니메이션 회전과 블렌드

    private Animator m_refAnimator;
    private Transform m_refLeftHandGrip;

    private void Awake()
    {
        m_refAnimator = GetComponent<Animator>();
    }

    public void SetLeftHandGrip(Transform _refGrip)
    {
        m_refLeftHandGrip = _refGrip;
    }

    private void OnAnimatorIK(int _iLayerIndex)
    {
        if (_iLayerIndex != 0)
            return;

        float fWeight = m_refLeftHandGrip != null ? 1f : 0f;

        m_refAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, fWeight);
        m_refAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, fWeight * m_fRotationWeight);

        if (m_refLeftHandGrip == null)
            return;

        m_refAnimator.SetIKPosition(AvatarIKGoal.LeftHand, m_refLeftHandGrip.position);
        m_refAnimator.SetIKRotation(AvatarIKGoal.LeftHand, m_refLeftHandGrip.rotation);
    }
}
