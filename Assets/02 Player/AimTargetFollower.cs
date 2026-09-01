using UnityEngine;

/*///////////////////////////////////////////
                AimTargetFollower
목적 : Aim.TargetPosition을 매 프레임 자기 위치로 복사만 하는 오브젝트.
       Multi-Aim Constraint(WeaponAimIK)의 Source Object로 참조되어 무기 조준
       방향을 결정한다. Update에서 위치값만 쓰기 때문에, 이 값을 읽는 RigBuilder
       그래프(Animation 단계, Update 다음 순서)는 항상 이번 프레임 최신 조준점을
       받는다 — Camera.main처럼 LateUpdate에서만 갱신되는 값을 참조할 때 생기는
       프레임 지연이 없다.
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
