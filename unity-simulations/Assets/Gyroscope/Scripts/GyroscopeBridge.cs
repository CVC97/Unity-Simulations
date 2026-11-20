using System;
using System.Runtime.InteropServices;
using UnityEngine;


// A complete page state for the simulation. Fields missing in the page's JSON keep the defaults below, so a preset
// never depends on what ran before. The field names are the JSON keys.
[Serializable]
public class GyroscopePreset {
    public float rpm = 300f;                                                    // clamped to the apparatus (1000)
    public float addOnMassGrams = 0f;                                           // on the hub; 120 g balance the top
    public float theta0Deg = 15f;                                               // θ₀ of L⃗, clamped to 5–25°
    public string startMode = "guided";                                         // "guided" | "released"
    public float frictionFactor = 0f;                                           // Lagerreibung, × the apparatus' value (0 = none)
    public float pushThetaDot = 0.5f;                                           // rad/s, strength of the Stoß
    public float pushPhiDot = 0f;                                               // rad/s
    public float timeScale = 1f;
    public bool paused = false;
    public HelperFlags show = new HelperFlags();
}


// Messages to the page: {source, type, ...}.
[Serializable]
public class SimReadyMessage {
    public string source = "gyroscope";
    public string type = "ready";
}


// ~10 Hz in real time, also while paused. Rates as magnitudes except phiDot.
[Serializable]
public class GyroscopeStateMessage {
    public string source = "gyroscope";
    public string type = "state";
    public int run;                                                             // +1 on every restart
    public int segment;                                                         // +1 on every restart and rpm/weight/θ₀ change
    public float time;                                                          // s of simulated time since the restart
    public float thetaDeg;                                                      // tilt of L⃗ (the rod) from the vertical
    public float phiDeg;                                                        // precession angle, continuous (not wrapped)
    public float phiDot;                                                        // rad/s, signed: negative = backwards (hanging top)
    public float omegaR;                                                        // rad/s
    public float omegaP;                                                        // rad/s
    public float addOnMassGrams;
    public bool balanced;                                                       // no torque: the three cones can show
    public bool paused;
    public bool spinLimited;                                                    // drawn spin slower than the real one
    public bool fallen;                                                         // tipped onto its pedestal, until restart
}


// The page ↔ simulation contract. The page holds the Unity canvas itself (no iframe) and calls
//   unityInstance.SendMessage("WebBridge", method, value)
// with a number or a string, so switches come as 0/1 and structured data as a JSON string. Back to the page goes JSON
// through SimBridge.jslib → window.onSimMessage(message) (Assets/Shared/Plugins).
// The GameObject keeps the name WebBridge (the SendMessage target).
// Taken over from the precession repo's ModuleWebBridge, without apparatus switch and assembly game.
public class GyroscopeBridge : MonoBehaviour {
    [SerializeField] private GyroscopeController controller;
    [SerializeField] private GyroscopeHelpers helpers;
    [SerializeField] private SimulationTime simTime;
    [SerializeField] private float stateInterval = 0.1f;                        // s of real time

    [Header("Editor test")]
    [SerializeField, TextArea(3, 8)] private string testPreset =
        "{\"rpm\":300,\"addOnMassGrams\":120,\"theta0Deg\":10,\"pushThetaDot\":0.6,\"show\":{\"D\":false}}";
    [SerializeField, TextArea(3, 8)] private string lastMessage;                // what was sent last (written in Play mode)

    private readonly GyroscopeStateMessage state = new GyroscopeStateMessage();
    private float sinceState;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SimPostToPage(string json);                     // Assets/Shared/Plugins/SimBridge.jslib
#endif


    // ---- called from JavaScript via SendMessage ----

    public void ApplyPreset(string json) {
        var preset = new GyroscopePreset();
        if (!TryOverwrite(json, preset)) return;

        if (TryParseStartMode(preset.startMode, out var startMode)) controller.SetStartMode(startMode);
        controller.SetRpm(preset.rpm);
        controller.SetAddOnMassGrams(preset.addOnMassGrams);
        controller.SetTheta0Deg(preset.theta0Deg);
        controller.SetFriction(preset.frictionFactor);
        controller.SetPush(preset.pushThetaDot, preset.pushPhiDot);
        controller.Restart();                                                   // also if no value changed: a preset is a fresh start

        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(preset.show), helpers.Show);    // copy into the switchboard's object
        simTime.SetTimeScale(preset.timeScale);
        simTime.SetPaused(preset.paused);
    }

    // partial: {"trail":false} changes only the trail
    public void SetHelpers(string json) {
        TryOverwrite(json, helpers.Show);
    }

    public void SetStartMode(string value) {
        if (TryParseStartMode(value, out var startMode)) controller.SetStartMode(startMode);
    }

    public void SetRpm(float value) => controller.SetRpm(value);
    public void SetAddOnMassGrams(float value) => controller.SetAddOnMassGrams(value);
    public void SetTheta0Deg(float value) => controller.SetTheta0Deg(value);
    public void SetFriction(float factor) => controller.SetFriction(factor);
    public void SetTimeScale(float value) => simTime.SetTimeScale(value);
    public void SetPaused(float on) => simTime.SetPaused(on > 0.5f);
    public void SetPushThetaDot(float value) => controller.SetPush(value, controller.PushPhiDot);
    public void SetPushPhiDot(float value) => controller.SetPush(controller.PushThetaDot, value);
    public void Restart() => controller.Restart();
    public void Push() => controller.Push();


    [ContextMenu("Apply test preset")]
    private void ApplyTestPreset() => ApplyPreset(testPreset);


    void Start() {
        Post(JsonUtility.ToJson(new SimReadyMessage()));
    }


    // after the controller's Update, so the state is this frame's; real time, so the page also hears while paused
    void LateUpdate() {
        sinceState += Time.unscaledDeltaTime;
        if (sinceState < stateInterval || controller.Model == null) return;
        sinceState = 0f;

        state.run = controller.Run;
        state.segment = controller.Segment;
        state.time = (float)controller.SimTime;
        state.thetaDeg = (float)(controller.Theta * Mathf.Rad2Deg);
        state.phiDeg = (float)(controller.Model.Phi * Mathf.Rad2Deg);
        state.phiDot = (float)controller.Model.PhiDot;
        state.omegaR = (float)controller.OmegaR;
        state.omegaP = (float)controller.OmegaP;
        state.addOnMassGrams = controller.AddOnMassGrams;
        state.balanced = controller.Model.TorqueConst == 0;
        state.paused = simTime.Paused;
        state.spinLimited = controller.SpinDisplayLimited;
        state.fallen = controller.Fallen;
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
            Debug.LogWarning($"GyroscopeBridge: ignored malformed JSON ({e.Message}): {json}");
            return false;
        }
    }

    private static bool TryParseStartMode(string value, out GyroscopeController.StartMode startMode) {
        switch (value) {
            case "guided": startMode = GyroscopeController.StartMode.Guided; return true;
            case "released": startMode = GyroscopeController.StartMode.Released; return true;
        }
        Debug.LogWarning($"GyroscopeBridge: unknown start mode \"{value}\" (guided | released)");
        startMode = default;
        return false;
    }
}
