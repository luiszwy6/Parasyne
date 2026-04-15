using UnityEngine;

[RequireComponent(typeof(Light))]
public class BrokenLight : MonoBehaviour
{
    [Header("Movement")]
    public Vector3 moveAxis = Vector3.right;
    public float moveAmplitude = 0.08f;
    public float moveSpeed = 1.2f;

    [Header("Flicker")]
    public float baseIntensity = 1.6f;
    public float flickerAmount = 0.9f;
    public float flickerSpeed = 18f;

    [Header("Hard Blink")]
    public bool useHardBlink = true;
    public float blinkChancePerSecond = 1.2f;
    public float blinkOffMin = 0.03f;
    public float blinkOffMax = 0.12f;

    [Header("Blink Intensities")]
    public float[] blinkIntensityOptions = { 0.0f, 0.15f, 0.3f, 0.6f };

    private Light _light;
    private Vector3 _startPos;
    private float _noiseSeed;
    private float _blinkTimer;
    private float _currentBlinkIntensity;

    void Awake()
    {
        _light = GetComponent<Light>();
        _startPos = transform.localPosition;
        _noiseSeed = Random.Range(0f, 9999f);
        _currentBlinkIntensity = 0f;
    }

    void Update()
    {
        float t = Time.time;

        float sway = Mathf.Sin(t * moveSpeed) * moveAmplitude;
        transform.localPosition = _startPos + moveAxis.normalized * sway;

        if (useHardBlink)
        {
            if (_blinkTimer > 0f)
            {
                _blinkTimer -= Time.deltaTime;
                _light.intensity = _currentBlinkIntensity;
                return;
            }

            float p = blinkChancePerSecond * Time.deltaTime;
            if (Random.value < p)
            {
                _blinkTimer = Random.Range(blinkOffMin, blinkOffMax);

                if (blinkIntensityOptions != null && blinkIntensityOptions.Length > 0)
                {
                    int index = Random.Range(0, blinkIntensityOptions.Length);
                    _currentBlinkIntensity = blinkIntensityOptions[index];
                }
                else
                {
                    _currentBlinkIntensity = 0f;
                }

                _light.intensity = _currentBlinkIntensity;
                return;
            }
        }

        float noise = Mathf.PerlinNoise(_noiseSeed, t * flickerSpeed);
        float centered = (noise - 0.5f) * 2f;
        float targetIntensity = baseIntensity + centered * flickerAmount;

        _light.intensity = Mathf.Max(0f, targetIntensity);
    }
}