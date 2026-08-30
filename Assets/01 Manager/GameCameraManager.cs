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
       발사 반동(Shake)은 카메라 자체를 건드리지 않는다 — PlayerMovement의 실제
       pitch/yaw에 직접 얹어서, 플레이어가 마우스로 눌러야만 상쇄되는 스프레이
       컨트롤로 동작한다(Shake 참고). 이 클래스는 그 결과 피벗을 그대로 따라갈 뿐이다.
 *///////////////////////////////////////////

public sealed class GameCameraManager : MonoBehaviour
{
    public static GameCameraManager m_Instance { get; private set; }

    [SerializeField] private Transform m_refCamera;
    [SerializeField] private Transform m_refFirstPersonPivot;
    [SerializeField] private PlayerMovement m_refPlayerMovement; // 반동을 실제 조준 pitch/yaw에 직접 얹기 위한 참조 — Shake() 참고

    [SerializeField] private float m_fBlendSpeed = 10f;

    // 줌(ADS) 중 눈 위치에서 앞으로 살짝 당기는 거리 — 예전엔 무기의 ZoomTr(총의 조준경 지점)
    // 위치로 카메라를 스냅시켰는데, Spine 조준 리그가 팔 전체를 조준 방향으로 크게 돌리게 되면서
    // 그 지점이 더 이상 눈 근처에 고정되어 있지 않게 됐다(어깨를 크게 돌리면 손이 멀리 휩쓸리듯,
    // 총도 조준 각도가 클수록 원래 자리에서 멀리 벗어남) — 그 지점을 카메라가 쫓아가면 조준 각도가
    // 커질 때마다 카메라가 엉뚱한 곳으로 튄다. 그래서 카메라 위치는 항상 팔 회전과 무관한 고정
    // 기준점(FirstPersonPivot)에서만 재고, 줌 느낌은 단순히 그 기준점에서 바라보는 방향으로 살짝
    // 당기는 것만으로 낸다.
    [SerializeField] private float m_fZoomDollyDistance = 0.15f;

    private bool m_bZoomed;

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

    // Weapon.OnBulletFired()가 발사마다 호출한다. _fAmount는 무기별 반동 각도(도, SOAttackInfo.RecoilAmount).
    // 반동 자체는 더 이상 카메라 쪽 오프셋이 아니라 PlayerMovement의 실제 pitch/yaw에 얹는다 —
    // 그래야 마우스로 직접 눌러서 상쇄하는 스프레이 컨트롤이 되고, 카메라 쪽 별도 상태가 없어져
    // 이전에 있었던 반동 누적(되먹임) 버그도 구조적으로 사라진다.
    public void Shake(float _fAmount)
    {
        if (_fAmount <= 0f)
            return;

        if (m_bZoomed == true)
            _fAmount /= 4.0f;

        if (m_refPlayerMovement != null)
            m_refPlayerMovement.AddRecoil(_fAmount);
    }


    private void LateUpdate()
    {
        if (m_refCamera == null || m_refFirstPersonPivot == null)
            return;

        // 위치/회전 모두 항상 FirstPersonPivot(마우스 pitch/yaw) 기준이다. 줌 중엔 그 지점에서
        // 바라보는 방향으로 살짝 당기기만 한다 — 팔/무기가 어디로 돌든 카메라는 항상 눈 근처에
        // 안정적으로 남는다.
        Vector3 vPosTarget = m_refFirstPersonPivot.position;
           
        float fT = Time.deltaTime * m_fBlendSpeed;

        m_refCamera.position = Vector3.Lerp(m_refCamera.position, vPosTarget, fT);
        m_refCamera.rotation = Quaternion.Slerp(m_refCamera.rotation, m_refFirstPersonPivot.rotation, fT);
    }

    // 실제 카메라 회전은 항상 FirstPersonPivot 기준이므로(위 LateUpdate 참고), 카메라 상대
    // 이동(PlayerMovement.Move())도 피벗이 아니라 실제 카메라 트랜스폼을 그대로 써야 줌 중
    // 이동 방향이 화면과 어긋나지 않는다.
    public Transform GetCameraTranform()
    {
        return m_refCamera;
    }
}
