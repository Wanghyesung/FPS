using UnityEngine;

[RequireComponent(typeof(WeaponIK))]
public class Player : MonoBehaviour
{
    [SerializeField] private Aim m_refAim;
    [SerializeField] private Transform m_refWeaponSocket;

    [SerializeField] private Weapon m_refWeapon = null; // 기본적으로 null — WeaponPickup을 통해 주워야 값이 채워진다

    private AnimationTable m_refAnimTable;
    public AnimationTable AnimationTable => m_refAnimTable;

    private PlayerMovement m_refMovement;
    private WeaponIK m_refWeaponIK;

    private void Awake()
    {
        m_refMovement = GetComponent<PlayerMovement>();
        m_refMovement.Init(this);

        m_refAnimTable = GetComponent<AnimationTable>();
        m_refWeaponIK = GetComponent<WeaponIK>();

        // 씬에 미리 장착된 무기(WeaponPickup 트리거를 거치지 않은 시작 무기)도
        // 소켓 정렬 + Init + 왼손 IK 타겟 연결이 필요하다
        if (m_refWeapon != null)
        {
            AlignWeaponToSocket(m_refWeapon);
            EquipWeapon(m_refWeapon);
        }
    }


    private void Update()
    {
        if(m_refWeapon != null)
        {
            bool bLButn = InputManager.m_Instance.InputInfo.OnRButton;
            m_refAnimTable.SetBool(eEntityState.Shot, true);

            GameCameraManager.m_Instance.SetZoomed(bLButn);
        }

        if (m_refWeapon != null && InputManager.m_Instance.InputInfo.OnLButon && m_refWeapon.CheckTime())
        {
            Fire();
        }
    }


    private void FixedUpdate()
    {

    }

    // WeaponPickup이 트리거 접촉 시 호출 — 무기를 손 소켓으로 옮기고 초기화한다.
    public void PickupWeapon(Weapon _refWeapon)
    {
        if (_refWeapon == null)
            return;

        Transform tSocket = m_refWeaponSocket != null ? m_refWeaponSocket : transform;
        _refWeapon.transform.SetParent(tSocket, false);
        AlignWeaponToSocket(_refWeapon);

        EquipWeapon(_refWeapon);

        GameCameraManager.m_Instance.ThirdPersonPivot = _refWeapon.ZoomTr;
    }

    // 무기의 RightHandGripTr이 소켓(오른손) 위치/회전에 정확히 겹치도록 무기 자체를 배치한다.
    // 소켓은 무기 종류와 무관한 고정값(대략 손 위 어딘가) 하나만 유지하고, 각 무기가 자기
    // 그립 포인트를 갖게 해서 — 무기마다 소켓을 따로 튜닝하지 않아도 어떤 무기든 손에 맞게 붙는다.
    private void AlignWeaponToSocket(Weapon _refWeapon)
    {
        Transform refWeapon = _refWeapon.transform;
        Transform refGrip = _refWeapon.RightHandGripTr;
        Transform refSocket = m_refWeaponSocket != null ? m_refWeaponSocket : transform;

        if (refGrip == null)
        {
            refWeapon.localPosition = Vector3.zero;
            refWeapon.localRotation = Quaternion.identity;
            return;
        }

        refWeapon.rotation = refSocket.rotation * Quaternion.Inverse(refGrip.localRotation);
        refWeapon.position += refSocket.position - refGrip.position;
    }

    // 오른손 부착(리지드 페어런팅)만으로는 왼손이 무기를 못 잡으므로,
    // Init 호출과 함께 왼손 IK 그립 타겟을 WeaponIK에 연결한다.
    private void EquipWeapon(Weapon _refWeapon)
    {
        m_refWeapon = _refWeapon;
        m_refWeapon.Init();
        m_refWeaponIK.SetLeftHandGrip(m_refWeapon.LeftHandGripTr);
    }

    private void Fire()
    {
        m_refWeapon.Fire(m_refAim.TargetPosition);
        m_refAnimTable.SetTrigger(eEntityState.Shot);
    }

}
