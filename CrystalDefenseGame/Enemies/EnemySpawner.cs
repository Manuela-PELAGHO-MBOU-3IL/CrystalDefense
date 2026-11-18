
using System;
using System.Collections.Generic;
using Engine;
using Engine.OpenGL;
using Engine.Materials;
using OpenTK.Mathematics;

namespace CrystalDefenseGame.Enemies;

public sealed class EnemySpawner
{
    private readonly Mesh _enemyMesh;
    private readonly Material _enemyMaterial;

    private CrystalGolemRenderer? _golemRenderer;

    private IReadOnlyDictionary<int, Material> _golemMaterials =
        new Dictionary<int, Material>();

    private readonly List<Enemy> _enemies = new();
    private readonly Queue<SpawnRequest> _spawnQueue = new();

    private readonly Random _random = new();

    private readonly Dictionary<Enemy, float> _animationTimes =
        new();

    private readonly Dictionary<Enemy, bool> _walkingStates =
        new();

    // =====================================================
    // DEATH FRAGMENTS
    // =====================================================

    private readonly Dictionary<Enemy, Vector3[]> _deathFragments =
        new();

    private const int FragmentCount = 12;
    private const float FragmentScale = 0.16f;
    private const float FragmentGravity = 2.5f;

    // =====================================================
    // ENEMY SETTINGS
    // =====================================================

    private const int MaxAttackers = 6;

    private const float GolemScale = 0.80f;
    private const float MinimumSpawnDistance = 1.6f;
    private const float SpawnVariation = 0.4f;

    private readonly Vector3[] _spawnGates =
    {
        new Vector3(0.0f, 0.5f, -9.0f),
        new Vector3(9.0f, 0.5f, 0.0f),
        new Vector3(0.0f, 0.5f, 9.0f),
        new Vector3(-9.0f, 0.5f, 0.0f)
    };

    private int _nextGateIndex;
    private float _spawnTimer;

    private Vector3 _crystalPosition = Vector3.Zero;

    private readonly record struct SpawnRequest(
        float Speed,
        int Health,
        float Interval
    );

    public IReadOnlyList<Enemy> Enemies => _enemies;

    // Wait for all death animations before clearing a wave.
    public bool AllEnemiesDefeated =>
        _spawnQueue.Count == 0 &&
        _enemies.TrueForAll(enemy =>
            !enemy.IsAlive &&
            (!enemy.IsDying || enemy.DeathAnimationFinished)
        );

    // =====================================================
    // CONSTRUCTOR
    // =====================================================

    public EnemySpawner(
        Mesh enemyMesh,
        Material enemyMaterial)
    {
        _enemyMesh = enemyMesh;
        _enemyMaterial = enemyMaterial;
    }

    // =====================================================
    // GOLEM RENDERER
    // =====================================================

    public void SetGolemRenderer(
        CrystalGolemRenderer renderer,
        IReadOnlyDictionary<int, Material> materials)
    {
        _golemRenderer =
            renderer ?? throw new ArgumentNullException(
                nameof(renderer)
            );

        _golemMaterials =
            materials ?? throw new ArgumentNullException(
                nameof(materials)
            );
    }

    public void SetGolemRenderer(
        CrystalGolemRenderer renderer)
    {
        SetGolemRenderer(
            renderer,
            new Dictionary<int, Material>()
        );
    }

    // =====================================================
    // WAVE SPAWNING
    // =====================================================

    public void SpawnWave(
        int enemyCount,
        float speed,
        int health,
        float spawnInterval)
    {
        if (enemyCount <= 0)
            return;

        bool wasEmpty = _spawnQueue.Count == 0;

        float interval = MathF.Max(
            0.1f,
            spawnInterval
        );

        // Append new groups without clearing pending enemies.
        for (int i = 0; i < enemyCount; i++)
        {
            _spawnQueue.Enqueue(
                new SpawnRequest(
                    speed,
                    health,
                    interval
                )
            );
        }

        if (wasEmpty)
            _spawnTimer = 0.0f;

        Console.WriteLine(
            $"Scheduled {enemyCount} enemies."
        );
    }

    private bool IsSpawnPositionFree(Vector3 position)
    {
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsAlive)
                continue;

            Vector3 difference =
                enemy.Position - position;

            difference.Y = 0.0f;

