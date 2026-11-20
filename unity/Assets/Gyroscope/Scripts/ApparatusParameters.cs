using UnityEngine;


// What the controller needs from an apparatus: a symmetric top with one fixed point, described by GyroscopeModel;
// only these numbers differ between tops. Implemented by LectureGyroscope.
// Convention: the figure axis points from the pivot along the rod to the disk; k > 0 pulls it down (top-heavy),
// k < 0 back up (hanging, centre of mass below the tip).
public abstract class ApparatusParameters : ScriptableObject {
    public abstract double I3 { get; }                              // kg·m², about the figure axis
    public abstract double I1(double addOnMass);                    // kg·m², about a horizontal axis through the pivot
    public abstract double TorqueConst(double addOnMass);           // N·m, D = k sinθ
    public abstract double FrictionCoeff { get; }                   // N·m·s, bearing torque −c ψ̇ at friction factor 1

    // sign of ω3 for which L points from the pivot along the figure axis
    public abstract int SpinSign { get; }

    // realistic upper spin rate; the arrows are scaled for this range (the controller clamps to it)
    public abstract float MaxRpm { get; }
}
