
using System;

namespace CrystalDefenseGame.Enemies;

public sealed class EnemyWaveManager
{
    private readonly EnemySpawner _spawner;

    private const int TotalWaves = 10;
    private const float WaveInterval = 25.0f;
    private const float InitialDelay = 5.0f;

    private float _timer;
    private bool _running;

    // Initial preparation time
    private float _initialTimer;
    private bool _waitingForFirstWave;

    // Groups inside the current wave
    private int _remainingGroups;
    private int _spawnedEnemyCount;
    private int _waveEnemyCount;

    private float _groupTimer;
    private float _groupInterval;

    public int CurrentWave { get; private set; }

    // Sarah's upgrade system
    public bool AutoStartWaves { get; set; } = true;

    public int TotalWaveCount => TotalWaves;

    // The wave is cleared only when every group has
    // been scheduled and all enemies are defeated.
    public bool IsCurrentWaveCleared =>
        CurrentWave > 0 &&
        _remainingGroups == 0 &&
        _spawner.AllEnemiesDefeated;

    public bool AllWavesCompleted =>
        CurrentWave >= TotalWaves &&
        IsCurrentWaveCleared;

    public EnemyWaveManager(EnemySpawner spawner)
    {
        _spawner = spawner;
    }

    public void Start()
    {
        CurrentWave = 0;
        _timer = 0.0f;
        _running = true;

        _remainingGroups = 0;
        _spawnedEnemyCount = 0;
        _waveEnemyCount = 0;

        // Wait 8 seconds before the first wave
        _initialTimer = InitialDelay;
        _waitingForFirstWave = true;

        Console.WriteLine(
            "Game started. First wave in 8 seconds."
        );
    }

    public void StartNextWaveNow()
    {
        if (!_running || CurrentWave >= TotalWaves)
            return;

        // Do not skip unfinished waves
        if (CurrentWave > 0 && !IsCurrentWaveCleared)
            return;

        _timer = 0.0f;
        StartNextWave();
    }

    public void Update(float deltaTime)
    {
        if (!_running || deltaTime <= 0.0f)
            return;

        // Initial countdown
        if (_waitingForFirstWave)
        {
            _initialTimer -= deltaTime;

            if (_initialTimer <= 0.0f)
            {
                _waitingForFirstWave = false;
                StartNextWave();
            }

            return;
        }

        // Spawn remaining groups
        if (_remainingGroups > 0)
        {
            _groupTimer += deltaTime;

            if (_groupTimer >= _groupInterval)
            {
                _groupTimer -= _groupInterval;
                SpawnNextGroup();
            }
        }

        // Sarah controls the next wave after upgrade choice
        if (!AutoStartWaves)
            return;

        // Optional automatic mode
        if (CurrentWave >= TotalWaves)
            return;

        _timer += deltaTime;

        if (_timer >= WaveInterval && IsCurrentWaveCleared)
        {
            _timer = 0.0f;
            StartNextWave();
        }
    }

    private void StartNextWave()
    {
        if (CurrentWave >= TotalWaves)
            return;

        CurrentWave++;

        // Wave 1 = 10, Wave 2 = 13, Wave 3 = 16...
        _waveEnemyCount =
            10 + (CurrentWave - 1) * 3;

        _spawnedEnemyCount = 0;
        _remainingGroups = 3;
        _groupTimer = 0.0f;

        // More preparation time between groups
        _groupInterval = MathF.Max(
            8.0f,
            12.0f - (CurrentWave - 1) * 0.5f
        );

        Console.WriteLine(
            $"========== WAVE {CurrentWave}/{TotalWaves} =========="
        );

        Console.WriteLine(
            $"Total enemies: {_waveEnemyCount}"
        );

        // First group starts immediately when the wave begins
        SpawnNextGroup();
    }

    private void SpawnNextGroup()
    {
        if (_remainingGroups <= 0)
            return;

        int groupNumber = 4 - _remainingGroups;
        int groupSize;

        // Split enemies into three groups
        if (groupNumber == 1)
        {
            groupSize = Math.Max(
                1,
                _waveEnemyCount / 5
            );
        }
        else if (groupNumber == 2)
        {
            groupSize = Math.Max(
                1,
                _waveEnemyCount * 3 / 10
            );
        }
        else
        {
            groupSize =
                _waveEnemyCount - _spawnedEnemyCount;
        }

        int remainingEnemies =
            _waveEnemyCount - _spawnedEnemyCount;

        groupSize = Math.Min(
            groupSize,
            remainingEnemies
        );

        // Enemy speed increases each wave
        float speed =
            0.5f + (CurrentWave - 1) * 0.06f;

        // Enemy health increases gradually
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

        // Delay between individual enemies
        float spawnInterval = 1.0f;

        _spawner.SpawnWave(
            groupSize,
            speed,
            health,
            spawnInterval
        );

        _spawnedEnemyCount += groupSize;
        _remainingGroups--;

        Console.WriteLine(
            $"Group {groupNumber}: {groupSize} enemies"
        );
    }
}
