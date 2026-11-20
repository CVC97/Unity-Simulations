using System;
using UnityEngine;
using UnityEngine.Serialization;


// Runs the lecture gyroscope: the physics (GyroscopeModel, RK4 in FixedUpdate), its pose, the trail and the stop on
// the pedestal. Parameters in lecture language: Drehzahl, Zusatzgewicht on the hub, Neigung, guided or released start,
// push, friction.
// The setters only note a change, applied once in the next FixedUpdate, so a whole preset from the page costs one
// restart, not one per value. The start mode restarts; Drehzahl, Zusatzgewicht and Neigung continue the running
// gyroscope from where it is (ContinueSimulation).
// Taken over from the precession repo's ModuleGyroscopeController (module build, 2026-11), without its lab gyroscope,
// simple top and assembly game.
public class GyroscopeController : MonoBehaviour, IGyroscopeModelSource {
    public enum StartMode { Released, Guided }                                  // losgelassen / geführt

    [Header("Apparatus")]
    [FormerlySerializedAs("lectureRig")]
    [SerializeField] private ApparatusRig rig;

    [Header("Experiment")]
    [SerializeField, Range(0f, 1000f)] private float rpm = 300f;                // Drehzahl
    [SerializeField, Range(0f, 300f)] private float addOnMassGrams = 0f;        // Zusatzgewicht on the hub
    [SerializeField, Range(0f, 40f)] private float theta0Deg = 15f;             // Neigung θ₀ of L⃗; clamped to the rig's limits
    [SerializeField] private StartMode startMode = StartMode.Guided;
    [SerializeField, Range(0f, 10f)] private float frictionFactor = 0f;         // Lagerreibung: 0 = none, 1 = the apparatus' value

    [Header("Push (Stoß)")]
    [SerializeField] private float pushThetaDot = 0.5f;                         // rad/s, knock on the rod
    [SerializeField] private float pushPhiDot = 0f;                             // rad/s, knock from the side

    [Header("Numerics")]
    [SerializeField, Range(1, 1000)] private int substeps = 10;                 // RK4 steps per FixedUpdate

    [Header("Display")]
    [SerializeField] private float maxVisibleSpinRps = 5f;                      // revolutions per REAL second
    // trail points per physics step, computed from the integration: the pose changes at only 50 Hz, too coarse for
    // fast nutation. 0 = off, the trail takes the pose once per frame.
    [SerializeField, Range(0, 20)] private int trailSamplesPerStep = 8;

    [Header("Readout (written in Play mode)")]
    [SerializeField] private float omegaR;                                      // rad/s
    [SerializeField] private float omegaP;                                      // rad/s
    [SerializeField] private float thetaDeg;
    [SerializeField] private float energyError;

    private GyroscopeModel gyroscope;
    private VectorDisplay vectors;
    private bool resetPending;
    private bool changePending;                                                 // rpm, weight or θ₀: continue, no restart
    private bool tiltPending;                                                   // θ₀ changed: the rod goes to the new tilt
    private bool pushPending;
    private double e0, spinEnergy0;
    private float displaySpinDeg;
    private Vector3 trailTipLocal, lastTrailPoint;

    public GyroscopeModel Model => gyroscope;
    public ApparatusRig Rig => rig;
    public bool SpinDisplayLimited { get; private set; }
    // the top tipped as far as its pedestal allows (ApparatusRig.StopThetaDeg): the physics stops until the next
    // restart; a slider change after that restarts
    public bool Fallen { get; private set; }

    public float Rpm => rpm;
    public float AddOnMassGrams => addOnMassGrams;
    public float Theta0Deg => theta0Deg;
    public StartMode SelectedStartMode => startMode;
    public float FrictionFactor => frictionFactor;
    public float PushThetaDot => pushThetaDot;
    public float PushPhiDot => pushPhiDot;

    public double SimTime { get; private set; }                                 // s since the last restart, simulated time
    public int Run { get; private set; }                                        // counts restarts: the page sees when a new run begins
    public int Segment { get; private set; }                                    // counts restarts and parameter changes

    // θ: angle between L⃗ (the rod, disk side) and the vertical. The figure axis points along the rod to the disk and
    // the spin is positive (SpinSign +1), so it is the model's θ.
    public double Theta => gyroscope.Theta;

    // readouts as magnitudes (the signs only encode the spin sense). Drehzahl = spin about the figure axis ω₃,
    // constant without friction; ψ̇ (the spin relative to the rod's own frame, what the lab's light barrier saw) would
    // mix in precession and nutation (ψ̇ = ω₃ − φ̇ cos θ).
    public double OmegaR => Math.Abs(gyroscope.Omega3);
    public double OmegaP => Math.Abs(gyroscope.PhiDot);
    // relative to the initial spin energy; only meaningful without friction
    public double EnergyError => spinEnergy0 > 0 ? (gyroscope.Energy - e0) / spinEnergy0 : 0;


