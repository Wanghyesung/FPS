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
    }

    private void RequestFire()
    {
        if(m_refWeapon == null)
            return;

        if (m_bOnFire == true)
            m_refWeapon.RequestFire(m_refAim.TargetPosition);
    }

    private void Zoom()
    {
        m_bOnFire = true;
        m_refWeapon.Zoom();

        //내가 바라보는 시점이 아니라, 무기에서 바라보는 시점으로 카메라 피벗을 바꾼다.
        m_refAim.ChangePivot(m_refWeapon.ZoomTr);
        GameCameraManager.m_Instance.ThirdPersonPivot = m_refWeapon.ZoomTr;
    }
    private void UnZoom()
    {
        m_bOnFire = false;
        m_refWeapon.UnZoom();

        m_refAim.ChangePivot(null);
        GameCameraManager.m_Instance.ThirdPersonPivot = null;
    }
}
