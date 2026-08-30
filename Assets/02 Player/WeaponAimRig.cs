using UnityEngine;
using UnityEngine.Animations.Rigging;

/*///////////////////////////////////////////
                WeaponAimRig
목적 : 줌(ADS) 여부에 따라 상체 조준용 Multi-Aim Constraint의 weight를 0↔1로
       부드럽게 블렌드한다. 
 *///////////////////////////////////////////

public sealed class WeaponAimRig : MonoBehaviour
{
    [SerializeField] private MultiAimConstraint m_refAimConstraint;
    [SerializeField] private float m_fBlendSpeed = 8f;

    [SerializeField] private Transform m_refIKTarget = null;
    private float m_fCurrentWeight;
    private bool m_bZoomed;

    public void SetZoomed(bool _bZoomed, Vector3 _vTarget)
    {
        m_bZoomed = _bZoomed;

        if (m_refIKTarget != null)
            m_refIKTarget.position = _vTarget;
    }



    // 줌이 풀렸을 때도(m_bZoomed == false) weight를 0으로 되돌리는 블렌드는 계속 돌아야 한다 —
    // 예전엔 여기서 바로 return해버려서 한 번 줌하면 weight가 끝까지 1에 고정된 채 상체가
    // 영원히 조준 방향으로 꺾여있는 버그가 있었다.
    private void Update()
    {
        if (m_refAimConstraint == null || m_refIKTarget == null)
            return;

        float fTargetWeight = m_bZoomed ? 1f : 0f;
        m_fCurrentWeight = Mathf.MoveTowards(m_fCurrentWeight, fTargetWeight, Time.deltaTime * m_fBlendSpeed);
        m_refAimConstraint.weight = m_fCurrentWeight;

        if (m_fCurrentWeight <= 0.0001f)
            return;

        var sources = m_refAimConstraint.data.sourceObjects;
        sources.SetTransform(0, m_refIKTarget);
    }
}
