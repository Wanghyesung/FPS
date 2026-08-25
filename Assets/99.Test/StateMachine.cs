using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine : MonoBehaviour
{
    private IState m_ICurState;
    public void ChangeState(IState _IState)
    {
        m_ICurState.Exit();
        m_ICurState = _IState;
        m_ICurState.Enter();
    }
    public void Update()
    {
        m_ICurState?.Tick();
    }
}
