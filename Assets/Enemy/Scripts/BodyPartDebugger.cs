using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class BodyPartDebugger : MonoBehaviour
{
    [Header("Refs")]
    public Enemy enemy;
    public EnemyPartBreaks partBreaks;

    [Header("Debug Damage - Stun Parts")]
    public float headDebugDamage = 5f;
    public float torsoDebugDamage = 5f;
    public float abdomenDebugDamage = 5f;

    [Header("Debug Damage - Arm Parts")]
    public float leftArmDebugDamage = 10f;
    public float rightArmDebugDamage = 10f;
    public float bothArmsDebugDamage = 10f;

    [Header("Debug Damage - Leg Parts")]
    public float leftLegDebugDamage = 10f;
    public float rightLegDebugDamage = 10f;
    public float bothLegsDebugDamage = 10f;

    [Header("Long Stun Button Target")]
    public LongStunTarget longStunButtonTarget = LongStunTarget.Torso;

    public enum LongStunTarget
    {
        Torso,
        Abdomen,
        Alternate
    }

    private bool _alternateToggle = false;

    // Cached previous values for stun-trigger detection (head/torso/abdomen only)
    private float _prevHead;
    private float _prevTorso;
    private float _prevAbdomen;

    private void Reset()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (partBreaks == null) partBreaks = GetComponent<EnemyPartBreaks>();
    }

    private void Awake()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (partBreaks == null) partBreaks = GetComponent<EnemyPartBreaks>();

        SyncPrevValues();
    }

    private void OnEnable()
    {
        SyncPrevValues();
    }

    private void Update()
    {
        if (enemy == null || partBreaks == null) return;

        // Detect decrease and trigger stun logic for body/core parts.
        if (partBreaks.headPartHealth < _prevHead)
        {
            enemy.TryEnterStunFromBodyPart(EnemyHurtBoxSettings.EnemyBodyPartType.Head);
        }

        if (partBreaks.torsoPartHealth < _prevTorso)
        {
            enemy.TryEnterStunFromBodyPart(EnemyHurtBoxSettings.EnemyBodyPartType.Torso);
        }

        if (partBreaks.abdomenPartHealth < _prevAbdomen)
        {
            enemy.TryEnterStunFromBodyPart(EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen);
        }

        SyncPrevValues();
    }

    public void DebugDamageHead()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.Head, headDebugDamage);
        Debug.Log($"[BodyPartDebugger] Head damaged by {headDebugDamage}");
    }

    public void DebugDamageLongStunPart()
    {
        if (partBreaks == null) return;

        EnemyHurtBoxSettings.EnemyBodyPartType target = ResolveLongStunTarget();
        float dmg = (target == EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen) ? abdomenDebugDamage : torsoDebugDamage;

        partBreaks.ApplyPartDamage(target, dmg);
        Debug.Log($"[BodyPartDebugger] {target} damaged by {dmg}");
    }

    public void DebugDamageTorso()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.Torso, torsoDebugDamage);
        Debug.Log($"[BodyPartDebugger] Torso damaged by {torsoDebugDamage}");
    }

    public void DebugDamageAbdomen()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen, abdomenDebugDamage);
        Debug.Log($"[BodyPartDebugger] Abdomen damaged by {abdomenDebugDamage}");
    }

    public void DebugDamageLeftArm()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.LeftArm, leftArmDebugDamage);
        Debug.Log($"[BodyPartDebugger] LeftArm damaged by {leftArmDebugDamage}");
    }

    public void DebugDamageRightArm()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.RightArm, rightArmDebugDamage);
        Debug.Log($"[BodyPartDebugger] RightArm damaged by {rightArmDebugDamage}");
    }

    public void DebugDamageBothArms()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.LeftArm, bothArmsDebugDamage);
        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.RightArm, bothArmsDebugDamage);
        Debug.Log($"[BodyPartDebugger] Both arms damaged by {bothArmsDebugDamage}");
    }

    public void DebugDamageLeftLeg()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.LeftLeg, leftLegDebugDamage);
        Debug.Log($"[BodyPartDebugger] LeftLeg damaged by {leftLegDebugDamage}");
    }

    public void DebugDamageRightLeg()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.RightLeg, rightLegDebugDamage);
        Debug.Log($"[BodyPartDebugger] RightLeg damaged by {rightLegDebugDamage}");
    }

    public void DebugDamageBothLegs()
    {
        if (partBreaks == null) return;

        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.LeftLeg, bothLegsDebugDamage);
        partBreaks.ApplyPartDamage(EnemyHurtBoxSettings.EnemyBodyPartType.RightLeg, bothLegsDebugDamage);
        Debug.Log($"[BodyPartDebugger] Both legs damaged by {bothLegsDebugDamage}");
    }

    private EnemyHurtBoxSettings.EnemyBodyPartType ResolveLongStunTarget()
    {
        switch (longStunButtonTarget)
        {
            case LongStunTarget.Abdomen:
                return EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen;

            case LongStunTarget.Alternate:
                _alternateToggle = !_alternateToggle;
                return _alternateToggle
                    ? EnemyHurtBoxSettings.EnemyBodyPartType.Torso
                    : EnemyHurtBoxSettings.EnemyBodyPartType.Abdomen;

            case LongStunTarget.Torso:
            default:
                return EnemyHurtBoxSettings.EnemyBodyPartType.Torso;
        }
    }

    private void SyncPrevValues()
    {
        if (partBreaks == null) return;

        _prevHead = partBreaks.headPartHealth;
        _prevTorso = partBreaks.torsoPartHealth;
        _prevAbdomen = partBreaks.abdomenPartHealth;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BodyPartDebugger))]
public class BodyPartDebuggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        BodyPartDebugger dbg = (BodyPartDebugger)target;

        GUI.enabled = Application.isPlaying;

        EditorGUILayout.LabelField("Stun Debug (Head / Torso / Abdomen)", EditorStyles.boldLabel);

        if (GUILayout.Button("Damage Head (Short Stun)"))
        {
            dbg.DebugDamageHead();
        }

        if (GUILayout.Button("Damage Torso/Abdomen (Long Stun)"))
        {
            dbg.DebugDamageLongStunPart();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Damage Torso"))
        {
            dbg.DebugDamageTorso();
        }
        if (GUILayout.Button("Damage Abdomen"))
        {
            dbg.DebugDamageAbdomen();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Arm Break Debug", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Damage Left Arm"))
        {
            dbg.DebugDamageLeftArm();
        }
        if (GUILayout.Button("Damage Right Arm"))
        {
            dbg.DebugDamageRightArm();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Damage Both Arms"))
        {
            dbg.DebugDamageBothArms();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Leg Break Debug", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Damage Left Leg"))
        {
            dbg.DebugDamageLeftLeg();
        }
        if (GUILayout.Button("Damage Right Leg"))
        {
            dbg.DebugDamageRightLeg();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Damage Both Legs"))
        {
            dbg.DebugDamageBothLegs();
        }

        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the debug buttons.", MessageType.Info);
        }
    }
}
#endif