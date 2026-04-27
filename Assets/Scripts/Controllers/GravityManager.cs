using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NEW DESIGN — Global Radial Gravity
/// ─────────────────────────────────────────────────────────────────────────────
/// • No more trigger zone for gravity. ALL launched planets are ALWAYS pulled
///   toward the black hole regardless of where they are on screen.
/// • The dotted circle is now a "safe zone" boundary drawn visually.
///   If a planet SETTLES (velocity drops low for settleTime seconds) with its
///   centre OUTSIDE that boundary → Game Over.
/// • PlanetFactory.SpawnPlanet() calls RegisterPlanet(); Planet.OnDestroy()
///   calls UnregisterPlanet() automatically.
/// ─────────────────────────────────────────────────────────────────────────────
/// Attach to the BlackHole GameObject. Remove the old CircleCollider2D trigger —
/// it is no longer needed for gravity. A separate small solid CircleCollider2D
/// (radius = coreRadius, non-trigger) handles planet stacking on the surface.
/// </summary>
public class GravityManager : MonoBehaviour
{
    public static GravityManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────
    [Header("Radial Gravity  F = GravityConstant / distance")]
    public float gravityConstant   = 80f;

    [Header("Safe-Zone Boundary (dotted circle)")]
    [Tooltip("Planets that settle outside this radius trigger Game Over")]
    public float zoneRadius        = 3.5f;

    [Header("Settle Detection")]
    [Tooltip("Speed (units/s) below which a planet is considered 'settling'")]
    public float settleSpeedThreshold = 0.4f;
    [Tooltip("Seconds a planet must stay below the speed threshold to be 'settled'")]
    public float settleRequiredTime   = 1.8f;

    [Header("Boundary Visual")]
    public bool  drawBoundary      = true;
    public Color boundaryColor     = new Color(0.02f, 0.85f, 1f, 0.85f);
    public float boundaryLineWidth = 0.035f;
    public int   boundarySegments  = 140;        // many segments → dense dot ring
    [Range(0.01f, 0.9f)]
    public float dashFraction      = 0.18f;      // short dashes look like dots

    [Header("Warning Ring (inner alert boundary)")]
    public float warningRingFraction  = 0.88f;   // fraction of zoneRadius
    public Color warningRingColor     = new Color(1.00f, 0.30f, 0.10f, 0.55f);
    public float warningRingWidth     = 0.022f;
    public int   warningRingSegments  = 100;
    [Range(0.01f, 0.9f)]
    public float warningDashFraction  = 0.12f;

    // ── Runtime ───────────────────────────────────────────────────
    private readonly List<Planet>          _planets     = new List<Planet>();
    private readonly Dictionary<int, float> _settleTimers = new Dictionary<int, float>();

    // ─────────────────────────────────────────────────────────────
    #region Unity Lifecycle

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (drawBoundary) BuildBoundaryVisual();
    }

    /// <summary>Expose the warning ring radius for other scripts.</summary>
    public float WarningRingRadius => zoneRadius * warningRingFraction;

    void FixedUpdate()
    {
        for (int i = _planets.Count - 1; i >= 0; i--)
        {
            Planet p = _planets[i];
            if (p == null) { _planets.RemoveAt(i); continue; }
            if (!p.IsLaunched || p.IsMerging) continue;

            ApplyRadialGravity(p);
            CheckSettle(p);
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Planet Registration

    public void RegisterPlanet(Planet p)
    {
        if (!_planets.Contains(p))
        {
            _planets.Add(p);
            // Gravity is always radial — disable Unity gravity immediately
            p.Rb.gravityScale = 0f;
        }
    }

    public void UnregisterPlanet(Planet p)
    {
        _planets.Remove(p);
        if (p != null)
            _settleTimers.Remove(p.GetInstanceID());
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Gravity

    void ApplyRadialGravity(Planet p)
    {
        Vector2 toCenter = (Vector2)transform.position - (Vector2)p.transform.position;
        float   distance = toCenter.magnitude;
        if (distance < 0.05f) return;

        float force = gravityConstant / distance;
        p.Rb.AddForce(toCenter.normalized * force, ForceMode2D.Force);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Settle / Lose Condition

    void CheckSettle(Planet p)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;

        int id = p.GetInstanceID();

        if (p.Rb.linearVelocity.magnitude < settleSpeedThreshold)
        {
            if (!_settleTimers.ContainsKey(id)) _settleTimers[id] = 0f;
            _settleTimers[id] += Time.fixedDeltaTime;

            if (_settleTimers[id] >= settleRequiredTime)
            {
                // Planet has settled — check if any part of it sticks outside the safe zone
                float dist      = Vector2.Distance(p.transform.position, transform.position);
                float edgeRadius = p.data != null ? p.data.radius : 0f;
                if (dist + edgeRadius > zoneRadius)
                {
                    GameManager.Instance.TriggerLose();
                }
            }
        }
        else
        {
            // Still moving — reset settle timer
            _settleTimers.Remove(id);
        }
    }

    /// <summary>Resets all settle timers (call on game restart).</summary>
    public void ResetTimers()
    {
        _settleTimers.Clear();
        _planets.Clear();
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Boundary Visual

    public void BuildBoundaryVisual()
    {
        // Destroy any pre-existing boundary
        var old = transform.Find("BoundaryVisual");
        if (old) Destroy(old.gameObject);

        var container = new GameObject("BoundaryVisual");
        container.transform.SetParent(transform, false);

        // ── Outer safe-zone dashed ring ───────────────────────────
        BuildDashedRing(container.transform, "Outer",
            zoneRadius, boundarySegments, dashFraction,
            boundaryColor, boundaryLineWidth, 5);

        // ── Inner warning ring (slightly smaller, red-tinted) ─────
        float warnR = zoneRadius * warningRingFraction;
        BuildDashedRing(container.transform, "Warn",
            warnR, warningRingSegments, warningDashFraction,
            warningRingColor, warningRingWidth, 4);
    }

    void BuildDashedRing(Transform parent, string prefix,
        float radius, int segments, float dashFrac,
        Color color, float lineWidth, int sortOrder)
    {
        float angleStep = 360f / segments;
        float dashAngle = angleStep * dashFrac;

        for (int i = 0; i < segments; i++)
        {
            float a0 = i * angleStep;
            float a1 = a0 + dashAngle;

            var dash = new GameObject($"{prefix}D{i}");
            dash.transform.SetParent(parent, false);

            var lr            = dash.AddComponent<LineRenderer>();
            lr.useWorldSpace  = false;
            lr.loop           = false;
            lr.positionCount  = 2;
            lr.startWidth     = lineWidth;
            lr.endWidth       = lineWidth;
            lr.startColor     = color;
            lr.endColor       = color;
            lr.material       = new Material(Shader.Find("Sprites/Default")) { color = color };
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder   = sortOrder;

            lr.SetPosition(0, ArcPoint(a0, radius));
            lr.SetPosition(1, ArcPoint(a1, radius));
        }
    }

    static Vector3 ArcPoint(float deg, float r)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, 0f);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Public Helpers

    public float ZoneRadius => zoneRadius;

    #endregion
}
