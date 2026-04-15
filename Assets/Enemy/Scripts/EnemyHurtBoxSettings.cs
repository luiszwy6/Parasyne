using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class EnemyBodyParts
{
    [Header("Head")]
    public Collider HB_Head;

    [Header("Torso")]
    public Collider HB_Chest;
    public Collider HB_Hip;

    [Header("Abdomen")]
    public Collider HB_Abdomen;

    [Header("Left Leg")]
    public Collider HB_UpLeg_L;
    public Collider HB_Leg_L;
    public Collider HB_Foot_L;

    [Header("Right Leg")]
    public Collider HB_UpLeg_R;
    public Collider HB_Leg_R;
    public Collider HB_Foot_R;

    [Header("Left Arm")]
    public Collider HB_Shoulder_L;
    public Collider HB_Arm_L;
    public Collider HB_ForeArm_L;
    public Collider HB_Hand_L;

    [Header("Right Arm")]
    public Collider HB_Shoulder_R;
    public Collider HB_Arm_R;
    public Collider HB_ForeArm_R;
    public Collider HB_Hand_R;
}

[DisallowMultipleComponent]
public class EnemyHurtBoxSettings : MonoBehaviour
{
    [Header("Body Parts")]
    public EnemyBodyParts parts = new EnemyBodyParts();

    [Header("Auto Find")]
    [SerializeField] private bool includeInactive = true;

    [Header("Optional Enforcement")]
    [SerializeField] private bool enforceIsTrigger = true;
    [SerializeField] private bool enforceLayer = false;
    [SerializeField] private string hurtboxLayerName = "EnemyHurtBox";

    [Header("Tag Names (must exist in Tag Manager)")]
    [SerializeField] private string tagHead = "E_Head";
    [SerializeField] private string tagTorso = "E_Torso";
    [SerializeField] private string tagAbdomen = "E_Abdomen";
    [SerializeField] private string tagLeg = "E_Leg";
    [SerializeField] private string tagArm = "E_Arm";

    // Read-only grouped lists for utilities/debugging
    public IReadOnlyList<Collider> All => _all;
    public IReadOnlyList<Collider> HeadGroup => _head;
    public IReadOnlyList<Collider> TorsoGroup => _torso;
    public IReadOnlyList<Collider> AbdomenGroup => _abdomen;
    public IReadOnlyList<Collider> LeftLegGroup => _leftLeg;
    public IReadOnlyList<Collider> RightLegGroup => _rightLeg;
    public IReadOnlyList<Collider> LeftArmGroup => _leftArm;
    public IReadOnlyList<Collider> RightArmGroup => _rightArm;

    private readonly List<Collider> _all = new(18);
    private readonly List<Collider> _head = new(1);
    private readonly List<Collider> _torso = new(2);
    private readonly List<Collider> _abdomen = new(1);
    private readonly List<Collider> _leftLeg = new(3);
    private readonly List<Collider> _rightLeg = new(3);
    private readonly List<Collider> _leftArm = new(4);
    private readonly List<Collider> _rightArm = new(4);

    // Keep the enum name as requested, but now use detailed parts.
    public enum EnemyBodyPartType
    {
        Head,
        Torso,
        Abdomen,
        LeftLeg,
        RightLeg,
        LeftArm,
        RightArm
    }

    private void Awake()
    {
        RebuildLists();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLists();

        if (!Application.isPlaying)
        {
            if (enforceIsTrigger) EnforceIsTrigger();
            if (enforceLayer) EnforceLayer();
            // ApplyTags();
        }
    }
#endif

    // -------------------------
    // Public API
    // -------------------------

