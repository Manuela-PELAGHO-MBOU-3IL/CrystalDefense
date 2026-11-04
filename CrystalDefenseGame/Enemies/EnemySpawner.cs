
using OpenTK.Mathematics;

namespace CrystalDefenseGame.Enemies;

public class EnemySpawner
{
    private readonly List<Enemy> _enemies = new();

    public IReadOnlyList<Enemy> Enemies => _enemies;

    public bool AllEnemiesDefeated =>
        _enemies.Count > 0 &&
        _enemies.All(enemy => !enemy.IsAlive);

    public void SpawnWave(int count, float speed)
    {
        _enemies.Clear();

        float radius = 5.0f;

        for (int i = 0; i < count; i++)
        {
            float angle =
                i * MathF.PI * 2.0f / count;

            Vector3 position = new Vector3(
                MathF.Cos(angle) * radius,
                0.0f,
                MathF.Sin(angle) * radius
            );

            _enemies.Add(new Enemy(position, speed));
        }

        Console.WriteLine(
            $"{count} enemies spawned!"
        );
    }

    public int Update(float deltaTime, Vector3 crystalPosition)
    {
        int damage = 0;

        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsAlive) continue;

            enemy.Update(deltaTime, crystalPosition);

            if (enemy.HasReachedCrystal)
            {
                damage += enemy.AttackDamage;

                Console.WriteLine(
                    "Enemy hit the crystal!"
                );
            }
        }

        return damage;
    }
}
