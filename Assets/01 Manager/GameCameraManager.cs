using UnityEngine;

/*///////////////////////////////////////////
                GameCameraManager
목적 : 메인 카메라를 관리하는 싱글톤
 *///////////////////////////////////////////

[DefaultExecutionOrder(100)] // CameraZoom(90)이 FOV를 확정한 뒤 카메라를 옮긴다
public sealed class GameCameraManager : MonoBehaviour
{
    public static GameCameraManager m_Instance { get; private set; }

    [SerializeField] private Transform m_refCamera;
    [SerializeField] private CameraZoom m_refCameraZoom; // 같은 오브젝트(System/Camera)의 컴포넌트
    [SerializeField] private Transform m_refFirstPersonPivot;
    public CameraZoom CameraZoom => m_refCameraZoom;


    // 아이언사이트 조준 시 카메라가 다가갈 지점
    private Transform m_refAimPivot;

    public void SetAimPivot(Transform _refPivot)
    {
        m_refAimPivot = _refPivot;
    }

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;

        if (m_refCamera == null && Camera.main != null)
            m_refCamera = Camera.main.transform;
    }

    private void OnDestroy()
    {
        if (m_Instance == this)
            m_Instance = null;
    }


    private void LateUpdate()
    {
        if (m_refCamera == null || m_refFirstPersonPivot == null)
            return;

        Vector3 vPos = m_refFirstPersonPivot.position;

        // 아이언사이트 조준 중이면 조준선 쪽으로 진행도만큼 다가간다
        if (m_refAimPivot != null && m_refCameraZoom != null)
            vPos = Vector3.Lerp(vPos, m_refAimPivot.position, m_refCameraZoom.ZoomProgress);

        m_refCamera.SetPositionAndRotation(vPos, m_refFirstPersonPivot.rotation);
    }
}
