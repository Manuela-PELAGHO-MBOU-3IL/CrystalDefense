
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

    public bool CanAttackCrystal { get; private set; }

    private bool _allowedToAttack;
    private Vector3 _steeringDirection = Vector3.Zero;

    private const float SeparationMargin = 0.35f;
    private const float AvoidanceRange = 2.4f;
    private const float SteeringStrength = 1.2f;
    private const float TurnSpeed = 5.0f;
    private const float CrystalExtraDistance = 0.05f;

    // =====================================================
    // DEATH ANIMATION
    // =====================================================

    public bool IsDying { get; private set; }

    public float DeathTimer { get; private set; }

    public const float DeathDuration = 1.5f;

    public bool DeathAnimationFinished =>
        IsDying && DeathTimer >= DeathDuration;

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
    }

    public void SetAttackPermission(bool allowed)
    {
        _allowedToAttack = allowed;

        if (!allowed)
            CanAttackCrystal = false;
    }

    public void UpdateDeathAnimation(float deltaTime)
    {
        if (!IsDying)
            return;

        DeathTimer = MathF.Min(
            DeathTimer + MathF.Max(0.0f, deltaTime),
            DeathDuration
        );
    }

    public void Update(
        float deltaTime,
        Vector3 crystalPosition,
        float crystalRadius)
    {
        Update(
            deltaTime,
            crystalPosition,
            crystalRadius,
            null
        );
    }

    public void Update(
        float deltaTime,
        Vector3 crystalPosition,
        float crystalRadius,
        IReadOnlyList<Enemy>? otherEnemies)
    {
        if (!IsAlive || deltaTime <= 0.0f)
            return;

        Vector3 toCrystal = crystalPosition - Position;
        toCrystal.Y = 0.0f;

        float crystalDistance = toCrystal.Length;

        float stopDistance =
            crystalRadius + Radius + CrystalExtraDistance;

        float desiredStopDistance = _allowedToAttack
            ? stopDistance
            : stopDistance + 1.9f;

        if (crystalDistance <= desiredStopDistance + 0.08f)
        {
            CanAttackCrystal =
                _allowedToAttack &&
                crystalDistance <= stopDistance + 0.20f;

            _steeringDirection = Vector3.Zero;
            return;
        }

        CanAttackCrystal = false;

        Vector3 desiredDirection =
            toCrystal / crystalDistance;

        Vector3 avoidance = Vector3.Zero;

        if (otherEnemies != null)
        {
            foreach (Enemy other in otherEnemies)
            {
                if (ReferenceEquals(other, this) ||
                    !other.IsAlive)
                    continue;

                Vector3 difference =
                    Position - other.Position;

                difference.Y = 0.0f;

                float distance = difference.Length;

                if (distance < 0.0001f ||
                    distance >= AvoidanceRange)
                    continue;

                float strength =
                    (AvoidanceRange - distance) /
                    AvoidanceRange;

                avoidance +=
                    difference / distance * strength;
            }
        }

        Vector3 desiredSteering =
            desiredDirection +
            avoidance * SteeringStrength;

        if (desiredSteering.LengthSquared < 0.0001f)
            desiredSteering = desiredDirection;

        desiredSteering.Normalize();

        if (_steeringDirection.LengthSquared < 0.0001f)
        {
            _steeringDirection = desiredSteering;
        }
        else
        {
            float blend = Math.Clamp(
                TurnSpeed * deltaTime,
                0.0f,
                1.0f
            );

            Vector3 blended =
                _steeringDirection * (1.0f - blend) +
                desiredSteering * blend;

            if (blended.LengthSquared > 0.0001f)
            {
                blended.Normalize();
                _steeringDirection = blended;
            }
            else
            {
                _steeringDirection = desiredSteering;
            }
        }

        float step = MathF.Min(
            Speed * deltaTime,
            crystalDistance - desiredStopDistance
        );

        if (step <= 0.0f)
            return;

        Vector3 nextPosition =
            Position + _steeringDirection * step;

        Vector3 nextToCrystal =
            nextPosition - crystalPosition;

        nextToCrystal.Y = 0.0f;

        if (nextToCrystal.Length <
            crystalRadius + Radius)
            return;

        if (otherEnemies != null)
        {
            foreach (Enemy other in otherEnemies)
            {
                if (ReferenceEquals(other, this) ||
                    !other.IsAlive)
                    continue;

                Vector3 difference =
                    nextPosition - other.Position;

                difference.Y = 0.0f;

                float minimumDistance =
                    Radius +
                    other.Radius +
                    SeparationMargin;

                if (difference.LengthSquared <
                    minimumDistance * minimumDistance)
                    return;
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
            IsDying = true;
            DeathTimer = 0.0f;
            CanAttackCrystal = false;
            _allowedToAttack = false;

            Destroy();

            Console.WriteLine(
                "Crystal Golem defeated! Shattering..."
            );
        }
    }
}
