using System;
using System.Runtime.InteropServices;
using UnityEngine;


// A complete page state for the simulation. Fields missing in the page's JSON keep the defaults below, so a preset
// never depends on what ran before. The field names are the JSON keys.
[Serializable]
public class FoucaultPreset {
    public float latitudeDeg = 51.5f;                                           // Breitengrad, north positive (Göttingen)
    public float amplitude = 1.2f;                                              // m, horizontal, clamped to 0.8–1.6
    public string frame = "earth";                                              // "earth" | "stars" (stars only at ±90°)
    public float timeScale = 1f;                                                // Zeitraffer, 1 = real time
    public bool paused = false;
    public FoucaultHelperFlags show = new FoucaultHelperFlags();
}


// Messages to the page: {source, type, ...}.
[Serializable]
public class FoucaultReadyMessage {
    public string source = "foucault";
    public string type = "ready";
}


// ~10 Hz in real time, also while paused. Angles counter-clockwise seen from above positive.
[Serializable]
public class FoucaultStateMessage {
    public string source = "foucault";
    public string type = "state";
    public int run;                                                             // +1 on every release
    public int segment;                                                         // +1 on every release and latitude change
    public float time;                                                          // s of simulated time since the release
    public float latitudeDeg;
    public float planeRateDegPerHour;                                           // −15.04 °/h · sin φ: < 0 = clockwise
    public float turnedDeg;                                                     // measured since the release, unwrapped
    public float amplitude;                                                     // m, measured semi-major axis
    public float swingPeriod;                                                   // s, small swings
    public float timeScale;                                                     // asked for
    public float actualTimeScale;                                               // done (lower on a slow computer)
    public bool lagging;
    public string frame;                                                        // shown: "earth" | "stars"
    public bool starsAllowed;                                                   // only at the poles
    public int pinsDown;
    public int pinsTotal;
    public bool bobVisible;                                                     // false: drawn as the swept fan
    public bool paused;
}


// The page ↔ simulation contract (design_notes.md, section 5). The page holds the Unity canvas itself and calls
//   unityInstance.SendMessage("WebBridge", method, value)
// with a number or a string, so switches come as 0/1 and structured data as a JSON string. Back to the page goes JSON
// through SimBridge.jslib → window.onSimMessage(message) (Assets/Shared/Plugins). The GameObject keeps the name
// WebBridge (the SendMessage target). Modelled on GyroscopeBridge.
public class FoucaultBridge : MonoBehaviour {
    [SerializeField] private FoucaultController controller;
    [SerializeField] private FoucaultHelpers helpers;
    [SerializeField] private SimulationTime simTime;
    [SerializeField] private float stateInterval = 0.1f;                        // s of real time

    [Header("Editor test")]
    [SerializeField, TextArea(3, 8)] private string testPreset = "{\"latitudeDeg\":90,\"frame\":\"stars\",\"timeScale\":1000}";
    [SerializeField, TextArea(3, 8)] private string lastMessage;                // what was sent last (written in Play mode)

    private readonly FoucaultStateMessage state = new FoucaultStateMessage();
    private float sinceState;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SimPostToPage(string json);                     // Assets/Shared/Plugins/SimBridge.jslib
#endif


    // ---- called from JavaScript via SendMessage ----

    public void ApplyPreset(string json) {
        var preset = new FoucaultPreset();
        if (!TryOverwrite(json, preset)) return;

        controller.SetLatitudeDeg(preset.latitudeDeg);
        controller.SetAmplitude(preset.amplitude);
        if (TryParseFrame(preset.frame, out var frame)) controller.SetFrame(frame);
        controller.SetTimeFactor(preset.timeScale);
        controller.Restart();                                                   // also if no value changed: a preset is a fresh start

        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(preset.show), helpers.Show);    // copy into the switchboard's object
        simTime.SetPaused(preset.paused);
    }

    // partial: {"trace":false} changes only the trace
    public void SetHelpers(string json) {
        TryOverwrite(json, helpers.Show);
    }

    public void SetFrame(string value) {
        if (TryParseFrame(value, out var frame)) controller.SetFrame(frame);
    }

    public void SetLatitude(float deg) => controller.SetLatitudeDeg(deg);
    public void SetAmplitude(float metres) => controller.SetAmplitude(metres);
    public void SetTimeScale(float factor) => controller.SetTimeFactor(factor);
    public void SetPaused(float on) => simTime.SetPaused(on > 0.5f);
    public void Restart() => controller.Restart();


    [ContextMenu("Apply test preset")]
    private void ApplyTestPreset() => ApplyPreset(testPreset);


    void Start() {
        // the Zeitraffer is the controller's own; Unity's time scale stays 1 (0 while paused)
        simTime.SetTimeScale(1f);
        Post(JsonUtility.ToJson(new FoucaultReadyMessage()));
    }


    // after the controller's Update, so the state is this frame's; real time, so the page also hears while paused
    void LateUpdate() {
        sinceState += Time.unscaledDeltaTime;
        if (sinceState < stateInterval || controller.Model == null) return;
        sinceState = 0f;

        FoucaultModel model = controller.Model;
        state.run = controller.Run;
        state.segment = controller.Segment;
        state.time = (float)controller.SimTime;
        state.latitudeDeg = controller.LatitudeDeg;
        state.planeRateDegPerHour = (float)(model.PlaneRate * Mathf.Rad2Deg * 3600);
        state.turnedDeg = (float)(controller.PlaneTurned * Mathf.Rad2Deg);
        state.amplitude = (float)controller.SwingMajor;
        state.swingPeriod = (float)model.SwingPeriod;
        state.timeScale = controller.TimeFactor;
        state.actualTimeScale = controller.ActualTimeFactor;
        state.lagging = controller.Lagging;
        state.frame = controller.ShownFrame == FoucaultController.Frame.Stars ? "stars" : "earth";
        state.starsAllowed = controller.StarsAllowed;
        state.pinsDown = controller.Pins.DownCount;
        state.pinsTotal = controller.Pins.Count;
        state.bobVisible = controller.BobVisible;
        state.paused = simTime.Paused;
        Post(JsonUtility.ToJson(state));
    }


    private void Post(string json) {
        lastMessage = json;
#if UNITY_WEBGL && !UNITY_EDITOR
        SimPostToPage(json);
#endif
    }


    // JsonUtility throws on malformed JSON; a bad message from the page must not stop the simulation
    private static bool TryOverwrite(string json, object target) {
        try {
            JsonUtility.FromJsonOverwrite(json, target);
            return true;
        } catch (ArgumentException e) {
            Debug.LogWarning($"FoucaultBridge: ignored malformed JSON ({e.Message}): {json}");
            return false;
        }
    }

    private static bool TryParseFrame(string value, out FoucaultController.Frame frame) {
        switch (value) {
            case "earth": frame = FoucaultController.Frame.Earth; return true;
            case "stars": frame = FoucaultController.Frame.Stars; return true;
        }
        Debug.LogWarning($"FoucaultBridge: unknown frame \"{value}\" (earth | stars)");
        frame = default;
        return false;
    }
}
