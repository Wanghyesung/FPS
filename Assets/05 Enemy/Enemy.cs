using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;


[RequireComponent(typeof(WeaponRigTarget))]

public class Enemy : MonoBehaviour, IDamageable
{
    private BehaviorTree m_refBT;
    [SerializeField] private ObjectInfo m_refObjInfo = new();

    private AnimationTable m_refAnimTable;
    private NavMeshAgent m_refAgent;
    private RigBuilder m_refRigBuilder;

    [SerializeField] private Transform m_refWeaponSocket;
    [SerializeField] private WeaponRigTarget m_refWeaponRigTarget;
    private Weapon m_refWeapon = null;

    private void Awake()
    {
        m_refAnimTable = GetComponent<AnimationTable>();
        m_refAgent = GetComponent<NavMeshAgent>();
        m_refRigBuilder = GetComponent<RigBuilder>();

        m_refBT = GetComponent<BehaviorTree>();

        m_refBT.BlackBoard.Owner = this;
        m_refBT.BlackBoard.Agent = m_refAgent;
        m_refBT.BlackBoard.ObjInfo = m_refObjInfo;

        // 씬에 하나뿐인 Player를 교전 타겟으로 캐싱 — 매 프레임이 아니라 Awake에서 한 번만
        Player refPlayer = FindObjectOfType<Player>();
        if (refPlayer != null)
            m_refBT.BlackBoard.TargetTr = refPlayer.transform;

        m_refBT.BlackBoard.POV = 80.0f;

        m_refObjInfo.State = eEntityState.Idle;
        m_refObjInfo.CurrentHP = 100.0f;
        m_refObjInfo.Speed = 4.0f;

        m_refAgent.speed = m_refObjInfo.Speed;
    }

    // RigBuilder는 자기 Awake에서 한 번 자동으로 Build()를 도는데, 이 시점은 Animator가
    // Humanoid PlayableGraph를 아직 다 짜기 전이라 Start에서 한 번 더 Build해 바로잡는다.
    private void Start()
    {
        if (m_refRigBuilder != null)
            m_refRigBuilder.Build();
    }

    public void TakeDamage(AttackInfo _refAttackInfo, tShotInfo _tShotInfo)
    {
        
    }

    private void Update()
    {
        //어떠한 기능, 목적을 수행
        m_refBT.Evaluate();

        CheckMoveState();
    }

    //기본적인 State값을 전달하기 위해서
    private void CheckMoveState()
    {
        float fSpeed01 = m_refAgent.speed > 0f ? m_refAgent.velocity.magnitude / m_refAgent.speed : 0f;
        if(fSpeed01 < 0.01f)
            m_refObjInfo.State = eEntityState.Idle;
        else
        {
            Vector3 vWorldDir = m_refAgent.velocity;

            Vector3 vForward = transform.forward.normalized;
            Vector3 vRight = Vector3.Cross(Vector3.up, vForward).normalized;

            //월드의 방향과 내 방향을 정사영시켜서 얼마나 비슷한 각으로 보는지
            float fLocalX = Vector3.Dot(vWorldDir, vRight);
            float fLocalZ = Vector3.Dot(vWorldDir, vForward);
            m_refAnimTable.SetFloat(eEntityState.MoveX, fLocalX);
            m_refAnimTable.SetFloat(eEntityState.MoveZ, fLocalZ);
        }

        m_refAnimTable.SetFloat(eEntityState.Move, fSpeed01);
    }


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
            m_refWeapon.LeftHandGripTr, m_refWeaponRigTarget.LeftHint,
            m_refWeapon.RightHandGripTr, m_refWeaponRigTarget.RightHint);

        m_refBT.BlackBoard.Weapon = m_refWeapon;
        m_refAnimTable.SetBool(eEntityState.HasWeapon, true);
    }

}
