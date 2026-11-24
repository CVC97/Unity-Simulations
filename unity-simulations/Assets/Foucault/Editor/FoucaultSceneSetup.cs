using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;


// The Foucault pendulum, noir. FoucaultScene.unity started as a file copy of GyroscopeScene.unity (2026-11-24: camera
// with rim light, key light, volume, snow); the steps below take out the Kreisel and build the pendulum hall from
// primitives and generated meshes. They stay rerunnable and overwrite manual changes to what they touch, so tune the
// values here:
//   Foucault → 1 Build hall         removes the Kreisel's objects; floor, platform with degree ring, pins, trace,
//                                   pendulum (wire, bob, fan); materials, lights, camera, snow. Rebuilds the whole
//                                   Hall, so run 2 and 3 after it.
//   Foucault → 2 Add helpers        swing plane, compass letters, Ω and Coriolis arrows with labels, switchboard
//   Foucault → 3 Use web bridge     FoucaultBridge and SimulationTime on the WebBridge object
//   Foucault → Set up everything    all three in order (also FoucaultSceneSetup.SetUpFromCommandLine in batch mode)
// Axes: Unity x = East, z = North, y = up (PhysicsToUnity: model East-North-Up). The Hall is the Earth-fixed root; in
// the star view (poles only) it turns beneath the camera, so everything on the floor and the pendulum are its children.
public static class FoucaultSceneSetup {
    private const string Folder = "Assets/Foucault";
    private const string ScenePath = "Assets/Foucault/FoucaultScene.unity";
    private const string MaterialFolder = "Assets/Foucault/Materials";
    private const string MeshFolder = "Assets/Foucault/Meshes";
    private const string TextureFolder = "Assets/Foucault/Textures";
    private const string SharedMaterialFolder = "Assets/Shared/Materials";
    private const string ReflectionCubemapPath = "Assets/Shared/Textures/studio_reflection.hdr";

    // the pendulum (estimated, no real one in Göttingen known: Carlo, 2026-11-24); keep in step with FoucaultController
    private const float Length = 15f;                   // m, suspension to the bob's centre
    private const float BobRadius = 0.13f;              // m: a 26 cm steel sphere (hollow, 25 kg)
    private const float StylusReach = 0.21f;            // m, bob centre to the stylus tip
    private const float StylusGap = 0.015f;             // m, stylus tip above the platform at rest
    // the platform: a low black disk with a chrome skirt, degree ring and compass letters near its edge
    private const float PlatformRadius = 2.6f;
    private const float FloorRadius = 10f;              // m: the Kreisel's 4 m × (camera 7.5 m / 2.9 m)
    private const float PlatformTop = 0.03f;
    private const float TickOuter = 2.32f;
    private const float LetterRadius = 2.47f;
    // the pins: 48 on a ring a little inside the swing (placed at runtime, PinRingDisplay); the stylus reaches them
    // below their heads for every amplitude of the slider (0.8–1.6 m)
    private const int PinCount = 48;
    private const float PinHeight = 0.11f;
    private const float PinDiameter = 0.012f;
    // where Ω stands: on the pendulum's axis, 2 m up (above the bob and its Coriolis arrow, in the start view's frame).
    // Labels have a fixed size in the world: beside the platform the orbiting camera came close and they grew huge;
    // on the axis they stay at the orbit distance like everything else.
    private static readonly Vector3 OmegaAnchor = new Vector3(0f, 2f, 0f);

    // the scale against the original toy top (camera 0.5 m): the Kreisel is 4, this hall about 2.6 times the Kreisel
    private const float SceneScale = 10f;

    private static float SuspensionHeight => PlatformTop + StylusGap + StylusReach + Length;

    // noir colours: white for Ω (like ω), red for the force (like D), greys for the rest
    private static readonly Color32 White = new Color32(255, 255, 255, 255);
    private static readonly Color32 LightGrey = new Color32(220, 223, 230, 255);
    private static readonly Color32 Grey = new Color32(110, 112, 118, 255);


    [MenuItem("Foucault/1 Build hall")]
    private static void HallMenu() => Run(BuildHall);

