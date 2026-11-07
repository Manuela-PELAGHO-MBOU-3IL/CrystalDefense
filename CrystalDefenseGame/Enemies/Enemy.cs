
using System;
using System.Collections.Generic;
using CrystalDefense.Core;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefenseGame.Enemies;

public class Enemy : GameObject
{
    public float Speed { get; }
    public int MaxHealth { get; }
    public int Health { get; private set; }
    public bool IsBoss { get; }

    // Small variation in the destination.
    private readonly Vector3 _targetOffset;

    public Enemy(
        Mesh mesh,
        Material material,
        Vector3 position,
        float speed = 0.5f,
        int health = 1,
        bool isBoss = false,
        Vector3? targetOffset = null
    ) : base(
        mesh,
        material,
        position,
        radius: isBoss ? 1.0f : 0.5f
    )
    {
        Speed = MathF.Max(0.0f, speed);
        MaxHealth = Math.Max(1, health);
        Health = MaxHealth;
        IsBoss = isBoss;

        _targetOffset = targetOffset ?? Vector3.Zero;
    }

    // Keep the original method for compatibility.
    public void Update(
        float deltaTime,
        Vector3 crystalPosition,
        float crystalRadius
    )
    {
        Update(
            deltaTime,
            crystalPosition,
            crystalRadius,
            null
        );
    }

    // Improved movement with enemy separation.
    public void Update(
        float deltaTime,
        Vector3 crystalPosition,
        float crystalRadius,
        IReadOnlyList<Enemy>? otherEnemies
    )
    {
        if (!IsAlive || deltaTime <= 0.0f)
            return;

        // All enemies approach the crystal,
        // but with slightly different directions.
        Vector3 target = crystalPosition + _targetOffset;

        Vector3 direction = target - Position;
        direction.Y = 0.0f;

        // Distance to the actual crystal center.
        Vector3 crystalDirection = crystalPosition - Position;
        crystalDirection.Y = 0.0f;

        float crystalDistance = crystalDirection.Length;
        float stopDistance = crystalRadius + Radius;

        // Stop when the enemy reaches the crystal.
        if (crystalDistance <= stopDistance)
            return;

        if (direction.LengthSquared < 0.0001f)
            direction = crystalDirection;

        if (direction.LengthSquared < 0.0001f)
            return;

        direction.Normalize();

        // Separation prevents enemies from
        // overlapping and moving in one line.
        Vector3 separation = Vector3.Zero;

        if (otherEnemies != null)
        {
            foreach (Enemy other in otherEnemies)
            {
                if (ReferenceEquals(other, this))
                    continue;

                if (!other.IsAlive)
                    continue;

                Vector3 difference = Position - other.Position;
                difference.Y = 0.0f;

                float distance = difference.Length;

                float minimumDistance =
                    Radius + other.Radius + 0.3f;

                if (distance > 0.0001f &&
                    distance < minimumDistance)
                {
                    float strength =
                        (minimumDistance - distance)
                        / minimumDistance;

                    separation +=
                        (difference / distance) * strength;
                }
            }
        }

        // Combine movement and separation.
        Vector3 movementDirection =
            direction + separation * 1.5f;

        if (movementDirection.LengthSquared < 0.0001f)
            movementDirection = direction;

        movementDirection.Normalize();

        // Prevent movement past the crystal.
        float movement = MathF.Min(
            Speed * deltaTime,
            crystalDistance - stopDistance
        );

        Vector3 nextPosition =
            Position + movementDirection * movement;

        // Do not allow movement inside the crystal.
        Vector3 nextDifference =
            nextPosition - crystalPosition;

        nextDifference.Y = 0.0f;

        if (nextDifference.Length < stopDistance)
        {
            Vector3 outward = nextDifference;

            if (outward.LengthSquared < 0.0001f)
                outward = -crystalDirection;

            if (outward.LengthSquared > 0.0001f)
            {
                outward.Normalize();

                nextPosition.X =
                    crystalPosition.X +
                    outward.X * stopDistance;

                nextPosition.Z =
                    crystalPosition.Z +
                    outward.Z * stopDistance;
            }
        }

        Position = nextPosition;
    }

    public void TakeDamage(int amount = 1)
    {
        if (!IsAlive || amount <= 0)
            return;

        Health = Math.Max(0, Health - amount);

        if (Health == 0)
        {
            Destroy();

            Console.WriteLine(
                IsBoss
                    ? "Boss defeated!"
                    : "Enemy defeated!"
            );
        }
    }
}
