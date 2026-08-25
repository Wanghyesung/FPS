using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IState
{
    public void Enter() { } //: 상태에 들어올 때 1번 실행
    public void Tick() { }//   : 상태에 머무는 동안, 매 프레임 실행
    public void Exit() { }//   : 상태를 떠날 때 1번 실행
}



public interface ICommand
{
    public void Execute();
    public void Undo();
}

public class MoveCommand : ICommand
{
    private readonly Transform _transform;
    private Vector3 vDir;
    private float speed;
    public MoveCommand(Transform tr, Vector3 _vDir, float _fSpeed)
    {
        _transform = tr;
        vDir = _vDir;
        speed = _fSpeed;
    }

    public void Execute()
    {
        _transform.position += vDir * Time.deltaTime * speed;
    }

    public void Undo()
    {
        _transform.position -= vDir * Time.deltaTime * speed;
    }
}

public class CommandInvoker
{
    private Stack<ICommand> m_sCom = new();

    public void ExeCute(ICommand _com)
    {
        _com.Execute();
        m_sCom.Push(_com);
    }
    public void UndoLast()
    {
        var com = m_sCom.Peek();
        if (com != null)
            com.Undo();

        m_sCom.Pop();
    }
}