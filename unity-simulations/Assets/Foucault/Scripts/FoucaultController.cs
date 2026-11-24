using System;
using UnityEngine;


// Runs the Foucault pendulum: the physics (FoucaultModel, RK4 with a fixed step), the Zeitraffer, the measured swing
// plane, the pose of bob and wire, the pins and the floor trace, and the star-fixed view at the poles.
// Sits on the Pendulum object: its origin is the suspension point, its axes are the hall's (x East, y up, z North),
// and it is a child of the Hall, the Earth-fixed root of everything that turns with the Earth.
//
// Time: the swing takes seconds, the plane's turning hours, so time runs up to a few thousand times faster than real
// time (Zeitraffer). It is not Unity's time scale (capped at 100, and fixedDeltaTime would grow with it): every frame
// integrates Time.deltaTime × factor of simulated time in fixed steps. Pause still goes through SimulationTime
// (Time.timeScale = 0, so Time.deltaTime = 0), like on the Kreisel. A frame does at most maxStepsPerFrame steps; a
// slower computer then runs slower than asked (Lagging, ActualTimeFactor).
//
// Frames: in the Earth frame (always) the hall stands still and the plane turns. In the star-fixed frame (only at the
// poles, Carlo 2026-11-24: elsewhere the hall would also tilt around the Earth's axis) the Hall object turns at
// Ω_E sin φ beneath the camera; the pendulum is its child, so its plane stays fixed in space without any extra
// physics. Switching back freezes the hall where it is (still a correct Earth-frame view, only from another side).
//
// The setters only note a change: Breitengrad continues the running pendulum (Ω changes, the swing goes on),
// amplitude and Restart release it anew. A preset from the page costs one restart.
public class FoucaultController : MonoBehaviour {
    public enum Frame { Earth, Stars }

    [Header("Apparatus (estimated)")]
    [SerializeField] private float length = 15f;                               // m, suspension to the bob's centre
    [SerializeField] private float bobMassKg = 25f;                             // only for the Coriolis force's size
    [SerializeField] private float stylusReach = 0.21f;                         // m, bob centre to stylus tip
    [SerializeField] private int pinCount = 48;
    [SerializeField] private float pinHitDistance = 0.015f;                     // m, stylus radius + pin radius
    [SerializeField] private float pinInset = 0.06f;                            // m inside the amplitude

    [Header("Parts")]
    [SerializeField] private Transform hall;                                    // Earth-fixed root, turns in the star view
    [SerializeField] private Transform bob;                                     // its local down points along the wire
    [SerializeField] private Transform wire;                                    // smooth cylinder from the suspension to the bob
    [SerializeField] private float bobRadius = 0.13f;                           // m
    [SerializeField] private float wireDiameter = 0.006f;                       // m (thicker than real: else sub-pixel)
    [SerializeField] private PinRingDisplay pins;
    [SerializeField] private TraceDisplay trace;
    // a new trace line only after the plane has turned this far: one line per half swing would turn by only ~0.02°
    // (pole) and pile up into a solid white wedge; spaced lines draw the classic fan of the floor trace
    [SerializeField] private float traceSpacingDeg = 1.5f;

    [Header("Experiment")]
    [SerializeField, Range(-90f, 90f)] private float latitudeDeg = 51.5f;       // Breitengrad, north positive
    [SerializeField, Range(0.8f, 1.6f)] private float amplitude = 1.2f;        // m, the bob pulled aside horizontally
    [SerializeField] private float startAzimuthDeg = 0f;                        // released towards East, swinging east-west
    [SerializeField] private Frame frame = Frame.Earth;
    [SerializeField] private float timeFactor = 1f;                             // Zeitraffer, 1 = real time

    [Header("Numerics")]
    [SerializeField] private float stepSeconds = 0.02f;                         // s of simulated time per RK4 step
    [SerializeField] private int maxStepsPerFrame = 6000;                       // 2000× at 20 fps

    [Header("Display")]
    [SerializeField] private float fanFromFactor = 20f;                         // the fan fades in from here …
    [SerializeField] private float fanFullFactor = 100f;                        // … to here; the bob hides at half

    [Header("Readout (written in Play mode)")]
    [SerializeField] private float turnedDeg;
    [SerializeField] private float simHours;
    [SerializeField] private float actualFactor;

    private FoucaultModel model;
    private PinRing pinRing;
    private bool resetPending, latitudePending;
    private double pendingSimSeconds;
    private double lastAzimuth;                                                 // measured, for unwrapping
    private double previousRadialSpeed;                                         // for the turning points of the trace
    private Vec3d lastTracePoint;                                               // the newest turning point in the trace
    private double hallAngle;                                                   // rad, counter-clockwise from above
    private Renderer[] bobRenderers;
    private float factorDone = 1f;                                              // smoothed share of the asked factor done

