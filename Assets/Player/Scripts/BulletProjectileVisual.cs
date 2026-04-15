using UnityEngine;

[DisallowMultipleComponent]
public class BulletProjectileVisual : MonoBehaviour
{
    [Header("Visual Travel")]
    [SerializeField] private float speed = 120f;
    [SerializeField] private float destroyAfterSeconds = 2f;
    [SerializeField] private bool faceTravelDirection = true;

    private Vector3 _targetPoint;
    private Vector3 _dir;
    private bool _inited;
    private float _lifeTimer;

    public void Init(Vector3 origin, Vector3 targetPoint, float projectileSpeed)
    {
        transform.position = origin;
        _targetPoint = targetPoint;

        Vector3 delta = targetPoint - origin;
        _dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.forward;

        speed = Mathf.Max(0.01f, projectileSpeed);
        _lifeTimer = 0f;
        _inited = true;

        if (faceTravelDirection)
            transform.rotation = Quaternion.LookRotation(_dir, Vector3.up);
    }

    private void Update()
    {
        if (!_inited) return;

        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= destroyAfterSeconds)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 toTarget = _targetPoint - transform.position;
        float remaining = toTarget.magnitude;
        float step = speed * Time.deltaTime;

        if (remaining <= 0.001f || remaining <= step)
        {
            transform.position = _targetPoint;
            Destroy(gameObject);
            return;
        }

        transform.position += _dir * step;
    }
}