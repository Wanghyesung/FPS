using System;
using UnityEngine;


[Serializable]
public class ObjectInfo
{
    public eEntityState State;
    public float CurrentHP;
    public float Speed;
}

[RequireComponent(typeof(WeaponRigTarget))]
public class Player : MonoBehaviour
{
    [SerializeField] private Aim m_refAim;
    [SerializeField] private ScopeController m_refScope;
    [SerializeField] private Transform m_refWeaponSocket;

    [SerializeField] private Weapon m_refWeapon = null; 

    private AnimationTable m_refAnimTable;
    public AnimationTable AnimationTable => m_refAnimTable;

    private PlayerMovement m_refMovement;
    private WeaponRigTarget m_refWeaponRigTarget;

    private bool m_bOnFire;

    [SerializeField] private Transform m_refBodyTr;
    public Transform BodyTr => m_refBodyTr;

    private void Awake()
    {
        m_refMovement = GetComponent<PlayerMovement>();
        m_refMovement.Init(this);

        m_refAnimTable = GetComponent<AnimationTable>();
        m_refWeaponRigTarget = GetComponent<WeaponRigTarget>();
    }

    private void Start()
    {
        if (m_refWeapon != null)
            EquipWeapon(m_refWeapon);

        InputManager.m_Instance.OnRButtonPressed += Zoom;
        InputManager.m_Instance.OnRButtonRelease += UnZoom;

        InputManager.m_Instance.OnLButtonPressed += RequestFire;

    }
    
    // WeaponPickup이 트리거 접촉 시 호출 — 무기를 손 소켓으로 옮기고 초기화한다.
    public void PickupWeapon(Weapon _refWeapon)
    {
        if (_refWeapon == null)
            return;

        Transform tSocket = m_refWeaponSocket != null ? m_refWeaponSocket : transform;

        _refWeapon.transform.SetParent(tSocket, true);

        _refWeapon.transform.localPosition = Vector3.zero;
        _refWeapon.transform.localRotation = Quaternion.identity;
        _refWeapon.transform.localScale = Vector3.one;

        EquipWeapon(_refWeapon);
    }


    private void EquipWeapon(Weapon _refWeapon)
    {
        m_refWeapon = _refWeapon;
        m_refWeapon.Init();
        m_refWeaponRigTarget.SetWeapon(
            m_refWeapon.transform,
            m_refWeapon.LeftHandGripTr,m_refWeaponRigTarget.LeftHint,
            m_refWeapon.RightHandGripTr,m_refWeaponRigTarget.RightHint);

        m_refAnimTable.SetBool(eEntityState.HasWeapon, true);

        if (m_refScope != null)
            m_refScope.SetWeapon(m_refWeapon);
    }

    private void RequestFire()
    {
        if(m_refWeapon == null)
            return;

        if (m_bOnFire == true)
            m_refWeapon.RequestFire(m_refAim.TargetPosition);
    }

    // 카메라는 CameraPivot3D에 고정한 채 FOV만 좁힌다.
    // 만약 무기 조준 위치로 이동  시키고 싶다면 아이언사이트 모드에서만 GameCameraManager.SetAimPivot을 호출한다.
    private void Zoom()
    {
        if (m_refWeapon == null)
            return;

        m_bOnFire = true;
        m_refWeapon.Zoom(); //리깅

        if (m_refScope != null)
            m_refScope.Enter();
    }
    private void UnZoom()
    {
        m_bOnFire = false;

        if (m_refWeapon != null)
            m_refWeapon.UnZoom();

        if (m_refScope != null)
            m_refScope.Exit();
    }
}
