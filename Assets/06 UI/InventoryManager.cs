using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager m_Instance { get; private set; }

    [SerializeField] private Interface m_refInterface;
    private void Awake()
    {
        if (m_Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);
    }


    public bool AddItem(SOData _SOData)
    {
        return m_refInterface.AddData(_SOData);
    }
}
