using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("Default Base Camera")]
    [SerializeField] private CinemachineCamera defaultCamera;

    [Header("Priority Settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 10;

    [Header("Projection Lists")]
    [Tooltip("when these cameras are active, Main Camera switches to Perspective.")]
    [SerializeField] private List<CinemachineCamera> perspectiveCameras = new();

    [Tooltip("when these cameras are active, Main Camera switches to Orthographic.")]
    [SerializeField] private List<CinemachineCamera> orthographicCameras = new();

    private readonly Stack<CinemachineCamera> cameraStack = new();
    private Camera mainCam;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        mainCam = Camera.main;
    }

    private void Start()
    {
        if (mainCam == null) mainCam = Camera.main;

        if (defaultCamera != null)
        {
            Push(defaultCamera);
        }
    }

    public void Push(CinemachineCamera cam)
    {
        if (cam == null) return;

        if (cameraStack.Count > 0)
        {
            cameraStack.Peek().Priority = inactivePriority;
        }

        cameraStack.Push(cam);
        cam.Priority = activePriority;

        ApplyProjectionFor(cam);
    }

    public void Pop(CinemachineCamera cam)
    {
        if (cam == null || cameraStack.Count == 0) return;

        if (cameraStack.Peek() == cam)
        {
            cameraStack.Pop();
            cam.Priority = inactivePriority;

            if (cameraStack.Count > 0)
            {
                var top = cameraStack.Peek();
                top.Priority = activePriority;
                ApplyProjectionFor(top);
            }
        }
    }

    private void ApplyProjectionFor(CinemachineCamera cam)
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null || cam == null) return;
        //check lists
        if (orthographicCameras != null && orthographicCameras.Contains(cam))
        {
            mainCam.orthographic = true;
            return;
        }

        if (perspectiveCameras != null && perspectiveCameras.Contains(cam))
        {
            mainCam.orthographic = false;
            return;
        }

    }
}
