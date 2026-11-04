
using OpenTK.Mathematics;

namespace CrystalDefenseGame.Enemies;

public class Enemy
{
    public Vector3 Position { get; private set; }
    public float Speed { get; set; }
    public bool IsAlive { get; private set; } = true;
    public bool HasReachedCrystal { get; private set; }
    public int AttackDamage { get; set; } = 10;
    public float CollisionDistance { get; set; } = 0.8f;

    public Enemy(Vector3 startPosition, float speed = 0.5f)
    {
        Position = startPosition;
        Speed = speed;
    }

    public void Update(float deltaTime, Vector3 crystalPosition)
    {
        if (!IsAlive) return;

        Vector3 direction = crystalPosition - Position;
        float distance = direction.Length;

        if (distance <= CollisionDistance)
        {
            HasReachedCrystal = true;
            IsAlive = false;
            return;
        }

        if (distance > 0.0001f)
        {
            direction.Normalize();

            float movement = MathF.Min(
                Speed * deltaTime,
                distance - CollisionDistance
            );

            Position += direction * movement;
        }
    }

    public void TakeDamage()
    {
        if (!IsAlive) return;

        IsAlive = false;
        HasReachedCrystal = false;

        Console.WriteLine("Enemy destroyed!");
    }
}
