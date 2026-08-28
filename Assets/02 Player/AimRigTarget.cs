using UnityEngine;

/*///////////////////////////////////////////
                AimRigTarget
목적 : Multi-Aim Constraint(상체/Chest)가 바라볼 소스 트랜스폼(이 오브젝트 자신)의
       위치를 매 프레임 Aim.TargetPosition으로 옮긴다. 회전 계산은 Multi-Aim
       Constraint가 담당하므로 위치만 갱신하면 된다 — 기존 Weapon.AimLookAt()이
       하던 수동 회전 역산이 필요 없어졌다. 이 오브젝트 자체를 Multi-Aim
       Constraint의 Source Objects 슬롯에 연결해서 쓴다.
 *///////////////////////////////////////////

public sealed class AimRigTarget : MonoBehaviour
{
    [SerializeField] private Aim m_refAim;

    private void Update()
    {
        if (m_refAim == null)
            return;

        transform.position = m_refAim.TargetPosition;
    }
}
