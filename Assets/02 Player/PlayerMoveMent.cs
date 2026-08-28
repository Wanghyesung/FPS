using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Player m_refPlayer;
    [SerializeField] private CharacterController m_refCharCon;
    [SerializeField] private Transform m_refCameraPitchTr; // 카메라 피벗(예: CameraPivot3D) — pitch는 몸이 아니라 여기에만 적용한다

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

        // 몸(Player 루트)은 좌우로만 돈다 — 여기에 pitch까지 넣으면 이 트랜스폼의 자식인
        // 캐릭터 전체(Visual/스켈레톤)가 시선 위아래를 따라 통째로 숙여진다.
        transform.rotation = Quaternion.Euler(0f, m_fYaw, 0f);

        if (m_refCameraPitchTr != null)
            m_refCameraPitchTr.localRotation = Quaternion.Euler(m_fPitch, 0f, 0f);
    }

    // GameCameraManager.Shake()가 발사마다 호출 — 반동을 실제 조준 pitch/yaw에 직접 얹는다.
    // 자동 회복이 없음: 플레이어가 마우스를 반대로 움직여 m_fPitch/m_fYaw를 되돌려야만 상쇄된다(스프레이 컨트롤).
    public void AddRecoil(float _fAmount)
    {
        float fPitchKick = Mathf.Abs(_fAmount) * Random.Range(0.7f, 1f); // 반동은 항상 위쪽(부호 고정)
        float fYawKick = Random.Range(-_fAmount, _fAmount) * 0.5f;

        m_fPitch = Mathf.Clamp(m_fPitch - fPitchKick, m_fMaxDown, m_fMaxUP);
        m_fYaw += fYawKick;
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
