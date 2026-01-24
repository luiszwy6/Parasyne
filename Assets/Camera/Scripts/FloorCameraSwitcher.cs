using UnityEngine;
using Unity.Cinemachine;

public class FloorCameraSwitcher : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private CinemachineCamera targetCamera;  // The camera to push/pop for these floors

    [Header("Priority Settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;
    [SerializeField] private PlayerFloorTagDetector playerDetector;

    private void OnEnable()
    {
        var detector = FindObjectOfType<PlayerFloorTagDetector>();
        if (detector != null)
        {
            detector.OnFloorEntered.AddListener(OnFloorEntered);
            detector.OnFloorExited.AddListener(OnFloorExited);
        }
    }

    private void OnDisable()
    {
        var detector = FindObjectOfType<PlayerFloorTagDetector>();
        if (detector != null)
        {
            detector.OnFloorEntered.RemoveListener(OnFloorEntered);
            detector.OnFloorExited.RemoveListener(OnFloorExited);
        }
    }

    private void OnFloorEntered(string floorTag)
    {
        if (targetCamera == null) return;

        // Check if any child floor has this tag
        foreach (Transform child in transform)
        {
            if (child.CompareTag(floorTag))
            {
                if (CameraManager.Instance != null)
                {
                    CameraManager.Instance.Push(targetCamera);
                    Debug.Log($"Floor group entered (tag: {floorTag}) → Push {targetCamera.name}");
                }
                return;
            }
        }
    }

    private void OnFloorExited(string floorTag)
    {
        if (targetCamera == null) return;

        // Check if the exited tag belongs to any child in this group
        foreach (Transform child in transform)
        {
            if (child.CompareTag(floorTag))
            {
                if (CameraManager.Instance != null)
                {
                    CameraManager.Instance.Pop(targetCamera);
                    Debug.Log($"Floor group exited (tag: {floorTag}) → Pop {targetCamera.name}");
                }
                return;
            }
        }
    }
}