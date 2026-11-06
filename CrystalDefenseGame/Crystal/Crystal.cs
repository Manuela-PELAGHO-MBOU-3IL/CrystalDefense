
using Engine;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;
using CrystalDefense.Core;

namespace CrystalDefense.Crystal;

public sealed class Crystal : GameObject
{
    public const float MaxHealth = 100.0f;

    public float Health { get; private set; } = MaxHealth;

    public Crystal(
        Mesh mesh,
        Material material,
        Vector3 position,
        float collisionRadius
    ) : base(mesh, material, position, collisionRadius)
    {
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive || damage <= 0)
            return;

        Health = MathF.Max(0, Health - damage);

        Console.WriteLine(
            $"Crystal health: {Health}/{MaxHealth}"
        );

        if (Health <= 0)
        {
            Destroy();
            Console.WriteLine("CRYSTAL DESTROYED!");
        }
    }

    public float HealthPercentage => Health / MaxHealth;
}
