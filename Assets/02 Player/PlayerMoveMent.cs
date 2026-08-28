using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Player m_refPlayer;
    [SerializeField] private CharacterController m_refCharCon;

    [SerializeField] private float m_fMaxUP = 60.0f;
    [SerializeField] private float m_fMaxDown = -60.0f;

    [SerializeField] private float m_fSpeed = 10.0f;
    [SerializeField] private float m_fRotSpeed = 7.0f;

    private Vector2 m_vDelta;
   
    private float m_fPitch;
    private float m_fYaw;
    private Vector3 m_vVelocity;
    private bool m_bIsGrounded = true;

    public float m_fGravity = -9.81f;
    public float m_fJumpHeight = 1.5f;

    public void Init(Player _refOwner)
    {
        m_refPlayer = _refOwner;
        m_refCharCon = GetComponent<CharacterController>();
    }
    private void Update()
    {
        m_vDelta = InputManager.m_Instance.InputInfo.Delta;
        Rotate();
        Move();
    }


    private void Rotate()
    {
        // Update pitch/yaw using mouse delta and clamp pitch to avoid Euler wrap issues
        m_fPitch -= m_vDelta.y * Time.deltaTime * m_fRotSpeed;
        m_fYaw += m_vDelta.x * Time.deltaTime * m_fRotSpeed;

        m_fPitch = Mathf.Clamp(m_fPitch, m_fMaxDown, m_fMaxUP);

        transform.rotation = Quaternion.Euler(m_fPitch, m_fYaw, 0f);
      
    }

    private void Move()
    {
        Vector2 vMoveDir = InputManager.m_Instance.InputInfo.MoveDir;
        var refCamTr  = GameCameraManager.m_Instance.GetCameraTranform();

        Vector3 vMove = refCamTr.forward * vMoveDir.y + refCamTr.right * vMoveDir.x;

        m_refPlayer.AnimationTable.SetFloat(eEntityState.Move, vMoveDir.magnitude);
        m_refCharCon.Move(vMove * m_fSpeed * Time.deltaTime);

        m_bIsGrounded = m_refCharCon.isGrounded;
        if (m_bIsGrounded && m_vVelocity.y < 0f)
            m_vVelocity.y = -2f; // 접지 유지용 상시 하향 가속

        // 점프 처리
        if (InputManager.m_Instance.InputInfo.OnSpace && m_bIsGrounded)
        {
            m_vVelocity.y = Mathf.Sqrt(m_fJumpHeight * -2f * m_fGravity);
            m_bIsGrounded = false;
        }

        // 중력 적용
        m_vVelocity.y += m_fGravity * Time.deltaTime;
        m_refCharCon.Move(m_vVelocity * Time.deltaTime);
    }

}