            if (difference.LengthSquared <
                MinimumSpawnDistance * MinimumSpawnDistance)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryFindSpawnPosition(
        out Vector3 position,
        out int gateIndex)
    {
        for (int attempt = 0;
             attempt < _spawnGates.Length;
             attempt++)
        {
            int index =
                (_nextGateIndex + attempt)
                % _spawnGates.Length;

            Vector3 candidate = _spawnGates[index];

            float offset =
                ((float)_random.NextDouble() * 2.0f - 1.0f)
                * SpawnVariation;

            if (index == 0 || index == 2)
                candidate.X += offset;
            else
                candidate.Z += offset;

            if (!IsSpawnPositionFree(candidate))
                continue;

            position = candidate;
            gateIndex = index;
            return true;
        }

        position = Vector3.Zero;
        gateIndex = -1;
        return false;
    }

    private bool SpawnNextEnemy()
    {
        if (_spawnQueue.Count == 0)
            return false;

        if (!TryFindSpawnPosition(
            out Vector3 position,
            out int gateIndex))
        {
            return false;
        }

        SpawnRequest request = _spawnQueue.Dequeue();

        _nextGateIndex =
            (gateIndex + 1) % _spawnGates.Length;

        Enemy enemy = new Enemy(
            _enemyMesh,
            _enemyMaterial,
            position,
            request.Speed,
            request.Health,
            isBoss: false
        );

        _enemies.Add(enemy);

        _animationTimes[enemy] = 0.0f;
        _walkingStates[enemy] = false;

        Console.WriteLine(
            $"Enemy spawned at gate {gateIndex + 1}."
        );

        return true;
    }

    // =====================================================
    // ATTACK PERMISSIONS
    // =====================================================

    private void UpdateAttackPermissions()
    {
        foreach (Enemy enemy in _enemies)
        {
            enemy.SetAttackPermission(false);
        }

        List<Enemy> alive = new();

        foreach (Enemy enemy in _enemies)
        {
            if (enemy.IsAlive)
                alive.Add(enemy);
        }

        alive.Sort((a, b) =>
        {
            float distanceA =
                (a.Position - _crystalPosition).LengthSquared;

            float distanceB =
                (b.Position - _crystalPosition).LengthSquared;

            return distanceA.CompareTo(distanceB);
        });

        int count = Math.Min(
            MaxAttackers,
            alive.Count
        );

        for (int i = 0; i < count; i++)
        {
            alive[i].SetAttackPermission(true);
        }
    }

    // =====================================================
    // CREATE DEATH FRAGMENTS
    // =====================================================

    private Vector3[] CreateDeathFragments()
    {
        Vector3[] fragments = new Vector3[FragmentCount];

        for (int i = 0; i < fragments.Length; i++)
        {
            float angle =
                (float)_random.NextDouble() *
                MathF.PI * 2.0f;

            float speed =
                0.8f +
                (float)_random.NextDouble() * 1.5f;

            fragments[i] = new Vector3(
                MathF.Cos(angle) * speed,
                1.0f + (float)_random.NextDouble() * 1.5f,
                MathF.Sin(angle) * speed
            );
        }

        return fragments;
    }

    // =====================================================
    // UPDATE
    // =====================================================

    public void Update(
        float deltaTime,
        Vector3 crystalPosition,
        float crystalRadius)
    {
        if (deltaTime <= 0.0f)
            return;

        _crystalPosition = crystalPosition;

        // Spawn queued enemies.
        if (_spawnQueue.Count > 0)
        {
            SpawnRequest next = _spawnQueue.Peek();

            _spawnTimer += deltaTime;

            if (_spawnTimer >= next.Interval)
            {
                if (SpawnNextEnemy())
                    _spawnTimer = 0.0f;
                else
                    _spawnTimer = next.Interval;
            }
        }

        UpdateAttackPermissions();

        // Update death animations.
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsDying)
                continue;

            if (!_deathFragments.ContainsKey(enemy))
            {
                _deathFragments[enemy] =
                    CreateDeathFragments();
            }

