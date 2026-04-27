using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core planet component.
///
/// Physics notes:
///   • gravityScale is always 0. GravityManager applies radial pull each FixedUpdate.
///   • The black hole has a solid CircleCollider2D so planets land and stack on it.
///   • Merge detection happens in OnCollisionEnter2D; the actual merge is deferred
///     to MergeManager so both collision callbacks finish before any Destroy calls.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Planet : MonoBehaviour
{
    // ── Public state ──────────────────────────────────────────────
    public PlanetData data { get; private set; }

    public bool IsLaunched { get; private set; }
    public bool IsMerging  { get; private set; }

    public Rigidbody2D    Rb { get; private set; }
    public SpriteRenderer Sr { get; private set; }

    // ── Merge guard ───────────────────────────────────────────────
    private static readonly HashSet<int> s_mergingIds = new HashSet<int>();
    private static readonly object       s_mergeLock  = new object();

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        Sr = GetComponent<SpriteRenderer>();
    }

    void Start() => ApplyPhysics();

    // ─────────────────────────────────────────────────────────────
    #region Initialisation

    public void Initialise(PlanetData planetData)
    {
        data = planetData;
        ApplyPhysics();
        ApplyVisuals();
    }

    void ApplyPhysics()
    {
        if (data == null) return;

        Rb.mass                   = data.mass;
        Rb.linearDamping          = data.drag;
        Rb.angularDamping         = data.angularDrag;
        Rb.gravityScale           = 0f;
        Rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Rb.interpolation          = RigidbodyInterpolation2D.Interpolate;

        var col            = GetComponent<CircleCollider2D>();
        col.radius         = 0.5f;
        col.sharedMaterial = new PhysicsMaterial2D("PM")
            { bounciness = data.bounciness, friction = 0.25f };
    }

    void ApplyVisuals()
    {
        if (data == null) return;
        float d = data.radius * 2f;
        transform.localScale = new Vector3(d, d, 1f);
        Sr.color = data.primaryColor;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Launch Control

    /// <summary>Freeze in place at the spawn point while waiting to be shot.</summary>
    public void Freeze()
    {
        Rb.bodyType        = RigidbodyType2D.Kinematic;
        Rb.linearVelocity  = Vector2.zero;
        Rb.angularVelocity = 0f;
    }

    public void Unfreeze() => Rb.bodyType = RigidbodyType2D.Dynamic;

    /// <summary>Called by SlingshotController on release.</summary>
    public void Launch(Vector2 velocity)
    {
        IsLaunched        = true;
        Rb.bodyType       = RigidbodyType2D.Dynamic;
        Rb.gravityScale   = 0f;
        Rb.linearVelocity = velocity;

        GravityManager.Instance?.RegisterPlanet(this);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Merge Detection

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!IsLaunched || data == null) return;

        var other = col.gameObject.GetComponent<Planet>();
        if (other == null || !other.IsLaunched || other.data == null) return;

        // Warning pulse: if either planet is touching the warning ring, fire the alert.
        CheckWarningPulse(other);

        if (other.data.tier != data.tier) return;

        // Two Saturns touching → Win (no nextTier to merge into)
        if (data.nextTier == null)
        {
            TryTriggerWin(other);
            return;
        }

        TryScheduleMerge(other);
    }

    void CheckWarningPulse(Planet other)
    {
        if (GravityManager.Instance == null || GravityZoneVisual.Instance == null) return;

        float warnR   = GravityManager.Instance.WarningRingRadius;
        Vector2 bhPos = GravityManager.Instance.transform.position;

        float myDist    = Vector2.Distance(transform.position, bhPos);
        float otherDist = Vector2.Distance(other.transform.position, bhPos);

        bool myNear    = myDist    + (data      != null ? data.radius      : 0f) >= warnR;
        bool otherNear = otherDist + (other.data != null ? other.data.radius : 0f) >= warnR;

        if (myNear || otherNear)
            GravityZoneVisual.Instance.EmitWarningPulse();
    }

    void TryScheduleMerge(Planet other)
    {
        if (data.nextTier == null) return;

        int myId    = GetInstanceID();
        int otherId = other.GetInstanceID();

        lock (s_mergeLock)
        {
            if (s_mergingIds.Contains(myId) || s_mergingIds.Contains(otherId)) return;
            s_mergingIds.Add(myId);
            s_mergingIds.Add(otherId);
        }

        IsMerging       = true;
        other.IsMerging = true;

        Vector2 midpoint         = ((Vector2)transform.position + (Vector2)other.transform.position) * 0.5f;
        Vector2 combinedVelocity = (Rb.linearVelocity + other.Rb.linearVelocity) * 0.5f;

        MergeManager.Instance.ScheduleMerge(this, other, midpoint, combinedVelocity);
    }

    void TryTriggerWin(Planet other)
    {
        int myId    = GetInstanceID();
        int otherId = other.GetInstanceID();

        lock (s_mergeLock)
        {
            if (s_mergingIds.Contains(myId) || s_mergingIds.Contains(otherId)) return;
            s_mergingIds.Add(myId);
            s_mergingIds.Add(otherId);
        }

        GameManager.Instance?.TriggerWin();
    }

    public static void ClearMergeState() => s_mergingIds.Clear();

    #endregion

    // ─────────────────────────────────────────────────────────────
    void OnDestroy()
    {
        s_mergingIds.Remove(GetInstanceID());
        GravityManager.Instance?.UnregisterPlanet(this);
    }
}
