using UnityEngine;

/*///////////////////////////////////////////
                CameraZoom
목적 : 렌더링 카메라의 FOV를 기본↔줌 사이로 보간한다.
       줌 진행도(0~1)와 시야각 기반 마우스 감도 배율을 파생값으로 제공한다.
 *///////////////////////////////////////////

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(90)] // GameCameraManager(100)가 카메라를 옮기기 전에 FOV를 확정한다
public sealed class CameraZoom : MonoBehaviour
{
    [SerializeField] private float m_fBaseFov = 60f;           
    [SerializeField] private float m_fDefaultBlendTime = 0.12f; // 무기가 블렌드 시간을 주지 않았을 때 쓰는 기본값

    private Camera m_refCamera;

    private float m_fZoomFov;         // 현재 무기가 요구하는 줌 FOV
    private float m_fBlendTime;
    private float m_fProgress;        // 0 = 기본, 1 = 완전 줌
    private float m_fTargetProgress;
    private bool m_bStart = true;

    public float ZoomProgress => m_fProgress;

    // 확대 배율은 tan(base/2) / tan(cur/2) 이므로, 화면상 커서 속도를 줌 전후 동일하게 유지하려면
    // 마우스 감도에 그 역수를 곱해야 한다.
    public float LookSensitivityScale { get; private set; }

    private void Awake()
    {
        m_refCamera = GetComponent<Camera>();

        m_fZoomFov = m_fBaseFov;
        m_fBlendTime = m_fDefaultBlendTime;

        ApplyFov(m_fBaseFov);
    }

    public void SetZoom(float _fZoomFov, float _fBlendTime)
    {
        m_fZoomFov = Mathf.Clamp(_fZoomFov, 1f, m_fBaseFov);
        m_fBlendTime = _fBlendTime > 0f ? _fBlendTime : m_fDefaultBlendTime;

        m_fTargetProgress = 1f;
        m_bStart = true;
    }

    public void ClearZoom()
    {
        m_fTargetProgress = 0f;
        m_bStart = true;
    }

    private void LateUpdate()
    { 
        if (m_bStart == false)
            return;

        //블랜드 시간만큼 FOV를 보간
        m_fProgress = Mathf.MoveTowards(m_fProgress, m_fTargetProgress, Time.deltaTime / Mathf.Max(m_fBlendTime, 0.01f));
        ApplyFov(Mathf.Lerp(m_fBaseFov, m_fZoomFov, Mathf.SmoothStep(0f, 1f, m_fProgress)));

        if (m_fProgress == m_fTargetProgress)
            m_bStart = false;
    }

    private void ApplyFov(float _fFov)
    {
        m_refCamera.fieldOfView = _fFov;

        float fBaseTan = Mathf.Tan(m_fBaseFov * 0.5f * Mathf.Deg2Rad);
        float fCurTan = Mathf.Tan(_fFov * 0.5f * Mathf.Deg2Rad);

        LookSensitivityScale = fBaseTan > 0.0001f ? (fCurTan / fBaseTan) : 1f;
    }
}
