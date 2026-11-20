using System;
using UnityEngine;


// What may be shown. A plain serializable class, so the page can send it as JSON (inside a preset, or alone for a
// partial change: JsonUtility.FromJsonOverwrite only touches the fields named in the JSON). The field names are the
// JSON keys; L and D are capitals like the physics symbols.
[Serializable]
public class HelperFlags {
    public bool L = true;
    public bool omega = true;
    public bool D = true;
    public bool labels = true;
    public bool precessionCone = true;
    public bool nutationCone = true;
    public bool spaceCone = true;
    public bool bodyCone = true;
    public bool trail = true;                                                   // grey trail of the spike's tip
}


// Switchboard for everything that helps reading the motion but isn't part of the apparatus: vectors, their labels,
// cones, the trail. The page sets these through the bridge (GyroscopeBridge); in the editor the Inspector toggles work
// live, because the flags are applied every frame. All off = the bare experiment.
// The flags say what may be shown; for the cones the physics decides as well: the precession cone needs a torque,
// the nutation cones need none (only then is L⃗ fixed and they are more than momentary shapes).
public class GyroscopeHelpers : MonoBehaviour {
    [SerializeField] private GyroscopeController controller;
    [SerializeField] private VectorDisplay vectors;
    [SerializeField] private GameObject labels;
    [SerializeField] private GameObject precessionCone;                         // cone swept by the rod, with its ω_P label
    [SerializeField] private GameObject nutationCone;                           // Nutationskegel: figure axis around L⃗
    [SerializeField] private GameObject spaceCone;                              // Rastpolkegel: ω⃗ around L⃗
    [SerializeField] private GameObject bodyCone;                               // Gangpolkegel: ω⃗ around the figure axis

    [SerializeField] private HelperFlags show = new HelperFlags();

    public HelperFlags Show => show;                                            // a reference: changes apply in the next frame


    void LateUpdate() {
        vectors.ShowL = show.L;
        vectors.ShowOmega = show.omega;
        vectors.ShowD = show.D;
        labels.SetActive(show.labels);

        bool forceFree = controller.Model != null && controller.Model.TorqueConst == 0;    // balanced top
        precessionCone.SetActive(show.precessionCone && !forceFree);
        nutationCone.SetActive(show.nutationCone && forceFree);
        spaceCone.SetActive(show.spaceCone && forceFree);
        bodyCone.SetActive(show.bodyCone && forceFree);

        TrailRenderer trail = controller.Rig.Trail;
        if (trail.enabled != show.trail) {
            trail.Clear();                                                      // no old segments when it comes back
            trail.enabled = show.trail;
        }
    }
}
