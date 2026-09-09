using System;
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

    // ObjInfo.State는 CheckMoveState가 매 프레임 Idle/Move로 덮어쓰므로 사망 판정에 쓸 수 없다
    private bool m_bIsDead;

    static public event Action OnEnemyDead;
    static public event Action<int> OnDamaged;

    private void Awake()
    {
        m_refAnimTable = GetComponent<AnimationTable>();
        m_refAgent = GetComponent<NavMeshAgent>();
        m_refRigBuilder = GetComponent<RigBuilder>();

        m_refBT = GetComponent<BehaviorTree>();

        m_refBT.BlackBoard.Owner = this;
        m_refBT.BlackBoard.Agent = m_refAgent;
        m_refBT.BlackBoard.ObjInfo = m_refObjInfo;
        m_refBT.BlackBoard.Aim = GetComponent<Aim>();

        // 씬에 하나뿐인 Player를 교전 타겟으로 캐싱 — 매 프레임이 아니라 Awake에서 한 번만
        Player refPlayer = FindObjectOfType<Player>();
        if (refPlayer != null)
        {
            m_refBT.BlackBoard.TargetTr = refPlayer.BodyTr;
            m_refBT.BlackBoard.TargetRoot = refPlayer.transform;
        }

        m_refBT.BlackBoard.POV = 140.0f;
        m_refObjInfo.State = eEntityState.Idle;
        m_refObjInfo.CurrentHP = 100.0f;
        m_refObjInfo.Speed = 4.0f;

        m_refAgent.speed = m_refObjInfo.Speed;
    }

    private void Start()
    {
        if (m_refRigBuilder != null)
            m_refRigBuilder.Build();

    }

    public void TakeDamage(AttackInfo _refAttackInfo, tShotInfo _tShotInfo)
    {
        if (_refAttackInfo == null || m_bIsDead == true)
            return;

        SetHitAlert(_tShotInfo);

        int iDamage = _refAttackInfo.Damage;
        m_refObjInfo.CurrentHP -= _refAttackInfo.Damage;
        OnDamaged?.Invoke(iDamage);
        
        if(m_refObjInfo.CurrentHP <= 0)
            Die();
    }

    // 투사체 진행 방향의 반대가 공격자 쪽이다.
    // MoveDir이 비어 있는 피해(방향 없는 데미지)는 돌아볼 곳이 없으므로 무시한다
    private void SetHitAlert(tShotInfo _tShotInfo)
    {
        Vector3 vFromDir = -_tShotInfo.MoveDir;
        vFromDir.y = 0.0f;

        if (vFromDir.sqrMagnitude < 0.0001f)
            return;

        BlackBoard refBB = m_refBT.BlackBoard;

        refBB.HitFromDir = vFromDir.normalized;
        refBB.HasPendingHit = true;
        refBB.AlertEndTime = 0.0f; // 연속 피격이면 주시 시간을 처음부터 다시 잡는다
    }

    private void Die()
    {
        if (m_bIsDead == true)
            return;

        m_bIsDead = true;
        m_refObjInfo.State = eEntityState.Dead;

        // 리그를 먼저 꺼야 Dead 클립이 온전히 재생된다
        if (m_refRigBuilder != null)
            m_refRigBuilder.enabled = false;

        m_refAnimTable.SetTrigger(eEntityState.Dead);

        m_refBT.StopBT();

        if (m_refAgent != null && m_refAgent.isOnNavMesh == true)
            m_refAgent.isStopped = true;
    }

    // Dead 클립 마지막 프레임의 Animation Event가 호출한다.
    // 이벤트는 메서드 이름을 문자열로 들고 있으므로, 이름을 바꾸면 클립의 이벤트도 같이 고쳐야 한다.
    public void OnDeadAnimationEnd()
    {
        if (m_bIsDead == false)
            return;

        OnEnemyDead?.Invoke();
        // 나중에 Enemy를 풀링하게 되면 여기서 ObjectPoolManager.PushObject로 바꾼다
        gameObject.SetActive(false);

    }

    private void Update()
    {
        // 사망 후에도 CheckMoveState가 돌면 State를 Idle로 되돌려 Dead 상태가 지워진다
        if (m_bIsDead == true)
            return;

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
    }

}