            enemy.UpdateDeathAnimation(deltaTime);
        }

        // Move living enemies.
        foreach (Enemy enemy in _enemies)
        {
            if (!enemy.IsAlive)
                continue;

            Vector3 oldPosition = enemy.Position;

            enemy.Update(
                deltaTime,
                crystalPosition,
                crystalRadius,
                _enemies
            );

            Vector3 movement =
                enemy.Position - oldPosition;

            movement.Y = 0.0f;

            bool walking =
                movement.LengthSquared > 0.00000001f;

            _walkingStates[enemy] = walking;

            if (walking)
            {
                _animationTimes[enemy] += deltaTime;
            }
        }
    }

    // =====================================================
    // REMOVE DEAD ENEMIES
    // =====================================================

    public void RemoveDeadEnemies()
    {
        // Keep dying enemies until their animation ends.
        _enemies.RemoveAll(enemy =>
            !enemy.IsAlive &&
            (!enemy.IsDying || enemy.DeathAnimationFinished)
        );

        var deadAnimationEntries = new List<Enemy>();

        foreach (Enemy enemy in _animationTimes.Keys)
        {
            if (!enemy.IsAlive)
            {
                deadAnimationEntries.Add(enemy);
            }
        }

        foreach (Enemy enemy in deadAnimationEntries)
        {
            _animationTimes.Remove(enemy);
            _walkingStates.Remove(enemy);
        }

        var finishedFragments = new List<Enemy>();

        foreach (Enemy enemy in _deathFragments.Keys)
        {
            if (enemy.DeathAnimationFinished)
            {
                finishedFragments.Add(enemy);
            }
        }

        foreach (Enemy enemy in finishedFragments)
        {
            _deathFragments.Remove(enemy);
        }
    }

    // =====================================================
    // DRAW DEATH FRAGMENTS
    // =====================================================

    private void DrawDeathFragments(
        OpenGlRenderer renderer,
        Camera camera,
        Enemy enemy)
    {
        if (!_deathFragments.TryGetValue(
            enemy,
            out Vector3[]? fragments))
        {
            return;
        }

        float t = enemy.DeathTimer;

        float progress = Math.Clamp(
            t / Enemy.DeathDuration,
            0.0f,
            1.0f
        );

        float scale =
            FragmentScale * (1.0f - progress);

        if (scale <= 0.001f)
            return;

        for (int i = 0; i < fragments.Length; i++)
        {
            Vector3 velocity = fragments[i];

            Vector3 offset =
                velocity * t +
                new Vector3(
                    0.0f,
                    -FragmentGravity * t * t,
                    0.0f
                );

            Vector3 position =
                enemy.Position + offset;

            position.Y = MathF.Max(
                0.1f,
                position.Y
            );

            Matrix4 modelMatrix =
                Matrix4.CreateScale(scale) *
                Matrix4.CreateRotationY(t * 4.0f + i) *
                Matrix4.CreateTranslation(position);

            // Draw a small mesh for each fragment.
            // Uses the existing golem renderer.
            if (_golemRenderer != null)
            {
                _golemRenderer.Draw(
                    renderer,
                    camera,
                    _golemMaterials,
                    _enemyMaterial,
                    modelMatrix,
                    0.0f,
                    false
                );
            }
        }
    }

    // =====================================================
    // DRAW
    // =====================================================

    public void Draw(
        OpenGlRenderer renderer,
        Camera camera)
    {
        foreach (Enemy enemy in _enemies)
        {
            // Draw fragments for defeated enemies.
            if (enemy.IsDying)
            {
                if (!enemy.DeathAnimationFinished)
                {
                    DrawDeathFragments(
                        renderer,
                        camera,
                        enemy
                    );
                }

                continue;
            }

            if (!enemy.IsAlive)
                continue;

            if (_golemRenderer == null)
            {
                enemy.Draw(renderer, camera);
                continue;
            }

            Vector3 direction =
                _crystalPosition - enemy.Position;

            direction.Y = 0.0f;

            if (direction.LengthSquared < 0.0001f)
                direction = Vector3.UnitZ;

            direction.Normalize();

            float angle = MathF.Atan2(
                direction.X,
                direction.Z
            );

            Matrix4 modelMatrix =
                Matrix4.CreateScale(GolemScale) *
                Matrix4.CreateRotationY(angle) *
                Matrix4.CreateTranslation(enemy.Position);

            float animationTime =
                _animationTimes.TryGetValue(
                    enemy,
                    out float time
                )
                ? time
                : 0.0f;

            bool walking =
                _walkingStates.TryGetValue(
                    enemy,
                    out bool isWalking
                ) && isWalking;

            _golemRenderer.Draw(
                renderer,
                camera,
                _golemMaterials,
                _enemyMaterial,
                modelMatrix,
                animationTime,
                walking
            );
        }
    }
}
