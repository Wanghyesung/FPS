using UnityEngine;

[RequireComponent(typeof(WeaponRigTarget))]
public class Player : MonoBehaviour
{
    [SerializeField] private Aim m_refAim;
    [SerializeField] private Transform m_refWeaponSocket;
    [SerializeField] private WeaponAimRig m_refWeaponAimRig;

    [SerializeField] private Weapon m_refWeapon = null; // 기본적으로 null — WeaponPickup을 통해 주워야 값이 채워진다

    private AnimationTable m_refAnimTable;
    public AnimationTable AnimationTable => m_refAnimTable;

    private PlayerMovement m_refMovement;
    private WeaponRigTarget m_refWeaponRigTarget;

   

    private bool m_bWaitFire = false;
    private void Awake()
    {
        m_refMovement = GetComponent<PlayerMovement>();
        m_refMovement.Init(this);

        m_refAnimTable = GetComponent<AnimationTable>();
        m_refWeaponRigTarget = GetComponent<WeaponRigTarget>();

        // 씬에 미리 장착된 무기(WeaponPickup 트리거를 거치지 않은 시작 무기)도
        // 소켓 정렬 + Init + 왼손 IK 타겟 연결이 필요하다
        if (m_refWeapon != null)
        {
            TakeWeapon(m_refWeapon);
            EquipWeapon(m_refWeapon);
        }
    }


    private void Update()
    {
        if(m_refWeapon != null)
        {
            bool bRButn = InputManager.m_Instance.InputInfo.OnRButton;
            bool bLButton = InputManager.m_Instance.InputInfo.OnLButon;
            if (bRButn == true)
            {
                m_bWaitFire = true;
                m_refAnimTable.SetSpeed(0.0f);
            }
            else
            {
                m_bWaitFire = false;
                m_refAnimTable.SetSpeed(1.0f);
            }

            //GameCameraManager.m_Instance.SetZoomed(bRButn);
            if (m_refWeaponAimRig != null)
                m_refWeaponAimRig.SetZoomed(bRButn, m_refAim.TargetPosition);

            m_refWeapon.SetAimCorrection(m_refAim.TargetPosition);
            m_refAnimTable.SetBool(eEntityState.Fire, bRButn);

            if (bRButn && bLButton && m_refWeapon.CheckTime())
                Fire();
  
        }

    }


    // WeaponPickup이 트리거 접촉 시 호출 — 무기를 손 소켓으로 옮기고 초기화한다.
    public void PickupWeapon(Weapon _refWeapon)
    {
        if (_refWeapon == null)
            return;

        Transform tSocket = m_refWeaponSocket != null ? m_refWeaponSocket : transform;
        _refWeapon.transform.SetParent(tSocket, false);
        TakeWeapon(_refWeapon);

        EquipWeapon(_refWeapon);
    }

    // 무기의 RightHandGripTr이 소켓(오른손) 위치/회전에 정확히 겹치도록 무기 자체를 배치한다.
    // 소켓은 무기 종류와 무관한 고정값(대략 손 위 어딘가) 하나만 유지하고, 각 무기가 자기
    // 그립 포인트를 갖게 해서 — 무기마다 소켓을 따로 튜닝하지 않아도 어떤 무기든 손에 맞게 붙는다.
    private void TakeWeapon(Weapon _refWeapon)
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

    private void EquipWeapon(Weapon _refWeapon)
    {
        m_refWeapon = _refWeapon;
        m_refWeapon.Init();
        m_refWeaponRigTarget.SetWeapon(
            m_refWeapon.LeftHandGripTr,
            m_refWeaponRigTarget.LeftHint,
            m_refWeapon.RightHandGripTr,
            m_refWeaponRigTarget.RightHint);

        m_refAnimTable.SetBool(eEntityState.HasWeapon, true);
    }

    private void Fire()
    {
        m_refAnimTable.SetSpeed(1.0f);
        m_refWeapon.Fire(m_refAim.TargetPosition);
    }


}