    // ---- called by the bridge / Inspector ----

    public void SetRpm(float value) {
        rpm = value;
        changePending = true;
    }

    public void SetAddOnMassGrams(float value) {
        addOnMassGrams = value;
        changePending = true;
    }

    public void SetTheta0Deg(float value) {
        theta0Deg = value;
        changePending = true;
        tiltPending = true;
    }

    public void SetStartMode(StartMode value) {
        startMode = value;
        resetPending = true;
    }

    // no restart: the friction changes while the gyroscope runs
    public void SetFriction(float factor) {
        frictionFactor = Mathf.Max(0f, factor);
        if (gyroscope != null) gyroscope.FrictionCoeff = FrictionCoeff(rig.Parameters);
    }

    private double FrictionCoeff(ApparatusParameters p) => frictionFactor * p.FrictionCoeff;

    [ContextMenu("Neustart")]
    public void Restart() {
        resetPending = true;
    }

    // strength of the next push, no restart
    public void SetPush(float thetaDot, float phiDot) {
        pushThetaDot = thetaDot;
        pushPhiDot = phiDot;
    }

    // applied in the next FixedUpdate, after a pending restart: a preset followed by a push hits the new run
    [ContextMenu("Stoß")]
    public void Push() {
        pushPending = true;
    }


    private void ResetSimulation() {
        ApparatusParameters p = rig.Parameters;
        vectors.SetScales(rig.VectorScales.x, rig.VectorScales.y, rig.VectorScales.z);

        double addOnMass = addOnMassGrams / 1000.0;
        gyroscope = GyroscopeModel.FromInertia(p.I1(addOnMass), p.I3, p.TorqueConst(addOnMass));
        gyroscope.FrictionCoeff = FrictionCoeff(p);

        double omega3 = p.SpinSign * Mathf.Min(rpm, p.MaxRpm) * 2*Math.PI / 60;
        double theta0 = Mathf.Clamp(theta0Deg, rig.MinTheta0Deg, rig.MaxTheta0Deg) * Math.PI / 180;
        double phiDot0 = 0;                                                     // released: the rod starts at rest
        if (startMode == StartMode.Guided) {
            phiDot0 = gyroscope.SteadyPrecessionRate(theta0, omega3);           // guided by hand into steady precession
            if (double.IsNaN(phiDot0)) {
                Debug.LogWarning("Guided start: spin too slow for steady precession, starting released instead.");
                phiDot0 = 0;
            }
        }
        gyroscope.SetInitialConditions(theta0, 0, phiDot0, omega3);

        e0 = gyroscope.Energy;
        spinEnergy0 = 0.5 * p.I3 * omega3*omega3;
        resetPending = changePending = tiltPending = false;
        Fallen = false;
        SimTime = 0;
        Run++;
        Segment++;

        ApplyPose();
        if (rig.Trail != null) {
            rig.Trail.Clear();
            // the trail sits on the body (not the spinning rotor): its place in this object's frame is fixed
            trailTipLocal = transform.InverseTransformPoint(rig.Trail.transform.position);
        }
        lastTrailPoint = Vector3.positiveInfinity;                              // the next sample always counts
    }


    // Drehzahl, Zusatzgewicht or Neigung changed while the gyroscope runs: it goes on from where it is (azimuth, spin
    // angle, θ̇), with the new values; a new θ₀ tilts the rod at its current azimuth. Guided stays guided: φ̇ shifts by
    // the change of the steady precession rate, so a calm precession stays calm, and any nodding keeps its deviation
    // from it (the balanced top keeps nutating after a push). SimTime and the trail go on.
    private void ContinueSimulation() {
        ApparatusParameters p = rig.Parameters;
        GyroscopeModel before = gyroscope;

        double addOnMass = addOnMassGrams / 1000.0;
        gyroscope = GyroscopeModel.FromInertia(p.I1(addOnMass), p.I3, p.TorqueConst(addOnMass));
        gyroscope.FrictionCoeff = FrictionCoeff(p);

        double omega3 = p.SpinSign * Mathf.Min(rpm, p.MaxRpm) * 2*Math.PI / 60;
        double theta = tiltPending
            ? Mathf.Clamp(theta0Deg, rig.MinTheta0Deg, rig.MaxTheta0Deg) * Math.PI / 180
            : before.Theta;
        double phiDot = before.PhiDot;
        if (startMode == StartMode.Guided) {
            double steadyBefore = before.SteadyPrecessionRate(before.Theta, before.Omega3);
            double steadyAfter = gyroscope.SteadyPrecessionRate(theta, omega3);
            if (!double.IsNaN(steadyBefore) && !double.IsNaN(steadyAfter)) phiDot += steadyAfter - steadyBefore;
        }
        gyroscope.SetState(theta, before.Phi, before.Psi, before.ThetaDot, phiDot, omega3);

        e0 = gyroscope.Energy;
        spinEnergy0 = 0.5 * p.I3 * omega3*omega3;
        changePending = tiltPending = false;
        Segment++;
    }


