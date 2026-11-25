using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;


// The lecture gyroscope (Vorlesungskreisel), noir. The scene GyroscopeScene.unity is the source: it was made once by
// editor script in the precession repo (ilias-precession-simulation, commit 5f36b9b: a copy of its noir toy-top scene
// with the lecture apparatus built from primitives) and moved here on 2026-11-20. The steps below stay rerunnable and
// overwrite manual changes to what they touch, so tune the values here:
//   Gyroscope → 1 Apply look        materials, disk picture, pedestal (rebuilt), lights, reflections, snow, camera,
//                                   arrow and label sizes
//   Gyroscope → 2 Add helpers       switchboard, precession cone and the three nutation cones as light cones
//   Gyroscope → 3 Use web bridge    GyroscopeBridge on the WebBridge object
//   Gyroscope → Set up everything   all three in order (also GyroscopeSceneSetup.SetUpFromCommandLine in batch mode)
// Local up of the Gyroscope object is the figure axis (rod, disk side); it sits exactly on the tip of the pin.
public static class GyroscopeSceneSetup {
    private const string Folder = "Assets/Gyroscope";
    private const string ScenePath = "Assets/Gyroscope/GyroscopeScene.unity";
    private const string AssetPath = "Assets/Gyroscope/LectureGyroscope.asset";
    private const string MaterialFolder = "Assets/Gyroscope/Materials";
    private const string TextureFolder = "Assets/Gyroscope/Textures";
    private const string SharedMaterialFolder = "Assets/Shared/Materials";
    // Carlo's own picture for the disk (square, the disk is the inscribed circle); without it a generated placeholder
    private const string DiskArtPath = "Assets/Gyroscope/Textures/DiskArt.png";
    private const string DiskArtPlaceholderPath = "Assets/Gyroscope/Textures/DiskArtPlaceholder.png";

    // stage: the tip of the pin, m above the floor (a tall pedestal: the lecture top stands well above the table)
    private const float PivotHeight = 0.80f;
    // the reflections for the metal (a small studio cubemap, dimmed): without any, steel and chrome mirror the black
    // stage and look black (there is no baked lighting with a reflection probe)
    private const string ReflectionCubemapPath = "Assets/Shared/Textures/studio_reflection.hdr";
    // a short spike on top of the hub: the trail, the arrows and the precession cone's rim are at its tip
    private const float SpikeLength = 0.06f;
    // the scale between the original toy top (camera 0.5 m) and this apparatus (camera 2 m): lights, snow, labels
    private const float SceneScale = 4f;

    // noir colours: only the original's (L yellow, ω white, D red) and greys; precession light grey (Carlo)
    private static readonly Color32 Yellow = new Color32(255, 235, 4, 255);
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 LightGrey = new Color32(220, 223, 230, 255);
    private static readonly Color32 Grey = new Color32(110, 112, 118, 255);         // Nutationskegel: clearly darker than the white Gangpolkegel


    [MenuItem("Gyroscope/1 Apply look")]
    private static void LookMenu() => Run(ApplyLook);

    [MenuItem("Gyroscope/2 Add helpers")]
    private static void HelpersMenu() => Run(AddHelpers);

    [MenuItem("Gyroscope/3 Use web bridge")]
    private static void BridgeMenu() => Run(UseWebBridge);

    [MenuItem("Gyroscope/Set up everything")]
    private static void SetUpMenu() => SetUpEverything();


    // Unity -batchmode -nographics -projectPath unity-simulations -executeMethod GyroscopeSceneSetup.SetUpFromCommandLine -logFile setup.log
    public static void SetUpFromCommandLine() {
        EditorApplication.Exit(SetUpEverything() ? 0 : 1);
    }


