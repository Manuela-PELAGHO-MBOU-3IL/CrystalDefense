using Engine;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefense.Core;

// Common base class of everything that exists in the arena: player, projectiles, enemies, crystal.
// SHARED FILE: change it only after agreeing with the whole team.
public abstract class GameObject
{
    protected GameObject(Mesh mesh, Material material, Vector3 position, float radius, float scale = 1.0f)
    {
        Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        Material = material ?? throw new ArgumentNullException(nameof(material));
        Position = position;
        Radius = radius;
        Scale = scale;
    }

    // The 3D model and its appearance. They are only referenced: many objects can share them.
    public Mesh Mesh { get; }
    public Material Material { get; }

    // Where the object is in the arena (Y is the height above the floor).
    public Vector3 Position { get; set; }

    // Rotation around the vertical axis (Y), in radians.
    public float RotationY { get; set; }

    // Size factor of the model.
    public float Scale { get; set; }

    // Radius of the sphere around the object, used for collisions.
    public float Radius { get; }

    // false = the object has been destroyed and must be removed from the game.
    public bool IsAlive { get; private set; } = true;

    public void Destroy() => IsAlive = false;

    // Two objects touch when the distance between their centers is smaller than the sum of their radii.
    public bool CollidesWith(GameObject other)
    {
        float radii = Radius + other.Radius;
        return Vector3.DistanceSquared(Position, other.Position) < radii * radii;
    }

    // Scale, then rotate, then move to the position (OpenTK multiplies from left to right).
    public Matrix4 ModelMatrix =>
        Matrix4.CreateScale(Scale) * Matrix4.CreateRotationY(RotationY) * Matrix4.CreateTranslation(Position);

    public void Draw(OpenGlRenderer renderer, Camera camera) =>
        renderer.Draw(Mesh, Material, ModelMatrix, camera);
}