    public void AutoFindByName()
    {
        parts.HB_Head = FindHB("HB_Head");

        parts.HB_Chest = FindHB("HB_Chest");
        parts.HB_Abdomen = FindHB("HB_Abdomen");
        parts.HB_Hip = FindHB("HB_Hip");

        parts.HB_Foot_L = FindHB("HB_Foot_L");
        parts.HB_Foot_R = FindHB("HB_Foot_R");

        parts.HB_Leg_L = FindHB("HB_Leg_L");
        parts.HB_Leg_R = FindHB("HB_Leg_R");

        parts.HB_UpLeg_L = FindHB("HB_UpLeg_L");
        parts.HB_UpLeg_R = FindHB("HB_UpLeg_R");

        parts.HB_Shoulder_L = FindHB("HB_Shoulder_L");
        parts.HB_Shoulder_R = FindHB("HB_Shoulder_R");

        parts.HB_Arm_L = FindHB("HB_Arm_L");
        parts.HB_Arm_R = FindHB("HB_Arm_R");

        parts.HB_ForeArm_L = FindHB("HB_ForeArm_L");
        parts.HB_ForeArm_R = FindHB("HB_ForeArm_R");

        parts.HB_Hand_L = FindHB("HB_Hand_L");
        parts.HB_Hand_R = FindHB("HB_Hand_R");

        RebuildLists();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void ApplyTags()
    {
        // Head
        SetTagSafe(parts.HB_Head, tagHead);

        // Torso
        SetTagSafe(parts.HB_Chest, tagTorso);
        SetTagSafe(parts.HB_Hip, tagTorso);

        // Abdomen
        SetTagSafe(parts.HB_Abdomen, tagAbdomen);

        // Legs (same tag for both sides)
        SetTagSafe(parts.HB_UpLeg_L, tagLeg);
        SetTagSafe(parts.HB_Leg_L, tagLeg);
        SetTagSafe(parts.HB_Foot_L, tagLeg);

        SetTagSafe(parts.HB_UpLeg_R, tagLeg);
        SetTagSafe(parts.HB_Leg_R, tagLeg);
        SetTagSafe(parts.HB_Foot_R, tagLeg);

        // Arms (same tag for both sides)
        SetTagSafe(parts.HB_Shoulder_L, tagArm);
        SetTagSafe(parts.HB_Arm_L, tagArm);
        SetTagSafe(parts.HB_ForeArm_L, tagArm);
        SetTagSafe(parts.HB_Hand_L, tagArm);

        SetTagSafe(parts.HB_Shoulder_R, tagArm);
        SetTagSafe(parts.HB_Arm_R, tagArm);
        SetTagSafe(parts.HB_ForeArm_R, tagArm);
        SetTagSafe(parts.HB_Hand_R, tagArm);

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    // Detailed resolution (left/right aware). This should be used by gameplay logic.
    public EnemyBodyPartType ResolvePartFromCollider(Collider hitCol)
    {
        if (hitCol == null) return EnemyBodyPartType.Torso;

        // Exact reference checks first (left/right precision)
        if (hitCol == parts.HB_Head) return EnemyBodyPartType.Head;

        if (hitCol == parts.HB_Chest || hitCol == parts.HB_Hip)
            return EnemyBodyPartType.Torso;

        if (hitCol == parts.HB_Abdomen)
            return EnemyBodyPartType.Abdomen;

        if (hitCol == parts.HB_UpLeg_L || hitCol == parts.HB_Leg_L || hitCol == parts.HB_Foot_L)
            return EnemyBodyPartType.LeftLeg;

        if (hitCol == parts.HB_UpLeg_R || hitCol == parts.HB_Leg_R || hitCol == parts.HB_Foot_R)
            return EnemyBodyPartType.RightLeg;

        if (hitCol == parts.HB_Shoulder_L || hitCol == parts.HB_Arm_L || hitCol == parts.HB_ForeArm_L || hitCol == parts.HB_Hand_L)
            return EnemyBodyPartType.LeftArm;

        if (hitCol == parts.HB_Shoulder_R || hitCol == parts.HB_Arm_R || hitCol == parts.HB_ForeArm_R || hitCol == parts.HB_Hand_R)
            return EnemyBodyPartType.RightArm;

        // Fallback by membership (in case references are rebuilt but equality path missed)
        if (_head.Contains(hitCol)) return EnemyBodyPartType.Head;
        if (_abdomen.Contains(hitCol)) return EnemyBodyPartType.Abdomen;
        if (_torso.Contains(hitCol)) return EnemyBodyPartType.Torso;
        if (_leftLeg.Contains(hitCol)) return EnemyBodyPartType.LeftLeg;
        if (_rightLeg.Contains(hitCol)) return EnemyBodyPartType.RightLeg;
        if (_leftArm.Contains(hitCol)) return EnemyBodyPartType.LeftArm;
        if (_rightArm.Contains(hitCol)) return EnemyBodyPartType.RightArm;

        // Final fallback
        return EnemyBodyPartType.Torso;
    }

    // -------------------------
    // Internal
    // -------------------------

    private Collider FindHB(string exactName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(includeInactive);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == exactName)
            {
                return children[i].GetComponent<Collider>();
            }
        }
        return null;
    }

