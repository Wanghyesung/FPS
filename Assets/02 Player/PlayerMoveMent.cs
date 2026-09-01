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

    // 물리(Rigidbody)에 관여하는 것은 전부 FixedUpdate에서 처리한다 — Update에서
    // transform.rotation이나 Rigidbody.velocity를 직접 건드리면 물리 스텝과 주기가
    // 어긋나서(프레임레이트 의존) 떨림/끊김이 생긴다.
    private void FixedUpdate()
    {
        ApplyYaw();
        Move();
    }

    // 마우스 델타 → pitch/yaw 누적과 카메라 pitch 반영은 매 프레임(Update)에서 해야
    // 시야 회전이 뚝뚝 끊기지 않는다. yaw는 여기서 값만 누적하고, 실제 몸 회전 적용은
    // FixedUpdate의 ApplyYaw()가 담당한다.
    private void Look()
    {
        m_fPitch -= m_vDelta.y * Time.deltaTime * m_fRotSpeed;
        m_fYaw += m_vDelta.x * Time.deltaTime * m_fRotSpeed;

        m_fPitch = Mathf.Clamp(m_fPitch, m_fMaxDown, m_fMaxUP);

        if (m_refCameraPitchTr != null)
            m_refCameraPitchTr.localRotation = Quaternion.Euler(m_fPitch, 0f, 0f);
    }

    // 몸(Player 루트)은 좌우로만 돈다 — 여기에 pitch까지 넣으면 이 트랜스폼의 자식인
    // 캐릭터 전체(Visual/스켈레톤)가 시선 위아래를 따라 통째로 숙여진다.
    // Rigidbody가 붙은 transform은 직접 rotation을 대입하지 않고 MoveRotation으로
    // 돌려야 물리 스텝과 충돌 처리가 안정적으로 맞물린다.
    // MoveRotation은 논-키네마틱 Rigidbody에서 목표 회전에 도달하기 위한 각속도를
    // 내부적으로 계산해서 남기는데, 회전을 100% 스크립트로만 제어할 거라면 이 각속도가
    // 다음 스텝에도 관성으로 남아 카메라가 미세하게 계속 흔들리는 원인이 된다(실측:
    // 마우스를 안 움직이는데도 angularVelocity.y가 0으로 안 떨어지고 남아있었음).
    // 매 스텝 명시적으로 0으로 지워서 순수하게 스크립트가 정한 값만 반영되게 한다.
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
        //if(m_vMoveDir.y > 0.1f || m_vMoveDir.x < 0.1f)
        //    RollFoward();
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

    private void RollFoward()
    {
        m_fVerticalVelocity += Mathf.Sqrt(m_fRollHeight * -2f * m_fGravity);
        m_bIsGrounded = false;
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
                Debug.Log("착지");
            }
        }

    }

    // CharacterController.isGrounded는 매 프레임 자동으로 갱신되지만, Rigidbody는
    // 그런 게 없다 — OnCollisionEnter로 착지만 잡으면 점프 없이 낭떠러지를 걸어서
    // 벗어날 때 m_bIsGrounded가 true로 고정된 채 남아 중력이 다시 안 붙는다.
    // Ground 콜라이더에서 벗어나는 순간을 여기서 풀어줘야 한다.
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
            m_bIsGrounded = false;
    }
}
