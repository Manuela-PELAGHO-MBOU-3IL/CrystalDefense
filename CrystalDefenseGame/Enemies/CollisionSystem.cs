
using System;
using System.Collections.Generic;

using CrystalDefense.PlayerAndProjectiles;
using OpenTK.Mathematics;

using CrystalEntity = CrystalDefense.Crystal.Crystal;

namespace CrystalDefenseGame.Enemies;

public sealed class CollisionSystem
{
    // =====================================================
    // ENEMY ATTACK SYSTEM
    // =====================================================

    private const float AttackCooldown = 3.0f;

    // Each enemy has its own attack timer.
    private readonly Dictionary<Enemy, float> _attackTimers = new();

    // =====================================================
    // PROJECTILE - ENEMY COLLISIONS
    // =====================================================

    public void CheckProjectileEnemyCollisions(
        IReadOnlyList<Enemy> enemies,
        IReadOnlyList<Projectile> projectiles
    )
    {
        foreach (Projectile projectile in projectiles)
        {
            if (!projectile.IsAlive)
                continue;

            foreach (Enemy enemy in enemies)
            {
                if (!enemy.IsAlive)
                    continue;

                float distance = Vector3.Distance(
                    projectile.Position,
                    enemy.Position
                );

                float collisionDistance =
                    projectile.Radius + enemy.Radius;

                if (distance <= collisionDistance)
                {
                    // Damage the enemy.
                    enemy.TakeDamage(1);

                    // Destroy the projectile after impact.
                    projectile.Destroy();

                    Console.WriteLine(
                        $"Projectile hit enemy! Enemy HP: {enemy.Health}"
                    );

                    // One projectile hits only one enemy.
                    break;
                }
            }
        }
    }

    // =====================================================
    // ENEMY - CRYSTAL COLLISIONS
    // =====================================================

    public void CheckEnemyCrystalCollisions(
        IReadOnlyList<Enemy> enemies,
        CrystalEntity crystal,
        float deltaTime
    )
    {
        foreach (Enemy enemy in enemies)
        {
            if (!enemy.IsAlive)
            {
                _attackTimers.Remove(enemy);
                continue;
            }

            // Calculate horizontal distance.
            float distance = Vector2.Distance(
                new Vector2(
                    enemy.Position.X,
                    enemy.Position.Z
                ),
                new Vector2(
                    crystal.Position.X,
                    crystal.Position.Z
                )
            );

            float collisionDistance =
                enemy.Radius + crystal.Radius;

            bool touchingCrystal =
                distance <= collisionDistance + 0.01f;

            // Enemy has not reached the crystal.
            if (!touchingCrystal)
            {
                _attackTimers.Remove(enemy);
                continue;
            }

            // Initialize the attack timer.
            if (!_attackTimers.ContainsKey(enemy))
            {
                _attackTimers[enemy] = 0.0f;

                Console.WriteLine(
                    "Enemy reached the crystal!"
                );
            }

            // Increase the timer.
            _attackTimers[enemy] += MathF.Max(0.0f, deltaTime);

            // Attack every 3 seconds.
            if (_attackTimers[enemy] >= AttackCooldown)
            {
                crystal.TakeDamage(1);

                _attackTimers[enemy] -= AttackCooldown;

                Console.WriteLine(
                    $"Enemy attacks crystal! Crystal HP: {crystal.Health}"
                );
            }

            // IMPORTANT:
            // Do NOT destroy the enemy here.
            // The enemy stays alive until the player kills it.
        }
    }

    // =====================================================
    // PLAYER SWORD - ENEMY COLLISIONS (added by Sarah)
    // =====================================================

    // In the middle of a sword slash, every enemy in a cone in front of the knight takes damage.
    public void CheckSwordEnemyCollisions(
        Player player,
        IReadOnlyList<Enemy> enemies
    )
    {
        if (!player.ConsumeSwordStrike())
            return;

        float minimumCos = MathF.Cos(
            MathHelper.DegreesToRadians(Player.SwordHalfAngleDegrees)
        );

        foreach (Enemy enemy in enemies)
        {
            if (!enemy.IsAlive)
                continue;

            // Horizontal vector from the knight to the enemy.
            Vector3 toEnemy = enemy.Position - player.Position;
            toEnemy.Y = 0.0f;
            float distance = toEnemy.Length;

            if (distance > Player.SwordReach + enemy.Radius)
                continue;

            // Is the enemy in front of the knight? (dot product = cosine of the angle)
            if (distance > 0.001f &&
                Vector3.Dot(toEnemy / distance, player.FacingDirection) < minimumCos)
                continue;

            enemy.TakeDamage(Player.SwordDamage);

            Console.WriteLine(
                $"Sword hit enemy! Enemy HP: {enemy.Health}"
            );
        }
    }

    // =====================================================
    // ENEMY - PLAYER COLLISIONS (added by Sarah)
    // =====================================================

    // An enemy that touches the knight hurts him (he is then invincible for a moment).
    public void CheckEnemyPlayerCollisions(
        IReadOnlyList<Enemy> enemies,
        Player player
    )
    {
        if (!player.CanAct)
            return;

        foreach (Enemy enemy in enemies)
        {
            if (enemy.IsAlive && enemy.CollidesWith(player))
            {
                player.TakeDamage(1);
                return;
            }
        }
    }
}
