using UnityEngine;

public class Room : MonoBehaviour
{
    [Header("Render Root")]
    [Tooltip("Put EVERYTHING that should be invisible when not in this room under here: " +
             "environment meshes, lights, VFX, AND enemy prefabs if you don't want to split them.")]
    [SerializeField] private GameObject renderRoot;

    [HideInInspector] public int id;

    public bool IsVisible { get; private set; }

    private void Reset()
    {
        // default: assume visuals are children
        if (renderRoot == null && transform.childCount > 0)
            renderRoot = transform.GetChild(0).gameObject;
    }

    public void SetVisible(bool visible)
    {
        IsVisible = visible;

        if (renderRoot != null)
            renderRoot.SetActive(visible);
    }
}