    [MenuItem("Foucault/2 Add helpers")]
    private static void HelpersMenu() => Run(AddHelpers);

    [MenuItem("Foucault/3 Use web bridge")]
    private static void BridgeMenu() => Run(UseWebBridge);

    [MenuItem("Foucault/Set up everything")]
    private static void SetUpMenu() => SetUpEverything();


    // Unity -batchmode -nographics -projectPath unity-simulations -executeMethod FoucaultSceneSetup.SetUpFromCommandLine -logFile setup.log
    public static void SetUpFromCommandLine() {
        EditorApplication.Exit(SetUpEverything() ? 0 : 1);
    }


    private static bool SetUpEverything() {
        if (!EditorApplication.isPlaying && SceneManager.GetActiveScene().path != ScenePath) {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        return Run(BuildHall) && Run(AddHelpers) && Run(UseWebBridge);
    }


    // runs a step on the open Foucault scene and saves it
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
        EnsureFolders();
        if (!step(scene)) return false;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }


    private static void Fail(string message) {
        if (Application.isBatchMode) Debug.LogError("FoucaultSceneSetup: " + message);
        else EditorUtility.DisplayDialog("Foucault scene", message, "OK");
    }


    // ---- 1 ----

    private static bool BuildHall(Scene scene) {
        // the Kreisel's objects (the scene is a copy of its scene) and this step's own from an earlier run
        foreach (string name in new[] { "Gyroscope", "LecturePedestal", "Helpers", "WebBridge", "Floor", "Hall", "ViewTarget" }) {
            Transform old = FindInScene(scene, name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        Transform labels = FindInScene(scene, "VectorLabels");
        if (labels != null) {
            for (int i = labels.childCount - 1; i >= 0; i--) Object.DestroyImmediate(labels.GetChild(i).gameObject);
        }

        var m = new Dictionary<string, Material> {
            ["Floor"] = Lit("FoucaultFloor", new Color(0.2f, 0.2f, 0.2f), 0f, 0.08f),          // rough: no glare (Kreisel)
            // the Kreisel's metals: the noir keeps colour for the vectors. The disk is the big surface in the spot, so
            // it is as dark as the Kreisel's floor reads, not its cast iron (small parts there): blackened iron, rough
            // enough that it doesn't mirror the studio reflection as a bright, bluish rectangle (as gloss lacquer did)
            // little metal, so it doesn't pick up the studio reflection outside the spot and read grey all over
            ["DarkIron"] = Lit("FoucaultDarkIron", new Color32(34, 34, 37, 255), 0.3f, 0.35f),
            // the bob: darker and more satin than the Kreisel's dark steel, so it doesn't glare in the spot
            ["Bob"] = Lit("FoucaultBob", new Color32(72, 74, 79, 255), 0.7f, 0.42f),
            ["Chrome"] = Lit("FoucaultChrome", new Color32(235, 236, 240, 255), 0.95f, 0.85f),
            ["Steel"] = Lit("FoucaultSteel", new Color32(215, 217, 222, 255), 0.8f, 0.72f),
            ["Mount"] = Lit("FoucaultMount", new Color32(40, 40, 44, 255), 0.5f, 0.5f),
        };
        Mesh cylinder = NoirEditor.SmoothCylinder();

        Transform hall = NewRoot(scene, "Hall").transform;

        // floor: a disk that ends in the dark like the Kreisel's (its edge 1.4 camera distances out; a floor to the
        // horizon reads as a grey plane in the ambient light). Round, so it can turn along in the star view.
        Transform floor = Cylinder("Floor", hall, 2 * FloorRadius, -0.001f, 0f, m["Floor"], cylinder);
        floor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

        // platform: dark iron disk, a chrome skirt, the degree ring (part of the apparatus, not a helper)
        Transform platform = Empty("Platform", hall, Vector3.zero);
        Cylinder("Disk", platform, 2 * PlatformRadius, 0f, PlatformTop, m["DarkIron"], cylinder);
        Cylinder("Skirt", platform, 2 * PlatformRadius + 0.06f, 0f, PlatformTop - 0.008f, m["Chrome"], cylinder);
        Transform ticks = Empty("DegreeRing", platform, new Vector3(0f, PlatformTop + 0.001f, 0f));
        ticks.gameObject.AddComponent<MeshFilter>().sharedMesh = StoreMesh(DegreeRingMesh(), "DegreeRing");
        var tickRenderer = ticks.gameObject.AddComponent<MeshRenderer>();
        tickRenderer.sharedMaterial = Glow("FoucaultTicks", Grey, 0.9f);
        tickRenderer.shadowCastingMode = ShadowCastingMode.Off;

        // pins: placed on their ring at runtime; pivot at the foot
        Transform pinRoot = Empty("Pins", hall, new Vector3(0f, PlatformTop, 0f));
        var pins = new Transform[PinCount];
        for (int i = 0; i < PinCount; i++) {
            Transform pin = Empty($"Pin{i:00}", pinRoot, Vector3.zero);
            // steel, not chrome: 48 chrome heads in the spot sparkle like the snow
            Cylinder("Shaft", pin, PinDiameter, 0f, PinHeight, m["Steel"], cylinder);
            Transform head = Primitive(PrimitiveType.Sphere, "Head", pin, m["Steel"]);
            head.localPosition = new Vector3(0f, PinHeight, 0f);
            head.localScale = Vector3.one * 0.02f;
            pins[i] = pin;
        }
        var pinDisplay = pinRoot.gameObject.AddComponent<PinRingDisplay>();
        var pd = new SerializedObject(pinDisplay);
        SetArray(pd.FindProperty("pins"), pins);
        pd.FindProperty("pinRadius").floatValue = PinDiameter / 2;
        pd.ApplyModifiedPropertiesWithoutUndo();

        // the trace on the floor: a ribbon mesh with vertex colours (the Kreisel's trail shader, both faces)
        Transform traceObject = Empty("Trace", hall, new Vector3(0f, PlatformTop + 0.002f, 0f));
        traceObject.gameObject.AddComponent<MeshFilter>();
        var traceRenderer = traceObject.gameObject.AddComponent<MeshRenderer>();
        traceRenderer.sharedMaterial = TraceMaterial();
        traceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        traceRenderer.receiveShadows = false;
        var trace = traceObject.gameObject.AddComponent<TraceDisplay>();

        // the pendulum: its origin is the suspension point, high up in the dark
        Transform pendulum = Empty("Pendulum", hall, new Vector3(0f, SuspensionHeight, 0f));
        Cylinder("Mount", pendulum, 0.45f, 0.02f, 0.08f, m["Mount"], cylinder);
        Cylinder("Collar", pendulum, 0.06f, -0.05f, 0.02f, m["Chrome"], cylinder);
        Transform wire = Cylinder("Wire", pendulum, 0.006f, -Length, 0f, m["Steel"], cylinder);
        wire.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;  // a hair-thin shadow only flickers
        Transform bob = Empty("Bob", pendulum, new Vector3(0f, -Length, 0f));
        Transform sphere = Primitive(PrimitiveType.Sphere, "Sphere", bob, m["Bob"]);
        sphere.localScale = Vector3.one * (2 * BobRadius);
        Cylinder("Band", bob, 2 * BobRadius + 0.004f, -0.012f, 0.012f, m["Chrome"], cylinder);   // a seam around the middle
        Cylinder("Hook", bob, 0.035f, BobRadius - 0.01f, BobRadius + 0.04f, m["Chrome"], cylinder);
        Cylinder("Stylus", bob, 0.016f, -StylusReach + 0.012f, -BobRadius + 0.01f, m["Chrome"], cylinder);
        Cylinder("StylusTip", bob, 0.006f, -StylusReach, -StylusReach + 0.012f, m["Chrome"], cylinder);

        // the fan of high Zeitraffer: additive surface glowing towards the bob's arc, the arc as a line
        Transform fanObject = Empty("Fan", pendulum, Vector3.zero);
        fanObject.gameObject.AddComponent<MeshFilter>();
        var fanSurface = fanObject.gameObject.AddComponent<MeshRenderer>();
        fanSurface.sharedMaterial = Additive("FoucaultFan", LightGrey, 0.07f, FanRamp());
        fanSurface.shadowCastingMode = ShadowCastingMode.Off;
        fanSurface.receiveShadows = false;
        LineRenderer fanArc = Line("Arc", fanObject, Additive("FoucaultFanArc", LightGrey, 0.4f, null), 0.03f);
        var fan = fanObject.gameObject.AddComponent<FanDisplay>();

        var controller = pendulum.gameObject.AddComponent<FoucaultController>();
        var c = new SerializedObject(controller);
        c.FindProperty("length").floatValue = Length;
        c.FindProperty("stylusReach").floatValue = StylusReach;
        c.FindProperty("pinCount").intValue = PinCount;
        c.FindProperty("hall").objectReferenceValue = hall;
        c.FindProperty("bob").objectReferenceValue = bob;
        c.FindProperty("wire").objectReferenceValue = wire;
        c.FindProperty("bobRadius").floatValue = BobRadius;
        c.FindProperty("pins").objectReferenceValue = pinDisplay;
        c.FindProperty("trace").objectReferenceValue = trace;
        c.ApplyModifiedPropertiesWithoutUndo();
        var f = new SerializedObject(fan);
        f.FindProperty("controller").objectReferenceValue = controller;
        f.FindProperty("arc").objectReferenceValue = fanArc;
        f.ApplyModifiedPropertiesWithoutUndo();

        // the camera orbits a fixed point on the floor, not the hall: in the star view the hall turns beneath it
        Transform viewTarget = NewRoot(scene, "ViewTarget").transform;
        Transform cameraObject = FindInScene(scene, "Main Camera");
        var orbit = new SerializedObject(cameraObject.GetComponent<OrbitCamera>());
        orbit.FindProperty("target").objectReferenceValue = viewTarget;
        orbit.FindProperty("targetOffset").vector3Value = new Vector3(0f, 0.5f, 0f);
        orbit.FindProperty("yaw").floatValue = 15f;
        orbit.FindProperty("pitch").floatValue = 32f;
        orbit.FindProperty("distance").floatValue = 7.5f;
        orbit.FindProperty("minDistance").floatValue = 2f;
        orbit.FindProperty("maxDistance").floatValue = 18f;
        orbit.FindProperty("minPitch").floatValue = 4f;                        // not at floor level: grazing views only show glare
        orbit.FindProperty("maxPitch").floatValue = 88f;                       // from straight above the trace reads best
        orbit.ApplyModifiedPropertiesWithoutUndo();
        Camera cam = cameraObject.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 80f;

        // the spot pool: high above, slightly to the side, the whole platform in it; the wire runs up out of it into
        // the dark. Same illuminance on the floor as the Kreisel's key light (1/r²).
        Light key = FindInScene(scene, "KeyLight").GetComponent<Light>();
        key.transform.position = new Vector3(2.2f, 8.5f, -2.6f);
        key.transform.LookAt(new Vector3(0f, 0.3f, 0f));
        key.range = 16f;
        key.intensity = 430f;
        key.spotAngle = 40f;                                                    // a pool about the platform, its rim fading
        key.innerSpotAngle = 16f;                                               // (relative size as on the Kreisel)

        // ambient and reflections as on the Kreisel (metal needs something to reflect)
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.09f, 0.09f, 0.095f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionCubemapPath);
        RenderSettings.reflectionIntensity = 0.55f;

        // snow scaled as a whole (it simulates in local space, flakes included) by the ratio of the camera distances
        // (7.5 m / 2.9 m): it looks as on the Kreisel and stays around the platform, never around the camera
        const float snowScale = 2.6f;
        foreach (string name in new[] { "Snow", "Snow (fast)" }) {
            Transform snow = FindInScene(scene, name);
            if (snow == null) continue;
            snow.position = new Vector3(0f, 0.8f * snowScale, 0f);
            snow.localScale = (name == "Snow" ? new Vector3(0.75f, 1f, 0.75f) : Vector3.one) * snowScale;
            // fewer flakes than the Kreisel's 100/s: the bigger spot lights more of them
            var emission = snow.GetComponent<ParticleSystem>().emission;
            emission.rateOverTimeMultiplier = 40f;
        }

        Debug.Log("Foucault hall built. Run 2 Add helpers and 3 Use web bridge next.");
        return true;
    }


    // ---- 2 ----

    private static bool AddHelpers(Scene scene) {
        Transform hall = FindInScene(scene, "Hall");
        Transform labels = FindInScene(scene, "VectorLabels");
        if (hall == null || labels == null) {
            Fail("Run Foucault → 1 Build hall first (the scene needs Hall and VectorLabels).");
            return false;
        }
        Transform old = hall.Find("Helpers");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        for (int i = labels.childCount - 1; i >= 0; i--) Object.DestroyImmediate(labels.GetChild(i).gameObject);

        var controller = hall.Find("Pendulum").GetComponent<FoucaultController>();
        Transform helpers = Empty("Helpers", hall, Vector3.zero);

        // swing plane: current line light grey, starting line grey, the arc between them light grey
        Transform plane = Empty("Plane", helpers, new Vector3(0f, PlatformTop + 0.004f, 0f));
        Material lineLight = Glow("FoucaultPlaneLine", LightGrey, 0.8f);         // below the bloom threshold
        Material lineDark = Glow("FoucaultPlaneStart", Grey, 0.9f);
        LineRenderer current = Line("Current", plane, lineLight, 0.018f);
        LineRenderer start = Line("Start", plane, lineDark, 0.014f);
        LineRenderer arc = Line("Arc", plane, lineLight, 0.012f);
        var planeDisplay = new SerializedObject(plane.gameObject.AddComponent<PlaneDisplay>());
        planeDisplay.FindProperty("controller").objectReferenceValue = controller;
        planeDisplay.FindProperty("current").objectReferenceValue = current;
        planeDisplay.FindProperty("startLine").objectReferenceValue = start;
        planeDisplay.FindProperty("arc").objectReferenceValue = arc;
        planeDisplay.ApplyModifiedPropertiesWithoutUndo();

        // compass letters on the platform's edge, readable from the south (north at the top, like a map)
        Transform compass = Empty("Compass", helpers, new Vector3(0f, PlatformTop + 0.001f, 0f));
        Material letterMaterial = TMP_Settings.defaultFontAsset.material;          // lit by nothing, drawn in the scene (no overlay)
        foreach ((string letter, float azimuthDeg) in new[] { ("N", 90f), ("O", 0f), ("S", 270f), ("W", 180f) }) {
            TextMeshPro text = NoirEditor.Label(compass, letter, letter, LightGrey, letterMaterial);
            text.fontSize = 2.2f;
            text.fontStyle = FontStyles.Bold;
            float a = azimuthDeg * Mathf.Deg2Rad;
            text.transform.localPosition = new Vector3(LetterRadius * Mathf.Cos(a), 0f, LetterRadius * Mathf.Sin(a));
            text.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);       // lying, facing up, top to the north
        }

        // vectors: Ω (white) and its two parts (light grey) beside the platform, the Coriolis force (red) at the bob
        Transform omegaAnchor = Empty("OmegaAnchor", helpers, OmegaAnchor);
        Transform vectorRoot = Empty("Vectors", helpers, Vector3.zero);
        Material omegaPart = EmissiveArrowMaterial("FoucaultArrowOmegaPart", LightGrey, 1f);   // under the bloom: no glow
        VectorArrow3D omega = Arrow("ArrowOmega", vectorRoot, Shared("ArrowOmega"), 1f);
        VectorArrow3D omegaVertical = Arrow("ArrowOmegaVertical", vectorRoot, omegaPart, 0.65f);
        VectorArrow3D omegaHorizontal = Arrow("ArrowOmegaHorizontal", vectorRoot, omegaPart, 0.65f);
        VectorArrow3D coriolis = Arrow("ArrowCoriolis", vectorRoot, Shared("ArrowD"), 1f);
        var vectors = vectorRoot.gameObject.AddComponent<FoucaultVectors>();
        var v = new SerializedObject(vectors);
        v.FindProperty("controller").objectReferenceValue = controller;
        v.FindProperty("omegaAnchor").objectReferenceValue = omegaAnchor;
        v.FindProperty("arrowOmega").objectReferenceValue = omega;
        v.FindProperty("arrowOmegaVertical").objectReferenceValue = omegaVertical;
        v.FindProperty("arrowOmegaHorizontal").objectReferenceValue = omegaHorizontal;
        v.FindProperty("arrowCoriolis").objectReferenceValue = coriolis;
        v.ApplyModifiedPropertiesWithoutUndo();

        // labels: the Kreisel's style (drawn on top), each hides with its arrow; but without the black halo, which is
        // invisible against the Kreisel's black stage and showed here as a dark border over the lit platform
        Material labelMaterial = NoirEditor.LabelOverlayMaterial(MaterialFolder + "/FoucaultLabel.mat", Color.clear);
        VectorLabelFor(labels, "LabelOmega", NoirEditor.VectorSymbol("Ω", 0.78f), White, labelMaterial, omega, 0.17f, 0f);
        VectorLabelFor(labels, "LabelOmegaVertical", "<i>Ω</i> sin <i>φ</i>", LightGrey, labelMaterial, omegaVertical, 0f, 0.45f);
        VectorLabelFor(labels, "LabelOmegaHorizontal", "<i>Ω</i> cos <i>φ</i>", LightGrey, labelMaterial, omegaHorizontal, 0.4f, 0f);
        VectorLabelFor(labels, "LabelCoriolis", NoirEditor.VectorSymbol("F", 0.78f) + "<sub>C</sub>", new Color32(224, 25, 47, 255),
                       labelMaterial, coriolis, 0.17f, 0f);

        var board = new SerializedObject(helpers.gameObject.AddComponent<FoucaultHelpers>());
        board.FindProperty("vectors").objectReferenceValue = vectors;
        board.FindProperty("labels").objectReferenceValue = labels.gameObject;
        board.FindProperty("plane").objectReferenceValue = plane.gameObject;
        board.FindProperty("compass").objectReferenceValue = compass.gameObject;
        board.FindProperty("trace").objectReferenceValue = hall.Find("Trace").gameObject;
        board.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = helpers.gameObject;
        Debug.Log("Foucault helpers added.");
        return true;
    }


