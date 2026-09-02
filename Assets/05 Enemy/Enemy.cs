using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


[RequireComponent(typeof(WeaponRigTarget))]

public class Enemy : MonoBehaviour, IDamageable
{
    private BehaviorTree m_refBT;
    [SerializeField] private ObjectInfo m_refObjInfo = new();

    private AnimationTable m_refAnimTable;
    private NavMeshAgent m_refAgent;

    private void Awake()
    {
        m_refAnimTable = GetComponent<AnimationTable>();
        m_refAgent = GetComponent<NavMeshAgent>();

        m_refBT = GetComponent<BehaviorTree>();

        m_refBT.BlackBoard.Agent = m_refAgent;
        m_refBT.BlackBoard.ObjInfo = m_refObjInfo;
        m_refObjInfo.State = eEntityState.Idle;
        m_refObjInfo.CurrentHP = 100.0f;
        m_refObjInfo.Speed = 4.0f;


        m_refAgent.speed = m_refObjInfo.Speed;
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

    private void CheckMoveState()
    {
        Vector3 vDir = m_refAgent.velocity.normalized;
        m_refAnimTable.SetFloat(eEntityState.MoveX, vDir.x);
        m_refAnimTable.SetFloat(eEntityState.MoveZ, vDir.y);
    }
}
