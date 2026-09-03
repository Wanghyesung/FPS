using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



/*///////////////////////////////////////////
                   Aim
기능 : ray, claude플레이어가 쏠 방향을 제공하는 클래스
 *///////////////////////////////////////////

// PlayerMovement.Look()이 이번 프레임 pitch/yaw를 CameraPivotTr에 먼저 확정해야
// 아래 RayCast()가 최신 값을 쓴다. 
[DefaultExecutionOrder(10)]
public class Aim : MonoBehaviour
{
    //줌했을 때 Ray와 일반 3인칭 시점의 레이를 구분하기 위해서 2개의 Transform을 가진다
    [SerializeField] private Transform m_refMainCameraTr; 
    private Transform m_refSecondCameraTr = null; 

    [SerializeField] private LayerMask m_tLayerMask;
    [SerializeField] private float m_fMaxLength;

    [SerializeField] private Image m_refAimImage;
    private Vector3 m_vTargetPosition = Vector3.zero;
    public Vector3 TargetPosition => m_vTargetPosition;

  
    public void ChangePivot(Transform _refPivot)
    {
        m_refSecondCameraTr = _refPivot;
    }
    private void Update()
    {
        m_vTargetPosition = RayCast();
    }

    public Vector3 RayCast()
    {
       Transform refCamTr = m_refSecondCameraTr != null ? m_refSecondCameraTr : m_refMainCameraTr;
        Ray tRay = new Ray(m_refMainCameraTr.position, m_refMainCameraTr.forward);

        RaycastHit hit;
        if(Physics.Raycast(tRay.origin, tRay.direction, out hit,  m_fMaxLength, m_tLayerMask) == true)
            return hit.point;

        return tRay.origin + tRay.direction * m_fMaxLength;
    }
}