    private static bool SetUpEverything() {
        if (!EditorApplication.isPlaying && SceneManager.GetActiveScene().path != ScenePath) {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        return Run(ApplyLook) && Run(AddHelpers) && Run(UseWebBridge);
    }


    // runs a step on the open gyroscope scene and saves it
    private static bool Run(System.Func<Scene, bool> step) {
        if (EditorApplication.isPlaying) {                                      // scene changes would be lost on exit
            Fail("Leave Play mode first: in Play mode the scene can't be saved.");
            return false;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) {
            Fail($"Open {ScenePath} first.");
            return false;
        }
        if (!step(scene)) return false;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }


    private static void Fail(string message) {
        if (Application.isBatchMode) Debug.LogError("GyroscopeSceneSetup: " + message);
        else EditorUtility.DisplayDialog("Gyroscope scene", message, "OK");
    }


    // the fixed part: a heavy lacquered pedestal with chrome edges, a tall column, the thin pin with the tip on top;
    // rebuilt by step 1, so its size can be tuned (the pivot height moves the gyroscope with it)
    private static void BuildPedestal(Transform pedestal) {
        for (int i = pedestal.childCount - 1; i >= 0; i--) Object.DestroyImmediate(pedestal.GetChild(i).gameObject);
        float columnTop = PivotHeight - 0.25f;                                  // the pin stands 25 cm above the column
        Cylinder("Foot", pedestal, 0f, 0.56f, 0f, 0.03f, "LectureLacquer");
        Cylinder("FootEdge", pedestal, 0f, 0.566f, 0.026f, 0.031f, "LectureChrome");
        Cylinder("Step", pedestal, 0f, 0.40f, 0.03f, 0.055f, "LectureLacquer");
        Cylinder("StepEdge", pedestal, 0f, 0.405f, 0.052f, 0.056f, "LectureChrome");
        Cylinder("Column", pedestal, 0f, 0.07f, 0.055f, columnTop, "LectureLacquer");
        Cylinder("ColumnRing", pedestal, 0f, 0.078f, 0.12f, 0.128f, "LectureChrome");
        Cylinder("Collar", pedestal, 0f, 0.085f, columnTop - 0.006f, columnTop + 0.006f, "LectureChrome");
        Cylinder("Pin", pedestal, 0f, 0.014f, columnTop + 0.006f, PivotHeight - 0.004f, "LectureChrome");
        Transform pinTip = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        Object.DestroyImmediate(pinTip.GetComponent<Collider>());
        pinTip.name = "PinTip";
        pinTip.SetParent(pedestal, false);
        pinTip.localPosition = new Vector3(0f, PivotHeight - 0.004f, 0f);
        pinTip.localScale = Vector3.one * 0.014f;
    }


    // ---- 1 ----

    private static bool ApplyLook(Scene scene) {
        Transform gyroscope = FindInScene(scene, "Gyroscope");
        Transform rig = gyroscope != null ? gyroscope.Find("LectureRig") : null;
        if (rig == null) {
            Fail("The scene has no Gyroscope/LectureRig.");
            return false;
        }
        LectureGyroscope p = LectureAsset();
        gyroscope.position = new Vector3(0f, PivotHeight, 0f);
        BuildPedestal(FindInScene(scene, "LecturePedestal"));
        Vector3 pivot = gyroscope.position;
        float h = p.DiskHeight;

        // apparatus: black-and-white metal, lacquer, rubber; the disk's picture; colour only for the red marker
        // the shared metals (Assets/Shared/Materials, every simulation uses them) and the Kreisel's own parts
        Dictionary<string, Material> materials = NoirEditor.LectureMaterials();
        materials["LectureLacquer"] = Lit("LectureLacquer", new Color32(34, 34, 38, 255), 0f, 0.75f);      // glossy black lacquer
        materials["LectureRubber"] = Lit("LectureRubber", new Color32(50, 50, 52, 255), 0f, 0.25f);
        materials["LectureDiskEdge"] = Lit("LectureDiskEdge", new Color32(190, 192, 198, 255), 0.8f, 0.65f);
        Material floorMaterial = materials["LectureFloor"];
        materials.Remove("LectureFloor");                                      // the floor isn't a part of the apparatus
        Material art = Lit("LectureDiskArt", Color.white, 0.15f, 0.55f);
        art.SetTexture("_BaseMap", DiskArt());
        EditorUtility.SetDirty(art);
        Mesh cylinder = NoirEditor.SmoothCylinder();
        foreach (Transform root in new[] { rig, FindInScene(scene, "LecturePedestal") }) {
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true)) {
                string part = r.name;
                if (part == "DiskFace") r.sharedMaterial = art;
                else if (part == "Marker") r.sharedMaterial = Shared("Marker");
                else if (part == "Ring" || part.StartsWith("Leg")) r.sharedMaterial = materials["LectureCastIron"];
                else if (part == "PinTip") r.sharedMaterial = materials["LectureChrome"];
                else if (r.sharedMaterial == null || !materials.ContainsValue(r.sharedMaterial)) r.sharedMaterial = materials["LectureSteel"];
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                var filter = r.GetComponent<MeshFilter>();
                if (filter.sharedMesh != null && filter.sharedMesh.name == "Cylinder") filter.sharedMesh = cylinder;
            }
        }
        // the parts got their materials by name when built (Cylinder): reapply the current versions
        foreach (MeshRenderer r in rig.GetComponentsInChildren<MeshRenderer>(true)) {
            if (r.sharedMaterial != null && materials.TryGetValue(r.sharedMaterial.name, out Material m)) r.sharedMaterial = m;
        }

