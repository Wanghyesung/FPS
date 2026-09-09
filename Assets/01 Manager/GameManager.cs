using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    //나중에 Map을 만들기 위해서
    [SerializeField] private List<Enemy> m_listEnemy = new List<Enemy>();

    [SerializeField] private GameObject m_refEndImage;
    private int m_iRemainEnemy;

    private int m_iAttackAmount = 0;
    private int m_iKillCount = 0;
    [SerializeField] private TextMeshProUGUI m_refDamageText;
    [SerializeField] private TextMeshProUGUI m_refKillText;

    [SerializeField] private Image m_refPlayerHP;

    private void Awake()
    {
        m_iRemainEnemy = m_listEnemy.Count;    
    }

    private void OnEnable()
    {
        m_iKillCount = 0;
        m_iAttackAmount = 0;
        Enemy.OnEnemyDead += DeadEnemy;
        Enemy.OnDamaged += Damaged;

        Player.OnDamaged += PlayerDamaged;
    }

    private void OnDisable()
    {
        m_iKillCount = 0;
        m_iAttackAmount = 0;
        Enemy.OnEnemyDead -= DeadEnemy;
        Enemy.OnDamaged -= Damaged;

        Player.OnDamaged -= PlayerDamaged;
    }

    private void Damaged(int _iAmount)
    {
        m_iAttackAmount += _iAmount;
    }
    private void PlayerDamaged(float _fCurrentHP)
    {
        m_refPlayerHP.fillAmount = _fCurrentHP;
    }
    private void DeadEnemy()
    {
        --m_iRemainEnemy;
        ++m_iKillCount;
        if(m_iRemainEnemy == 0)
            EndGame().Forget();
    }

    private async UniTaskVoid EndGame()
    {
        await UniTask.WaitForSeconds(2.0f);

        Time.timeScale = 0.0f;
        m_refEndImage.gameObject.SetActive(true);

        m_refKillText.text = m_iKillCount.ToString();
        m_refDamageText.text = m_iAttackAmount.ToString();    
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
