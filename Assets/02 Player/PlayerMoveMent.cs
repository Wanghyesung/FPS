using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Player m_refPlayer;
    [SerializeField] private Rigidbody m_refRb;
    [SerializeField] private Transform m_refCameraPitchTr; // 카메라 피벗(예: CameraPivot3D) — pitch는 몸이 아니라 여기에만 적용한다

    [SerializeField] private float m_fMaxUP = 60.0f;
    [SerializeField] private float m_fMaxDown = -60.0f;

    [SerializeField] private float m_fSpeed = 10.0f;
    [SerializeField] private float m_fRotSpeed = 7.0f;

    private Vector2 m_vDelta;

    private float m_fPitch;
    private float m_fYaw;
    private float m_fVerticalVelocity;
    private Vector3 m_vMoveDir;

    private bool m_bLockMove = false;
    private bool m_bIsGrounded = true;

    private float m_fDecayMove = 0.0f;

    public float m_fGravity = -9.81f;
    public float m_fJumpHeight = 10.5f;
    public float m_fRollHeight = 3.5f;

    public void Init(Player _refOwner)
    {
        m_fDecayMove = 0.0f;
        m_refPlayer = _refOwner;
        m_refRb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        InputManager.m_Instance.OnSpacePressed += Jump;
    }

    private void OnDisable()
    {
        InputManager.m_Instance.OnSpacePressed -= Jump;
    }

    private void Update()
    {
        m_vDelta = InputManager.m_Instance.InputInfo.Delta;
        m_vMoveDir = InputManager.m_Instance.InputInfo.MoveDir;
        Look();
    }

 
    private void FixedUpdate()
    {
        ApplyYaw();
        Move();
    }

    private void Look()
    {
        m_fPitch -= m_vDelta.y * Time.deltaTime * m_fRotSpeed;
        m_fYaw += m_vDelta.x * Time.deltaTime * m_fRotSpeed;

        m_fPitch = Mathf.Clamp(m_fPitch, m_fMaxDown, m_fMaxUP);

        if (m_refCameraPitchTr != null)
            m_refCameraPitchTr.localRotation = Quaternion.Euler(m_fPitch, 0f, 0f);
    }

    private void ApplyYaw()
    {
        m_refRb.MoveRotation(Quaternion.Euler(0f, m_fYaw, 0f));
        m_refRb.angularVelocity = Vector3.zero;
    }

    private void Move()
    {
        if (m_bLockMove == true)
            return;

        Vector3 vMoveDir = m_vMoveDir;
        m_vMoveDir = transform.forward * vMoveDir.y + transform.right * vMoveDir.x;

        m_refPlayer.AnimationTable.SetFloat(eEntityState.Move, vMoveDir.magnitude);

        m_refPlayer.AnimationTable.SetFloat(eEntityState.MoveX, vMoveDir.x);
        m_refPlayer.AnimationTable.SetFloat(eEntityState.MoveZ, vMoveDir.y);

        if (m_bIsGrounded && m_fVerticalVelocity < 0f)
            m_fVerticalVelocity = -2f; // 접지 유지용 상시 하향 가속

        // 중력 적용
        m_fVerticalVelocity += m_fGravity * Time.fixedDeltaTime;
    
        Vector3 vHorizontal = m_vMoveDir * (m_fSpeed - m_fDecayMove);
        m_refRb.velocity = new Vector3(vHorizontal.x, m_fVerticalVelocity, vHorizontal.z);
    }

    private void Jump()
    {
       
        m_refPlayer.AnimationTable.SetBool(eEntityState.Jump, true);

        m_fDecayMove = m_fSpeed * 0.8f;
        m_fVerticalVelocity = Mathf.Sqrt(m_fJumpHeight * -2f * m_fGravity);
        m_bIsGrounded = false;
    }

    public void UnLock()
    {

        m_bLockMove = false;
        m_fDecayMove = 0.0f;
        m_fVerticalVelocity = 0.0f;

    }


    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            if (m_bIsGrounded == false)
            {
                m_refPlayer.AnimationTable.SetBool(eEntityState.Jump, false);
                m_bIsGrounded = true;
                m_bLockMove = true; //착지 모션이 끝날 때 까지 대기
            }
        }

    }

    
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            m_bIsGrounded = false;
    }
}
