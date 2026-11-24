using System;
using UnityEngine;


// What may be shown. A plain serializable class, so the page can send it as JSON (inside a preset, or alone for a
// partial change: JsonUtility.FromJsonOverwrite only touches the fields named in the JSON). The field names are the
// JSON keys.
[Serializable]
public class FoucaultHelperFlags {
    public bool omega = true;                                                   // Ω with its vertical and horizontal part
    public bool coriolis = true;                                                // Coriolis force on the bob
    public bool plane = true;                                                   // current and starting plane, arc between
    public bool compass = true;                                                 // N, O, S, W on the platform
    public bool labels = true;                                                  // the vectors' symbols
    public bool trace = true;                                                   // the bob's trace on the floor
}


// Switchboard for everything that helps reading the motion but isn't part of the apparatus. The page sets these through
// the bridge (FoucaultBridge); in the editor the Inspector toggles work live, because the flags are applied every
// frame. All off = the bare experiment: pendulum, platform with its degree ring, pins. The fan of high Zeitraffer is
// not a helper (FanDisplay).
public class FoucaultHelpers : MonoBehaviour {
    [SerializeField] private FoucaultVectors vectors;
    [SerializeField] private GameObject labels;
    [SerializeField] private GameObject plane;
    [SerializeField] private GameObject compass;
    [SerializeField] private GameObject trace;

    [SerializeField] private FoucaultHelperFlags show = new FoucaultHelperFlags();

    public FoucaultHelperFlags Show => show;                                    // a reference: changes apply in the next frame


    void LateUpdate() {
        vectors.ShowOmega = show.omega;
        vectors.ShowCoriolis = show.coriolis;
        SetActive(labels, show.labels);
        SetActive(plane, show.plane);
        SetActive(compass, show.compass);
        // the trace keeps recording while hidden (TraceDisplay is on it): only its renderer goes
        var renderer = trace.GetComponent<MeshRenderer>();
        if (renderer.enabled != show.trace) renderer.enabled = show.trace;
    }

    private static void SetActive(GameObject go, bool on) {
        if (go.activeSelf != on) go.SetActive(on);
    }
}
