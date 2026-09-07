using UnityEngine;

/*///////////////////////////////////////////
                AimTargetFollower
목적 : Aim.RigAimPosition을 매 프레임 자기 위치로 복사만 하는 오브젝트.
       Multi-Aim Constraint(WeaponAimIK)의 Source Object로 참조되어 무기 조준
       방향을 결정한다.
 *///////////////////////////////////////////

// Aim(10)이 이번 프레임 TargetPosition을 확정한 뒤에 복사해야 한다.
// 순서가 반대면 리그가 항상 1프레임 늦은 곳을 조준한다.
[DefaultExecutionOrder(20)]
public sealed class AimTargetFollower : MonoBehaviour
{
    [SerializeField] private Aim m_refAim;

    private void Update()
    {
        if (m_refAim == null)
            return;

        // 실제 히트 지점(TargetPosition)이 아니라 시선 방향의 먼 지점을 따라간다.
        // 히트 지점을 쓰면 가까운 벽·바닥을 볼 때 척추 정렬이 상체를 확 비튼다.
        transform.position = m_refAim.RigAimPosition;
    }
}
