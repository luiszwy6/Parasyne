using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWeaponDmg : MonoBehaviour
{
    [Header("Damage (Current Use)")]
    [Tooltip("Damage applied to EnemyPartBreaks body parts (current system uses this).")]
    public float part_dmg = 12f;

    [Header("Damage (Reserved)")]
    [Tooltip("Reserved for future enemy HP system. Not used yet.")]
    public float base_dmg = 25f;
}