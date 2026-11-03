using CrystalDefense.Core;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CrystalDefense.PlayerAndProjectiles;

// The character controlled by the player: W/A/S/D moves it on the floor, it cannot leave the arena.
public sealed class Player : GameObject
{
    public Player(Mesh mesh, Material material, Vector3 startPosition, float radius)
        : base(mesh, material, startPosition, radius) { }

    // Speed in units per second.
    public float Speed { get; set; } = 6.0f;

    // The direction the player looks at (length 1). Projectiles are shot in this direction.
    // At the start the player looks along -Z, that is "forward" (away from the camera).
    public Vector3 FacingDirection { get; private set; } = -Vector3.UnitZ;

    public void Update(KeyboardState keyboard, float deltaTime, ArenaBounds bounds)
    {
        // 1. Read the keys and build the direction of the movement on the floor (X and Z).
        Vector3 direction = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) direction.Z -= 1;   // forward
        if (keyboard.IsKeyDown(Keys.S)) direction.Z += 1;   // backward
        if (keyboard.IsKeyDown(Keys.A)) direction.X -= 1;   // left
        if (keyboard.IsKeyDown(Keys.D)) direction.X += 1;   // right

        if (direction != Vector3.Zero)
        {
            // 2. Length 1, so that moving diagonally is not faster.
            direction = direction.Normalized();

            // 3. Move: speed × time since the last frame = same speed on every computer.
            Position += direction * Speed * deltaTime;

            // 4. Remember where the player looks and turn the model in that direction.
            FacingDirection = direction;
            RotationY = MathF.Atan2(-direction.X, -direction.Z);
        }

        // 5. Stay inside the arena.
        Position = bounds.Clamp(Position, Radius);
    }
}
