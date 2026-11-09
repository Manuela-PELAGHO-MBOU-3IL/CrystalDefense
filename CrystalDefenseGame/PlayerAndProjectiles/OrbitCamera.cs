using Engine;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CrystalDefense.PlayerAndProjectiles;

// A third-person camera that orbits around the player and follows him closely.
//   Q / E (A / E on an AZERTY keyboard)   turn around the player
//   R / F                                 zoom in / out
//   mouse wheel                           zoom
//   hold the mouse wheel (middle button)  turn and tilt with the mouse
// It can also shake for a moment, for example when the player is hit.
public sealed class OrbitCamera
{
    private const float MinDistance = 5.0f;
    private const float MaxDistance = 22.0f;
    private static readonly float MinPitch = MathHelper.DegreesToRadians(20.0f);
    private static readonly float MaxPitch = MathHelper.DegreesToRadians(80.0f);

    private readonly Random _random = new();
    private Vector3 _target;
    private float _shake;

    public OrbitCamera(Camera camera, Vector3 playerPosition)
    {
        Camera = camera;
        _target = playerPosition + TargetHeight;
        ApplyToCamera(Vector3.Zero);
    }

    public Camera Camera { get; }

    // Horizontal angle around the player (0 = the camera is behind the player, on the +Z side).
    public float Yaw { get; private set; }

    // Vertical angle (0 = horizontal, 90° = straight from above).
    public float Pitch { get; private set; } = MathHelper.DegreesToRadians(55.0f);

    public float Distance { get; private set; } = 11.0f;

    public float KeyRotateSpeed { get; set; } = 2.0f;        // radians per second
    public float KeyZoomSpeed { get; set; } = 10.0f;         // units per second
    public float MouseSensitivity { get; set; } = 0.005f;    // radians per pixel
    public float FollowSmoothness { get; set; } = 10.0f;     // larger = follows faster

    // The camera looks at the player's chest, not at his feet.
    private static Vector3 TargetHeight => new(0.0f, 1.0f, 0.0f);

    // "Forward" and "right" seen from the camera, flat on the floor (length 1).
    // The player moves relative to these, so W always means "away from the camera".
    public Vector3 FlatForward => new(-MathF.Sin(Yaw), 0.0f, -MathF.Cos(Yaw));
    public Vector3 FlatRight => new(MathF.Cos(Yaw), 0.0f, -MathF.Sin(Yaw));

    public void Shake(float strength) => _shake = MathF.Max(_shake, strength);

    public void Update(float deltaTime, KeyboardState keyboard, MouseState mouse, Vector3 playerPosition)
    {
        // 1. Turn and zoom with the keyboard.
        if (keyboard.IsKeyDown(Keys.Q)) Yaw += KeyRotateSpeed * deltaTime;
        if (keyboard.IsKeyDown(Keys.E)) Yaw -= KeyRotateSpeed * deltaTime;
        if (keyboard.IsKeyDown(Keys.R)) Distance -= KeyZoomSpeed * deltaTime;
        if (keyboard.IsKeyDown(Keys.F)) Distance += KeyZoomSpeed * deltaTime;

        // 2. Turn and tilt with the mouse while the wheel is held down, zoom with the wheel.
        if (mouse.IsButtonDown(MouseButton.Middle))
        {
            Yaw -= mouse.Delta.X * MouseSensitivity;
            Pitch += mouse.Delta.Y * MouseSensitivity;
        }
        Distance *= MathF.Pow(0.9f, mouse.ScrollDelta.Y);   // one notch = 10 % closer or further

        Distance = Math.Clamp(Distance, MinDistance, MaxDistance);
        Pitch = Math.Clamp(Pitch, MinPitch, MaxPitch);

        // 3. Follow the player smoothly (the same share of the remaining distance every second,
        //    independent of the frame rate).
        float blend = 1.0f - MathF.Exp(-FollowSmoothness * deltaTime);
        _target = Vector3.Lerp(_target, playerPosition + TargetHeight, blend);

        // 4. Shake.
        Vector3 shakeOffset = Vector3.Zero;
        if (_shake > 0.0f)
        {
            shakeOffset = new Vector3(RandomSigned(), RandomSigned(), RandomSigned()) * _shake;
            _shake = MathF.Max(0.0f, _shake - 1.5f * deltaTime);
        }

        ApplyToCamera(shakeOffset);
    }

    // Spherical coordinates: from the angles and the distance to a position around the target.
    private void ApplyToCamera(Vector3 shakeOffset)
    {
        Vector3 offset = new Vector3(
            MathF.Sin(Yaw) * MathF.Cos(Pitch),
            MathF.Sin(Pitch),
            MathF.Cos(Yaw) * MathF.Cos(Pitch)) * Distance;

        Camera.LookAt(_target + offset + shakeOffset, _target + shakeOffset);
    }

    private float RandomSigned() => (float)_random.NextDouble() * 2.0f - 1.0f;
}
