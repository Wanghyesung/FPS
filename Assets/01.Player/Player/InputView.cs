using UnityEngine;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
                InputView
목적 : New Input System과 게임 System 사이의 얇은 어댑터. PlayerControls를 소유하는
       유일한 클래스이며(씬당 1개), 게임 로직은 전혀 갖지 않는다.
       연속 입력(Move/Look/Sprint/Aim/Fire)은 Update에서 폴링해 tInputValue로 모아
       CurrentInput 프로퍼티로 노출한다 — 다른 System이 InputView에 새 메서드를
       추가하지 않고도 현재 입력값을 읽을 수 있게 하기 위함이다. 불연속 입력
       (Jump/Reload/Interact/SwitchWeapon/UseBandage/UseMedkit)은 여전히 performed
       콜백으로 각 System에 직접 전달한다.
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class InputView : MonoBehaviour
{
    [SerializeField] private PlayerSystem m_refPlayerSystem;
    [SerializeField] private CameraRigSystem m_refCameraRig;
    [SerializeField] private WeaponSystem m_refWeaponSystem;
    [SerializeField] private ItemSystem m_refItemSystem;
    [SerializeField] private bool m_bLockCursor = true;

    private PlayerControls m_controls;
    private tInputValue m_tCurrentInput;
    private bool m_bIsAiming;

    /// <summary>현재 프레임의 연속 입력값 스냅샷 — 다른 System이 읽기 전용으로 조회한다.</summary>
    public tInputValue CurrentInput => m_tCurrentInput;

    private void Awake()
    {
        m_controls = new PlayerControls();
    }

    private void OnEnable()
    {
        m_controls.Player.Enable();

        m_controls.Player.Jump.performed += OnJump;
        m_controls.Player.Crouch.performed += OnCrouch;
        m_controls.Player.Aim.performed += OnAimStarted;
        m_controls.Player.Aim.canceled += OnAimCanceled;
        m_controls.Player.Fire.performed += OnFireStarted;
        m_controls.Player.Fire.canceled += OnFireCanceled;
        m_controls.Player.Reload.performed += OnReload;
        m_controls.Player.SwitchToAK.performed += OnSwitchToAK;
        m_controls.Player.SwitchToTRG.performed += OnSwitchToTRG;
        m_controls.Player.Interact.performed += OnInteract;
        m_controls.Player.UseBandage.performed += OnUseBandage;
        m_controls.Player.UseMedkit.performed += OnUseMedkit;

        if (m_bLockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OnDisable()
    {
        m_controls.Player.Jump.performed -= OnJump;
        m_controls.Player.Crouch.performed -= OnCrouch;
        m_controls.Player.Aim.performed -= OnAimStarted;
        m_controls.Player.Aim.canceled -= OnAimCanceled;
        m_controls.Player.Fire.performed -= OnFireStarted;
        m_controls.Player.Fire.canceled -= OnFireCanceled;
        m_controls.Player.Reload.performed -= OnReload;
        m_controls.Player.SwitchToAK.performed -= OnSwitchToAK;
        m_controls.Player.SwitchToTRG.performed -= OnSwitchToTRG;
        m_controls.Player.Interact.performed -= OnInteract;
        m_controls.Player.UseBandage.performed -= OnUseBandage;
        m_controls.Player.UseMedkit.performed -= OnUseMedkit;

        m_controls.Player.Disable();

        if (m_bLockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnDestroy()
    {
        if (m_controls != null)
        {
            m_controls.Dispose();
            m_controls = null;
        }
    }

    private void Update()
    {
        m_tCurrentInput.vMove = m_controls.Player.Move.ReadValue<Vector2>();
        m_tCurrentInput.vLook = m_controls.Player.Look.ReadValue<Vector2>();
        m_tCurrentInput.bSprint = m_controls.Player.Sprint.IsPressed();
        m_tCurrentInput.bAim = m_bIsAiming;
        m_tCurrentInput.bFire = m_controls.Player.Fire.IsPressed();

        if (m_refPlayerSystem != null)
        {
            m_refPlayerSystem.SetMoveInput(m_tCurrentInput.vMove);
            m_refPlayerSystem.SetSprint(m_tCurrentInput.bSprint);
        }

        if (m_refCameraRig != null)
        {
            m_refCameraRig.SetLookInput(m_tCurrentInput.vLook);
        }
    }

    private void OnJump(InputAction.CallbackContext _ctx)
    {
        if (m_refPlayerSystem != null)
        {
            m_refPlayerSystem.Jump();
        }
    }

    private void OnCrouch(InputAction.CallbackContext _ctx)
    {
        if (m_refPlayerSystem != null)
        {
            m_refPlayerSystem.SetCrouch(true);
        }
    }

    private void OnAimStarted(InputAction.CallbackContext _ctx)
    {
        SetAim(true);
    }

    private void OnAimCanceled(InputAction.CallbackContext _ctx)
    {
        SetAim(false);
    }

    private void OnFireStarted(InputAction.CallbackContext _ctx)
    {
        if (m_refWeaponSystem != null)
        {
            m_refWeaponSystem.SetFireHeld(true);
        }
    }

    private void OnFireCanceled(InputAction.CallbackContext _ctx)
    {
        if (m_refWeaponSystem != null)
        {
            m_refWeaponSystem.SetFireHeld(false);
        }
    }

    private void OnReload(InputAction.CallbackContext _ctx)
    {
        if (m_refWeaponSystem != null)
        {
            m_refWeaponSystem.Reload();
        }
    }

    private void OnSwitchToAK(InputAction.CallbackContext _ctx)
    {
        SwitchSlot(WeaponSlot.AK);
    }

    private void OnSwitchToTRG(InputAction.CallbackContext _ctx)
    {
        SwitchSlot(WeaponSlot.TRG);
    }

    private void OnInteract(InputAction.CallbackContext _ctx)
    {
        if (m_refPlayerSystem != null)
        {
            m_refPlayerSystem.TryInteract();
        }
    }

    private void OnUseBandage(InputAction.CallbackContext _ctx)
    {
        if (m_refItemSystem != null)
        {
            m_refItemSystem.TryUseBandage();
        }
    }

    private void OnUseMedkit(InputAction.CallbackContext _ctx)
    {
        if (m_refItemSystem != null)
        {
            m_refItemSystem.TryUseMedkit();
        }
    }

    private void SetAim(bool _bIsAiming)
    {
        m_bIsAiming = _bIsAiming;

        if (m_refPlayerSystem != null)
        {
            m_refPlayerSystem.SetAiming(_bIsAiming);
        }

        if (m_refWeaponSystem != null)
        {
            m_refWeaponSystem.SetAiming(_bIsAiming);
        }

        if (m_refCameraRig != null && m_refWeaponSystem != null)
        {
            m_refCameraRig.SetAim(_bIsAiming, m_refWeaponSystem.ActiveSlot);
        }
    }

    private void SwitchSlot(WeaponSlot _eSlot)
    {
        if (m_refWeaponSystem == null)
        {
            return;
        }

        m_refWeaponSystem.SetActiveSlot(_eSlot);

        if (m_refCameraRig != null)
        {
            m_refCameraRig.SetAim(m_bIsAiming, m_refWeaponSystem.ActiveSlot);
        }
    }
}