    private void RebuildLists()
    {
        _all.Clear();
        _head.Clear();
        _torso.Clear();
        _abdomen.Clear();
        _leftLeg.Clear();
        _rightLeg.Clear();
        _leftArm.Clear();
        _rightArm.Clear();

        // Head
        Add(parts.HB_Head, _head);

        // Torso
        Add(parts.HB_Chest, _torso);
        Add(parts.HB_Hip, _torso);

        // Abdomen
        Add(parts.HB_Abdomen, _abdomen);

        // Left Leg
        Add(parts.HB_UpLeg_L, _leftLeg);
        Add(parts.HB_Leg_L, _leftLeg);
        Add(parts.HB_Foot_L, _leftLeg);

        // Right Leg
        Add(parts.HB_UpLeg_R, _rightLeg);
        Add(parts.HB_Leg_R, _rightLeg);
        Add(parts.HB_Foot_R, _rightLeg);

        // Left Arm
        Add(parts.HB_Shoulder_L, _leftArm);
        Add(parts.HB_Arm_L, _leftArm);
        Add(parts.HB_ForeArm_L, _leftArm);
        Add(parts.HB_Hand_L, _leftArm);

        // Right Arm
        Add(parts.HB_Shoulder_R, _rightArm);
        Add(parts.HB_Arm_R, _rightArm);
        Add(parts.HB_ForeArm_R, _rightArm);
        Add(parts.HB_Hand_R, _rightArm);

        _all.AddRange(_head);
        _all.AddRange(_torso);
        _all.AddRange(_abdomen);
        _all.AddRange(_leftLeg);
        _all.AddRange(_rightLeg);
        _all.AddRange(_leftArm);
        _all.AddRange(_rightArm);
    }

    private void Add(Collider c, List<Collider> group)
    {
        if (c == null) return;
        group.Add(c);
    }

    private void SetTagSafe(Collider c, string tagName)
    {
        if (c == null) return;
        if (string.IsNullOrEmpty(tagName)) return;

#if UNITY_EDITOR
        if (!TagExists(tagName))
        {
            Debug.LogWarning($"[EnemyHurtBoxSettings] Tag '{tagName}' does not exist. Create it in Tag Manager first.", this);
            return;
        }

        Undo.RecordObject(c.gameObject, "Set Hurtbox Tag");
#endif
        c.gameObject.tag = tagName;
    }

#if UNITY_EDITOR
    private static bool TagExists(string tag)
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
        );
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                return true;
        }

        return false;
    }
#endif

    private void EnforceIsTrigger()
    {
        foreach (var c in _all)
        {
            if (c == null) continue;
#if UNITY_EDITOR
            Undo.RecordObject(c, "Enforce IsTrigger");
#endif
            c.isTrigger = true;
        }
    }

    private void EnforceLayer()
    {
        int layer = LayerMask.NameToLayer(hurtboxLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[EnemyHurtBoxSettings] Layer '{hurtboxLayerName}' not found. Create it first.", this);
            return;
        }

        foreach (var c in _all)
        {
            if (c == null) continue;
#if UNITY_EDITOR
            Undo.RecordObject(c.gameObject, "Enforce Hurtbox Layer");
#endif
            c.gameObject.layer = layer;
        }
    }
}