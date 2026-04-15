using UnityEngine;

[DisallowMultipleComponent]
public class HitPartShakeReaction : MonoBehaviour
{
    [System.Serializable]
    public class HurtboxShakeBinding
    {
        [Header("Hurtbox")]
        public Collider hurtbox;              // e.g. HB_Arm_L collider
        public Transform hurtboxTransform;    // optional if you prefer transform mapping

        [Header("Bone")]
        public Transform targetBone;          // e.g. UpperArm_L bone

        [Header("Overrides (optional)")]
        public bool overrideAmplitude = false;
        public float posAmplitude = 0.02f;
        public float rotAmplitudeDeg = 6f;

        public bool overrideTiming = false;
        public float duration = 0.12f;
        public float fadeOut = 0.06f;
    }

    [Header("Bindings (Hurtbox -> Bone)")]
    [SerializeField] private HurtboxShakeBinding[] bindings;

    [Header("Default Position Shake")]
    [SerializeField] private bool usePosition = true;
    [SerializeField] private float posAmplitude = 0.02f;
    [SerializeField] private float posFrequency = 30f;

    [Header("Default Rotation Shake")]
    [SerializeField] private bool useRotation = true;
    [SerializeField] private float rotAmplitudeDeg = 6f;
    [SerializeField] private float rotFrequency = 30f;

    [Header("Default Timing")]
    [SerializeField] private float duration = 0.12f;
    [SerializeField] private float fadeOut = 0.06f;

    // runtime (single active shake at a time)
    private Transform activeBone;
    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;

    private float timer;
    private float activeDuration;
    private float activeFadeOut;
    private float activePosAmp;
    private float activeRotAmp;

    private float seedA;
    private float seedB;
    private bool playing;

    private void OnDisable()
    {
        Restore();
    }

    private void LateUpdate()
    {
        if (!playing) return;
        if (activeBone == null)
        {
            playing = false;
            return;
        }

        timer += Time.deltaTime;

        float t = timer;
        float total = Mathf.Max(0.0001f, activeDuration);
        float w = 1f;

        if (activeFadeOut > 0f)
        {
            float fadeStart = Mathf.Max(0f, total - activeFadeOut);
            if (t >= fadeStart)
            {
                float ft = (t - fadeStart) / Mathf.Max(0.0001f, activeFadeOut);
                w = 1f - Mathf.Clamp01(ft);
            }
        }

        Vector3 posOff = Vector3.zero;
        Quaternion rotOff = Quaternion.identity;

        if (usePosition)
        {
            float sx = Mathf.Sin((t * posFrequency) + seedA);
            float sy = Mathf.Sin((t * (posFrequency * 1.17f)) + seedB);
            float sz = Mathf.Sin((t * (posFrequency * 0.91f)) + seedA + seedB);
            posOff = new Vector3(sx, sy, sz) * (activePosAmp * w);
        }

        if (useRotation)
        {
            float rx = Mathf.Sin((t * rotFrequency) + seedB);
            float ry = Mathf.Sin((t * (rotFrequency * 1.13f)) + seedA);
            float rz = Mathf.Sin((t * (rotFrequency * 0.87f)) + seedA + seedB);
            Vector3 euler = new Vector3(rx, ry, rz) * (activeRotAmp * w);
            rotOff = Quaternion.Euler(euler);
        }

        activeBone.localPosition = baseLocalPos + posOff;
        activeBone.localRotation = baseLocalRot * rotOff;

        if (timer >= total)
        {
            playing = false;
            Restore();
        }
    }

    private void Restore()
    {
        if (activeBone == null) return;
        activeBone.localPosition = baseLocalPos;
        activeBone.localRotation = baseLocalRot;
    }

    // MAIN API: call with the exact hurtbox collider you hit
    public void PlayHurtbox(Collider hitHurtbox)
    {
        HurtboxShakeBinding b = FindBinding(hitHurtbox);
        if (b == null || b.targetBone == null) return;

        if (playing) Restore();

        activeBone = b.targetBone;

        baseLocalPos = activeBone.localPosition;
        baseLocalRot = activeBone.localRotation;

        activePosAmp = b.overrideAmplitude ? Mathf.Max(0f, b.posAmplitude) : Mathf.Max(0f, posAmplitude);
        activeRotAmp = b.overrideAmplitude ? Mathf.Max(0f, b.rotAmplitudeDeg) : Mathf.Max(0f, rotAmplitudeDeg);

        activeDuration = b.overrideTiming ? Mathf.Max(0.01f, b.duration) : Mathf.Max(0.01f, duration);
        activeFadeOut = b.overrideTiming ? Mathf.Max(0f, b.fadeOut) : Mathf.Max(0f, fadeOut);

        timer = 0f;
        seedA = Random.Range(0f, 1000f);
        seedB = Random.Range(0f, 1000f);
        playing = true;
    }

    // optional: if you prefer mapping by hurtbox transform (not collider)
    public void PlayHurtboxTransform(Transform hitHurtboxTransform)
    {
        HurtboxShakeBinding b = FindBinding(hitHurtboxTransform);
        if (b == null || b.targetBone == null) return;
        PlayHurtbox(b.hurtbox != null ? b.hurtbox : null);
        // if b.hurtbox is null but transform is used, fall back to manual start:
        if (b.hurtbox == null)
        {
            if (playing) Restore();

            activeBone = b.targetBone;
            baseLocalPos = activeBone.localPosition;
            baseLocalRot = activeBone.localRotation;

            activePosAmp = b.overrideAmplitude ? Mathf.Max(0f, b.posAmplitude) : Mathf.Max(0f, posAmplitude);
            activeRotAmp = b.overrideAmplitude ? Mathf.Max(0f, b.rotAmplitudeDeg) : Mathf.Max(0f, rotAmplitudeDeg);

            activeDuration = b.overrideTiming ? Mathf.Max(0.01f, b.duration) : Mathf.Max(0.01f, duration);
            activeFadeOut = b.overrideTiming ? Mathf.Max(0f, b.fadeOut) : Mathf.Max(0f, fadeOut);

            timer = 0f;
            seedA = Random.Range(0f, 1000f);
            seedB = Random.Range(0f, 1000f);
            playing = true;
        }
    }

    private HurtboxShakeBinding FindBinding(Collider hitHurtbox)
    {
        if (bindings == null) return null;
        if (hitHurtbox == null) return null;

        for (int i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b == null) continue;

            if (b.hurtbox == hitHurtbox) return b;

            // fallback: compare gameobject
            if (b.hurtbox != null && b.hurtbox.gameObject == hitHurtbox.gameObject) return b;
        }

        return null;
    }

    private HurtboxShakeBinding FindBinding(Transform hitHurtboxTransform)
    {
        if (bindings == null) return null;
        if (hitHurtboxTransform == null) return null;

        for (int i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b == null) continue;

            if (b.hurtboxTransform != null && b.hurtboxTransform == hitHurtboxTransform) return b;
            if (b.hurtbox != null && b.hurtbox.transform == hitHurtboxTransform) return b;
        }

        return null;
    }
}