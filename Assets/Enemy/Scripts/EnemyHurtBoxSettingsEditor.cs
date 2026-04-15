#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyHurtBoxSettings))]
public class EnemyHurtBoxSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var s = (EnemyHurtBoxSettings)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto Find HB_* By Name"))
        {
            s.AutoFindByName();
        }

        if (GUILayout.Button("Apply E_* Tags"))
        {
            s.ApplyTags();
        }

        if (GUILayout.Button("Auto Find + Apply Tags"))
        {
            s.AutoFindByName();
            s.ApplyTags();
        }
    }
}
#endif