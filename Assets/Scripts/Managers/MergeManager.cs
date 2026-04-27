using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Executes planet merges safely across physics frames.
///
/// Planet.cs schedules a merge here from OnCollisionEnter2D.
/// MergeManager processes it next Update so both collision callbacks
/// have completed before any Destroy calls happen.
/// </summary>
public class MergeManager : MonoBehaviour
{
    public static MergeManager Instance { get; private set; }

    private struct MergeRequest
    {
        public Planet  PlanetA;
        public Planet  PlanetB;
        public Vector2 Position;
        public Vector2 Velocity;
    }

    private readonly Queue<MergeRequest> _pending = new Queue<MergeRequest>();

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        while (_pending.Count > 0)
        {
            var req = _pending.Dequeue();
            if (req.PlanetA != null && req.PlanetB != null)
                ExecuteMerge(req);
        }
    }

    // ─────────────────────────────────────────────────────────────
    #region Public API

    public void ScheduleMerge(Planet a, Planet b, Vector2 position, Vector2 velocity)
    {
        _pending.Enqueue(new MergeRequest
        {
            PlanetA  = a,
            PlanetB  = b,
            Position = position,
            Velocity = velocity
        });
    }

    #endregion

    // ─────────────────────────────────────────────────────────────
    #region Merge Execution

    void ExecuteMerge(MergeRequest req)
    {
        PlanetData nextData = req.PlanetA.data.nextTier;
        Color      color    = req.PlanetA.data.primaryColor;

        GravityManager.Instance?.UnregisterPlanet(req.PlanetA);
        GravityManager.Instance?.UnregisterPlanet(req.PlanetB);

        ScoreManager.Instance?.AddScore(req.PlanetA.data.scoreOnMerge);

        Destroy(req.PlanetA.gameObject);
        Destroy(req.PlanetB.gameObject);

        if (nextData != null)
        {
            Planet spawned = PlanetFactory.Instance.SpawnPlanet(nextData, req.Position);
            spawned.Launch(req.Velocity);
        }

        StartCoroutine(FlashRing(req.Position, color));
    }

    /// <summary>Expanding ring burst at merge point — no prefab required.</summary>
    IEnumerator FlashRing(Vector2 pos, Color color)
    {
        var go = new GameObject("MergeFlash");
        go.transform.position = pos;

        var lr           = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop          = true;
        lr.startWidth    = 0.05f;
        lr.endWidth      = 0.05f;
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.positionCount = 32;

        const float duration  = 0.35f;
        const float maxRadius = 1.4f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t      = elapsed / duration;
            float radius = t * maxRadius;
            float alpha  = 1f - t;

            Color c = new Color(color.r, color.g, color.b, alpha);
            lr.startColor = c;
            lr.endColor   = c;

            for (int i = 0; i < 32; i++)
            {
                float angle = (float)i / 32 * Mathf.PI * 2f;
                lr.SetPosition(i, pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            yield return null;
        }

        Destroy(go);
    }

    #endregion
}
