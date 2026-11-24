using System;
using NUnit.Framework;


// Checks of FoucaultModel and PinRing with the simulated pendulum (estimated: 15 m, released 1.2 m aside) and the
// controller's step (0.02 s). Run in Unity: Window → General → Test Runner → EditMode → Run All. Plain NUnit, no
// UnityEngine, so the same file also runs outside Unity (dotnet test).
public class FoucaultModelTests {
    private const double L = 15;                            // m
    private const double Amplitude = 1.2;                   // m, horizontal
    private const double H = 0.02;                          // s, the controller's step
    private const double Goettingen = 51.5;                 // deg


    private static double Deg(double deg) => deg * Math.PI / 180;

    private static FoucaultModel Released(double latitudeDeg, double azimuth = 0, double earthRate = FoucaultModel.EarthRateDefault) {
        var model = new FoucaultModel(L, 9.81, earthRate) { Latitude = Deg(latitudeDeg) };
        model.Release(Amplitude, azimuth);
        return model;
    }

    // runs for `seconds` and returns the angle the swing plane turned (rad, unwrapped, counter-clockwise positive)
    private static double TurnedAngle(FoucaultModel model, double seconds) {
        model.SwingEllipse(out double start, out _, out _);
        double last = start, turned = 0;
        int steps = (int)Math.Round(seconds / H);
        for (int i = 0; i < steps; i++) {
            model.Step(H);
            if (i % 10 == 0) {                                  // like the controller: often enough to unwrap
                model.SwingEllipse(out double azimuth, out _, out _);
                turned += FoucaultModel.PlaneDelta(last, azimuth);
                last = azimuth;
            }
        }
        model.SwingEllipse(out double end, out _, out _);
        return turned + FoucaultModel.PlaneDelta(last, end);
    }


    [TestCase(Goettingen, TestName = "Plane turns at Ω sin φ, Göttingen (clockwise)")]
    [TestCase(-Goettingen, TestName = "Plane turns at Ω sin φ, southern hemisphere (counter-clockwise)")]
    [TestCase(90.0, TestName = "Plane turns at Ω sin φ, North Pole")]
    [TestCase(-90.0, TestName = "Plane turns at Ω sin φ, South Pole")]
    [TestCase(20.0, TestName = "Plane turns at Ω sin φ, 20° N")]
    public void PlaneTurnsAtEarthRateTimesSinLatitude(double latitudeDeg) {
        var model = Released(latitudeDeg);
        const double seconds = 2 * 3600;
        double expected = model.PlaneRate * seconds;            // −Ω_E sin φ · t

        double turned = TurnedAngle(model, seconds);

        Assert.That(Math.Sign(turned), Is.EqualTo(-Math.Sign(latitudeDeg)));
        // finite amplitude: the tiny Coriolis ellipse adds an Airy part of order (3/8)(a/L)² ≈ 0.3 %
        Assert.That(turned, Is.EqualTo(expected).Within(0.01 * Math.Abs(expected)));
    }


    [TestCase(0.0, TestName = "Equator: an east-west swing doesn't turn")]
    [TestCase(90.0, TestName = "Equator: a north-south swing doesn't turn")]
    public void EquatorDoesNotTurn(double azimuthDeg) {
        var model = Released(0, Deg(azimuthDeg));
        const double seconds = 2 * 3600;

        double turned = TurnedAngle(model, seconds);

        Assert.That(Math.Abs(turned), Is.LessThan(1e-3 * FoucaultModel.EarthRateDefault * seconds));
    }


    // without the Earth's rotation: the plain pendulum, with the exact period of the nonlinear swing, 4 K(k)/ω₀
    [Test]
    public void WithoutEarthRotationThePeriodIsTheExactPendulumPeriod() {
        var model = Released(Goettingen, 0, earthRate: 0);
        double theta0 = Math.Asin(Amplitude / L);
        double expected = 4 * EllipticK(Math.Sin(theta0 / 2)) / model.Omega0;
        double firstCrossing = double.NaN, lastCrossing = double.NaN;
        int crossings = 0;
        double previousX = model.Position.X;

        for (int i = 0; i < (int)(20 * expected / H); i++) {
            model.Step(H);
            double x = model.Position.X;
            if (previousX < 0 && x >= 0) {                      // upward zero crossing, interpolated within the step
                double t = model.Time - H * x / (x - previousX);
                if (double.IsNaN(firstCrossing)) firstCrossing = t;
                else crossings++;
                lastCrossing = t;
            }
            previousX = x;
        }

        Assert.That(crossings, Is.GreaterThanOrEqualTo(18));
        Assert.That((lastCrossing - firstCrossing) / crossings, Is.EqualTo(expected).Within(1e-6 * expected));
        Assert.That(Math.Abs(model.Position.Y), Is.LessThan(1e-9));     // and it stays flat
    }


