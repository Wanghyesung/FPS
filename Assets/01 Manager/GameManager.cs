using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    //나중에 Map을 만들기 위해서
    [SerializeField] private List<Enemy> m_listEnemy = new List<Enemy>();

    private int m_iRemainEnemy;
    
    private void Awake()
    {
        m_iRemainEnemy = m_listEnemy.Count;    
    }

    private void OnEnable()
    {
        Enemy.OnEnemyDead += DeadEnemy;
    }

    private void OnDisable()
    {
        Enemy.OnEnemyDead -= DeadEnemy;
    }


    private void DeadEnemy()
    {
        --m_iRemainEnemy;
        if(m_iRemainEnemy <=0)
        {

        }
    }


}
