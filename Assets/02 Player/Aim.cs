using UnityEngine;
using UnityEngine.Serialization;

/*///////////////////////////////////////////
                   Aim
기능 : ray, 플레이어가 쏠 방향을 제공하는 클래스
 *///////////////////////////////////////////

// PlayerMovement.Look()이 이번 프레임 pitch/yaw를 CameraPivotTr에 먼저 확정해야
// 아래 RayCast()가 최신 값을 쓴다.
[DefaultExecutionOrder(10)]
public class Aim : MonoBehaviour
{
    // 시선 피벗. 플레이어는 CameraPivot3D(= GameCameraManager가 카메라를 스냅시키는 그 트랜스폼),
    // 적은 자기 머리 피벗을 물린다. 렌더 카메라와 이 트랜스폼이 일치해야
    // 화면 중앙(크로스헤어)과 실제 탄착이 맞는다.
    [SerializeField, FormerlySerializedAs("m_refCameraPitchTr")]
    private Transform m_refMainCameraTr;

    [SerializeField] private LayerMask m_tLayerMask;
    [SerializeField] private float m_fMaxLength;

    // 실제로 레이가 맞은 지점. 사격·투척이 여기를 향한다(총구와 크로스헤어가 이 점에서 수렴).
    private Vector3 m_vTargetPosition = Vector3.zero;
    public Vector3 TargetPosition => m_vTargetPosition;

    // 리그(척추 정렬 SpineAimIK, 무기 정렬 WeaponAimIK)가 바라볼 지점.
    private Vector3 m_vRigAimPosition = Vector3.zero;
    public Vector3 RigAimPosition => m_vRigAimPosition;

    private int m_iCurrentHitLayer = -1;
    public int CurrentHitLayer => m_iCurrentHitLayer;

    private void Update()
    {
        Ray tRay = new Ray(m_refMainCameraTr.position, m_refMainCameraTr.forward);

        m_vRigAimPosition = tRay.origin + tRay.direction * m_fMaxLength;

        RaycastHit tHit;
        if (Physics.Raycast(tRay.origin, tRay.direction, out tHit, m_fMaxLength, m_tLayerMask) == true)
        {
            m_iCurrentHitLayer = tHit.collider.gameObject.layer;
            m_vTargetPosition = tHit.point;
            return;
        }

        m_vTargetPosition = m_vRigAimPosition;
    }
}