    [Test]
    public void JacobiEnergyIsConserved() {
        var model = Released(Goettingen);
        double e0 = model.JacobiEnergy;
        double swing = model.Gravity * (L - Math.Sqrt(L * L - Amplitude * Amplitude));   // the swing's energy per kg
        double worst = 0;

        for (int i = 0; i < (int)(3600 / H); i++) {
            model.Step(H);
            worst = Math.Max(worst, Math.Abs(model.JacobiEnergy - e0));
        }

        Assert.That(worst / swing, Is.LessThan(1e-6));
    }


    [Test]
    public void WireLengthStaysFixed() {
        var model = Released(Goettingen);
        for (int i = 0; i < 10000; i++) model.Step(H);

        Assert.That(model.Position.Length, Is.EqualTo(L).Within(1e-12 * L));
        Assert.That(Vec3d.Dot(model.Position, model.Velocity), Is.EqualTo(0).Within(1e-12));
    }


    // An elliptical swing precesses by itself (Airy), in the sense it is traversed: Ω_A ≈ (3/8) ω₀ a b / L². The reason
    // the real experiment is hard: L = 10 m, a = 1 m, b = 1 cm already give ≈ 7.7 °/h. Measured here without the Earth's
    // rotation, from the direction of the bob at its farthest points.
    [TestCase(10.0, 1.0, 0.05)]
    [TestCase(15.0, 1.2, 0.02)]
    public void AiryPrecessionMatchesFormula(double length, double a, double b) {
        var model = new FoucaultModel(length, 9.81, earthRate: 0);
        double w0 = model.Omega0;
        double z = -Math.Sqrt(length * length - a * a);
        model.SetState(new Vec3d(a, 0, z), new Vec3d(0, b * w0, 0));     // counter-clockwise from above
        double expected = 3.0 / 8 * w0 * a * b / (length * length);

        // azimuth of each apocentre (largest horizontal distance), unwrapped; a least-squares line through them
        double previous = double.NaN, previous2 = double.NaN, lastAzimuth = 0, turned = 0;
        double st = 0, sa = 0, stt = 0, sta = 0;
        int n = 0;
        for (int i = 0; i < (int)(4000 / H); i++) {
            Vec3d p = model.Position;
            double rho = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            if (previous > previous2 && previous >= rho) {      // the step before was a maximum
                double azimuth = Math.Atan2(p.Y, p.X);
                turned += n == 0 ? 0 : FoucaultModel.PlaneDelta(lastAzimuth, azimuth);
                lastAzimuth = azimuth;
                double t = model.Time;
                st += t; sa += turned; stt += t * t; sta += t * turned;
                n++;
            }
            previous2 = previous;
            previous = rho;
            model.Step(H);
        }
        double slope = (n * sta - st * sa) / (n * stt - st * st);

        Assert.That(n, Is.GreaterThan(500));
        Assert.That(slope, Is.EqualTo(expected).Within(0.05 * expected));
    }


    [Test]
    public void PinsFallOnlyWhereThePlaneHasTurned() {
        var ring = new PinRing(48, 0.015);
        ring.Reset(1.1, 0);                                     // starting plane east-west

        // one swing along the starting plane: it passes between the pins
        Assert.That(ring.Sweep(-1.2, 0, 1.2, 0, 1), Is.EqualTo(0));
        // the plane turned by half a spacing (3.75°): the swing hits the pin there and its partner opposite
        double a = Math.PI / 48;
        Assert.That(ring.Sweep(-1.2 * Math.Cos(a), -1.2 * Math.Sin(a), 1.2 * Math.Cos(a), 1.2 * Math.Sin(a), 2), Is.EqualTo(2));
        Assert.That(ring.IsDown(0) && ring.IsDown(24), Is.True);
        Assert.That(ring.DownTime(0), Is.EqualTo(2));
        // fallen pins don't fall again
        Assert.That(ring.Sweep(-1.2 * Math.Cos(a), -1.2 * Math.Sin(a), 1.2 * Math.Cos(a), 1.2 * Math.Sin(a), 3), Is.EqualTo(0));
        Assert.That(ring.DownCount, Is.EqualTo(2));
        // a step well inside the ring never reaches it
        Assert.That(ring.Sweep(0, 0, 0.5, 0.01, 4), Is.EqualTo(0));
    }


    // the complete elliptic integral of the first kind, K(k), by the arithmetic-geometric mean
    private static double EllipticK(double k) {
        double a = 1, g = Math.Sqrt(1 - k * k);
        while (Math.Abs(a - g) > 1e-15) {
            double next = 0.5 * (a + g);
            g = Math.Sqrt(a * g);
            a = next;
        }
        return Math.PI / (2 * a);
    }
}
