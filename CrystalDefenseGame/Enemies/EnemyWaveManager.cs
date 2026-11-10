
using System;

namespace CrystalDefenseGame.Enemies;

public sealed class EnemyWaveManager
{
    private readonly EnemySpawner _spawner;

    // Total number of waves
    private const int TotalWaves = 10;

    // Time between the start of two waves
    private const float WaveInterval = 25.0f;

    private float _timer;
    private bool _running;

    public int CurrentWave { get; private set; }

    // All waves are completed only when wave 10
    // has started and no enemies remain.
    public bool AllWavesCompleted =>
        CurrentWave >= TotalWaves &&
        _spawner.AllEnemiesDefeated;

    public EnemyWaveManager(EnemySpawner spawner)
    {
        _spawner = spawner;
    }

    public void Start()
    {
        CurrentWave = 0;
        _timer = 0.0f;
        _running = true;

        // Start wave 1 immediately
        StartNextWave();
    }

    public void Update(float deltaTime)
    {
        if (!_running || deltaTime <= 0.0f)
            return;

        // Do not create more than 10 waves
        if (CurrentWave >= TotalWaves)
        {
            _running = false;
            return;
        }

        // Count elapsed time independently
        // of the enemies still alive.
        _timer += deltaTime;

        // Start a new wave every 25 seconds
        if (_timer >= WaveInterval)
        {
            _timer -= WaveInterval;
            StartNextWave();
        }
    }

    private void StartNextWave()
    {
        if (CurrentWave >= TotalWaves)
            return;

        CurrentWave++;

        // Increase the number of enemies
        int enemyCount =
            4 + (CurrentWave - 1) * 2;

        // Increase movement speed
        float speed =
            0.5f + (CurrentWave - 1) * 0.06f;

        // Increase enemy health
        int health;

        if (CurrentWave <= 3)
        {
            health = 1;
        }
        else if (CurrentWave <= 6)
        {
            health = 2;
        }
        else
        {
            health = 3;
        }

        // Decrease spawn interval gradually
        float spawnInterval = MathF.Max(
            0.9f,
            2.0f - (CurrentWave - 1) * 0.13f
        );

        // Schedule the enemies of this wave
        _spawner.SpawnWave(
            enemyCount,
            speed,
            health,
            spawnInterval
        );

        Console.WriteLine(
            $"========== WAVE {CurrentWave}/{TotalWaves} =========="
        );

        Console.WriteLine(
            $"Enemies: {enemyCount}"
        );

        Console.WriteLine(
            $"Health: {health}"
        );

        Console.WriteLine(
            $"Speed: {speed:F2}"
        );

        Console.WriteLine(
            $"Spawn interval: {spawnInterval:F2}s"
        );
    }
}
