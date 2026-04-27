using UnityEngine;

/// <summary>
/// Generates a deep-space gradient background at runtime.
/// Gradient: top-left Indigo → centre Purple → bottom-right near-black
///
/// Attach to any persistent GameObject (e.g. Managers).
/// Requires no sprite assets.
/// </summary>
public class BackgroundManager : MonoBehaviour
{
    [Header("Depth (push behind everything)")]
    public int sortingOrder = -100;

    // Deep cosmic colours matching the "Cosmic Glass" redesign
    // Centre: oklch deep indigo #1A1438  Edge: near-void #010108
    static readonly Color ColorTopLeft     = new Color(0.031f, 0.016f, 0.165f, 1f); // #08042A
    static readonly Color ColorCentre      = new Color(0.102f, 0.078f, 0.220f, 1f); // #1A1438
    static readonly Color ColorBottomRight = new Color(0.004f, 0.004f, 0.031f, 1f); // #010108

    void Start()
    {
        // Camera matches the deepest background tone — #05030F
        if (Camera.main != null)
        {
            Camera.main.clearFlags      = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.020f, 0.012f, 0.059f, 1f);
        }
        Build();
    }

    void Build()
    {
        // 4-corner gradient via a mesh with per-vertex colours
        GameObject go = new GameObject("Background");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0f, 0f, 10f); // behind camera

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        // Use the URP 2D UNLIT mesh shader — immune to Global Light 2D tinting.
        // Falls back to Sprites/Default if not found (non-URP projects).
        var unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                       ?? Shader.Find("Sprites/Default");
        var mat       = new Material(unlitShader);
        mat.color     = Color.white;
        mr.material   = mat;
        mr.sortingOrder = sortingOrder;
        mr.renderingLayerMask = 0;

        // Make it large enough to cover any resolution
        float hw = 30f, hh = 20f;

        var mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-hw,  hh, 0f), // top-left     0
            new Vector3( hw,  hh, 0f), // top-right    1
            new Vector3( hw, -hh, 0f), // bottom-right 2
            new Vector3(-hw, -hh, 0f), // bottom-left  3
        };
        mesh.triangles = new int[] { 0, 1, 2,  0, 2, 3 };
        mesh.uv = new Vector2[]
        {
            new Vector2(0,1), new Vector2(1,1),
            new Vector2(1,0), new Vector2(0,0)
        };

        // Per-vertex colours — bias toward the rich indigo centre
        Color topLeft  = Color.Lerp(ColorTopLeft,  ColorCentre,      0.55f);
        Color topRight = Color.Lerp(ColorCentre,   ColorBottomRight, 0.50f);
        Color botRight = ColorBottomRight;
        Color botLeft  = Color.Lerp(ColorTopLeft,  ColorBottomRight, 0.30f);

        mesh.colors = new Color[] { topLeft, topRight, botRight, botLeft };
        mesh.RecalculateNormals();
        mf.mesh = mesh;

        // ── Star field ────────────────────────────────────────────
        BuildStars(go.transform);
    }

    void BuildStars(Transform parent)
    {
        // Scatter small white dots using a secondary mesh
        int starCount = 180;
        var verts  = new Vector3[starCount * 4];
        var tris   = new int[starCount * 6];
        var uvs    = new Vector2[starCount * 4];
        var cols   = new Color[starCount * 4];

        float hw = 28f, hh = 18f;

        for (int i = 0; i < starCount; i++)
        {
            float x    = Random.Range(-hw, hw);
            float y    = Random.Range(-hh, hh);
            float s    = Random.Range(0.02f, 0.10f);
            float bright = Random.Range(0.4f, 1.0f);
            Color col  = new Color(1f, 1f, 1f, bright * Random.Range(0.5f, 1f));

            int vi = i * 4;
            verts[vi+0] = new Vector3(x-s,  y+s, 0f);
            verts[vi+1] = new Vector3(x+s,  y+s, 0f);
            verts[vi+2] = new Vector3(x+s,  y-s, 0f);
            verts[vi+3] = new Vector3(x-s,  y-s, 0f);

            uvs[vi+0] = new Vector2(0,1); uvs[vi+1] = new Vector2(1,1);
            uvs[vi+2] = new Vector2(1,0); uvs[vi+3] = new Vector2(0,0);

            cols[vi+0] = cols[vi+1] = cols[vi+2] = cols[vi+3] = col;

            int ti = i * 6;
            tris[ti+0] = vi; tris[ti+1] = vi+1; tris[ti+2] = vi+2;
            tris[ti+3] = vi; tris[ti+4] = vi+2; tris[ti+5] = vi+3;
        }

        var mesh      = new Mesh();
        mesh.vertices = verts;
        mesh.triangles= tris;
        mesh.uv       = uvs;
        mesh.colors   = cols;
        mesh.RecalculateNormals();

        var go        = new GameObject("Stars");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.1f);

        go.AddComponent<MeshFilter>().mesh = mesh;
        var mr        = go.AddComponent<MeshRenderer>();
        var unlitShader2 = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                        ?? Shader.Find("Sprites/Default");
        mr.material   = new Material(unlitShader2);
        mr.sortingOrder = sortingOrder + 1;
        mr.renderingLayerMask = 0; // exclude from Global Light 2D
    }
}