    void Awake() {
        vectors = GetComponent<VectorDisplay>();
    }


    void Start() {
        ResetSimulation();
    }


    // Inspector changes in Play mode: restart once, like a preset
    void OnValidate() {
        if (Application.isPlaying && gyroscope != null) {
            resetPending = true;
        }
    }


    void FixedUpdate() {
        if (resetPending || (changePending && Fallen)) {                        // a fallen top can't continue: start again
            ResetSimulation();
        } else if (changePending) {
            ContinueSimulation();
        }
        if (Fallen) {
            pushPending = false;                                                // a Stoß on a fallen top is lost, not saved up
            return;
        }
        if (pushPending) {
            gyroscope.Push(pushThetaDot, pushPhiDot);
            pushPending = false;
        }
        double h = (double)Time.fixedDeltaTime / substeps;
        int trailInterval = trailSamplesPerStep > 0 ? Mathf.Max(1, substeps / trailSamplesPerStep) : 0;
        float stopDeg = rig.StopThetaDeg;
        for (int i = 0; i < substeps; i++) {
            gyroscope.RK4Step(h);
            if (trailInterval > 0 && (i + 1) % trailInterval == 0) AddTrailPoint();
            if (stopDeg > 0 && Theta * 180 / Math.PI >= stopDeg) {              // a leg touches the pin: stop here
                Fallen = true;
                break;
            }
        }
        SimTime += Time.fixedDeltaTime;
    }


    void Update() {
        // paused (time scale 0): no FixedUpdate, but a new preset, Neustart or a new tilt should show at once
        if (Time.timeScale == 0f) {
            if (resetPending || (changePending && Fallen)) {
                ResetSimulation();
            } else if (changePending) {
                ContinueSimulation();
            }
        }
        if (!Fallen) UpdateDisplaySpin();                                       // a fallen top rests on its pedestal
        ApplyPose();

        omegaR = (float)OmegaR;
        omegaP = (float)OmegaP;
        thetaDeg = (float)(Theta * 180 / Math.PI);
        energyError = (float)EnergyError;
    }


    // the drawn spin follows the real one, limited to a few turns per real second (else it would alias)
    private void UpdateDisplaySpin() {
        float spinRateDeg = -(float)gyroscope.PsiDot * Mathf.Rad2Deg * Time.timeScale;
        float maxRateDeg = maxVisibleSpinRps * 360f;

        SpinDisplayLimited = Mathf.Abs(spinRateDeg) > maxRateDeg;
        if (SpinDisplayLimited) {
            spinRateDeg = Mathf.Sign(spinRateDeg) * maxRateDeg;
        }

        displaySpinDeg += spinRateDeg * Time.unscaledDeltaTime;                 // real time
        displaySpinDeg = Mathf.Repeat(displaySpinDeg, 360f);
    }


    // model angles (rad, right-handed) → Unity degrees (left-handed: sign flip); local up of this object = figure axis
    private void ApplyPose() {
        rig.Rotor.localRotation = Quaternion.AngleAxis(displaySpinDeg, Vector3.up);
        transform.localRotation = ModelRotation();
    }

    private Quaternion ModelRotation() {
        Quaternion tilt = Quaternion.AngleAxis(PhysicsToUnity.Angle(gyroscope.Theta), Vector3.right);
        Quaternion precession = Quaternion.AngleAxis(PhysicsToUnity.Angle(gyroscope.Phi), Vector3.up);
        return precession * tilt;
    }


    // trail point at the current model state, also between two poses; skips points closer than minVertexDistance.
    // Emitting must stay on: with it off the trail disappeared without any error. The automatic point at the pose adds
    // nothing, because the last sample of a physics step lies at the pose.
    private void AddTrailPoint() {
        TrailRenderer trail = rig.Trail;
        if (trail == null || !trail.enabled) return;
        Vector3 point = transform.parent != null
            ? transform.parent.TransformPoint(transform.localPosition + ModelRotation() * trailTipLocal)
            : transform.localPosition + ModelRotation() * trailTipLocal;
        if ((point - lastTrailPoint).sqrMagnitude < trail.minVertexDistance * trail.minVertexDistance) return;
        trail.AddPosition(point);
        lastTrailPoint = point;
    }
}
