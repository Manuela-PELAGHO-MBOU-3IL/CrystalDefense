using OpenTK.Mathematics;

namespace CrystalDefense.Core;

// The playable rectangle of the arena on the floor (X = left/right, Z = forward/backward).
// SHARED FILE: the arena (Manuela) provides the real values; the others only read them.
public sealed class ArenaBounds
{
    public ArenaBounds(float minX, float maxX, float minZ, float maxZ)
    {
        MinX = minX;
        MaxX = maxX;
        MinZ = minZ;
        MaxZ = maxZ;
    }

    public float MinX { get; }
    public float MaxX { get; }
    public float MinZ { get; }
    public float MaxZ { get; }

    // true if the point is inside the arena (the height Y does not matter).
    public bool Contains(Vector3 point) =>
        point.X >= MinX && point.X <= MaxX && point.Z >= MinZ && point.Z <= MaxZ;

    // Moves a point back into the arena. The margin keeps an object of this radius away from the walls.
    public Vector3 Clamp(Vector3 point, float margin) => new(
        Math.Clamp(point.X, MinX + margin, MaxX - margin),
        point.Y,
        Math.Clamp(point.Z, MinZ + margin, MaxZ - margin));
}
