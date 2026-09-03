using UnityEngine;

/*///////////////////////////////////////////
                AimTargetFollower
목적 : Aim.TargetPosition을 매 프레임 자기 위치로 복사만 하는 오브젝트.
       Multi-Aim Constraint(WeaponAimIK)의 Source Object로 참조되어 무기 조준
       방향을 결정한다.
 *///////////////////////////////////////////

public sealed class AimTargetFollower : MonoBehaviour
{
    [SerializeField] private Aim m_refAim;

    private void Update()
    {
        if (m_refAim == null)
            return;

        transform.position = m_refAim.TargetPosition;
    }
}
