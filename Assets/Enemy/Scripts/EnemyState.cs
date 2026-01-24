using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyState
{
    protected Enemy enemy;
    protected EnemyState(Enemy enemy)
    {
        this.enemy = enemy;
    }
    public virtual void Enter()
    {
        // Default implementation (can be empty)
    }
    public virtual void Update()
    {
        // Default implementation (can be empty)
    }
    public virtual void Exit()
    {
        // Default implementation (can be empty)
    }
}
