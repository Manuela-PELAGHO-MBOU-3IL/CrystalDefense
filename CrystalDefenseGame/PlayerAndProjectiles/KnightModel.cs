using CrystalDefense.Core;
using Engine;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefense.PlayerAndProjectiles;

// The knight as a scene graph of six parts modelled in Blender (see Blender/make_knight.py):
//
//   root (position, rotation and scale of the player)
//    ├── body ──┬── right arm (with the sword)
//    │          ├── left arm  (with the shield)
//    │          └── cape
//    ├── right leg
//    └── left leg
//
// Each part was exported with its joint (hip, shoulder, top of the cape) at the origin, so it can be
// rotated around it. The animation (walking, sword slash, dash, cape) only changes the rotations of the nodes.
public sealed class KnightModel
{
    // Joints in the knight's own space (feet at y = 0, looking along -Z, right side = +X).
    // They come from the pivots used in Blender/make_knight.py.
    private static readonly Vector3 RightHip = new(0.12f, 0.85f, 0.0f);
    private static readonly Vector3 LeftHip = new(-0.12f, 0.85f, 0.0f);
    private static readonly Vector3 RightShoulder = new(0.31f, 1.32f, 0.0f);
    private static readonly Vector3 LeftShoulder = new(-0.31f, 1.32f, 0.0f);
    private static readonly Vector3 CapeTop = new(0.0f, 1.32f, 0.17f);

    private const float StrideAngle = 0.55f;          // leg swing while walking, in radians (about 30°)
    private const float StepsPerUnit = 2.6f;          // how fast the legs move compared to the distance walked
    private const float DashLeanAngle = -0.35f;       // the body leans forward during a dash

    private readonly SceneNode _root;
    private readonly SceneNode _body;
    private readonly SceneNode _rightLeg;
    private readonly SceneNode _leftLeg;
    private readonly SceneNode _rightArm;
    private readonly SceneNode _leftArm;
    private readonly SceneNode _cape;

    private float _time;
    private float _walkPhase;
    private float _walkBlend;   // 0 = standing, 1 = walking (smoothed, so that the legs do not snap)
    private float _lean;
    private float _capeLift;

    public KnightModel(Mesh body, Mesh rightLeg, Mesh leftLeg, Mesh rightArm, Mesh leftArm, Mesh cape, Material material)
    {
        _root = new SceneNode("Knight");
        _body = _root.AddChild(new SceneNode("Body", body, material));
        _rightLeg = _root.AddChild(new SceneNode("RightLeg", rightLeg, material, RightHip));
        _leftLeg = _root.AddChild(new SceneNode("LeftLeg", leftLeg, material, LeftHip));
        _rightArm = _body.AddChild(new SceneNode("RightArm", rightArm, material, RightShoulder));
        _leftArm = _body.AddChild(new SceneNode("LeftArm", leftArm, material, LeftShoulder));
        _cape = _body.AddChild(new SceneNode("Cape", cape, material, CapeTop));
    }

    // movedDistance: how far the knight walked in this frame.
    // swordProgress: 0 → 1 during a sword slash, negative when he does not attack.
    public void Animate(float deltaTime, float movedDistance, float swordProgress, bool isDashing)
    {
        _time += deltaTime;

        // --- walking: legs and arms swing in opposite directions ---
        bool isWalking = movedDistance > 0.0001f && !isDashing;
        _walkBlend = MoveTowards(_walkBlend, isWalking ? 1.0f : 0.0f, deltaTime * 6.0f);
        _walkPhase += movedDistance * StepsPerUnit;

        float swing = MathF.Sin(_walkPhase) * StrideAngle * _walkBlend;
        _rightLeg.Rotation = Matrix4.CreateRotationX(-swing);
        _leftLeg.Rotation = Matrix4.CreateRotationX(swing);
        _leftArm.Rotation = Matrix4.CreateRotationX(-swing * 0.4f);

        // --- body: small bounce at every step, slow breathing when standing, lean during a dash ---
        float bounce = MathF.Abs(MathF.Sin(_walkPhase)) * 0.05f * _walkBlend;
        float breathing = MathF.Sin(_time * 2.0f) * 0.008f;
        _body.Offset = new Vector3(0.0f, bounce + breathing, 0.0f);

        _lean = MoveTowards(_lean, isDashing ? DashLeanAngle : 0.0f, deltaTime * 4.0f);
        _body.Rotation = Matrix4.CreateRotationX(_lean);

        // --- cape: blown backwards when he runs (even more during a dash), with a small flutter ---
        float capeTarget = isDashing ? 0.9f : 0.08f + 0.35f * _walkBlend;
        _capeLift = MoveTowards(_capeLift, capeTarget, deltaTime * 3.0f);
        float flutter = MathF.Sin(_time * 7.0f) * 0.05f * _walkBlend;
        _cape.Rotation = Matrix4.CreateRotationX(-(_capeLift + flutter));

        // --- right arm: horizontal sword slash from right to left, otherwise it swings while walking ---
        if (swordProgress >= 0.0f)
        {
            float eased = 1.0f - MathF.Pow(1.0f - swordProgress, 3.0f);      // fast at the start, slow at the end
            float sweep = Lerp(MathHelper.DegreesToRadians(-80.0f), MathHelper.DegreesToRadians(70.0f), eased);
            float lift = MathHelper.DegreesToRadians(25.0f);
            _rightArm.Rotation = Matrix4.CreateRotationX(lift) * Matrix4.CreateRotationY(sweep);
        }
        else
        {
            _rightArm.Rotation = Matrix4.CreateRotationX(swing * 0.4f);
        }
    }

    // modelMatrix: position, rotation and scale of the whole knight in the arena.
    public void Draw(OpenGlRenderer renderer, Camera camera, Matrix4 modelMatrix) =>
        _root.Draw(renderer, camera, modelMatrix);

    private static float Lerp(float from, float to, float t) => from + (to - from) * t;

    private static float MoveTowards(float current, float target, float maxStep) =>
        current < target ? MathF.Min(current + maxStep, target) : MathF.Max(current - maxStep, target);
}
