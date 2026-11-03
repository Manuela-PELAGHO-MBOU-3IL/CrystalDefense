using CrystalDefense.Core;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefense.PlayerAndProjectiles;

// A projectile flies in a straight line. It is destroyed when it leaves the arena
// or when the collision system (enemies) calls Destroy() after a hit.
public sealed class Projectile : GameObject
{
    private readonly Vector3 _direction;

    public Projectile(Mesh mesh, Material material, Vector3 startPosition, Vector3 direction, float speed, float radius)
        : base(mesh, material, startPosition, radius)
    {
        _direction = direction.Normalized();
        Speed = speed;
    }

    public float Speed { get; }

    public void Update(float deltaTime, ArenaBounds bounds)
    {
        Position += _direction * Speed * deltaTime;

        // the projectile spins a little, so that it is easier to see
        RotationY += 10.0f * deltaTime;

        if (!bounds.Contains(Position))
            Destroy();
    }
}
