using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual companion to GravityManager — three distinct effects:
///
///   1. PULSING DASHED BOUNDARY — animates the cyan dashed circle built by
///      GravityManager, chasing-light phase shift per dash.
///
///   2. SCREEN-WIDE GRAVITY GRADIENT — a large semi-transparent radial gradient
///      mesh (dark at screen edges, slightly lighter near the black hole) that
///      reinforces the sense that gravity pulls from EVERYWHERE toward the centre.
///
///   3. GRAVITY PARTICLES — space-dust dots that drift from the screen edges
///      continuously toward the black hole, visualising omnidirectional gravity.
///      They respawn at a random position on the outer boundary when they arrive.
///
///   4. RADIAL FIELD LINES — faint inward-pointing lines showing gravity direction.
///
/// Attach to the BlackHole GameObject (alongside GravityManager and
/// BlackHoleController). No Inspector references required.
/// </summary>
public class GravityZoneVisual : MonoBehaviour
{
    public static GravityZoneVisual Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────
    [Header("Boundary Pulse")]
    public float pulseSpeed      = 2.2f;
    public float pulseMinAlpha   = 0.45f;
    public float pulseMaxAlpha   = 1.00f;
    public float pulseWidthRange = 0.025f;
    public float baseWidth       = 0.05f;

    [Header("Screen-Wide Gravity Gradient")]
    [Tooltip("Enable the large radial gradient across the screen")]
    public bool  showGravityGradient = true;
    [Tooltip("Radius of the gradient mesh — set to cover the full screen diagonally")]
    public float gradientRadius      = 14f;
    [Tooltip("Peak alpha at the outer edge of the gradient (centre is transparent)")]
    public float gradientEdgeAlpha   = 0.30f;
    [Tooltip("Color of the gravity gradient — dark void pull")]
    public Color gradientColor       = new Color(0.04f, 0.00f, 0.10f, 1f);

    [Header("Gravity Particles (space dust)")]
    public int   particleCount    = 120;
    [Tooltip("Particles spawn at this distance from BH center (use a large value to match screen edges)")]
    public float spawnRadius      = 10f;
    public float despawnRadius    = 0.80f;
    public float driftSpeed       = 1.8f;
    public float particleSize     = 0.06f;
    public float particleMinAlpha = 0.08f;
    public float particleMaxAlpha = 0.50f;
    public Color particleColor    = new Color(0.60f, 0.85f, 1.00f, 1f);

    [Header("Radial Field Lines")]
    public bool  showFieldLines  = true;
    public int   fieldLineCount  = 20;
    public float fieldLineInner  = 0.80f;
    public float fieldLineOuter  = 4.00f;
    public Color fieldLineColor  = new Color(0.50f, 0.20f, 0.90f, 0.10f);
    public float fieldLineWidth  = 0.018f;

    [Header("Warning Pulse")]
    public Color warningPulseColor    = new Color(1.00f, 0.25f, 0.10f, 0.75f);
    public float warningPulseDuration = 0.75f;
    public int   warningPulseSegs     = 48;
    public float warningPulseWidth    = 0.07f;

    // ── Runtime ───────────────────────────────────────────────────
    private List<LineRenderer> _boundaryDashes = new List<LineRenderer>();

    private struct Particle
    {
        public Transform      T;
        public SpriteRenderer Sr;
        public float          Speed;
        public float          Alpha;
        public float          Phase;
    }
    private Particle[] _particles;
    private Sprite     _dotSprite;

