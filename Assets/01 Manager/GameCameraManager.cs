using UnityEngine;

/*///////////////////////////////////////////
                GameCameraManager
목적 : 메인 카메라를 관리하는 싱글톤
 *///////////////////////////////////////////

public sealed class GameCameraManager : MonoBehaviour
{
    public static GameCameraManager m_Instance { get; private set; }

    [SerializeField] private Transform m_refCamera;
    [SerializeField] private Transform m_refFirstPersonPivot;
    [SerializeField] private Transform m_refThirdPersonPivot;
    [SerializeField] private PlayerMovement m_refPlayerMovement; // 반동을 실제 조준 pitch/yaw에 직접 얹기 위한 참조 — Shake() 참고
    public Transform ThirdPersonPivot { get { return m_refThirdPersonPivot; } set { m_refThirdPersonPivot = value; } }

    [SerializeField] private float m_fBlendSpeed = 10f;

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

        Transform refPositionTarget = m_refThirdPersonPivot != null ? m_refThirdPersonPivot : m_refFirstPersonPivot;
        float fT = Time.deltaTime * m_fBlendSpeed;

        m_refCamera.position = Vector3.Lerp(m_refCamera.position, refPositionTarget.position, fT);
        m_refCamera.rotation = Quaternion.Slerp(m_refCamera.rotation, m_refFirstPersonPivot.rotation, fT);
    }
}
