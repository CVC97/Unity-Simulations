using UnityEngine;


// The bob's trace on the floor, like the sand trace of the classic experiment: a flat ribbon through turning points
// (spaced by the controller, one per 1.5° of turning) plus a live piece from the newest one to the bob. Older lines
// fade out, so the trace is a fading fan of lines behind the turning plane and shows its sense of rotation.
// Sits on the Trace object, a child of the Hall just above the platform (it turns with the floor in the star view);
// its local x-z plane is the floor plan, Unity (x, z) = model (East, North). Vertex colours carry the fade: the material
// must use them (URP Particles/Unlit, like the Kreisel's trail).
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TraceDisplay : MonoBehaviour {
    [SerializeField] private int capacity = 120;                                // points kept: 120 × 1.5° = half a turn, a full star
    [SerializeField] private float width = 0.012f;                              // m
    [SerializeField] private Color color = new Color(0.86f, 0.87f, 0.9f, 1f);   // the Kreisel's precession light grey
    [SerializeField, Range(0f, 1f)] private float newestAlpha = 0.85f;
    [SerializeField] private float fadePower = 1.5f;                            // > 1: the old lines fade sooner

    private Vector2[] points;                                                   // ring buffer, floor plan, m
    private int start, count;
    private Vector2 live;
    private bool dirty;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color32[] colors;
    private int[] indices;
    private bool indicesSet;


    void Awake() {
        points = new Vector2[capacity];
        int segments = capacity;                                                // capacity − 1 old ones + the live one
        vertices = new Vector3[4 * segments];
        colors = new Color32[4 * segments];
        indices = new int[6 * segments];
        // the same pattern for every quad: facing up (Unity: clockwise seen from the front)
        for (int s = 0; s < segments; s++) {
            int v = 4 * s, t = 6 * s;
            indices[t] = v; indices[t + 1] = v + 1; indices[t + 2] = v + 2;
            indices[t + 3] = v; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
        }
        mesh = new Mesh { name = "Trace" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }


    public void Clear() {
        start = count = 0;
        dirty = true;
    }

    public void AddPoint(Vector2 p) {
        if (count < capacity) {
            points[(start + count) % capacity] = p;
            count++;
        } else {
            points[start] = p;                                                  // overwrite the oldest
            start = (start + 1) % capacity;
        }
        dirty = true;
    }

    public void SetLivePoint(Vector2 p) {
        if ((p - live).sqrMagnitude > 1e-6f) {
            live = p;
            dirty = true;
        }
    }


    void LateUpdate() {
        if (!dirty) return;
        dirty = false;
        int segments = 0;
        // oldest first; segment k joins point k and k + 1, the last one joins the newest point and the bob
        for (int k = 0; k < count; k++) {
            Vector2 a = points[(start + k) % capacity];
            Vector2 b = k + 1 < count ? points[(start + k + 1) % capacity] : live;
            float age = (float)(count - 1 - k) / capacity;                      // 0 = newest
            float alpha = newestAlpha * Mathf.Pow(1f - age, fadePower);
            Quad(segments++, a, b, alpha);
        }
        // the unused rest: degenerate (zero-area) quads, so the index buffer can stay the same size
        for (int i = 4 * segments; i < vertices.Length; i++) vertices[i] = Vector3.zero;

        mesh.vertices = vertices;
        mesh.colors32 = colors;
        if (!indicesSet) {                                                     // after the vertices: indices must fit them
            mesh.triangles = indices;
            indicesSet = true;
        }
        mesh.RecalculateBounds();
    }


    private void Quad(int segment, Vector2 a, Vector2 b, float alpha) {
        Vector2 d = b - a;
        Vector2 side = d.sqrMagnitude > 1e-12f ? new Vector2(-d.y, d.x).normalized * (width / 2) : Vector2.zero;
        int v = 4 * segment;
        vertices[v] = new Vector3(a.x - side.x, 0f, a.y - side.y);
        vertices[v + 1] = new Vector3(a.x + side.x, 0f, a.y + side.y);
        vertices[v + 2] = new Vector3(b.x + side.x, 0f, b.y + side.y);
        vertices[v + 3] = new Vector3(b.x - side.x, 0f, b.y - side.y);
        Color32 c = new Color(color.r, color.g, color.b, alpha);
        colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = c;
    }
}
