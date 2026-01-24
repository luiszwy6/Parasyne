using UnityEngine;

// PlayerAwareness: lightweight visibility/noise signals for AI.
// - lightness: computed from one or two RenderTexture probe cameras (0..4)
// - noisiness: set externally by movement/animations/ability code (0..2)

public class PlayerAwareness : MonoBehaviour
{
    [Header("Lightness(0 = darkest, 4 = brightest)")]
    [Range(0, 4)] public int lightness = 0;

    [Header("Noisiness (0 = silent, 2 = loudest)")]
    [Range(0, 2)] public int noisiness = 0;

    [Header("Light Detection (Two RenderTexture Probes)")]
    [Tooltip("Upper-body probe camera (should only render the LightProbe layer).")]
    [SerializeField] private Camera lightProbeCameraBody;

    [Tooltip("Foot probe camera (should only render the LightProbe layer).")]
    [SerializeField] private Camera lightProbeCameraBottom;

    [Header("Probe Renderers (Spheres)")]
    [Tooltip("Upper-body probe sphere Renderer (LightProbeBody).")]
    [SerializeField] private Renderer lightProbeBodyRenderer;

    [Tooltip("Foot probe sphere Renderer (LightProbeBottom).")]
    [SerializeField] private Renderer lightProbeBottomRenderer;

    [Header("RenderTextures (optional but recommended)")]
    [Tooltip("Optional RT output for body probe camera (useful for RawImage preview).")]
    [SerializeField] private RenderTexture outputRTBody;

    [Tooltip("Optional RT output for foot probe camera (useful for RawImage preview).")]
    [SerializeField] private RenderTexture outputRTBottom;

    [Tooltip("Update interval in seconds (0.1 is a common value).")]
    [SerializeField] private float updateInterval = 0.1f;

    [Header("Fallback RT size (only used if outputRT is not assigned)")]
    [SerializeField] private int rtSize = 64;

    [Header("Sampling")]
    [Tooltip("Sample only the center region of the RT (0.33 = center third).")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float centerRegionRatio = 1f / 3f;

    [Tooltip("If true, rotate the probe camera to LookAt the probe sphere before rendering.")]
    [SerializeField] private bool lookAtProbeBeforeRender = true;

    [Header("Map brightness(0..1) -> lightness(0..4) thresholds (dark->bright)")]
    [SerializeField] private float t0 = 0.08f;
    [SerializeField] private float t1 = 0.18f;
    [SerializeField] private float t2 = 0.35f;
    [SerializeField] private float t3 = 0.60f;

    [Tooltip("Global brightness gain (useful to match your scene lighting).")]
    [SerializeField] private float brightnessGain = 1.0f;

    [Header("Debug (optional)")]
    [SerializeField] private bool logBrightness = false;

    // runtime resources
    private RenderTexture _rtBody, _rtBottom;
    private Texture2D _readTexBody, _readTexBottom;
    private bool _rtBodyOwned, _rtBottomOwned;

    private float _timer;
    private bool _initialized;

    void Awake()
    {
        TryInit();
    }

    void OnDisable()
    {
        ReleaseResources();
        _initialized = false;
    }

    void OnDestroy()
    {
        ReleaseResources();
    }

    void Update()
    {
        if (!_initialized)
        {
            TryInit();
            if (!_initialized) return;
        }

        _timer += Time.deltaTime;
        if (_timer < updateInterval) return;
        _timer = 0f;

        float bBody = SampleBrightness01(lightProbeCameraBody, lightProbeBodyRenderer, _rtBody, _readTexBody);
        float bBottom = SampleBrightness01(lightProbeCameraBottom, lightProbeBottomRenderer, _rtBottom, _readTexBottom);

        // Combine probes: body + foot.
        // Note: final value uses the MAX of the two probes (more sensitive to any bright spot).
        float brightness01;
        if (lightProbeBodyRenderer != null && lightProbeBottomRenderer != null)
            brightness01 = 0.5f * (bBody + bBottom);
        else if (lightProbeBodyRenderer != null)
            brightness01 = bBody;
        else
            brightness01 = bBottom;

        brightness01 = Mathf.Max(bBody, bBottom);

        lightness = BrightnessToLightness(brightness01);

        if (logBrightness)
            Debug.Log($"body={bBody:F3}, bottom={bBottom:F3}, avg={brightness01:F3}, lightness={lightness}", this);
    }

    private void TryInit()
    {
        if (_initialized) return;

        // Need at least one (camera + renderer) pair to sample.
        bool hasBody = (lightProbeCameraBody != null && lightProbeBodyRenderer != null);
        bool hasBottom = (lightProbeCameraBottom != null && lightProbeBottomRenderer != null);
        if (!hasBody && !hasBottom) return;

        // Body RT
        if (hasBody)
        {
            if (outputRTBody != null)
            {
                _rtBody = outputRTBody;
                _rtBodyOwned = false;
            }
            else
            {
                int size = Mathf.Max(16, rtSize);
                _rtBody = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);
                _rtBody.Create();
                _rtBodyOwned = true;
            }

            _readTexBody = new Texture2D(_rtBody.width, _rtBody.height, TextureFormat.RGB24, false);
            lightProbeCameraBody.targetTexture = _rtBody;
        }

        // Bottom RT
        if (hasBottom)
        {
            if (outputRTBottom != null)
            {
                _rtBottom = outputRTBottom;
                _rtBottomOwned = false;
            }
            else
            {
                int size = Mathf.Max(16, rtSize);
                _rtBottom = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);
                _rtBottom.Create();
                _rtBottomOwned = true;
            }

            _readTexBottom = new Texture2D(_rtBottom.width, _rtBottom.height, TextureFormat.RGB24, false);
            lightProbeCameraBottom.targetTexture = _rtBottom;
        }

