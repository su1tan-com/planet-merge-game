using System.Collections;
using UnityEngine;

/// <summary>
/// Black Hole visual + physics surface.
///
/// Visual layers (built procedurally — NO rotating rings):
///   ScreenGlow → very faint full-screen radial gradient (gravity-well feel)
///   Halo       → soft radial glow around the core (dark purple)
///   Core       → solid dark sphere with a crisp glowing edge ring
///   EdgeRing   → thin bright outline at exactly the collision radius
///   EventShimmer → animated bright flicker at the horizon edge
///
/// Physics:
///   A solid CircleCollider2D (radius = coreRadius, NOT trigger) lives on
///   "BlackHoleBody" so planets physically land on and stack around the surface.
/// </summary>
public class BlackHoleController : MonoBehaviour
{
    public static BlackHoleController Instance { get; private set; }

    [Header("Core Size")]
    public float coreRadius = 0.66f;

    [Header("Edge Shimmer")]
    public float shimmerSpeed = 2.5f;

    [Header("Win Sequence")]
    public float winDuration  = 1.8f;
    public float winMaxScale  = 10f;
    public float winSuckForce = 250f;

    // Animated edge ring reference
    private SpriteRenderer _edgeRingSr;
    private static readonly Color EdgeBase  = new Color(0.72f, 0.18f, 1.00f, 1.00f); // vivid purple
    private static readonly Color EdgeShimmer = new Color(0.90f, 0.60f, 1.00f, 1.00f); // bright lavender

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildVisuals();
        BuildPhysicsBody();
    }

    /// <summary>
    /// Destroys and rebuilds only the visuals owned by this script.
    /// GravityManager and GravityZoneVisual children (BoundaryVisual, GravityParticles,
    /// FieldLines, GravityGradient) are left intact so the gravity zone stays visible.
    /// </summary>
    public void ResetVisuals()
    {
        // Only remove children this script builds — leave GravityManager's visuals alone.
        foreach (string cn in new[] { "BlackHoleBody", "Halo", "Core", "EdgeRing" })
        {
            var c = transform.Find(cn);
            if (c) Destroy(c.gameObject);
        }

        _edgeRingSr = null;

        BuildVisuals();
        BuildPhysicsBody();
    }

    void Update()
    {
        AnimateEdge();
    }

    // ─────────────────────────────────────────────────────────────
    #region Physics Body

    void BuildPhysicsBody()
    {
        foreach (var old in GetComponents<CircleCollider2D>())
            Destroy(old);

        var body = new GameObject("BlackHoleBody");
        body.transform.SetParent(transform, false);

        var col       = body.AddComponent<CircleCollider2D>();
        col.radius    = coreRadius;
        col.isTrigger = false;

        var mat       = new PhysicsMaterial2D("BHMat") { bounciness = 0.2f, friction = 0.4f };
        col.sharedMaterial = mat;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Win Sequence

    public IEnumerator PlayWinSequence()
    {
        Planet[] all  = FindObjectsByType<Planet>(FindObjectsSortMode.None);
        Transform coreT = transform.Find("Core");

        float elapsed = 0f;
        while (elapsed < winDuration)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / winDuration;

            if (coreT)
                coreT.localScale = Vector3.one * (coreRadius * 2f) * Mathf.Lerp(1f, winMaxScale, t);

            foreach (Planet p in all)
            {
                if (p == null) continue;
                Vector2 dir = (Vector2)transform.position - (Vector2)p.transform.position;
                p.Rb.AddForce(dir.normalized * winSuckForce * Mathf.Lerp(1f, 8f, t));
                float shrink = Mathf.Clamp01(dir.magnitude / 3f);
                p.transform.localScale *= Mathf.Lerp(0.96f, 1f, shrink);
            }
            yield return null;
        }

        foreach (Planet p in all)
            if (p != null) Destroy(p.gameObject);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Visual Construction

    void BuildVisuals()
    {
        BuildHalo();
        BuildCore();
        BuildEdgeRing();
    }

    // ── Very faint dark halo — gravity-well glow around the core ──
    void BuildHalo()
    {
        int res = 256; float c = res * 0.5f;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float t = Mathf.Clamp01(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c);
            // Dark purple glow, falls off quickly — purely subtle atmosphere
            float a = Mathf.Pow(1f - t, 2.8f) * 0.40f;
            Color col = Color.Lerp(new Color(0.40f, 0.05f, 0.70f),
                                   new Color(0.05f, 0.00f, 0.12f), t);
            tex.SetPixel(x, y, new Color(col.r, col.g, col.b, a));
        }
        tex.Apply();

        var go = MakeChild("Halo", tex, 0);
        float sz = coreRadius * 7f;
        go.transform.localScale = new Vector3(sz * 2f, sz * 2f, 1f);
    }

    // ── Solid sphere — black void with surface texture, clearly circular ──
    void BuildCore()
    {
        int res = 256; float c = res * 0.5f;
        float rOut = c - 1f;

        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float dx = x - c, dy = y - c;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);

            if (d > rOut + 1f) { tex.SetPixel(x, y, Color.clear); continue; }

            float edgeAlpha = Mathf.Clamp01(rOut - d + 1f);
            float t         = d / rOut; // 0 = centre, 1 = edge

            // Deep void with very subtle blue-purple centre depth
            Color col;
            if (t < 0.75f)
            {
                // Deep black void — tiny blue tint at centre for depth
                float cen = Mathf.Pow(1f - t / 0.75f, 3f) * 0.12f;
                col = new Color(cen * 0.3f, 0f, cen * 0.6f);
            }
            else
            {
                // Outer 25%: thin surface gradient toward the glowing edge
                float rim = (t - 0.75f) / 0.25f;
                col = Color.Lerp(new Color(0.02f, 0f, 0.05f),
                                 new Color(0.55f, 0.08f, 0.90f), rim * rim);
            }

            tex.SetPixel(x, y, new Color(col.r, col.g, col.b, edgeAlpha));
        }
        tex.Apply();

        var go = MakeChild("Core", tex, 4);
        float d2 = coreRadius * 2f;
        go.transform.localScale = new Vector3(d2, d2, 1f);
    }

    // ── Crisp bright ring exactly at the collision surface edge ──
    // This makes the "target circle" 100% visually legible.
    void BuildEdgeRing()
    {
        // Ring = thin annulus from 88% to 100% of core radius
        int res = 128; float c = res * 0.5f;
        float rOut = c - 1f;
        float rIn  = rOut * 0.82f;

        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));

            if (d > rOut + 1f || d < rIn - 1f) { tex.SetPixel(x, y, Color.clear); continue; }

            // Smooth outer and inner edges
            float fo = Mathf.Clamp01(rOut - d + 1f);
            float fi = Mathf.Clamp01(d - rIn + 1f);
            float a  = Mathf.Min(fo, fi);

            // Bright peak in the middle of the ring width
            float mid = 1f - Mathf.Abs((d - (rIn + rOut) * 0.5f)) / ((rOut - rIn) * 0.5f);
            a *= Mathf.Clamp01(mid * 1.8f);

            tex.SetPixel(x, y, new Color(EdgeBase.r, EdgeBase.g, EdgeBase.b, a));
        }
        tex.Apply();

        var go = MakeChild("EdgeRing", tex, 5);
        float d2 = coreRadius * 2f;
        go.transform.localScale = new Vector3(d2, d2, 1f);
        _edgeRingSr = go.GetComponent<SpriteRenderer>();
    }

    // ─────────────────────────────────────────────────────────────
    // Gentle edge shimmer — the ring pulses brightness to signal it's alive
    void AnimateEdge()
    {
        if (_edgeRingSr == null) return;
        float t = (Mathf.Sin(Time.time * shimmerSpeed) + 1f) * 0.5f;
        _edgeRingSr.color = Color.Lerp(EdgeBase, EdgeShimmer, t);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Helpers

    GameObject MakeChild(string childName, Texture2D tex, int sortOrder)
    {
        var go  = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var sr  = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width);
        sr.material     = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = sortOrder;
        return go;
    }

    #endregion
}
