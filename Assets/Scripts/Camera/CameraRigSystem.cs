using Unity.Cinemachine;
using UnityEngine;

/*///////////////////////////////////////////
                CameraRigSystem
목적 : 3인칭 ↔ 1인칭(ADS) 카메라 전환과 마우스룩(yaw/pitch)을 담당한다.
       CM3의 Orbital/Composer 입력축 대신 yaw/pitch를 직접 누적해 Transform에
       적용하고, vcam은 CameraPivot 자식으로 붙어 부모를 그대로 따라가게 한다
       (Body/Aim = Do Nothing). 시점 전환은 CinemachineBrain의 Default Blend가
       0.2초 이내로 처리한다(기획 §4.2, 인수조건 #2).
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class CameraRigSystem : MonoBehaviour
{
    private const int PRIORITY_THIRD_PERSON = 10;
    private const int PRIORITY_ADS_ACTIVE = 20;
    private const int PRIORITY_INACTIVE = 0;

    [SerializeField] private Transform m_refPlayerRoot;
    [SerializeField] private Transform m_refCameraPivot;
    [SerializeField] private CinemachineCamera m_refThirdPersonCam;
    [SerializeField] private CinemachineCamera m_refAdsCamAK;
    [SerializeField] private CinemachineCamera m_refAdsCamTRG;

    [SerializeField] private float m_fLookSensitivity = 0.12f;
    [SerializeField] private float m_fPitchMin = -80f;
    [SerializeField] private float m_fPitchMax = 80f;

    private float m_fYaw;
    private float m_fPitch;

    public float Yaw => m_fYaw;
    public float Pitch => m_fPitch;

    private void Awake()
    {
        if (m_refPlayerRoot == null)
        {
            m_refPlayerRoot = transform;
        }

        m_fYaw = m_refPlayerRoot.eulerAngles.y;
        ApplyRotation();
    }

    private void Start()
    {
        ApplyPriorities(false, WeaponSlot.AK);
    }

    public void SetLookInput(Vector2 _vDelta)
    {
        if (_vDelta.sqrMagnitude <= 0f)
        {
            return;
        }

        m_fYaw += _vDelta.x * m_fLookSensitivity;
        m_fPitch = Mathf.Clamp(m_fPitch - _vDelta.y * m_fLookSensitivity, m_fPitchMin, m_fPitchMax);
        ApplyRotation();
    }

    public void SetAim(bool _bIsAiming, WeaponSlot _eActiveSlot)
    {
        ApplyPriorities(_bIsAiming, _eActiveSlot);
    }

    private void ApplyRotation()
    {
        // yaw는 몸통(루트)에, pitch는 CameraPivot 로컬 회전에만 적용한다
        m_refPlayerRoot.rotation = Quaternion.Euler(0f, m_fYaw, 0f);

        if (m_refCameraPivot != null)
        {
            m_refCameraPivot.localRotation = Quaternion.Euler(m_fPitch, 0f, 0f);
        }
    }

    private void ApplyPriorities(bool _bIsAiming, WeaponSlot _eActiveSlot)
    {
        if (m_refThirdPersonCam != null)
        {
            m_refThirdPersonCam.Priority = PRIORITY_THIRD_PERSON;
        }

        bool bAkActive = _bIsAiming && _eActiveSlot == WeaponSlot.AK;
        bool bTrgActive = _bIsAiming && _eActiveSlot == WeaponSlot.TRG;

        if (m_refAdsCamAK != null)
        {
            m_refAdsCamAK.Priority = bAkActive ? PRIORITY_ADS_ACTIVE : PRIORITY_INACTIVE;
        }

        if (m_refAdsCamTRG != null)
        {
            m_refAdsCamTRG.Priority = bTrgActive ? PRIORITY_ADS_ACTIVE : PRIORITY_INACTIVE;
        }
    }
}
