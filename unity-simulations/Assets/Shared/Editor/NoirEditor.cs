using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;


// Editor helpers every simulation's scene setup can use (taken over from the precession repo's ModuleSceneSetup):
// finding objects, 3D labels in the noir style, the smooth cylinder mesh.
public static class NoirEditor {
    public const string SharedFolder = "Assets/Shared";
    public const string LabelMaterialPath = "Assets/Shared/Materials/VectorLabel.mat";       // black halo
    private const string CylinderMeshPath = "Assets/Shared/Meshes/SmoothCylinder.asset";


    // ---- the shared apparatus materials: every simulation uses these, so the collection looks like one (Carlo,
    // 2026-11-25). Values tuned on the Kreisel's screenshots (design notes, section 4). A scene that needs something
    // else (the Kreisel's lacquer and disk, ...) keeps it in its own folder; the noir difference between scenes comes
    // from the light, not the materials.

    public const string SharedMaterialFolder = "Assets/Shared/Materials";

    public static Dictionary<string, Material> LectureMaterials() {
        return new Dictionary<string, Material> {
            // metals partly diffuse, so they show their shape in the spot and not only in the (dim) reflections
            ["LectureSteel"] = Lit("LectureSteel", new Color32(215, 217, 222, 255), 0.8f, 0.72f),
            // glossy, since the Kreisel's rim light reaches no upward face; the white balls of its first versions were
            // the rim light mirrored in horizontal tops, not the gloss itself
            ["LectureChrome"] = Lit("LectureChrome", new Color32(235, 236, 240, 255), 0.95f, 0.85f),
            ["LectureDarkSteel"] = Lit("LectureDarkSteel", new Color32(125, 128, 135, 255), 0.75f, 0.62f),
            ["LectureCastIron"] = Lit("LectureCastIron", new Color32(105, 107, 113, 255), 0.55f, 0.45f),
            // rough: seen at a grazing angle against the spot, a smoother floor's Fresnel highlight bloomed
            ["LectureFloor"] = Lit("LectureFloor", new Color(0.2f, 0.2f, 0.2f), 0f, 0.08f),
            // a large surface in the light that should stay black, so only what lies on it catches the light (the
            // Foucault platform, Carlo 2026-11-25: with the floor's grey the pool read as a grey disk). Neutral, no
            // metal, almost no gloss: anything more mirrors the studio cubemap as a grey or bluish sheen
            ["LectureBlack"] = Lit("LectureBlack", new Color32(12, 12, 12, 255), 0f, 0.05f),
        };
    }

    // URP/Lit with colour, metallic and smoothness, no emission; created in the shared folder or updated in place
    private static Material Lit(string name, Color color, float metallic, float smoothness) {
        string path = $"{SharedMaterialFolder}/{name}.mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(mat);
        return mat;
    }


    // "Root/Child/Grandchild"; unlike GameObject.Find it also finds inactive objects
    public static Transform FindInScene(Scene scene, string path) {
        string[] parts = path.Split('/', 2);
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == parts[0]) {
                return parts.Length == 1 ? root.transform : root.transform.Find(parts[1]);
            }
        }
        return null;
    }


    // symbol with a small arrow above, via TextMeshPro rich text: step back over the letter, raise a small "→"
    public static string VectorSymbol(string symbol, float arrowHeightEm) {
        return $"<i>{symbol}</i><space=-0.55em><voffset={arrowHeightEm:0.##}em><size=55%>→</size></voffset>";
    }


    public static TextMeshPro Label(Transform parent, string name, string text, Color color, Material material) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = 0.5f;                                                    // 3D text: about 0.1 m per 1 pt
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(0.2f, 0.1f);
        tmp.fontSharedMaterial = material;
        return tmp;
    }


    // drawn on top of everything, with a soft halo behind the glyphs (underlay); an outline would grow into the thin
    // italic strokes. Noir: black halo (a white one would swallow the white ω⃗ on the black stage).
    public static Material LabelOverlayMaterial(string path, Color halo) {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) {
            mat = new Material(TMP_Settings.defaultFontAsset.material);
            mat.shader = Shader.Find("TextMeshPro/Distance Field Overlay");
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.DisableKeyword("OUTLINE_ON");
        mat.SetFloat("_OutlineWidth", 0f);
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", halo);
        mat.SetFloat("_UnderlayOffsetX", 0f);
        mat.SetFloat("_UnderlayOffsetY", 0f);
        mat.SetFloat("_UnderlayDilate", 0.6f);
        mat.SetFloat("_UnderlaySoftness", 0.35f);
        mat.SetFloat("_FaceDilate", 0.1f);                                      // slightly stronger strokes
        EditorUtility.SetDirty(mat);
        return mat;
    }


    // like Unity's cylinder primitive (radius 0.5, y from −1 to 1, so all scales stay valid), but with 96 sides;
    // smooth normals on the side, flat caps with their own vertices (sharp rim edge). Saved once as a mesh asset.
    public static Mesh SmoothCylinder() {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CylinderMeshPath);
        if (mesh != null) {
            return mesh;
        }
        const int segments = 96;
        const float r = 0.5f;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        // side: bottom/top vertex pairs with radial normals (front faces wind so that cross(B−A, C−A) points outwards)
        for (int i = 0; i <= segments; i++) {
            float a = 2*Mathf.PI/segments * i;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vertices.Add(new Vector3(r*radial.x, -1f, r*radial.z));
            vertices.Add(new Vector3(r*radial.x, 1f, r*radial.z));
            normals.Add(radial);
            normals.Add(radial);
        }
        for (int i = 0; i < segments; i++) {
            int b = 2*i;                                                            // bottom_i, top_i, bottom_i+1, top_i+1
            triangles.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
        }

        // caps: centre + ring, normal ±y
        foreach (float y in new[] { 1f, -1f }) {
            int centre = vertices.Count;
            vertices.Add(new Vector3(0f, y, 0f));
            normals.Add(new Vector3(0f, y, 0f));
            for (int i = 0; i <= segments; i++) {
                float a = 2*Mathf.PI/segments * i;
                vertices.Add(new Vector3(r*Mathf.Cos(a), y, r*Mathf.Sin(a)));
                normals.Add(new Vector3(0f, y, 0f));
            }
            for (int i = 0; i < segments; i++) {
                int ring = centre + 1 + i;
                if (y > 0) triangles.AddRange(new[] { centre, ring + 1, ring });
                else triangles.AddRange(new[] { centre, ring, ring + 1 });
            }
        }

        mesh = new Mesh { name = "SmoothCylinder" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, CylinderMeshPath);
        return mesh;
    }
}
