using UnityEngine;
using UnityEngine.AI;

public class EnemyAnimatorDriver : MonoBehaviour
{
    public Enemy enemy;
    public NavMeshAgent agent;

    [Tooltip("Multiply agent velocity before feeding animator Speed.")]
    public float speedMultiplier = 1f;

    [Tooltip("Minimum speed to consider 'moving' (prevents tiny jitter).")]
    public float moveEpsilon = 0.02f;

    void Awake()
    {
        if (!enemy) enemy = GetComponent<Enemy>();
        if (!agent) agent = GetComponent<NavMeshAgent>();

        // Safety: auto-find animator if missing
        if (enemy && !enemy.animator) enemy.animator = GetComponentInChildren<Animator>(true);
    }

    void Update()
    {
        if (!enemy || !enemy.animator || !agent) return;

        float speed = agent.velocity.magnitude * speedMultiplier;
        if (speed < moveEpsilon) speed = 0f;

        enemy.SetAnimSpeed(speed);
    }
}
