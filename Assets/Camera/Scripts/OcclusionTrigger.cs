using UnityEngine;

[DisallowMultipleComponent]
public class OcclusionTriggerReporter : MonoBehaviour
{
    [Tooltip("Only triggers on these layers will count as occlusion zones.")]
    public LayerMask triggerMask;

    [Tooltip("If true, logs enter/exit for debugging.")]
    public bool log = false;

    // How many occlusion triggers we are currently inside
    int _insideCount = 0;

    public bool IsOccluded => _insideCount > 0;

    void OnTriggerEnter(Collider other)
    {
        if (!other.isTrigger) return;
        if (((1 << other.gameObject.layer) & triggerMask.value) == 0) return;

        _insideCount++;
        if (log) Debug.Log($"[OcclusionTriggerReporter] Enter {other.name} count={_insideCount}", this);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.isTrigger) return;
        if (((1 << other.gameObject.layer) & triggerMask.value) == 0) return;

        _insideCount = Mathf.Max(0, _insideCount - 1);
        if (log) Debug.Log($"[OcclusionTriggerReporter] Exit {other.name} count={_insideCount}", this);
    }
}
