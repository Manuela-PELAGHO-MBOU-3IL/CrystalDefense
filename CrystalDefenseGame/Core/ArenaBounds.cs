
using OpenTK.Mathematics;

namespace CrystalDefense.Core;

// Circular playable area of the arena.
// The center is on the X/Z floor plane.
public sealed class ArenaBounds
{
    public ArenaBounds(Vector2 center, float radius)
    {
        if (radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius));

        Center = center;
        Radius = radius;
    }

    public Vector2 Center { get; }

    // Actual playable radius, measured from the center to the inner wall.
    public float Radius { get; }

    // Checks whether a point is inside the circular arena.
    public bool Contains(Vector3 point, float margin = 0.0f)
    {
        float allowedRadius = Radius - margin;

        if (allowedRadius < 0)
            return false;

        float dx = point.X - Center.X;
        float dz = point.Z - Center.Y;

        float distanceSquared = dx * dx + dz * dz;

        return distanceSquared <= allowedRadius * allowedRadius;
    }

    // Keeps the complete object inside the circular arena.
    public Vector3 Clamp(Vector3 point, float margin)
    {
        float allowedRadius = Radius - margin;

        if (allowedRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(margin));

        float dx = point.X - Center.X;
        float dz = point.Z - Center.Y;

        float distanceSquared = dx * dx + dz * dz;
        float allowedRadiusSquared = allowedRadius * allowedRadius;

        // The point is already inside the playable circle.
        if (distanceSquared <= allowedRadiusSquared)
            return point;

        // Avoid division by zero.
        if (distanceSquared == 0)
            return point;

        // Normalize the direction and place the player on the circle.
        float distance = MathF.Sqrt(distanceSquared);

        float scale = allowedRadius / distance;

        return new Vector3(
            Center.X + dx * scale,
            point.Y,
            Center.Y + dz * scale
        );
    }
}
