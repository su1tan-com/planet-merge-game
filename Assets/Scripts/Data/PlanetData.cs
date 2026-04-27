using UnityEngine;

[CreateAssetMenu(fileName = "PlanetData", menuName = "PlanetMerge/Planet Data", order = 1)]
public class PlanetData : ScriptableObject
{
    [Header("Identity")]
    public string planetName;
    public int tier; // 1 = Mercury ... 6 = Saturn

    [Header("Visuals")]
    public Color primaryColor   = Color.white;
    public Color glowColor      = Color.white;
    [Tooltip("Radius in Unity world units")]
    public float radius         = 0.5f;

    [Header("Physics")]
    [Tooltip("Rigidbody2D mass")]
    public float mass           = 1f;
    [Tooltip("Linear drag inside gravity zone")]
    public float drag           = 0.5f;
    [Tooltip("Angular drag for rolling friction")]
    public float angularDrag    = 0.5f;
    [Tooltip("PhysicsMaterial2D bounciness (0–1)")]
    public float bounciness     = 0.35f;

    [Header("Scoring")]
    public int scoreOnMerge     = 10;

    [Header("Merge Chain")]
    [Tooltip("The PlanetData produced when two of this tier merge. Null for Saturn.")]
    public PlanetData nextTier;
}
