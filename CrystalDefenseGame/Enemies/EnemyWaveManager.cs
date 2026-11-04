
namespace CrystalDefenseGame.Enemies;

public class EnemyWaveManager
{
    private readonly EnemySpawner _spawner;

    private float _nextWaveTimer;
    private const float WaveDelay = 3.0f;

    public int CurrentWave { get; private set; }
    public int MaxWaves { get; } = 3;

    public bool AllWavesCompleted =>
        CurrentWave >= MaxWaves &&
        _spawner.AllEnemiesDefeated;

    public EnemyWaveManager(EnemySpawner spawner)
    {
        _spawner = spawner;
    }

    public void Start()
    {
        CurrentWave = 0;
        StartNextWave();
    }

    private void StartNextWave()
    {
        if (CurrentWave >= MaxWaves)
            return;

        CurrentWave++;

        int enemyCount = CurrentWave switch
        {
            1 => 3,
            2 => 5,
            _ => 7
        };

        float speed = CurrentWave switch
        {
            1 => 0.5f,
            2 => 0.7f,
            _ => 0.9f
        };

        _spawner.SpawnWave(enemyCount, speed);

        _nextWaveTimer = 0.0f;

        Console.WriteLine(
            $"WAVE {CurrentWave}/{MaxWaves}"
        );
    }

    public void Update(float deltaTime)
    {
        if (CurrentWave >= MaxWaves)
            return;

        if (!_spawner.AllEnemiesDefeated)
        {
            _nextWaveTimer = 0.0f;
            return;
        }

        _nextWaveTimer += deltaTime;

        if (_nextWaveTimer >= WaveDelay)
        {
            StartNextWave();
        }
    }
}
