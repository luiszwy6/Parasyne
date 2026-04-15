using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Footstep Library")]
public class FootstepLibrary : ScriptableObject
{
    public FootstepSurface defaultSurface;
    public FootstepSurface[] surfaces;

    public FootstepSurface GetByTag(string tag)
    {
        if (!string.IsNullOrEmpty(tag))
        {
            for (int i = 0; i < surfaces.Length; i++)
            {
                var s = surfaces[i];
                if (s != null && s.surfaceTag == tag) return s;
            }
        }
        return defaultSurface;
    }
}
