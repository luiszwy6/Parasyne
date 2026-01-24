using System.Collections;
using UnityEngine;

public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private CanvasGroup cg;
    [SerializeField] private float fadeTime = 0.2f;

    void Awake()
    {
        Instance = this;
        if (cg == null) cg = GetComponentInChildren<CanvasGroup>(true);
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    public IEnumerator FadeOut()
    {
        cg.blocksRaycasts = true;
        yield return Fade(1f);
    }

    public IEnumerator FadeIn()
    {
        yield return Fade(0f);
        cg.blocksRaycasts = false;
    }

    private IEnumerator Fade(float target)
    {
        float start = cg.alpha;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime; 
            cg.alpha = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        cg.alpha = target;
    }
}
