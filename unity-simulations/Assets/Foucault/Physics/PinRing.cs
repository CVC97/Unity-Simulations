using System;


// The classic ring of small pins on the floor, which the bob's stylus knocks over one by one as the swing plane turns.
// Geometry only (horizontal plane, metres, azimuth as in FoucaultModel): the pins stand on a circle a little inside
// the swing's amplitude, offset by half a spacing from the starting plane, so none falls on the first swing; a pin
// falls when the stylus' path during one integration step comes within HitDistance of it.
public class PinRing {
    private readonly double[] x, y, azimuth;
    private readonly double[] downTime;                                         // s of simulated time, NaN = standing

    public PinRing(int count, double hitDistance) {
        Count = count;
        HitDistance = hitDistance;
        x = new double[count];
        y = new double[count];
        azimuth = new double[count];
        downTime = new double[count];
        Reset(1, 0);
    }

    public int Count { get; }
    public double HitDistance { get; }                                          // m, stylus radius + pin radius
    public double Radius { get; private set; }                                  // m
    public int DownCount { get; private set; }

    public double X(int i) => x[i];
    public double Y(int i) => y[i];
    public double Azimuth(int i) => azimuth[i];                                 // rad
    public bool IsDown(int i) => !double.IsNaN(downTime[i]);
    public double DownTime(int i) => downTime[i];


    // all pins up again, on a circle of `radius` around the centre; the starting plane runs through `planeAzimuth`
    public void Reset(double radius, double planeAzimuth) {
        Radius = radius;
        DownCount = 0;
        for (int i = 0; i < Count; i++) {
            azimuth[i] = planeAzimuth + 2 * Math.PI * (i + 0.5) / Count;
            x[i] = radius * Math.Cos(azimuth[i]);
            y[i] = radius * Math.Sin(azimuth[i]);
            downTime[i] = double.NaN;
        }
    }


    // the stylus moved from (ax, ay) to (bx, by) in one step and arrived at `time`; returns the number of pins knocked
    public int Sweep(double ax, double ay, double bx, double by, double time) {
        // a segment is farthest from the centre at an end: if both ends are well inside the ring, nothing can be hit
        double reach = Radius - HitDistance;
        if (ax * ax + ay * ay < reach * reach && bx * bx + by * by < reach * reach) return 0;

        int knocked = 0;
        double dx = bx - ax, dy = by - ay;
        double len2 = dx * dx + dy * dy;
        for (int i = 0; i < Count; i++) {
            if (!double.IsNaN(downTime[i])) continue;
            double t = len2 > 0 ? ((x[i] - ax) * dx + (y[i] - ay) * dy) / len2 : 0;
            t = Math.Max(0, Math.Min(1, t));
            double ex = ax + t * dx - x[i], ey = ay + t * dy - y[i];
            if (ex * ex + ey * ey <= HitDistance * HitDistance) {
                downTime[i] = time;
                DownCount++;
                knocked++;
            }
        }
        return knocked;
    }
}
