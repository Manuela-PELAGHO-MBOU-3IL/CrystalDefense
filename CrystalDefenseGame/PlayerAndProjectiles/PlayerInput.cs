using CrystalDefense.Core;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CrystalDefense.PlayerAndProjectiles;

// Reads the keyboard and the mouse once per frame and turns them into game actions.
// The game can be played with the keyboard only or with keyboard + mouse; it switches automatically:
//   - moving the mouse or clicking  → mouse mode: the knight looks at the cursor
//   - using the arrow keys          → keyboard mode: the knight looks where he shoots or walks
//
//   Keyboard (W A S D = Z Q S D on AZERTY)          Mouse
//   W A S D      move (relative to the camera)
//   arrow keys   aim and shoot in that direction    left click    shoot at the cursor
//   Enter / K    sword slash                        right click   sword slash
//   Space        dash
public sealed class PlayerInput
{
    // Keyboard aiming only has 8 directions, so it helps a little: an enemy close to the
    // chosen direction is aimed at directly.
    private const float AimAssistAngleDegrees = 25.0f;
    private const float AimAssistRange = 15.0f;

    // In mouse mode, if the mouse is not touched for this long and the player walks, go back to keyboard mode.
    private const float MouseIdleTimeout = 2.0f;

    private float _mouseIdleTime;

    public enum AimMode { Keyboard, Mouse }

    public AimMode Mode { get; private set; } = AimMode.Keyboard;

    // Walking direction on the floor (length 0 or 1).
    public Vector3 MoveDirection { get; private set; }

    // Where the knight should look (length 1), or null: then he looks where he walks.
    public Vector3? AimDirection { get; private set; }

    public bool IsShooting { get; private set; }
    public bool SwordPressed { get; private set; }
    public bool DashPressed { get; private set; }

    public void Read(
        KeyboardState keyboard,
        MouseState mouse,
        Vector2i windowSize,
        OrbitCamera camera,
        Vector3 playerPosition,
        float aimHeight,
        IEnumerable<GameObject> targets,
        float deltaTime)
    {
        Vector3 forward = camera.FlatForward;
        Vector3 right = camera.FlatRight;

        // 1. Movement, relative to the camera.
        Vector3 move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) move += forward;
        if (keyboard.IsKeyDown(Keys.S)) move -= forward;
        if (keyboard.IsKeyDown(Keys.D)) move += right;
        if (keyboard.IsKeyDown(Keys.A)) move -= right;
        MoveDirection = move.LengthSquared > 0.0f ? move.Normalized() : Vector3.Zero;

        // 2. Keyboard aiming with the arrow keys, also relative to the camera.
        Vector3 arrows = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.Up)) arrows += forward;
        if (keyboard.IsKeyDown(Keys.Down)) arrows -= forward;
        if (keyboard.IsKeyDown(Keys.Right)) arrows += right;
        if (keyboard.IsKeyDown(Keys.Left)) arrows -= right;
        bool arrowsUsed = arrows.LengthSquared > 0.0f;

        // 3. Which mode? (the middle button only turns the camera, it does not count as aiming)
        bool mouseUsed = mouse.Delta.LengthSquared > 1.0f
                         || mouse.IsButtonDown(MouseButton.Left)
                         || mouse.IsButtonDown(MouseButton.Right);
        _mouseIdleTime = mouseUsed ? 0.0f : _mouseIdleTime + deltaTime;

        if (arrowsUsed)
            Mode = AimMode.Keyboard;
        else if (mouseUsed && !mouse.IsButtonDown(MouseButton.Middle))
            Mode = AimMode.Mouse;
        else if (Mode == AimMode.Mouse && _mouseIdleTime > MouseIdleTimeout && MoveDirection != Vector3.Zero)
            Mode = AimMode.Keyboard;

        // 4. Actions.
        SwordPressed = mouse.IsButtonPressed(MouseButton.Right)
                       || keyboard.IsKeyPressed(Keys.Enter)
                       || keyboard.IsKeyPressed(Keys.KeyPadEnter)
                       || keyboard.IsKeyPressed(Keys.K);
        DashPressed = keyboard.IsKeyPressed(Keys.Space);
        IsShooting = arrowsUsed || (Mode == AimMode.Mouse && mouse.IsButtonDown(MouseButton.Left));

        // 5. Aim direction.
        AimDirection = null;
        if (arrowsUsed)
        {
            AimDirection = ApplyAimAssist(arrows.Normalized(), playerPosition, targets);
        }
        else if (Mode == AimMode.Mouse &&
                 MouseAiming.TryGetPointOnPlane(mouse.Position, windowSize, camera.Camera, aimHeight, out Vector3 point))
        {
            Vector3 toCursor = point - playerPosition;
            toCursor.Y = 0.0f;
            if (toCursor.LengthSquared > 0.01f)
                AimDirection = toCursor.Normalized();
        }
    }

    // Returns the direction to the alive target that is closest to the wanted direction
    // (at most AimAssistAngleDegrees away and within AimAssistRange), or the wanted direction itself.
    private static Vector3 ApplyAimAssist(Vector3 wanted, Vector3 playerPosition, IEnumerable<GameObject> targets)
    {
        float bestCos = MathF.Cos(MathHelper.DegreesToRadians(AimAssistAngleDegrees));
        Vector3 best = wanted;

        foreach (GameObject target in targets)
        {
            if (!target.IsAlive)
                continue;

            Vector3 toTarget = target.Position - playerPosition;
            toTarget.Y = 0.0f;
            float distance = toTarget.Length;
            if (distance < 0.001f || distance > AimAssistRange)
                continue;

            Vector3 direction = toTarget / distance;
            float cos = Vector3.Dot(direction, wanted);
            if (cos > bestCos)
            {
                bestCos = cos;
                best = direction;
            }
        }

        return best;
    }
}