    public FoucaultModel Model => model;
    public PinRing Pins => pinRing;
    public Transform Hall => hall;
    public float Length => length;
    public float BobMassKg => bobMassKg;
    public float LatitudeDeg => latitudeDeg;
    public float Amplitude => amplitude;
    public float TimeFactor => timeFactor;
    public Frame SelectedFrame => frame;
    public Frame ShownFrame => StarsAllowed ? frame : Frame.Earth;
    public bool StarsAllowed => Mathf.Abs(latitudeDeg) >= 89.99f;

    public double SimTime { get; private set; }                                 // s since the release
    public int Run { get; private set; }                                        // +1 on every release
    public int Segment { get; private set; }                                    // +1 on every release and latitude change
    public double PlaneStartAzimuth { get; private set; }                       // rad, counter-clockwise from East
    public double PlaneTurned { get; private set; }                             // rad, measured, unwrapped, signed
    public double PlaneAzimuth => PlaneStartAzimuth + PlaneTurned;
    public double SwingMajor { get; private set; }                              // m, semi-axes of the swing
    public double SwingMinor { get; private set; }
    public float ActualTimeFactor => timeFactor * factorDone;
    public bool Lagging => factorDone < 0.95f;
    // 0 … 1: how much the swept fan replaces the bob (the bob would alias at high factors)
    public float FanAmount { get; private set; }
    public bool BobVisible => FanAmount < 0.5f;
    public double HallAngle => hallAngle;

    // the bob's centre in world space, as drawn
    public Vector3 BobWorldPosition => bob.position;


    // ---- called by the bridge / Inspector ----

    public void SetLatitudeDeg(float value) {
        latitudeDeg = Mathf.Clamp(value, -90f, 90f);
        latitudePending = true;
    }

    public void SetAmplitude(float value) {
        amplitude = Mathf.Clamp(value, 0.8f, 1.6f);
        resetPending = true;
    }

    public void SetFrame(Frame value) => frame = value;

    public void SetTimeFactor(float value) {
        timeFactor = Mathf.Clamp(value, 0f, 5000f);
        pendingSimSeconds = 0;                                                  // no backlog from the old factor
    }

    [ContextMenu("Neustart")]
    public void Restart() => resetPending = true;


    private void ResetSimulation() {
        model = new FoucaultModel(length) { Latitude = latitudeDeg * Mathf.Deg2Rad };
        double azimuth = startAzimuthDeg * Mathf.Deg2Rad;
        model.Release(amplitude, azimuth);
        model.SwingEllipse(out lastAzimuth, out double major, out double minor);
        PlaneStartAzimuth = azimuth;
        PlaneTurned = 0;
        SwingMajor = major;
        SwingMinor = minor;
        SimTime = 0;
        pendingSimSeconds = 0;
        previousRadialSpeed = 0;
        hallAngle = 0;
        resetPending = latitudePending = false;
        Run++;
        Segment++;

        if (pinRing == null || pinRing.Count != pinCount) pinRing = new PinRing(pinCount, pinHitDistance);
        pinRing.Reset(amplitude * (length + stylusReach) / length - pinInset, azimuth);
        if (pins != null) pins.Place(pinRing);
        if (trace != null) {
            trace.Clear();
            trace.AddPoint(Horizontal(model.Position));                       // the release point starts the trace
            lastTracePoint = model.Position;
        }
    }


    void Awake() {
        bobRenderers = bob.GetComponentsInChildren<Renderer>(true);
    }


    void Start() {
        ResetSimulation();
        ApplyPose();
    }


    // Inspector changes in Play mode: release anew, like a preset
    void OnValidate() {
        if (Application.isPlaying && model != null) resetPending = true;
    }


