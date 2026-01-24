using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStateMachine
{
    public EnemyState currentState { get; private set; }

    public void Initialize(EnemyState newState)
    {
        currentState = newState;
        currentState?.Enter();
    }

    public void ChangeState(EnemyState newState)
    {
        if (newState == null){
            Debug.LogError("EnemyStateMachine: Attempted to change to a null state.");
            return;
        }
        if (currentState == newState)
            return;

        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }
}
