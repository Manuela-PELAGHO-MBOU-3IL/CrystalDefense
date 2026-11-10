
using System;
using System.Collections.Generic;
using CrystalDefense.Core;
using Engine;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefenseGame.Enemies;

public sealed class EnemySpawner
{
    // Enemy resources
    private readonly Mesh _enemyMesh;
    private readonly Material _enemyMaterial;

    // Active enemies
    private readonly List<Enemy> _enemies = new();

    // Enemies waiting to spawn
    private readonly Queue<SpawnRequest> _spawnQueue = new();

    private readonly Random _random = new();

    // Spawn timing
    private float _spawnTimer = 0.0f;
    private float _spawnInterval = 2.0f;

    public IReadOnlyList<Enemy> Enemies => _enemies;

    // A wave is finished only when all enemies
    // have spawned and all active enemies are defeated.
    public bool AllEnemiesDefeated =>
        _spawnQueue.Count == 0 &&
        _enemies.TrueForAll(enemy => !enemy.IsAlive);

    // Four arena gates
    private readonly Vector3[] _doors =
    {
        new(0.0f, 0.5f, -9.0f),
        new(0.0f, 0.5f,  9.0f),
        new(9.0f, 0.5f,  0.0f),
        new(-9.0f, 0.5f, 0.0f)
    };

    public EnemySpawner(
        Mesh enemyMesh,
        Material enemyMaterial
    )
    {
        _enemyMesh = enemyMesh;
        _enemyMaterial = enemyMaterial;
    }

    // =====================================================
    // SPAWN WAVE - ORIGINAL METHOD (3 PARAMETERS)
    // =====================================================

    public void SpawnWave(
        int enemyCount,
        float speed = 0.5f,
        int health = 1
    )
    {
        SpawnWave(
            enemyCount,
            speed,
            health,
            2.0f
        );
    }

    // =====================================================
    // SPAWN WAVE - NEW METHOD (4 PARAMETERS)
    // =====================================================

    public void SpawnWave(
        int enemyCount,
        float speed,
        int health,
        float spawnInterval
    )
    {
        _spawnInterval = MathF.Max(
            0.1f,
            spawnInterval
        );

        // First enemy spawns immediately.
        _spawnTimer = 0.0f;

        for (int i = 0; i < enemyCount; i++)
        {
            // Distribute enemies between the gates.
            int doorIndex = i % _doors.Length;

            // Small variation in speed.
            float randomSpeed =
                speed * RandomRange(0.85f, 1.15f);

            _spawnQueue.Enqueue(
                new SpawnRequest(
                    doorIndex,
                    randomSpeed,
                    health
                )
            );
        }

        Console.WriteLine(
            $"Scheduled {enemyCount} enemies."
        );
    }

    // =====================================================
    // UPDATE
    // =====================================================

    public void Update(
        float deltaTime,
        Vector3 crystalPosition,
        float crystalRadius
    )
    {
        if (deltaTime <= 0.0f)
            return;

        // Spawn enemies one by one.
        if (_spawnQueue.Count > 0)
        {
            _spawnTimer -= deltaTime;

            if (_spawnTimer <= 0.0f)
            {
                SpawnNextEnemy();

                _spawnTimer += _spawnInterval;
            }
        }

        // Update enemy movement.
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsAlive)
                continue;

            enemy.Update(
                deltaTime,
                crystalPosition,
                crystalRadius,
                _enemies
            );
        }
    }

    // =====================================================
    // SPAWN NEXT ENEMY
    // =====================================================

    private void SpawnNextEnemy()
    {
        if (_spawnQueue.Count == 0)
            return;

        SpawnRequest request = _spawnQueue.Dequeue();

        Vector3 position = _doors[request.DoorIndex];

        // Random position at the gate.
        float lateralOffset =
            RandomRange(-1.0f, 1.0f);

        float depthOffset =
            RandomRange(-0.3f, 0.3f);

        if (request.DoorIndex <= 1)
        {
            position.X += lateralOffset;
            position.Z += depthOffset;
        }
        else
        {
            position.Z += lateralOffset;
            position.X += depthOffset;
        }

        // Slightly different target position.
        Vector3 targetOffset = new Vector3(
            RandomRange(-0.6f, 0.6f),
            0.0f,
            RandomRange(-0.6f, 0.6f)
        );

        Enemy enemy = new Enemy(
            _enemyMesh,
            _enemyMaterial,
            position,
            request.Speed,
            request.Health,
            isBoss: false,
            targetOffset: targetOffset
        );

        _enemies.Add(enemy);

        Console.WriteLine(
            $"Enemy spawned at gate {request.DoorIndex + 1}. " +
            $"Remaining: {_spawnQueue.Count}"
        );
    }

    // =====================================================
    // REMOVE DEFEATED ENEMIES
    // =====================================================

    public void RemoveDeadEnemies()
    {
        _enemies.RemoveAll(
            enemy => !enemy.IsAlive
        );
    }

    // =====================================================
    // DRAW
    // =====================================================

    public void Draw(
        OpenGlRenderer renderer,
        Camera camera
    )
    {
        foreach (Enemy enemy in _enemies)
        {
            if (enemy.IsAlive)
            {
                enemy.Draw(
                    renderer,
                    camera
                );
            }
        }
    }

    // =====================================================
    // RANDOM HELPER
    // =====================================================

    private float RandomRange(
        float min,
        float max
    )
    {
        return min +
            (float)_random.NextDouble() *
            (max - min);
    }

    // =====================================================
    // SPAWN REQUEST
    // =====================================================

    private readonly record struct SpawnRequest(
        int DoorIndex,
        float Speed,
        int Health
    );
}
