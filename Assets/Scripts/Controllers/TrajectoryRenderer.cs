using UnityEngine;

/// <summary>
/// Renders a curved trajectory preview that accounts for RADIAL gravity
/// toward the black hole — NOT simple parabolic downward gravity.
///
/// Uses Euler-integration simulation so the arc curves correctly toward
/// the black hole center, matching actual in-game physics.
///
/// Attach alongside SlingshotController on the same Manager GameObject.
/// </summary>
public class TrajectoryRenderer : MonoBehaviour
{
    [Header("Dots")]
    public Color  dotColor      = new Color(0.02f, 0.90f, 1f, 0.85f);
    public float  dotRadius     = 0.035f;
    public int    dotCount      = 16;
    [Tooltip("Simulation steps between each visible dot")]
    public int    stepsPerDot   = 2;
    [Tooltip("Simulation dt per step (seconds)")]
    public float  timeStep      = 0.018f;

    // ── Pool ──────────────────────────────────────────────────────
    private GameObject[]     _dots;
    private SpriteRenderer[] _dotSrs;
    private Sprite           _circleSp;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        // Force values — public fields get overridden by Unity's serialized
        // scene cache, so we set them here to guarantee the right behaviour.
        dotCount    = 16;
        stepsPerDot = 2;
        dotRadius   = 0.035f;

        _circleSp = MakeCircle(32);
        BuildPool();
        Hide();
    }

    // ─────────────────────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// Simulate the planet's trajectory under radial black-hole gravity
    /// and place dot sprites along the resulting arc.
    /// </summary>
    public void Show(Vector2 startPos, Vector2 velocity)
    {
        // Gravity source
        Vector2 bhPos = GravityManager.Instance != null
            ? (Vector2)GravityManager.Instance.transform.position
            : Vector2.zero;
        float G = GravityManager.Instance != null
            ? GravityManager.Instance.gravityConstant
            : 80f;

        // Euler integration — simulate forward in time
        Vector2 pos = startPos;
        Vector2 vel = velocity;

        int placed = 0;
        int totalSteps = dotCount * stepsPerDot;

        for (int step = 0; step < totalSteps && placed < _dots.Length; step++)
        {
            // Apply radial gravity
            Vector2 toCenter = bhPos - pos;
            float   dist     = Mathf.Max(toCenter.magnitude, 0.15f);
            float   accel    = G / dist;  // F = G/d, mass normalised to 1
            vel += toCenter.normalized * accel * timeStep;
            pos += vel * timeStep;

            // Place a dot every stepsPerDot simulation steps
            if (step % stepsPerDot == 0)
            {
                float t     = (float)placed / _dots.Length;
                float alpha = Mathf.Lerp(0.90f, 0.10f, t);

                _dots[placed].transform.position = pos;
                _dots[placed].SetActive(true);
                _dotSrs[placed].color = new Color(dotColor.r, dotColor.g, dotColor.b, alpha);
                placed++;
            }
        }

        // Hide leftover dots
        for (int k = placed; k < _dots.Length; k++)
            _dots[k].SetActive(false);
    }

    public void Hide()
    {
        if (_dots == null) return;
        foreach (var d in _dots) d.SetActive(false);
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Pool

    void BuildPool()
    {
        _dots   = new GameObject[dotCount];
        _dotSrs = new SpriteRenderer[dotCount];

        for (int i = 0; i < dotCount; i++)
        {
            _dots[i]              = new GameObject($"TDot{i}");
            _dots[i].transform.SetParent(transform, false);
            _dotSrs[i]            = _dots[i].AddComponent<SpriteRenderer>();
            _dotSrs[i].sprite     = _circleSp;
            _dotSrs[i].color      = dotColor;
            _dotSrs[i].sortingOrder = 10;
            float d               = dotRadius * 2f;
            _dots[i].transform.localScale = new Vector3(d, d, 1f);
        }

        // no arrow
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Procedural Sprites

    static Sprite MakeCircle(int res)
    {
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = res * 0.5f, r = c - 1f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01(r - d + 1f);
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,res,res), Vector2.one*0.5f, res);
    }


    #endregion
}