    // ---- 3 ----

    private static bool UseWebBridge(Scene scene) {
        Transform hall = FindInScene(scene, "Hall");
        Transform helpers = hall != null ? hall.Find("Helpers") : null;
        if (helpers == null) {
            Fail("Run Foucault → 2 Add helpers first.");
            return false;
        }
        Transform bridgeObject = FindInScene(scene, "WebBridge");
        if (bridgeObject == null) bridgeObject = NewRoot(scene, "WebBridge").transform;
        GameObject go = bridgeObject.gameObject;
        go.SetActive(true);                                                     // SendMessage only finds active objects
        var simTime = go.GetComponent<SimulationTime>();
        if (simTime == null) simTime = go.AddComponent<SimulationTime>();
        var bridge = go.GetComponent<FoucaultBridge>();
        if (bridge == null) bridge = go.AddComponent<FoucaultBridge>();

        var so = new SerializedObject(bridge);
        so.FindProperty("controller").objectReferenceValue = hall.Find("Pendulum").GetComponent<FoucaultController>();
        so.FindProperty("helpers").objectReferenceValue = helpers.GetComponent<FoucaultHelpers>();
        so.FindProperty("simTime").objectReferenceValue = simTime;
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = go;
        Debug.Log("WebBridge uses FoucaultBridge.");
        return true;
    }


