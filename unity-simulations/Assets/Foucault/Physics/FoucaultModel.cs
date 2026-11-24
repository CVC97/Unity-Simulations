using System;


// The Foucault pendulum: a spherical pendulum (point mass on a massless wire of fixed length) in the Earth-fixed,
// rotating frame. Local frame East–North–Up (x, y, z), right-handed, suspension at the origin, the bob below it:
//
//     r̈ = g + λ r − 2 Ω × ṙ,     |r| = L,     g = (0, 0, −g),     Ω = Ω_E (0, cos φ, sin φ)
//
// The centrifugal term is absorbed in g (it only tilts and shortens it a little). λ r is the wire's tension per mass,
// fixed by the constraint: d²|r|²/dt² = 0 gives λ = −(|ṙ|² + r·(g − 2 Ω × ṙ)) / |r|².
// Cartesian, not spherical coordinates: those are singular at the bottom, where the bob passes on every swing. The
// full nonlinear equations, no small-angle solution: the Airy precession of an elliptical swing is a nonlinear effect.
// RK4 with a fixed step, then the state is put back onto the sphere (|r| = L, ṙ ⊥ r), so the wire length never drifts.
// Angles in the plane: azimuth from East counter-clockwise seen from above (East 0, North π/2), like the x-y plane.
public class FoucaultModel {
    public const double SiderealDay = 86164.0905;                               // s
    public const double EarthRateDefault = 2 * Math.PI / SiderealDay;           // Ω_E ≈ 7.292·10⁻⁵ rad/s

    private readonly double length, gravity, earthRate;
    private double latitude;
    private Vec3d omega;
    private Vec3d r, v;

    public FoucaultModel(double length, double gravity = 9.81, double earthRate = EarthRateDefault) {
        this.length = length;
        this.gravity = gravity;
        this.earthRate = earthRate;
        Latitude = 0;
        Release(0, 0);
    }

    public double Length => length;                                             // m
    public double Gravity => gravity;                                           // m/s²
    public double EarthRate => earthRate;                                       // rad/s
    public double Omega0 => Math.Sqrt(gravity / length);                        // rad/s, small swings
    public double SwingPeriod => 2 * Math.PI / Omega0;                          // s, small swings
    public double Time { get; private set; }                                    // s since the release

    // rad, north positive; changes Ω, not the state: the running pendulum goes on from where it is
    public double Latitude {
        get => latitude;
        set {
            latitude = value;
            omega = new Vec3d(0, earthRate * Math.Cos(value), earthRate * Math.Sin(value));
        }
    }

    public Vec3d Omega => omega;                                                // Earth's angular velocity, local frame
    public Vec3d Position => r;                                                 // m, from the suspension
    public Vec3d Velocity => v;                                                 // m/s, in the Earth frame

    // the swing plane's turning rate for small swings (rad/s, counter-clockwise seen from above positive):
    // only the vertical part of Ω turns it; north (φ > 0) clockwise, south counter-clockwise, none at the equator
    public double PlaneRate => -earthRate * Math.Sin(latitude);

    // Coriolis acceleration −2 Ω × ṙ (m/s²); times the mass it is the Coriolis force
    public Vec3d CoriolisAcceleration => Coriolis(v);

    // energy per mass in the rotating frame (the Jacobi constant; the Coriolis force does no work), J/kg
    public double JacobiEnergy => 0.5 * v.SqrLength + gravity * r.Z;


    // pulled aside by `amplitude` (m, horizontally) towards `azimuth` (rad) and let go from rest (in the Earth frame):
    // the bob swings through the centre, its trace on the floor is the classic star of cusps
    public void Release(double amplitude, double azimuth) {
        double a = Math.Min(Math.Abs(amplitude), 0.99 * length);
        double z = -Math.Sqrt(length * length - a * a);
        SetState(new Vec3d(a * Math.Cos(azimuth), a * Math.Sin(azimuth), z), default);
    }

    // any start (tests: elliptical swings); put onto the sphere like after every step
    public void SetState(Vec3d position, Vec3d velocity) {
        r = position;
        v = velocity;
        Project();
        Time = 0;
    }


    public void Step(double h) {
        Vec3d k1r = v,                k1v = Acceleration(r, v);
        Vec3d k2r = v + k1v * (h/2),  k2v = Acceleration(r + k1r * (h/2), v + k1v * (h/2));
        Vec3d k3r = v + k2v * (h/2),  k3v = Acceleration(r + k2r * (h/2), v + k2v * (h/2));
        Vec3d k4r = v + k3v * h,      k4v = Acceleration(r + k3r * h, v + k3v * h);
        r = r + (k1r + k2r * 2 + k3r * 2 + k4r) * (h/6);
        v = v + (k1v + k2v * 2 + k3v * 2 + k4v) * (h/6);
        Project();
        Time += h;
    }


    private Vec3d Coriolis(Vec3d velocity) => Vec3d.Cross(omega, velocity) * -2;

    private Vec3d Acceleration(Vec3d position, Vec3d velocity) {
        Vec3d free = new Vec3d(0, 0, -gravity) + Coriolis(velocity);
        double lambda = -(velocity.SqrLength + Vec3d.Dot(position, free)) / position.SqrLength;
        return free + position * lambda;
    }

    // back onto the sphere |r| = L, velocity tangential
    private void Project() {
        double rl = r.Length;
        if (rl < 1e-12) {                                                       // never at the suspension: hang down
            r = new Vec3d(0, 0, -length);
            rl = length;
        }
        Vec3d n = r / rl;
        r = n * length;
        v = v - n * Vec3d.Dot(v, n);
    }


    // The swing as an ellipse in the horizontal plane, from the tensor A = ω₀² p pᵀ + v vᵀ of the horizontal position p
    // and velocity v. For the linear (isotropic) oscillator it is constant along the orbit and its eigenvectors are the
    // ellipse's axes, so it gives the swing plane at every instant, not only at the turning points. For a flat swing it
    // is exact (p ∥ v, rank one); for an ellipse it wobbles slightly within a swing (the pendulum isn't linear).
    // azimuth: of the major axis, rad, in (−π/2, π/2]; major, minor: semi-axes, m.
    public void SwingEllipse(out double azimuth, out double major, out double minor) {
        double w2 = gravity / length;
        double axx = w2 * r.X * r.X + v.X * v.X;
        double ayy = w2 * r.Y * r.Y + v.Y * v.Y;
        double axy = w2 * r.X * r.Y + v.X * v.Y;
        double mean = 0.5 * (axx + ayy);
        double half = Math.Sqrt(0.25 * (axx - ayy) * (axx - ayy) + axy * axy);
        azimuth = 0.5 * Math.Atan2(2 * axy, axx - ayy);
        major = Math.Sqrt(Math.Max(mean + half, 0) / w2);
        minor = Math.Sqrt(Math.Max(mean - half, 0) / w2);
    }


    // a line's direction is only defined up to π: the smallest change from one azimuth to the next, for unwrapping
    public static double PlaneDelta(double from, double to) {
        double d = (to - from) % Math.PI;
        if (d > Math.PI / 2) d -= Math.PI;
        if (d <= -Math.PI / 2) d += Math.PI;
        return d;
    }
}