        // trail: the original's grey, wider for the bigger scene, long enough for one slow precession
        TrailRenderer trail = rig.GetComponentInChildren<TrailRenderer>(true);
        trail.widthMultiplier = 0.002f * SceneScale;
        trail.minVertexDistance = 0.004f;
        trail.time = 12f;
        trail.numCornerVertices = 3;
        trail.numCapVertices = 2;
        trail.emitting = true;

        // arrows (the original's emissive materials): thicker for the bigger scene; drawn from the spike's tip
        foreach (VectorArrow3D arrow in gyroscope.GetComponentsInChildren<VectorArrow3D>(true)) {
            var a = new SerializedObject(arrow);
            a.FindProperty("shaftDiameter").floatValue = 0.002f * SceneScale;
            a.FindProperty("headDiameter").floatValue = 0.006f * SceneScale;
            a.FindProperty("headLength").floatValue = 0.012f * SceneScale;
            a.ApplyModifiedPropertiesWithoutUndo();
        }
        // L ≈ 1.3 kg·m²/s at 300 rpm → 0.36 m; ω ≈ 31 rad/s → 0.25 m; D ≈ 0.23 N·m (hanging, 15°) → 0.18 m
        var scales = new Vector3(0.28f, 0.008f, 0.8f);
        var vectors = new SerializedObject(gyroscope.GetComponent<VectorDisplay>());
        vectors.FindProperty("scaleL").floatValue = scales.x;
        vectors.FindProperty("scaleOmega").floatValue = scales.y;
        vectors.FindProperty("scaleD").floatValue = scales.z;
        vectors.FindProperty("anchorHeight").floatValue = h + SpikeLength;
        vectors.ApplyModifiedPropertiesWithoutUndo();
        var rigSettings = new SerializedObject(rig.GetComponent<ApparatusRig>());
        rigSettings.FindProperty("vectorScales").vector3Value = scales;
        rigSettings.FindProperty("coneLength").floatValue = h + SpikeLength;              // the precession cone's rim at the tip
        rigSettings.ApplyModifiedPropertiesWithoutUndo();

        // labels: the original's (black halo), at the same size on screen with the camera 4 × farther away
        Transform labels = FindInScene(scene, "VectorLabels");
        if (labels != null) {
            foreach (VectorLabel label in labels.GetComponentsInChildren<VectorLabel>(true)) {
                label.GetComponent<TextMeshPro>().fontSize = 0.23f * SceneScale;
                label.GetComponent<TextMeshPro>().UpdateMeshPadding();
                var l = new SerializedObject(label);
                bool beside = label.name == "LabelOmega";                       // ω⃗ beside its tip, as in the original
                l.FindProperty("offset").floatValue = beside ? 0f : 0.016f * SceneScale;
                l.FindProperty("sideOffset").floatValue = beside ? 0.02f * SceneScale : 0f;
                l.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // the spot pool: the original's key light, moved and scaled with the scene (same angle, 1/r² intensity)
        Light key = FindInScene(scene, "KeyLight").GetComponent<Light>();
        Vector3 aim = pivot + Vector3.up * 0.05f;                              // the whole apparatus, pedestal included
        key.transform.position = pivot + new Vector3(0.3f, 0.45f, -0.1f) * SceneScale * 1.1f;
        key.transform.LookAt(aim);
        key.range = 2f * SceneScale * 1.2f;
        key.intensity = 2f * SceneScale * SceneScale * 1.2f;
        key.spotAngle = 50f;
        key.innerSpotAngle = 20f;

        // rim light (a directional light on the camera, the original's): it came from behind the apparatus 20° from
        // above, so at low camera angles every horizontal top (foot, collar, floor) mirrored it straight into the
        // bloom. From just below the horizon it still rims the rod, legs and column, but no upward face can catch it.
        Transform rim = FindInScene(scene, "Main Camera/RimLight");
        if (rim != null) {
            rim.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.06f, -1f));   // towards the camera, slightly up
            rim.GetComponent<Light>().intensity = 2.5f;
        }

