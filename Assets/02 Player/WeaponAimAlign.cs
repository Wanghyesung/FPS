using UnityEngine;
using UnityEngine.Animations.Rigging;

/*///////////////////////////////////////////
                WeaponAimAlign
목적 : 줌(조준) 여부에 따라 무기 조준용 Multi-Aim Constraint(WeaponAimIK)의
       weight를 부드럽게 블렌딩한다. 
 *///////////////////////////////////////////

public sealed class WeaponAimAlign : MonoBehaviour
{
    [SerializeField] private float m_fBlendSpeed = 8f; // 초당 가중치 변화량 — 줌 On/Off 시 즉시 스냅되지 않고 이 속도로 부드럽게 붙었다 떨어짐

    private MultiAimConstraint m_refAimConstraint;
    private float m_fWeight; // 0 = 정렬 미적용, 1 = 완전 정렬 — Zoom을 향해 매 프레임 보간됨

    public bool Zoom { get; set; }

    // WeaponRigTarget.SetWeapon()이 장착 시점에 호출한다.
    public void SetAimConstraint(MultiAimConstraint _refConstraint)
    {
        m_refAimConstraint = _refConstraint;
    }

    private void Update()
    {
        float fTargetWeight = Zoom ? 1f : 0f;
        m_fWeight = Mathf.MoveTowards(m_fWeight, fTargetWeight, m_fBlendSpeed * Time.deltaTime);

        if (m_refAimConstraint != null)
            m_refAimConstraint.weight = m_fWeight;
    }
}
