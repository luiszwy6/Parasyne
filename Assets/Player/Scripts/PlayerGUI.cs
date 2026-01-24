using UnityEngine;

// Debug HUD for PlayerAwareness (lightness + noisiness).
// Uses IMGUI (OnGUI) so it can run without any UI setup.

[DisallowMultipleComponent]
public class PlayerAwarenessGUI : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private PlayerAwareness awareness;

    [Header("Layout")]
    [SerializeField] private Vector2 margin = new Vector2(16f, 16f);
    [SerializeField] private float lineHeight = 22f;
    [SerializeField] private int fontSize = 16;

    [Header("Display")]
    [SerializeField] private bool showLightness = true;
    [SerializeField] private bool showNoisiness = true;
    [SerializeField] private bool showBars = true;

    private GUIStyle style;

    private void Reset()
    {
        if (awareness == null) awareness = FindFirstObjectByType<PlayerAwareness>();
    }

    private void Awake()
    {
        // Only cache references here. GUIStyle is lazily created in OnGUI().
        if (awareness == null) awareness = FindFirstObjectByType<PlayerAwareness>();
    }

    private void OnGUI()
    {
        if (awareness == null) return;

        // Safe style init (create once).
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = true
            };
        }

        float x = margin.x;
        float y = margin.y;

        if (showLightness)
        {
            int v = Mathf.Clamp(awareness.lightness, 0, 4);
            string bar = showBars ? MakeBar(v, 4) : "";
            GUI.Label(new Rect(x, y, 420f, lineHeight),
                $"<b>Light</b>: {v}/4  {bar}",
                style);
            y += lineHeight;
        }

        if (showNoisiness)
        {
            int v = Mathf.Clamp(awareness.noisiness, 0, 2);
            string bar = showBars ? MakeBar(v, 2) : "";
            GUI.Label(new Rect(x, y, 420f, lineHeight),
                $"<b>Noise</b>: {v}/2  {bar}",
                style);
            y += lineHeight;
        }
    }

    private static string MakeBar(int value, int max)
    {
        value = Mathf.Clamp(value, 0, max);
        string filled = new string('■', value);
        string empty  = new string('□', max - value);
        return $"[{filled}{empty}]";
    }
}