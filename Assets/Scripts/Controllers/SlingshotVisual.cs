using UnityEngine;

/// <summary>
/// Two slingshot visual aids built entirely in code:
///
///   1. SPAWN CIRCLE — a thin static dotted ring drawn around the spawn point.
///      Its radius is fixed (slightly larger than Earth) so the player always
///      knows the default drop zone regardless of which planet is loaded.
///
///   2. PULL LINE — a thin line from the spawn point to the planet while the
///      player is dragging, giving a clear sense of pull direction and magnitude.
///
/// Attach to the same GameObject as SlingshotController.
/// </summary>
public class SlingshotVisual : MonoBehaviour
{
    [Header("Spawn Circle")]
    [Tooltip("World-space radius of the dotted spawn indicator (just bigger than Earth = 0.52)")]
    public float spawnCircleRadius   = 0.68f;
    public int   spawnCircleSegments = 64;
    [Range(0.01f, 0.9f)]
    public float spawnCircleDash     = 0.14f;  // fraction of segment that is drawn
    public Color spawnCircleColor    = new Color(1f, 1f, 1f, 0.22f);
    public float spawnCircleWidth    = 0.018f;

    [Header("Pull Line")]
    public Color pullLineColorNear = new Color(1f, 1f, 1f, 0.65f);  // close to spawn
    public Color pullLineColorFar  = new Color(1f, 0.4f, 0.2f, 0.85f); // near max pull
    public float pullLineWidth     = 0.035f;
    public float pullLineTipWidth  = 0.010f;

    // ── Runtime ───────────────────────────────────────────────────
    private LineRenderer _pullLR;
    private Transform    _spawnPt;

    // ─────────────────────────────────────────────────────────────
    void Start()
    {
        // Give SlingshotController one frame to Awake first
        _spawnPt = SlingshotController.Instance != null
            ? SlingshotController.Instance.spawnPoint
            : null;

        if (_spawnPt != null) BuildSpawnCircle();
        BuildPullLine();
    }

    void Update()
    {
        if (_pullLR == null || SlingshotController.Instance == null) return;

        bool dragging = SlingshotController.Instance.IsDragging;
        _pullLR.enabled = dragging && SlingshotController.Instance.HasPlanetLoaded;

        if (_pullLR.enabled)
        {
            Vector2 spawn  = _spawnPt != null ? (Vector2)_spawnPt.position : Vector2.zero;
            Vector2 planet = SlingshotController.Instance.CurrentPlanetPos;

            _pullLR.SetPosition(0, spawn);
            _pullLR.SetPosition(1, planet);

            // Colour shifts toward orange-red as the drag approaches max radius
            float t = 0f;
            if (SlingshotController.Instance != null)
            {
                float drag    = Vector2.Distance(spawn, planet);
                float maxDrag = SlingshotController.Instance.maxDragRadius;
                t = Mathf.Clamp01(drag / maxDrag);
            }

            _pullLR.startColor = Color.Lerp(pullLineColorNear, pullLineColorFar, t);
            _pullLR.endColor   = new Color(
                _pullLR.startColor.r, _pullLR.startColor.g,
                _pullLR.startColor.b, _pullLR.startColor.a * 0.15f);
        }
    }

    // ─────────────────────────────────────────────────────────────
    #region Build

    void BuildSpawnCircle()
    {
        var container = new GameObject("SpawnCircle");
        container.transform.SetParent(transform, false);

        float angleStep = 360f / spawnCircleSegments;
        float dashAngle = angleStep * spawnCircleDash;

        Vector2 centre = _spawnPt.position;

        for (int i = 0; i < spawnCircleSegments; i++)
        {
            float a0 = i * angleStep;
            float a1 = a0 + dashAngle;

            var dash = new GameObject($"SD{i}");
            dash.transform.SetParent(container.transform, false);

            var lr               = dash.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.positionCount     = 2;
            lr.startWidth        = spawnCircleWidth;
            lr.endWidth          = spawnCircleWidth;
            lr.startColor        = spawnCircleColor;
            lr.endColor          = spawnCircleColor;
            lr.material          = new Material(Shader.Find("Sprites/Default"))
                                       { color = spawnCircleColor };
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
            lr.sortingOrder      = 10;

            lr.SetPosition(0, centre + CirclePt(a0, spawnCircleRadius));
            lr.SetPosition(1, centre + CirclePt(a1, spawnCircleRadius));
        }
    }

    void BuildPullLine()
    {
        var go = new GameObject("PullLine");
        go.transform.SetParent(transform, false);

        _pullLR               = go.AddComponent<LineRenderer>();
        _pullLR.useWorldSpace = true;
        _pullLR.positionCount = 2;
        _pullLR.startWidth    = pullLineWidth;
        _pullLR.endWidth      = pullLineTipWidth;
        _pullLR.startColor    = pullLineColorNear;
        _pullLR.endColor      = new Color(pullLineColorNear.r, pullLineColorNear.g,
                                          pullLineColorNear.b, 0.10f);
        _pullLR.material          = new Material(Shader.Find("Sprites/Default"));
        _pullLR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _pullLR.receiveShadows    = false;
        _pullLR.sortingOrder      = 10;
        _pullLR.enabled           = false;
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    static Vector2 CirclePt(float deg, float r)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r);
    }
}
