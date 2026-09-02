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

        m_refBT.BlackBoard.Owner = this;
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

    // NavMeshAgent가 updateRotation으로 항상 이동 방향을 바라보므로(스트레이프 없음)
    // 로컬 좌/우 성분은 의미가 없다 — 속도 크기 하나만 0~1로 정규화해서 넘긴다
    private void CheckMoveState()
    {
        float fSpeed01 = m_refAgent.speed > 0f ? m_refAgent.velocity.magnitude / m_refAgent.speed : 0f;
        m_refAnimTable.SetFloat(eEntityState.Move, fSpeed01);
    }
}