        // the noir stays dark, but not pitch black: a little more ambient than the original (0.04) and dim studio
        // reflections, so the structure keeps its edges outside the spot's centre
        RenderSettings.skybox = null;                                          // never visible: the camera clears to black
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.095f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionCubemapPath);
        RenderSettings.reflectionIntensity = 0.55f;

        // floor large enough for the bigger pool; snow scaled as a whole (it simulates in local space: emitter, fall,
        // flake size and noise scale together; the fall time and the number of flakes stay the original's)
        Transform floor = FindInScene(scene, "Floor");
        floor.localScale = new Vector3(2f * SceneScale, 0.001f, 2f * SceneScale);
        // shared floor material, rougher than the original's (0.3): seen at a grazing angle against the spot, the
        // Fresnel highlight of the original floor bloomed into a white ball around the pedestal's foot
        floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
        float snowScale = (pivot.y + h + 1.4f) / 0.8f;                         // the original snows from 0.8 m
        foreach (string name in new[] { "Snow", "Snow (fast)" }) {
            Transform snow = FindInScene(scene, name);
            if (snow == null) continue;
            snow.position = new Vector3(0f, 0.8f * snowScale, 0f);
            snow.localScale = (name == "Snow" ? new Vector3(0.75f, 1f, 0.75f) : Vector3.one) * snowScale;
        }

        // camera: the original's orbit around the pivot, looking at the middle of the apparatus
        var orbit = new SerializedObject(FindInScene(scene, "Main Camera").GetComponent<OrbitCamera>());
        orbit.FindProperty("targetOffset").vector3Value = new Vector3(0f, 0.02f, 0f);   // apparatus spans 0 … 1.6 m
        orbit.FindProperty("pitch").floatValue = 24f;
        orbit.FindProperty("yaw").floatValue = 0f;
        orbit.FindProperty("distance").floatValue = 2.9f;
        orbit.FindProperty("minDistance").floatValue = 0.9f;
        orbit.FindProperty("maxDistance").floatValue = 5f;
        orbit.FindProperty("minPitch").floatValue = 4f;                        // not at floor level: grazing views only show glare
        orbit.ApplyModifiedPropertiesWithoutUndo();
        Camera cam = FindInScene(scene, "Main Camera").GetComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 30f;

        Debug.Log("Gyroscope look applied.");
        return true;
    }


    // ---- 2 ----

    private static bool AddHelpers(Scene scene) {
        Transform gyroscope = FindInScene(scene, "Gyroscope");
        Transform rig = gyroscope != null ? gyroscope.Find("LectureRig") : null;
        Transform labels = FindInScene(scene, "VectorLabels");
        if (rig == null || labels == null) {
            Fail("The scene needs Gyroscope/LectureRig and VectorLabels.");
            return false;
        }
        Transform old = FindInScene(scene, "Helpers");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform oldLabel = labels.Find("LabelPrecession");
        if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);

        var controller = gyroscope.GetComponent<GyroscopeController>();
        float tipLength = LectureAsset().DiskHeight + SpikeLength;
        var helpersObject = new GameObject("Helpers");
        SceneManager.MoveGameObjectToScene(helpersObject, scene);
        Transform helpers = helpersObject.transform;

        // precession: the cone the rod sweeps around the vertical, rim at the spike's tip, ω_P label beside it
        Transform precession = Empty("Precession", helpers, Vector3.zero);
        HelperCone precessionCone = LightCone(precession, "Cone", "Precession", LightGrey, 0.1f);
        TextMeshPro label = NoirEditor.Label(labels, "LabelPrecession", "<i>ω</i><sub><i>P</i></sub>", LightGrey,
                                                   AssetDatabase.LoadAssetAtPath<Material>(NoirEditor.LabelMaterialPath));
        label.fontSize = 0.23f * SceneScale;
        label.fontStyle = FontStyles.Bold;
        label.UpdateMeshPadding();
        var display = new SerializedObject(precession.gameObject.AddComponent<PrecessionDisplay>());
        display.FindProperty("controller").objectReferenceValue = controller;
        display.FindProperty("cone").objectReferenceValue = precessionCone;
        display.FindProperty("label").objectReferenceValue = label;
        display.FindProperty("smoothingSeconds").floatValue = 3f;              // nutation ≈ 1 s, precession ≈ 10 s
        display.FindProperty("labelOffset").floatValue = 0.12f;
        display.ApplyModifiedPropertiesWithoutUndo();

        // the three cones of the balanced top (colours: what they contain or turn around; provisional, Carlo decides):
        // Nutationskegel grey (the rod), Rastpolkegel yellow (around L⃗), Gangpolkegel white (ω⃗, around the rod)
        Transform nutation = Empty("Nutation", helpers, Vector3.zero);
        HelperCone nutationCone = LightCone(nutation, "NutationCone", "Nutation", Grey, 0.14f);
        HelperCone spaceCone = LightCone(nutation, "SpaceCone", "Space", Yellow, 0.14f);
        HelperCone bodyCone = LightCone(nutation, "BodyCone", "Body", White, 0.1f);
        var cones = new SerializedObject(nutation.gameObject.AddComponent<NutationCones>());
        cones.FindProperty("controller").objectReferenceValue = controller;
        cones.FindProperty("nutationCone").objectReferenceValue = nutationCone;
        cones.FindProperty("spaceCone").objectReferenceValue = spaceCone;
        cones.FindProperty("bodyCone").objectReferenceValue = bodyCone;
        cones.FindProperty("nutationLength").floatValue = tipLength;           // the spike's tip runs along its rim
        cones.FindProperty("spaceLength").floatValue = tipLength + 0.2f;
        cones.FindProperty("bodyLength").floatValue = tipLength + 0.4f;
        cones.ApplyModifiedPropertiesWithoutUndo();

        var board = new SerializedObject(helpers.gameObject.AddComponent<GyroscopeHelpers>());
        board.FindProperty("controller").objectReferenceValue = controller;
        board.FindProperty("vectors").objectReferenceValue = gyroscope.GetComponent<VectorDisplay>();
        board.FindProperty("labels").objectReferenceValue = labels.gameObject;
        board.FindProperty("precessionCone").objectReferenceValue = precessionCone.gameObject;
        board.FindProperty("nutationCone").objectReferenceValue = nutationCone.gameObject;
        board.FindProperty("spaceCone").objectReferenceValue = spaceCone.gameObject;
        board.FindProperty("bodyCone").objectReferenceValue = bodyCone.gameObject;
        board.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = helpersObject;
        Debug.Log("Gyroscope helpers added.");
        return true;
    }


    // ---- 3 ----

    private static bool UseWebBridge(Scene scene) {
        Transform helpers = FindInScene(scene, "Helpers");
        if (helpers == null) {
            Fail("Run Gyroscope → 2 Add helpers first.");
            return false;
        }
        Transform bridgeObject = FindInScene(scene, "WebBridge");
        if (bridgeObject == null) {
            bridgeObject = new GameObject("WebBridge").transform;
            SceneManager.MoveGameObjectToScene(bridgeObject.gameObject, scene);
        }
        GameObject go = bridgeObject.gameObject;
        go.SetActive(true);                                                     // SendMessage only finds active objects
        var simTime = go.GetComponent<SimulationTime>();
        if (simTime == null) simTime = go.AddComponent<SimulationTime>();
        var bridge = go.GetComponent<GyroscopeBridge>();
        if (bridge == null) bridge = go.AddComponent<GyroscopeBridge>();

        var so = new SerializedObject(bridge);
        so.FindProperty("controller").objectReferenceValue = FindInScene(scene, "Gyroscope").GetComponent<GyroscopeController>();
        so.FindProperty("helpers").objectReferenceValue = helpers.GetComponent<GyroscopeHelpers>();
        so.FindProperty("simTime").objectReferenceValue = simTime;
        so.FindProperty("testPreset").stringValue =
            "{\"rpm\":300,\"addOnMassGrams\":120,\"theta0Deg\":10,\"pushThetaDot\":0.6,\"show\":{\"D\":false}}";
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = go;
        Debug.Log("WebBridge uses GyroscopeBridge.");
        return true;
    }


    // ---- parts ----

    // smooth cylinder along local y from y0 to y1 (Unity's cylinder spans −1 … 1, so scale.y is half the length)
    private static Transform Cylinder(string name, Transform parent, float x, float diameter, float y0, float y1, string material) {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(go.GetComponent<Collider>());                   // no physics engine involved
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, (y0 + y1) / 2, 0f);
        go.transform.localScale = new Vector3(diameter, (y1 - y0) / 2, diameter);
        go.GetComponent<MeshFilter>().sharedMesh = NoirEditor.SmoothCylinder();
        go.GetComponent<MeshRenderer>().sharedMaterial = LitPlaceholder(material);
        return go.transform;
    }

    private static Transform Empty(string name, Transform parent, Vector3 localPosition) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }


    // light cone: an additive surface that brightens towards the rim (HelperCone's UV v: 0 apex, 1 rim) and glows
    // through the bloom, plus a bright rim line. On the black stage it reads as a cone of light.
    private static HelperCone LightCone(Transform parent, string name, string materialName, Color color, float surfaceIntensity) {
        var coneObject = new GameObject(name);
        coneObject.transform.SetParent(parent, false);
        HelperCone cone = coneObject.AddComponent<HelperCone>();                // also adds MeshFilter and MeshRenderer
        var surface = coneObject.GetComponent<MeshRenderer>();
        surface.sharedMaterial = ConeSurfaceMaterial("LectureCone" + materialName, color, surfaceIntensity);
        surface.shadowCastingMode = ShadowCastingMode.Off;
        surface.receiveShadows = false;
        Transform rimObject = Empty("Rim", coneObject.transform, Vector3.zero);
        var rim = rimObject.gameObject.AddComponent<LineRenderer>();
        rim.positionCount = 0;                                                  // HelperCone fills it at runtime
        rim.sharedMaterial = GlowMaterial("LectureConeRim" + materialName, color, 0.8f);    // below the bloom threshold
        rim.widthMultiplier = 0.004f;
        rim.alignment = LineAlignment.View;
        rim.shadowCastingMode = ShadowCastingMode.Off;
        rim.receiveShadows = false;
        var so = new SerializedObject(cone);
        so.FindProperty("rim").objectReferenceValue = rim;
        so.ApplyModifiedPropertiesWithoutUndo();
        return cone;
    }


    // ---- materials and textures ----

    private static Material LoadOrCreate(string name, string shader) {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) {
            mat = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find(shader);
        return mat;
    }

    // URP/Lit with colour, metallic and smoothness (no emission: the apparatus only shines where the spot hits it)
    private static Material Lit(string name, Color color, float metallic, float smoothness) {
        Material mat = LoadOrCreate(name, "Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetColor("_EmissionColor", Color.black);
        mat.DisableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // while building, before step 3 sets the real values: the asset by that name (shared metals first), created plain
    // if missing
    private static Material LitPlaceholder(string name) {
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{NoirEditor.SharedMaterialFolder}/{name}.mat");
        if (mat == null) mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
        return mat != null ? mat : Lit(name, Color.grey, 0.5f, 0.5f);
    }

    // URP/Unlit opaque in HDR (× intensity): the bloom makes it glow, like the original's emissive arrows
    private static Material GlowMaterial(string name, Color color, float intensity) {
        Material mat = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
        Color hdr = color.linear * intensity;
        hdr.a = 1f;
        mat.SetColor("_BaseColor", hdr.gamma);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // URP/Unlit, transparent, additive (adds light, never darkens), both faces, no depth write; the alpha ramp texture
    // fades the surface in towards the rim. Alpha of the colour = brightness of the surface.
    private static Material ConeSurfaceMaterial(string name, Color color, float intensity) {
        Material mat = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 2f);                                             // additive
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetTexture("_BaseMap", GlowRamp());
        Color c = color;
        c.a = intensity;
        mat.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // alpha ramp along v (apex 0 → rim 1): nothing near the apex, a soft rise towards the rim; rewritten on every run
    private static Texture2D GlowRamp() {
        string path = $"{TextureFolder}/ConeGlow.asset";
        const int n = 128;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool isNew = tex == null;
        if (isNew) tex = new Texture2D(4, n, TextureFormat.RGBA32, false) { name = "ConeGlow", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) {
            float v = (float)y / (n - 1);
            float a = 0.05f + 0.75f * v * v * v + 0.2f * Mathf.Pow(v, 16f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }
        tex.Apply();
        if (isNew) AssetDatabase.CreateAsset(tex, path);
        else EditorUtility.SetDirty(tex);
        return tex;
    }

    private static Material Shared(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{SharedMaterialFolder}/{name}.mat");


    // Carlo's picture if it is there, else the generated placeholder: a noir night through cell bars (moon, bars,
    // a red spot), white on black, in the inscribed circle of a square texture
    private static Texture2D DiskArt() {
        if (File.Exists(DiskArtPath)) {
            ConfigureArtImport(DiskArtPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(DiskArtPath);
        }
        if (!File.Exists(DiskArtPlaceholderPath)) {
            const int n = 1024;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var black = new Color(0.02f, 0.02f, 0.025f);
            for (int y = 0; y < n; y++) {
                for (int x = 0; x < n; x++) {
                    float u = 2f * x / (n - 1) - 1f, v = 2f * y / (n - 1) - 1f;         // −1 … 1
                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c = black;
                    // moon: a disk with a bite taken out, behind the bars
                    float moon = Mathf.Sqrt((u + 0.2f) * (u + 0.2f) + (v - 0.25f) * (v - 0.25f));
                    float bite = Mathf.Sqrt((u + 0.05f) * (u + 0.05f) + (v - 0.33f) * (v - 0.33f));
                    if (moon < 0.3f && bite > 0.27f) c = new Color(0.92f, 0.92f, 0.9f);
                    // a red spot, the noir's one colour
                    if (Mathf.Sqrt((u - 0.38f) * (u - 0.38f) + (v + 0.35f) * (v + 0.35f)) < 0.07f) c = new Color(0.85f, 0.02f, 0.02f);
                    // cell bars and a cross bar
                    float bar = Mathf.Abs(Mathf.Repeat(u + 0.1f, 0.25f) - 0.125f);
                    if (bar < 0.018f || Mathf.Abs(v + 0.05f) < 0.02f) c = new Color(0.8f, 0.8f, 0.82f);
                    if (bar < 0.006f) c = new Color(1f, 1f, 1f);                     // highlight on each bar
                    // rim: a white ring and a thin black edge
                    if (r > 0.9f && r < 0.94f) c = Color.white;
                    if (r >= 0.94f) c = black;
                    tex.SetPixel(x, y, c);
                }
            }
            File.WriteAllBytes(DiskArtPlaceholderPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(DiskArtPlaceholderPath);
        }
        ConfigureArtImport(DiskArtPlaceholderPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(DiskArtPlaceholderPath);
    }

    private static void ConfigureArtImport(string path) {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.maxTextureSize == 1024 && importer.wrapMode == TextureWrapMode.Clamp && importer.mipmapEnabled) return;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.anisoLevel = 4;                                                // the disk is often seen at a grazing angle
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }


    // ---- assets and lookup ----

    private static void EnsureFolders() {
        foreach (string sub in new[] { "Materials", "Textures" }) {
            if (!AssetDatabase.IsValidFolder($"{Folder}/{sub}")) AssetDatabase.CreateFolder(Folder, sub);
        }
    }

    private static LectureGyroscope LectureAsset() {
        EnsureFolders();
        var asset = AssetDatabase.LoadAssetAtPath<LectureGyroscope>(AssetPath);
        if (asset == null) {
            asset = ScriptableObject.CreateInstance<LectureGyroscope>();        // with the default values from the class
            AssetDatabase.CreateAsset(asset, AssetPath);
        }
        return asset;
    }

    private static Transform FindInScene(Scene scene, string path) => NoirEditor.FindInScene(scene, path);
}
