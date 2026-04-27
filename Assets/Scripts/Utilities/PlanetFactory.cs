using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton factory — creates and configures planet GameObjects at runtime.
/// Generates a gradient circle Texture2D per tier and caches it so no
/// external sprite assets are needed.
/// </summary>
public class PlanetFactory : MonoBehaviour
{
    public static PlanetFactory Instance { get; private set; }

    [Header("Texture Quality")]
    [Tooltip("Pixel resolution of generated planet sprites (power of two recommended)")]
    public int spriteResolution = 128;

    private readonly Dictionary<int, Sprite> _spriteCache = new Dictionary<int, Sprite>();

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// Instantiates a planet at <paramref name="position"/>.
    /// Starts frozen (Kinematic) — call <see cref="Planet.Launch"/> when ready.
    /// </summary>
    public Planet SpawnPlanet(PlanetData data, Vector2 position)
    {
        var go = new GameObject(data.planetName);
        go.transform.position = position;

        var sr           = go.AddComponent<SpriteRenderer>();
        sr.sprite        = GetOrCreateSprite(data);
        sr.sortingOrder  = 2;
        sr.material      = new Material(Shader.Find("Sprites/Default"));

        var col          = go.AddComponent<CircleCollider2D>();
        col.radius       = 0.5f;

        var rb                        = go.AddComponent<Rigidbody2D>();
        rb.bodyType                   = RigidbodyType2D.Kinematic;
        rb.gravityScale               = 0f;
        rb.collisionDetectionMode     = CollisionDetectionMode2D.Continuous;
        rb.interpolation              = RigidbodyInterpolation2D.Interpolate;
        rb.constraints                = RigidbodyConstraints2D.FreezeRotation;

        var planet = go.AddComponent<Planet>();
        planet.Initialise(data);

        return planet;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Sprite Generation

    Sprite GetOrCreateSprite(PlanetData data)
    {
        if (_spriteCache.TryGetValue(data.tier, out Sprite cached)) return cached;

        Sprite s = GeneratePlanetSprite(data.primaryColor, data.glowColor, spriteResolution);
        _spriteCache[data.tier] = s;
        return s;
    }

    /// <summary>
    /// Generates a radial-gradient circle with:
    ///   • Bright glow centre fading to dark edge
    ///   • Top-left specular highlight
    ///   • Outer glow ring
    ///   • Anti-aliased border
    /// </summary>
    static Sprite GeneratePlanetSprite(Color primary, Color glow, int res)
    {
        var tex        = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        float center      = res * 0.5f;
        float outerRadius = center - 1f;
        Color darkEdge    = primary * 0.35f;

        Vector2 specPos = new Vector2(center - outerRadius * 0.3f, center + outerRadius * 0.35f);
        float   specRad = outerRadius * 0.22f;

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float dx   = x - center, dy = y - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);

            if (dist > outerRadius + 1f) { tex.SetPixel(x, y, Color.clear); continue; }

            float edgeAlpha   = Mathf.Clamp01(outerRadius - dist + 1f);
            float t           = dist / outerRadius;
            Color baseColor   = Color.Lerp(glow * 1.2f, darkEdge, t * t);

            // Specular highlight (top-left)
            float sdx   = x - specPos.x, sdy = y - specPos.y;
            float spec  = Mathf.Clamp01(1f - Mathf.Sqrt(sdx*sdx + sdy*sdy) / specRad) * 0.45f;
            baseColor   = Color.Lerp(baseColor, Color.white, spec);

            // Outer glow border ring
            float border = Mathf.Clamp01((t - 0.85f) / 0.15f);
            baseColor    = Color.Lerp(baseColor, new Color(glow.r, glow.g, glow.b, 0.6f), border * 0.4f);

            tex.SetPixel(x, y, new Color(baseColor.r, baseColor.g, baseColor.b, edgeAlpha));
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f), res);
    }

    #endregion
}
