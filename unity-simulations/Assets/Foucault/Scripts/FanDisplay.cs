using UnityEngine;


// At high Zeitraffer the bob swings several times per frame and would only flicker (aliasing). Instead the swept swing
// plane is drawn: a faint fan from the suspension down to the bob's arc, glowing towards the arc, plus the arc as a
// line. It fades in with the factor (FoucaultController.FanAmount), the bob hides at half, and it turns with the
// measured plane. Not a helper: it is how the motion is drawn, so it ignores the Hilfen switch.
// Sits on the Fan object, a child of the Pendulum (origin = suspension); the mesh lies in its local x-y plane, apex at
// the origin, UV v from 0 (apex) to 1 (arc) for the material's alpha ramp; additive, both faces.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FanDisplay : MonoBehaviour {
    [SerializeField] private FoucaultController controller;
    [SerializeField] private LineRenderer arc;                                  // on a child, local space
    [SerializeField] private int segments = 48;

    private MeshRenderer surface;
    private Color surfaceColor, arcColor;
    private MaterialPropertyBlock block;
    private float builtFor = -1f;                                               // the half-width the mesh was built for


    void Awake() {
        surface = GetComponent<MeshRenderer>();
        surfaceColor = surface.sharedMaterial.GetColor("_BaseColor");
        arcColor = arc.sharedMaterial.GetColor("_BaseColor");
        block = new MaterialPropertyBlock();
        arc.useWorldSpace = false;
        arc.positionCount = segments + 1;
    }


    void LateUpdate() {
        if (controller.Model == null) return;
        float amount = controller.FanAmount;
        bool visible = amount > 0.001f;
        surface.enabled = visible;
        arc.enabled = visible;
        if (!visible) return;

        float halfWidth = (float)controller.SwingMajor;
        if (Mathf.Abs(halfWidth - builtFor) > 0.005f) Build(halfWidth);
        transform.localRotation = Quaternion.AngleAxis(PhysicsToUnity.Angle(controller.PlaneAzimuth), Vector3.up);

        block.SetColor("_BaseColor", surfaceColor * amount);                   // additive: darker = fainter
        surface.SetPropertyBlock(block);
        block.SetColor("_BaseColor", arcColor * amount);
        arc.SetPropertyBlock(block);
    }


    private void Build(float halfWidth) {
        builtFor = halfWidth;
        float length = controller.Length;
        float beta = Mathf.Asin(Mathf.Clamp(halfWidth / length, 0f, 0.99f));
        var vertices = new Vector3[segments + 2];                               // 0 = apex, then the arc
        var uv = new Vector2[segments + 2];
        var triangles = new int[3 * segments];
        var arcPoints = new Vector3[segments + 1];
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0f);
        for (int i = 0; i <= segments; i++) {
            float b = Mathf.Lerp(-beta, beta, (float)i / segments);
            arcPoints[i] = new Vector3(length * Mathf.Sin(b), -length * Mathf.Cos(b), 0f);
            vertices[i + 1] = arcPoints[i];
            uv[i + 1] = new Vector2((float)i / segments, 1f);
            if (i < segments) {
                triangles[3 * i] = 0;
                triangles[3 * i + 1] = i + 1;
                triangles[3 * i + 2] = i + 2;
            }
        }
        var mesh = new Mesh { name = "Fan", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        arc.SetPositions(arcPoints);
    }
}
