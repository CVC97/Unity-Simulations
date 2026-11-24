using System;


public struct Vec3d {
    public double X;
    public double Y;
    public double Z;

    // unit vectors
    public static readonly Vec3d UnitX = new Vec3d {
        X = 1,
    };

    public static readonly Vec3d UnitY = new Vec3d {
        Y = 1,
    };

    public static readonly Vec3d UnitZ = new Vec3d {
        Z = 1,
    };

    // operator +
    public static Vec3d operator +(Vec3d a, Vec3d b) => 
        new Vec3d {
            X = a.X + b.X,
            Y = a.Y + b.Y,
            Z = a.Z + b.Z,
        };

    // operator -
    public static Vec3d operator -(Vec3d a, Vec3d b) => 
        new Vec3d {
            X = a.X - b.X,
            Y = a.Y - b.Y,
            Z = a.Z - b.Z,
        };

    // operator * (scalar multiplication)
    public static Vec3d operator *(Vec3d a, double x) => 
        new Vec3d {
            X = a.X * x,
            Y = a.Y * x,
            Z = a.Z * x,
        };

    // method dot product
    public static double Dot(Vec3d a, Vec3d b) {
        double dotProduct = a.X*b.X + a.Y*b.Y + a.Z*b.Z;
        return dotProduct;
    }

    // method cross product
    public static Vec3d Cross(Vec3d a, Vec3d b) {
        Vec3d crossProduct = new Vec3d {
            X = a.Y*b.Z - a.Z*b.Y,
            Y = a.Z*b.X - a.X*b.Z,
            Z = a.X*b.Y - a.Y*b.X,
        };
        return crossProduct;
    }

    // added for the Foucault pendulum (2026-11-24); shared by all simulations since then (Assets/Shared/Physics)
    public Vec3d(double x, double y, double z) {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vec3d operator -(Vec3d a) => new Vec3d(-a.X, -a.Y, -a.Z);
    public static Vec3d operator *(double x, Vec3d a) => a * x;
    public static Vec3d operator /(Vec3d a, double x) => new Vec3d(a.X / x, a.Y / x, a.Z / x);

    public double SqrLength => X*X + Y*Y + Z*Z;
    public double Length => Math.Sqrt(SqrLength);

    public override string ToString() => $"({X:G6}, {Y:G6}, {Z:G6})";
}