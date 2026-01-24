using UnityEngine;
using UnityEngine.Events;

public class PlayerFloorTagDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float rayDistance = 2f;        // Length of downward raycast
    [SerializeField] private LayerMask floorLayerMask = -1; // Layer(s) to detect as floor

    [Header("Events")]
    public FloorTagEvent OnFloorEntered;     // Triggered when entering a new floor tag
    public FloorTagEvent OnFloorExited;      // Triggered when leaving current floor tag

    [System.Serializable]
    public class FloorTagEvent : UnityEvent<string> { }

    private string currentFloorTag = "";

    private void Update()
    {
        if (rayOrigin == null) return;

        Ray ray = new Ray(rayOrigin.position, Vector3.down);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, floorLayerMask))
        {
            string newTag = hit.collider.tag;

            if (newTag != currentFloorTag)
            {
                if (!string.IsNullOrEmpty(currentFloorTag))
                {
                    OnFloorExited?.Invoke(currentFloorTag);
                }

                currentFloorTag = newTag;

                OnFloorEntered?.Invoke(newTag);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(currentFloorTag))
            {
                OnFloorExited?.Invoke(currentFloorTag);
                currentFloorTag = "";
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (rayOrigin != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(rayOrigin.position, Vector3.down * rayDistance);
        }
    }
#endif
}