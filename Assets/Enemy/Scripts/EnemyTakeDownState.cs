using UnityEngine;
using UnityEngine.AI;

public class EnemyTakeDownState : EnemyState
{
    public EnemyTakeDownState(Enemy enemy) : base(enemy) { }

    public override void Enter()
    {
        base.Enter();
        enemy.SetAnimTakeDown(true);
    }

    public override void Update()
    {
        // Default implementation (can be empty)
    }
    public override void Exit()
    {
        enemy.SetAnimTakeDown(false);
    }
}