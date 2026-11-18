
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
                    enemy.TakeDamage(1);

                    projectile.Destroy();

                    Console.WriteLine(
                        $"Projectile hit enemy! Enemy HP: {enemy.Health}"
                    );

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
            // Remove timers for dead enemies.
            if (!enemy.IsAlive)
            {
                _attackTimers.Remove(enemy);
                continue;
            }

            // Only enemies assigned to one of the
            // six attack positions can attack.
            // Waiting enemies cannot damage the crystal.
            if (!enemy.CanAttackCrystal)
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

            // Small tolerance for the assigned attack position.
            bool inAttackRange =
                distance <= collisionDistance + 0.25f;

            if (!inAttackRange)
            {
                _attackTimers.Remove(enemy);
                continue;
            }

            // Initialize the attack timer.
            if (!_attackTimers.ContainsKey(enemy))
            {
                _attackTimers[enemy] = 0.0f;

                Console.WriteLine(
                    "Enemy reached an attack position!"
                );
            }

            // Increase attack timer.
            _attackTimers[enemy] += MathF.Max(
                0.0f,
                deltaTime
            );

            // Attack once every 3 seconds.
            if (_attackTimers[enemy] >= AttackCooldown)
            {
                crystal.TakeDamage(1);

                _attackTimers[enemy] -= AttackCooldown;

                Console.WriteLine(
                    $"Enemy attacks crystal! Crystal HP: {crystal.Health}"
                );
            }

            // Enemy stays alive until the player kills it.
        }
    }

    // =====================================================
    // PLAYER SWORD - ENEMY COLLISIONS (added by Sarah)
    // =====================================================

    public void CheckSwordEnemyCollisions(
        Player player,
        IReadOnlyList<Enemy> enemies
    )
    {
        if (!player.ConsumeSwordStrike())
            return;

        float minimumCos = MathF.Cos(
            MathHelper.DegreesToRadians(
                Player.SwordHalfAngleDegrees
            )
        );

        foreach (Enemy enemy in enemies)
        {
            if (!enemy.IsAlive)
                continue;

            Vector3 toEnemy =
                enemy.Position - player.Position;

            toEnemy.Y = 0.0f;

            float distance = toEnemy.Length;

            if (distance > Player.SwordReach + enemy.Radius)
                continue;

            if (distance > 0.001f &&
                Vector3.Dot(
                    toEnemy / distance,
                    player.FacingDirection
                ) < minimumCos)
            {
                continue;
            }

            enemy.TakeDamage(player.SwordDamage);

            Console.WriteLine(
                $"Sword hit enemy! Enemy HP: {enemy.Health}"
            );
        }
    }

    // =====================================================
    // ENEMY - PLAYER COLLISIONS (added by Sarah)
    // =====================================================

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
