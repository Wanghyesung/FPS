using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;



/*///////////////////////////////////////////
                   Aim
기능 : ray, claude플레이어가 쏠 방향을 제공하는 클래스
 *///////////////////////////////////////////

public class Aim : MonoBehaviour
{
    [SerializeField] private LayerMask m_tLayerMask;
    [SerializeField] private float m_fMaxLength;

    [SerializeField] private Image m_refAimImage;
    private Vector3 m_vTargetPosition = Vector3.zero;
    public Vector3 TargetPosition => m_vTargetPosition;

    // Camera.main은 내부적으로 태그 검색(FindGameObjectsWithTag)을 돌기 때문에 매 프레임 호출하면 안 된다.
    private Camera m_refMainCamera;

    private void Awake()
    {
        m_refMainCamera = Camera.main;
    }

    private void Update()
    {
        m_vTargetPosition = RayCast();
    }

    public Vector3 RayCast()
    {
        // 카메라가 나중에 생성/교체되는 경우에만 다시 찾는다 — 정상 흐름에서는 Awake의 캐시를 그대로 쓴다.
        if (m_refMainCamera == null)
            m_refMainCamera = Camera.main;

        if (m_refMainCamera == null)
            return m_vTargetPosition;

        Ray tRay = m_refMainCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));

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
