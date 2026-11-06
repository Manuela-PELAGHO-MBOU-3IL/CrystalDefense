
using CrystalDefense.Core;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CrystalDefense.PlayerAndProjectiles;

public sealed class Player : GameObject
{
    public Player(
        Mesh mesh,
        Material material,
        Vector3 startPosition,
        float radius
    ) : base(mesh, material, startPosition, radius) { }

    public float Speed { get; set; } = 6.0f;

    public Vector3 FacingDirection { get; private set; } = -Vector3.UnitZ;

    public void Update(
        KeyboardState keyboard,
        float deltaTime,
        ArenaBounds bounds,
        Vector3 obstacleCenter,
        float obstacleRadius
    )
    {
        Vector3 direction = Vector3.Zero;

        if (keyboard.IsKeyDown(Keys.W)) direction.Z -= 1;
        if (keyboard.IsKeyDown(Keys.S)) direction.Z += 1;
        if (keyboard.IsKeyDown(Keys.A)) direction.X -= 1;
        if (keyboard.IsKeyDown(Keys.D)) direction.X += 1;

        if (direction == Vector3.Zero)
            return;

        direction = direction.Normalized();

        FacingDirection = direction;
        RotationY = MathF.Atan2(-direction.X, -direction.Z);

        float movement = Speed * deltaTime;

        // Move horizontally first.
        Vector3 nextX = Position;
        nextX.X += direction.X * movement;
        nextX = bounds.Clamp(nextX, Radius);

        if (!OverlapsObstacle(nextX, obstacleCenter, obstacleRadius))
            Position = nextX;

        // Move vertically on the floor (Z axis) separately.
        Vector3 nextZ = Position;
        nextZ.Z += direction.Z * movement;
        nextZ = bounds.Clamp(nextZ, Radius);

        if (!OverlapsObstacle(nextZ, obstacleCenter, obstacleRadius))
            Position = nextZ;
    }

    private bool OverlapsObstacle(
        Vector3 playerPosition,
        Vector3 obstacleCenter,
        float obstacleRadius
    )
    {
        float dx = playerPosition.X - obstacleCenter.X;
        float dz = playerPosition.Z - obstacleCenter.Z;

        float minimumDistance = Radius + obstacleRadius;

        return dx * dx + dz * dz <
               minimumDistance * minimumDistance;
    }
}