    // ---- parts ----

    private static GameObject NewRoot(Scene scene, string name) {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static Transform Empty(string name, Transform parent, Vector3 localPosition) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    private static Transform Primitive(PrimitiveType type, string name, Transform parent, Material material) {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());                   // no physics engine involved
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go.transform;
    }

    // smooth cylinder along local y from y0 to y1 (the mesh spans −1 … 1, so scale.y is half the length)
    private static Transform Cylinder(string name, Transform parent, float diameter, float y0, float y1, Material material, Mesh mesh) {
        Transform t = Primitive(PrimitiveType.Cylinder, name, parent, material);
        t.GetComponent<MeshFilter>().sharedMesh = mesh;
        t.localPosition = new Vector3(0f, (y0 + y1) / 2, 0f);
        t.localScale = new Vector3(diameter, (y1 - y0) / 2, diameter);
        return t;
    }

    private static LineRenderer Line(string name, Transform parent, Material material, float width) {
        Transform t = Empty(name, parent, Vector3.zero);
        var line = t.gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 0;                                                 // filled at runtime
        line.sharedMaterial = material;
        line.widthMultiplier = width;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    // shaft (smooth cylinder) and head (ConeMesh) as VectorArrow3D wants them; sizes like the Kreisel's, scaled
    private static VectorArrow3D Arrow(string name, Transform parent, Material material, float thickness) {
        Transform root = Empty(name, parent, Vector3.zero);
        Transform shaft = Primitive(PrimitiveType.Cylinder, "Shaft", root, material);
        shaft.GetComponent<MeshFilter>().sharedMesh = NoirEditor.SmoothCylinder();
        shaft.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        var headObject = new GameObject("Head");
        headObject.transform.SetParent(root, false);
        headObject.AddComponent<MeshFilter>();
        var headRenderer = headObject.AddComponent<MeshRenderer>();
        headRenderer.sharedMaterial = material;
        headRenderer.shadowCastingMode = ShadowCastingMode.Off;
        headObject.AddComponent<ConeMesh>();                                    // radius 0.5, height 1, base at y = 0
        var arrow = root.gameObject.AddComponent<VectorArrow3D>();
        var a = new SerializedObject(arrow);
        a.FindProperty("shaft").objectReferenceValue = shaft;
        a.FindProperty("head").objectReferenceValue = headObject.transform;
        a.FindProperty("shaftDiameter").floatValue = 0.002f * SceneScale * thickness;
        a.FindProperty("headDiameter").floatValue = 0.006f * SceneScale * thickness;
        a.FindProperty("headLength").floatValue = 0.012f * SceneScale * thickness;
        a.ApplyModifiedPropertiesWithoutUndo();
        return arrow;
    }

    private static void VectorLabelFor(Transform labels, string name, string text, Color color, Material material,
                                       VectorArrow3D arrow, float offset, float sideOffset) {
        TextMeshPro tmp = NoirEditor.Label(labels, name, text, color, material);
        tmp.fontSize = 0.23f * SceneScale;
        tmp.fontStyle = FontStyles.Bold;
        tmp.UpdateMeshPadding();
        var label = new SerializedObject(tmp.gameObject.AddComponent<VectorLabel>());
        label.FindProperty("arrow").objectReferenceValue = arrow;
        label.FindProperty("offset").floatValue = offset;
        label.FindProperty("sideOffset").floatValue = sideOffset;
        label.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(SerializedProperty property, Transform[] items) {
        property.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }


    // ---- meshes ----

    // the degree ring: a tick every 5°, longer every 10°, longest every 30°, and a thin circle outside; flat quads
    private static Mesh DegreeRingMesh() {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) {                 // a b c d around, seen from above
            int i = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        for (int k = 0; k < 72; k++) {
            float deg = 5f * k;
            float len = k % 6 == 0 ? 0.16f : k % 2 == 0 ? 0.1f : 0.06f;
            float halfWidth = (k % 6 == 0 ? 0.008f : 0.005f);
            float a = deg * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var across = new Vector3(-radial.z, 0f, radial.x) * halfWidth;
            Vector3 inner = radial * (TickOuter - len), outer = radial * TickOuter;
            Quad(inner - across, outer - across, outer + across, inner + across);
        }
        const int segments = 360;
        for (int i = 0; i < segments; i++) {
            float a0 = 2 * Mathf.PI * i / segments, a1 = 2 * Mathf.PI * (i + 1) / segments;
            var r0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var r1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            float rIn = TickOuter + 0.02f, rOut = TickOuter + 0.03f;
            Quad(r0 * rIn, r0 * rOut, r1 * rOut, r1 * rIn);
        }
        var mesh = new Mesh { name = "DegreeRing" };
        mesh.SetVertices(vertices);
        // the winding of the quads isn't the same for ticks and circle: both faces get drawn via doubled triangles
        var both = new List<int>(triangles);
        for (int i = 0; i < triangles.Count; i += 3) both.AddRange(new[] { triangles[i], triangles[i + 2], triangles[i + 1] });
        mesh.SetTriangles(both, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // a generated mesh as an asset (the scene refers to it); an existing asset is overwritten in place, so references stay
    private static Mesh StoreMesh(Mesh built, string name) {
        string path = $"{MeshFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) {
            AssetDatabase.CreateAsset(built, path);
            return built;
        }
        existing.Clear();
        existing.SetVertices(built.vertices);
        existing.SetNormals(built.normals);
        existing.SetTriangles(built.triangles, 0);
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(built);
        return existing;
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

    // URP/Unlit opaque (× intensity): lines and marks that should read in the dark; below 1 stays under the bloom
    private static Material Glow(string name, Color color, float intensity) {
        Material mat = LoadOrCreate(name, "Universal Render Pipeline/Unlit");
        Color hdr = color.linear * intensity;
        hdr.a = 1f;
        mat.SetColor("_BaseColor", hdr.gamma);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // URP/Unlit, transparent, additive (adds light, never darkens), both faces, no depth write; an optional alpha
    // ramp along UV v. Alpha of the colour = brightness. As the Kreisel's light cones.
    private static Material Additive(string name, Color color, float intensity, Texture2D ramp) {
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
        mat.SetTexture("_BaseMap", ramp);
        Color c = color;
        c.a = intensity;
        mat.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // the trace: a copy of the Kreisel's trail material (Particles/Unlit, alpha blended, vertex colours), both faces
    private static Material TraceMaterial() {
        string path = $"{MaterialFolder}/FoucaultTrace.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) {
            AssetDatabase.CopyAsset($"{SharedMaterialFolder}/TrailWhite.mat", path);
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // a copy of a shared arrow material (URP/Lit, black, HDR emission: glows through the bloom) in another colour
    private static Material EmissiveArrowMaterial(string name, Color color, float intensity) {
        string path = $"{MaterialFolder}/{name}.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) {
            AssetDatabase.CopyAsset($"{SharedMaterialFolder}/ArrowOmega.mat", path);
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Color hdr = color * intensity;                                          // as stored in ArrowOmega (1.5 = white glow)
        hdr.a = 1f;
        mat.SetColor("_EmissionColor", hdr);
        mat.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // alpha ramp along v (0 at the suspension → 1 at the bob's arc): almost nothing along the wire, bright at the arc
    private static Texture2D FanRamp() {
        string path = $"{TextureFolder}/FanGlow.asset";
        const int n = 256;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        bool isNew = tex == null;
        if (isNew) tex = new Texture2D(4, n, TextureFormat.RGBA32, false) { name = "FanGlow", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++) {
            float v = (float)y / (n - 1);
            float a = 0.02f + 0.18f * v * v + 0.8f * Mathf.Pow(v, 40f);          // the last ~5 % of the wire's length glow
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
        }
        tex.Apply();
        if (isNew) AssetDatabase.CreateAsset(tex, path);
        else EditorUtility.SetDirty(tex);
        return tex;
    }

    private static Material Shared(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{SharedMaterialFolder}/{name}.mat");


    // ---- assets and lookup ----

    private static void EnsureFolders() {
        foreach (string sub in new[] { "Materials", "Meshes", "Textures" }) {
            if (!AssetDatabase.IsValidFolder($"{Folder}/{sub}")) AssetDatabase.CreateFolder(Folder, sub);
        }
    }

    private static Transform FindInScene(Scene scene, string path) => NoirEditor.FindInScene(scene, path);
}
