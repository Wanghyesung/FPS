using System;
using System.Collections.Generic;
using UnityEngine;
using static Weapon;


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
    private List<Weapon> m_listWeapon = new();

    private AnimationTable m_refAnimTable;
    public AnimationTable AnimationTable => m_refAnimTable;

    private PlayerMovement m_refMovement;
    private WeaponRigTarget m_refWeaponRigTarget;

    private bool m_bOnFire;

    [SerializeField] private Transform m_refBodyTr;
    public Transform BodyTr => m_refBodyTr;

    static private Player GPlayer = null;
    static public Player CurrentPlayer => GPlayer;

    private void Awake()
    {
        m_refMovement = GetComponent<PlayerMovement>();
        m_refMovement.Init(this);

        m_refAnimTable = GetComponent<AnimationTable>();
        m_refWeaponRigTarget = GetComponent<WeaponRigTarget>();

        GPlayer = this;
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
   
    public void PickupWeapon(Weapon _refWeapon, SOEquipData _SOUIData)
    {
        if (_refWeapon == null)
            return;
        if (InventoryManager.m_Instance.AddItem(_SOUIData) == false)
            return;

        Transform tSocket = m_refWeaponSocket != null ? m_refWeaponSocket : transform;

        _refWeapon.transform.SetParent(tSocket, true);

        _refWeapon.transform.localPosition = Vector3.zero;
        _refWeapon.transform.localRotation = Quaternion.identity;
        _refWeapon.transform.localScale = Vector3.one;

        // 여기서 EquipWeapon(리그 바인딩)을 하면 안 된다 — 바로 아래에서 비활성화되므로
        // 지금 손에 든 무기의 IK 바인딩을 '비활성 트랜스폼'으로 덮어써 버린다.
        // 리그는 실제로 꺼내 드는 UseWeapon 시점에 건다.
        _refWeapon.gameObject.SetActive(false);
        m_listWeapon.Add(_refWeapon); //TODO : 중복으로 같은 웨폰이 들어오면 문제
    }


    // 리그(손 IK target / WeaponAimIK 대상)를 이 무기로 다시 묶는다.
    // RigBuilder.Build()는 호출 시점의 트랜스폼에 핸들을 바인딩하므로,
    // 무기를 바꿀 때마다 '활성화한 뒤에' 반드시 다시 호출해야 한다.
    private void EquipWeapon(Weapon _refWeapon)
    {
        _refWeapon.Init();
        m_refWeaponRigTarget.SetWeapon(
            _refWeapon.transform,
            _refWeapon.LeftHandGripTr,m_refWeaponRigTarget.LeftHint,
            _refWeapon.RightHandGripTr,m_refWeaponRigTarget.RightHint);
    }

    public void UseWeapon(eWeaponType _eWeaponType)
    {
        if(m_refWeapon != null && m_refWeapon.WeaponType == _eWeaponType)
            return;

        for (int i = 0; i < m_listWeapon.Count; ++i)
        {
            if (m_listWeapon[i].WeaponType == _eWeaponType)
            {
                
                if(m_refWeapon != null)
                    m_refWeapon.gameObject.SetActive(false);

                m_refWeapon = m_listWeapon[i];
                m_refWeapon.gameObject.SetActive(true);

                // 활성화 뒤에 리그를 다시 건다. 이게 없으면 IK/조준 콘스트레인트가
                // 직전에 바인딩된(그리고 지금은 비활성인) 무기를 계속 붙들고 있어
                // 손이 총을 못 잡고 줌을 해도 무기가 정렬되지 않는다.
                EquipWeapon(m_refWeapon);

                if (m_refScope != null)
                    m_refScope.SetWeapon(m_refWeapon);

                m_refAnimTable.SetBool(eEntityState.HasWeapon, true);
                return;
            }
        }
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