    void Update() {
        if (resetPending) {
            ResetSimulation();
        } else if (latitudePending) {
            model.Latitude = latitudeDeg * Mathf.Deg2Rad;                       // the pendulum swings on
            latitudePending = false;
            Segment++;
        }

        Integrate(Time.deltaTime);
        MeasurePlane();
        ApplyPose();

        float lowLog = Mathf.Log10(fanFromFactor), highLog = Mathf.Log10(fanFullFactor);
        FanAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lowLog, highLog, Mathf.Log10(Mathf.Max(timeFactor, 1e-3f))));

        turnedDeg = (float)(PlaneTurned * Mathf.Rad2Deg);
        simHours = (float)(SimTime / 3600);
        actualFactor = ActualTimeFactor;
    }


    // Time.deltaTime × factor of simulated time in fixed steps; the rest waits for the next frame
    private void Integrate(float deltaTime) {
        if (deltaTime <= 0f) return;                                            // paused
        pendingSimSeconds += deltaTime * (double)timeFactor;
        int steps = (int)(pendingSimSeconds / stepSeconds);
        float done = 1f;
        if (steps > maxStepsPerFrame) {                                         // too slow for this factor: drop the rest
            done = (float)(maxStepsPerFrame * stepSeconds / pendingSimSeconds);
            steps = maxStepsPerFrame;
            pendingSimSeconds = steps * (double)stepSeconds;
        }
        factorDone = Mathf.Lerp(factorDone, done, 1f - Mathf.Exp(-deltaTime / 0.5f));
        pendingSimSeconds -= steps * (double)stepSeconds;

        double stylusScale = (length + stylusReach) / length;                  // stylus tip: on the wire's line, below the bob
        double omegaZ = model.Omega.Z;
        bool stars = ShownFrame == Frame.Stars;
        for (int i = 0; i < steps; i++) {
            Vec3d before = model.Position;
            model.Step(stepSeconds);
            SimTime += stepSeconds;
            if (stars) hallAngle += omegaZ * stepSeconds;

            Vec3d p = model.Position, v = model.Velocity;
            pinRing.Sweep(before.X * stylusScale, before.Y * stylusScale, p.X * stylusScale, p.Y * stylusScale, SimTime);

            // turning point (farthest from the centre): a candidate for the trace. The swing is a line through the
            // centre to far better than a millimetre, so chords between turning points draw it exactly.
            double radialSpeed = p.X * v.X + p.Y * v.Y;
            if (previousRadialSpeed > 0 && radialSpeed <= 0 && trace != null) TryTracePoint(p);
            previousRadialSpeed = radialSpeed;
        }
    }


    // A turning point joins the trace if it lies at the other end of the swing than the newest one (so every line still
    // runs through the centre) and the plane has turned by traceSpacingDeg since. The chord between them misses the
    // centre by amplitude × sin(spacing / 2), 1.6 cm at 1.5°: about the line's width.
    private void TryTracePoint(Vec3d p) {
        double ax = -lastTracePoint.X, ay = -lastTracePoint.Y;                  // the opposite end of the newest line
        double dot = p.X * ax + p.Y * ay;
        if (dot <= 0) return;                                                   // same end as the newest point
        double turned = Math.Abs(Math.Atan2(ax * p.Y - ay * p.X, dot));
        if (turned < traceSpacingDeg * Mathf.Deg2Rad) return;
        trace.AddPoint(Horizontal(p));
        lastTracePoint = p;
    }


    // the plane from the swing's ellipse, unwrapped against the last frame (it turns by far less than 90° per frame)
    private void MeasurePlane() {
        model.SwingEllipse(out double azimuth, out double major, out double minor);
        PlaneTurned += FoucaultModel.PlaneDelta(lastAzimuth, azimuth);
        lastAzimuth = azimuth;
        SwingMajor = major;
        SwingMinor = minor;
    }


    // model (East-North-Up, from the suspension) → this object's local space; the Hall turns in the star view
    private void ApplyPose() {
        hall.localRotation = Quaternion.AngleAxis(PhysicsToUnity.Angle(hallAngle), Vector3.up);

        Vector3 r = PhysicsToUnity.Vector(model.Position);
        Vector3 down = r.normalized;
        bob.localPosition = r;
        bob.localRotation = Quaternion.FromToRotation(Vector3.down, down);
        float wireLength = length - bobRadius;
        wire.localPosition = down * (wireLength / 2);
        wire.localRotation = Quaternion.FromToRotation(Vector3.up, down);
        wire.localScale = new Vector3(wireDiameter, wireLength / 2, wireDiameter);

        bool visible = BobVisible;
        if (bobRenderers[0].enabled != visible) {
            foreach (Renderer r2 in bobRenderers) r2.enabled = visible;
            wire.GetComponent<Renderer>().enabled = visible;
        }
        // the live piece from the newest point to the bob. With the bob hidden in the fan it would jump about every
        // frame: then it is the whole swing through the centre (at the equator the only line there is)
        if (trace != null) trace.SetLivePoint(Horizontal(visible ? model.Position : -lastTracePoint));
    }


    // the bob's place on the floor plan: Unity (x, z) = model (East, North)
    private static Vector2 Horizontal(Vec3d p) => new Vector2((float)p.X, (float)p.Y);
}