        _initialized = true;
    }

    private void ReleaseResources()
    {
        if (_rtBodyOwned && _rtBody != null) _rtBody.Release();
        if (_rtBottomOwned && _rtBottom != null) _rtBottom.Release();

        _rtBody = null;
        _rtBottom = null;
        _rtBodyOwned = false;
        _rtBottomOwned = false;

        if (_readTexBody != null) Destroy(_readTexBody);
        if (_readTexBottom != null) Destroy(_readTexBottom);

        _readTexBody = null;
        _readTexBottom = null;
    }

    private float SampleBrightness01(Camera cam, Renderer probeR, RenderTexture rt, Texture2D readTex)
    {
        if (cam == null || probeR == null || rt == null || readTex == null) return 0f;

        if (lookAtProbeBeforeRender)
        {
            cam.transform.LookAt(probeR.bounds.center);
        }

        cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        readTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readTex.Apply(false);
        RenderTexture.active = prev;

        int w = rt.width;
        int h = rt.height;

        float ratio = Mathf.Clamp(centerRegionRatio, 0.1f, 1.0f);
        int regionW = Mathf.Max(2, Mathf.RoundToInt(w * ratio));
        int regionH = Mathf.Max(2, Mathf.RoundToInt(h * ratio));
        int x0 = (w - regionW) / 2;
        int y0 = (h - regionH) / 2;

        float sum = 0f;
        int count = 0;

        for (int y = y0; y < y0 + regionH; y++)
        {
            for (int x = x0; x < x0 + regionW; x++)
            {
                Color c = readTex.GetPixel(x, y);
                float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                sum += lum;
                count++;
            }
        }

        return (count > 0) ? (sum / count) : 0f;
    }

    private int BrightnessToLightness(float b01)
    {
        if (b01 < t0) return 0;
        if (b01 < t1) return 1;
        if (b01 < t2) return 2;
        if (b01 < t3) return 3;
        return 4;
    }
}