    private List<LineRenderer> _fieldLines = new List<LineRenderer>();

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        _dotSprite = MakeDotSprite();
        FindBoundaryDashes();
        if (showGravityGradient) BuildGravityGradient();
        BuildParticles();
        if (showFieldLines) BuildFieldLines();
    }

    // ─────────────────────────────────────────────────────────────
    #region Warning Pulse

    /// <summary>
    /// Emit a semi-transparent ring that shrinks inward from the warning ring — signals
    /// that a planet near the zone edge just collided with another planet.
    /// </summary>
    public void EmitWarningPulse()
    {
        if (GravityManager.Instance == null) return;
        StartCoroutine(WarningPulseCoroutine(GravityManager.Instance.WarningRingRadius));
    }

    IEnumerator WarningPulseCoroutine(float startRadius)
    {
        var go = new GameObject("WarningPulse");
        go.transform.SetParent(transform, false);

        var lr            = go.AddComponent<LineRenderer>();
        lr.useWorldSpace  = true;
        lr.loop           = true;
        lr.positionCount  = warningPulseSegs;
        lr.startWidth     = warningPulseWidth;
        lr.endWidth       = warningPulseWidth;
        lr.material       = new Material(Shader.Find("Sprites/Default"));
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.sortingOrder   = 6;

        Vector2 centre  = transform.position;
        float   endR    = GravityManager.Instance != null
                          ? GravityManager.Instance.ZoneRadius * 0.08f
                          : startRadius * 0.1f;
        float   elapsed = 0f;

        while (elapsed < warningPulseDuration)
        {
            elapsed += Time.deltaTime;
            float t      = elapsed / warningPulseDuration;
            float radius = Mathf.Lerp(startRadius, endR, t);
            float alpha  = warningPulseColor.a * (1f - t);

            Color c = new Color(warningPulseColor.r, warningPulseColor.g, warningPulseColor.b, alpha);
            lr.startColor = c;
            lr.endColor   = c;

            for (int i = 0; i < warningPulseSegs; i++)
            {
                float ang = (float)i / warningPulseSegs * Mathf.PI * 2f;
                lr.SetPosition(i, centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius);
            }

            yield return null;
        }

        Destroy(go);
    }

    #endregion

    void Update()
    {
        PulseBoundary();
        UpdateParticles();
        PulseFieldLines();
    }

    // ─────────────────────────────────────────────────────────────
    #region Boundary Pulse

    void FindBoundaryDashes()
    {
        _boundaryDashes.Clear();
        Transform container = transform.Find("BoundaryVisual");
        if (container == null) return;

        // Only collect the outer boundary dashes (named "OuterD…").
        // Warning ring dashes ("WarnD…") keep their static red colour.
        foreach (Transform child in container)
        {
            if (!child.name.StartsWith("Outer")) continue;
            var lr = child.GetComponent<LineRenderer>();
            if (lr) _boundaryDashes.Add(lr);
        }
    }

    void PulseBoundary()
    {
        if (_boundaryDashes.Count == 0) return;

        float t     = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);
        float width = baseWidth + Mathf.Sin(Time.time * pulseSpeed * 0.7f) * pulseWidthRange;

        int n = _boundaryDashes.Count;
        for (int i = 0; i < n; i++)
        {
            float phase  = (float)i / n * Mathf.PI * 2f;
            float iAlpha = Mathf.Clamp01(alpha + 0.2f * Mathf.Sin(Time.time * pulseSpeed + phase));
            Color c = new Color(0.02f, 0.85f, 1f, iAlpha);
            _boundaryDashes[i].startColor = c;
            _boundaryDashes[i].endColor   = c;
            _boundaryDashes[i].startWidth = width;
            _boundaryDashes[i].endWidth   = width;
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Screen-Wide Gravity Gradient

    /// <summary>
    /// Builds a large radial gradient disc: transparent at centre, darkening
    /// toward the edges — reinforcing the gravity-well effect across the whole screen.
    /// </summary>
    void BuildGravityGradient()
    {
        int rings    = 24;   // number of concentric rings
        int segments = 64;   // polygon segments per ring

        int vertCount = rings * segments;
        var verts  = new Vector3[vertCount + 1]; // +1 for centre
        var cols   = new Color[vertCount + 1];
        var uvs    = new Vector2[vertCount + 1];

        // Centre vertex — fully transparent (gravity pull comes from outside)
        verts[0] = Vector3.zero;
        cols[0]  = new Color(gradientColor.r, gradientColor.g, gradientColor.b, 0f);
        uvs[0]   = Vector2.one * 0.5f;

        for (int ring = 0; ring < rings; ring++)
        {
            float frac  = (float)(ring + 1) / rings; // 0..1 from centre outward
            float r     = frac * gradientRadius;
            // Alpha increases toward the outer edge (dark vignette feel)
            float alpha = Mathf.Pow(frac, 1.5f) * gradientEdgeAlpha;
            Color c     = new Color(gradientColor.r, gradientColor.g, gradientColor.b, alpha);

            for (int s = 0; s < segments; s++)
            {
                float ang = (float)s / segments * Mathf.PI * 2f;
                int   vi  = 1 + ring * segments + s;
                verts[vi] = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
                cols[vi]  = c;
                uvs[vi]   = new Vector2(0.5f + Mathf.Cos(ang) * 0.5f * frac,
                                        0.5f + Mathf.Sin(ang) * 0.5f * frac);
            }
        }

        // Triangles: fan from centre for ring 0, quads between consecutive rings
        int triCount = segments                            // centre fan
                     + (rings - 1) * segments * 2;        // ring quads
        var tris = new int[triCount * 3];
        int ti = 0;

        // Centre fan (ring 0)
        for (int s = 0; s < segments; s++)
        {
            int a = 1 + s;
            int b = 1 + (s + 1) % segments;
            tris[ti++] = 0; tris[ti++] = a; tris[ti++] = b;
        }

        // Quad strips for rings 1..rings-1
        for (int ring = 1; ring < rings; ring++)
        {
            int baseOuter = 1 + ring * segments;
            int baseInner = 1 + (ring - 1) * segments;
            for (int s = 0; s < segments; s++)
            {
                int sn = (s + 1) % segments;
                int o0 = baseOuter + s,  o1 = baseOuter + sn;
                int i0 = baseInner + s,  i1 = baseInner + sn;
                tris[ti++] = i0; tris[ti++] = o0; tris[ti++] = i1;
                tris[ti++] = i1; tris[ti++] = o0; tris[ti++] = o1;
            }
        }

        var mesh = new Mesh();
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.uv        = uvs;
        mesh.colors    = cols;
        mesh.RecalculateNormals();

        var go = new GameObject("GravityGradient");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.5f); // slightly behind core but in front of bg

        go.AddComponent<MeshFilter>().mesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.material     = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
        mr.sortingOrder = -50;
        mr.renderingLayerMask = 0;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Gravity Particles

    void BuildParticles()
    {
        _particles = new Particle[particleCount];
        var parent = new GameObject("GravityParticles");
        parent.transform.SetParent(transform, false);

        for (int i = 0; i < particleCount; i++)
        {
            var go          = new GameObject($"P{i}");
            go.transform.SetParent(parent.transform, false);

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _dotSprite;
            sr.sortingOrder = 1;
            sr.material     = new Material(Shader.Find("Sprites/Default"));

            float alpha = Random.Range(particleMinAlpha, particleMaxAlpha);
            sr.color = new Color(particleColor.r, particleColor.g, particleColor.b, alpha);

            float sz = particleSize * Random.Range(0.5f, 1.6f);
            go.transform.localScale = new Vector3(sz, sz, 1f);

            _particles[i] = new Particle
            {
                T     = go.transform,
                Sr    = sr,
                Speed = Random.Range(0.6f, 1.4f),
                Alpha = alpha,
                Phase = Random.Range(0f, Mathf.PI * 2f)
            };

            // Start scattered at random depths so they don't all arrive together
            RespawnParticle(ref _particles[i], randomizeDepth: true);
        }
    }

    void UpdateParticles()
    {
        Vector2 bhPos = transform.position;

        for (int i = 0; i < _particles.Length; i++)
        {
            ref Particle p = ref _particles[i];

            Vector2 pos = p.T.position;
            Vector2 dir = bhPos - pos;
            float dist  = dir.magnitude;

            if (dist < despawnRadius)
            {
                RespawnParticle(ref p, randomizeDepth: false);
                continue;
            }

            // Speed increases as particle gets closer (gravity-like acceleration)
            float speed = driftSpeed * p.Speed * (spawnRadius / Mathf.Max(dist, 0.5f)) * 0.30f;
            p.T.position = (Vector2)p.T.position + dir.normalized * speed * Time.deltaTime;

            // Twinkle + fade out near the centre
            float distFade = Mathf.Clamp01((dist - despawnRadius) / 1.5f);
            float twinkle  = 0.75f + 0.25f * Mathf.Sin(Time.time * 1.8f + p.Phase);
            float a = p.Alpha * twinkle * distFade;
            p.Sr.color = new Color(particleColor.r, particleColor.g, particleColor.b, a);
        }
    }

    void RespawnParticle(ref Particle p, bool randomizeDepth)
    {
        float angle  = Random.Range(0f, Mathf.PI * 2f);
        float radius = randomizeDepth
            ? Random.Range(despawnRadius + 0.5f, spawnRadius)
            : spawnRadius * Random.Range(0.88f, 1.0f);

        Vector2 bhPos = transform.position;
        p.T.position  = bhPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        p.Phase       = Random.Range(0f, Mathf.PI * 2f);
        p.Alpha       = Random.Range(particleMinAlpha, particleMaxAlpha);
        float sz      = particleSize * Random.Range(0.5f, 1.6f);
        p.T.localScale = new Vector3(sz, sz, 1f);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Field Lines

    void BuildFieldLines()
    {
        var parent = new GameObject("FieldLines");
        parent.transform.SetParent(transform, false);

        float angleStep = 360f / fieldLineCount;
        for (int i = 0; i < fieldLineCount; i++)
        {
            float   angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 dir   = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 outer = (Vector2)transform.position + (Vector2)dir * fieldLineOuter;
            Vector3 inner = (Vector2)transform.position + (Vector2)dir * fieldLineInner;

            var go = new GameObject($"FL{i}");
            go.transform.SetParent(parent.transform, false);

            var lr           = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, outer);   // outer → inner (inward direction)
            lr.SetPosition(1, inner);
            lr.startWidth    = fieldLineWidth * 0.5f;
            lr.endWidth      = fieldLineWidth * 2.5f; // wider near BH = stronger force
            lr.startColor    = new Color(fieldLineColor.r, fieldLineColor.g, fieldLineColor.b, 0f);
            lr.endColor      = fieldLineColor;
            lr.material      = new Material(Shader.Find("Sprites/Default"));
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
            lr.sortingOrder      = 0;
            _fieldLines.Add(lr);
        }
    }

    void PulseFieldLines()
    {
        if (_fieldLines.Count == 0) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.2f);
        float a     = fieldLineColor.a * Mathf.Lerp(0.4f, 1.1f, pulse);
        Color c     = new Color(fieldLineColor.r, fieldLineColor.g, fieldLineColor.b, a);
        foreach (var lr in _fieldLines)
            lr.endColor = c;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Helpers

    static Sprite MakeDotSprite()
    {
        int res = 16; float c = res * .5f, r = c - 1f;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float a = Mathf.Clamp01(r - Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) + 1f);
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * .5f, res);
    }

    #endregion
}
