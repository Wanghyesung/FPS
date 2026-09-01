using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



/*///////////////////////////////////////////
                   Aim
기능 : ray, claude플레이어가 쏠 방향을 제공하는 클래스
 *///////////////////////////////////////////

// PlayerMovement.Look()이 이번 프레임 pitch/yaw를 CameraPivotTr에 먼저 확정해야
// 아래 RayCast()가 최신 값을 쓴다. PlayerMovement는 기본 실행 순서(0)라 이 값보다
// 큰 순서를 줘서 항상 그 다음에 실행되게 한다.
[DefaultExecutionOrder(10)]
public class Aim : MonoBehaviour
{
    [SerializeField] private Transform m_refCameraPitchTr; // PlayerMovement의 CameraPivot3D — 같은 프레임에 이미 확정된 pitch/yaw를 그대로 씀. Camera.main은 GameCameraManager.LateUpdate()에서만 갱신되어 한 프레임 지연된 값이라 쓰지 않는다.
    [SerializeField] private LayerMask m_tLayerMask;
    [SerializeField] private float m_fMaxLength;

    [SerializeField] private Image m_refAimImage;
    private Vector3 m_vTargetPosition = Vector3.zero;
    public Vector3 TargetPosition => m_vTargetPosition;

    private void Update()
    {
        m_vTargetPosition = RayCast();
    }

    public Vector3 RayCast()
    {
        if (m_refCameraPitchTr == null)
            return m_vTargetPosition;

        Ray tRay = new Ray(m_refCameraPitchTr.position, m_refCameraPitchTr.forward);

        RaycastHit hit;
        if(Physics.Raycast(tRay.origin, tRay.direction, out hit,  m_fMaxLength, m_tLayerMask) == true)
            return hit.point;

        return tRay.origin + tRay.direction * m_fMaxLength;
    }



    private void ChangeCollor(bool _bHit)
    {
        if (m_refAimImage == null)
            return;

        m_refAimImage.color = _bHit ? Color.red : Color.white;
    }
}
