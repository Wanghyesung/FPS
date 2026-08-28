using UnityEngine;

/*///////////////////////////////////////////
                GameCameraManager
목적 : 메인 카메라를 관리하는 싱글톤. 카메라는 Player의 자식이 아니다 —
       CharacterController 이동 + PlayerMovement의 회전과 같은 프레임에서
       부모-자식으로 얽히면 업데이트 순서 차이로 흔들림(jitter)이 생기기
       때문에, LateUpdate에서 카메라를 목표 피벗(1인칭/3인칭)의 월드 위치로
       그냥 따라가게(Lerp/Slerp)만 한다.
       줌 여부는 이 클래스가 직접 입력을 읽지 않는다 — Player가 입력을 읽고
       SetZoomed()를 호출해서 알려준다.
       발사 반동(Shake)도 같은 LateUpdate 한 곳에서만 카메라 회전에 얹는다
       (여러 스크립트가 각자 다른 타이밍에 카메라를 건드리면 그 자체로 흔들림의
       원인이 되므로, 카메라를 만지는 로직은 이 클래스 하나로 모아둔다).
 *///////////////////////////////////////////

public sealed class GameCameraManager : MonoBehaviour
{
    public static GameCameraManager m_Instance { get; private set; }

    [SerializeField] private Transform m_refCamera;
    [SerializeField] private Transform m_refFirstPersonPivot;
    [SerializeField] private Transform m_refThirdPersonPivot;
    public Transform ThirdPersonPivot { get { return m_refThirdPersonPivot; } set { m_refThirdPersonPivot = value; } }

    [SerializeField] private float m_fBlendSpeed = 10f;
    [SerializeField] private float m_fRecoverySpeed = 6f; // 초당 반동 회복 비율

    private bool m_bZoomed;
    private Vector3 m_vRecoilOffset;

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

    // Player.Update()가 우클릭 여부를 판단해서 매 프레임 호출한다 — 여기서는 입력을 읽지 않는다.
    public void SetZoomed(bool _bZoomed)
    {
        m_bZoomed = _bZoomed;
    }

    // Weapon.OnBulletFired()가 발사마다 호출한다. _fAmount는 무기별 반동 각도(도, SOAttackInfo.RecoilAmount)
    public void Shake(float _fAmount)
    {
        if (_fAmount <= 0f)
            return;

        if (m_bZoomed == true)
            _fAmount /= 4.0f;

        m_vRecoilOffset += new Vector3(
            -Mathf.Abs(_fAmount) * Random.Range(0.7f, 1f), // 반동은 항상 위쪽(부호 고정)
            Random.Range(-_fAmount, _fAmount) * 0.5f,
            0f);
    }

    
    private void LateUpdate()
    {
        if (m_refCamera == null || m_refFirstPersonPivot == null)
            return;

        Transform refTarget = m_bZoomed ? m_refThirdPersonPivot : m_refFirstPersonPivot;
        float fT = Time.deltaTime * m_fBlendSpeed;

        m_refCamera.position = Vector3.Lerp(m_refCamera.position, refTarget.position, fT);
        Quaternion qFollow = Quaternion.Slerp(m_refCamera.rotation, refTarget.rotation, fT);

        m_vRecoilOffset = Vector3.Lerp(m_vRecoilOffset, Vector3.zero, Time.deltaTime * m_fRecoverySpeed);
        m_refCamera.rotation = qFollow * Quaternion.Euler(m_vRecoilOffset);
    }

    public Transform GetCameraTranform()
    {
        Transform refTarget = m_bZoomed ? m_refThirdPersonPivot : m_refFirstPersonPivot;
        return refTarget;
    }
}
