
using Engine;
using Engine.Geometry;
using Engine.Geometry.Loaders;
using Engine.Lighting;
using Engine.Materials;
using Engine.Materials.NormalMapping;
using Engine.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using CrystalDefenseGame.Enemies;
using CrystalDefenseGame.Crystal;

namespace Example;

internal static class Program
{
    private static void Main()
    {
        var settings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(960, 720),
            Title = "Crystal Defense",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core
        };

        using var game = new CrystalDefenseWindow(
            GameWindowSettings.Default,
            settings
        );

        game.Run();
    }
}

internal sealed class CrystalDefenseWindow : Application
{
    private OpenGlRenderer? _renderer;
    private Camera? _camera;

    // Crystal
    private Mesh? _crystalMesh;
    private Texture? _crystalTexture;
    private Texture? _crystalNormal;
    private NormalMappingMaterial? _crystalMaterial;

    private readonly Vector3 _crystalPosition =
        Vector3.Zero;

    private CrystalHealth? _crystalHealth;

    // Arena
    private Mesh? _hallMesh;
    private Texture? _hallTexture;
    private Texture? _hallNormal;
    private NormalMappingMaterial? _hallMaterial;

    private readonly Matrix4 _hallModel =
        Matrix4.CreateScale(10.0f);

    // Enemies
    private EnemySpawner? _enemySpawner;
    private EnemyWaveManager? _waveManager;
    private Mesh? _enemyMesh;

    // Health bar
    private Mesh? _healthBarMesh;

    private const float BarWidth = 2.4f;
    private const float BarHeight = 0.18f;
    private const float BarDepth = 0.12f;

    private bool _gameFinished;

    public CrystalDefenseWindow(
        GameWindowSettings gameSettings,
        NativeWindowSettings windowSettings)
        : base(gameSettings, windowSettings)
    {
    }

    protected override void Initialize()
    {
        VSync = VSyncMode.On;

        _renderer = new OpenGlRenderer();

        _camera = new Camera(
            new Vector3(0.0f, 8.0f, 14.0f),
            Vector3.Zero
        );

        _renderer.DirectionalLight = new DirectionalLight
        {
            Direction = new Vector3(-1.0f, 1.7f, 1.0f),
            AmbientColor = new Vector3(0.3f),
            DiffuseColor = new Vector3(1.0f),
            SpecularColor = new Vector3(1.0f)
        };

        string assets = Path.Combine(
            AppContext.BaseDirectory,
            "Assets"
        );

        // Crystal
        _crystalTexture = Texture.FromFile(
            Path.Combine(assets, "blur_colors.png")
        );

        _crystalNormal = Texture.FromFile(
            Path.Combine(assets, "pattern_normal.png")
        );

        _crystalMaterial = new NormalMappingMaterial(
            _crystalTexture,
            _crystalNormal
        )
        {
            Shininess = 40.0f
        };

        _crystalMesh = new Mesh(
            Primitives.CreateCube()
        );

        _crystalHealth = new CrystalHealth(100);

        // Arena
        _hallMesh = new Mesh(
            ObjLoader.Load(
                Path.Combine(assets, "hall.obj")
            )
        );

        _hallTexture = Texture.FromFile(
            Path.Combine(assets, "hall_color.png")
        );

        _hallNormal = Texture.FromFile(
            Path.Combine(assets, "hall_normal.png")
        );

        _hallMaterial = new NormalMappingMaterial(
            _hallTexture,
            _hallNormal
        )
        {
            Shininess = 90.0f
        };

        // Enemy geometry
        _enemyMesh = new Mesh(
            Primitives.CreateCube()
        );

        // Health bar geometry
        _healthBarMesh = new Mesh(
            Primitives.CreateCube()
        );

        // Waves
        _enemySpawner = new EnemySpawner();

        _waveManager = new EnemyWaveManager(
            _enemySpawner
        );

        _waveManager.Start();

        Console.WriteLine(
            "CRYSTAL DEFENSE STARTED!"
        );

        Console.WriteLine("Crystal HP: 100/100");
    }

    protected override void FramebufferResized(
        int width,
        int height)
    {
        _renderer!.Resize(width, height);
    }

    protected override void Update(float deltaTime)
    {
        if (_gameFinished)
            return;

        // Move enemies and calculate collisions
        int damage = _enemySpawner!.Update(
            deltaTime,
            _crystalPosition
        );

        if (damage > 0)
        {
            _crystalHealth!.TakeDamage(damage);
        }

        // Game over
        if (_crystalHealth!.IsDestroyed)
        {
            _gameFinished = true;
            Console.WriteLine("GAME OVER!");
            return;
        }

        // Update waves
        _waveManager!.Update(deltaTime);

        // Victory
        if (_waveManager.AllWavesCompleted)
        {
            _gameFinished = true;
            Console.WriteLine(
                "VICTORY! All waves completed!"
            );
        }
    }

    protected override void Render()
    {
        _renderer!.Clear();

        // Arena
        _renderer.Draw(
            _hallMesh!,
            _hallMaterial!,
            _hallModel,
            _camera!
        );

        // Crystal
        _renderer.Draw(
            _crystalMesh!,
            _crystalMaterial!,
            Matrix4.Identity,
            _camera!
        );

        // Enemies
        foreach (Enemy enemy in _enemySpawner!.Enemies)
        {
            if (!enemy.IsAlive)
                continue;

            DrawEnemy(enemy);
        }

        // Health bar
        DrawHealthBar();
    }

    // =====================================
    // ENEMY DESIGN
    // =====================================

    private void DrawEnemy(Enemy enemy)
    {
        Vector3 p = enemy.Position;

        // Main body
        DrawEnemyPart(
            p + new Vector3(0, 0.0f, 0),
            new Vector3(0.45f, 0.55f, 0.35f)
        );

        // Head
        DrawEnemyPart(
            p + new Vector3(0, 0.45f, 0),
            new Vector3(0.35f, 0.35f, 0.35f)
        );

        // Left arm
        DrawEnemyPart(
            p + new Vector3(-0.35f, 0, 0),
            new Vector3(0.18f, 0.50f, 0.25f)
        );

        // Right arm
        DrawEnemyPart(
            p + new Vector3(0.35f, 0, 0),
            new Vector3(0.18f, 0.50f, 0.25f)
        );

        // Left leg
        DrawEnemyPart(
            p + new Vector3(-0.15f, -0.45f, 0),
            new Vector3(0.18f, 0.35f, 0.22f)
        );

        // Right leg
        DrawEnemyPart(
            p + new Vector3(0.15f, -0.45f, 0),
            new Vector3(0.18f, 0.35f, 0.22f)
        );
    }

    private void DrawEnemyPart(
        Vector3 position,
        Vector3 scale)
    {
        Matrix4 model =
            Matrix4.CreateScale(scale) *
            Matrix4.CreateTranslation(position);

        _renderer!.Draw(
            _enemyMesh!,
            _crystalMaterial!,
            model,
            _camera!
        );
    }

    // =====================================
    // HEALTH BAR
    // =====================================

    private void DrawHealthBar()
    {
        float percentage = Math.Clamp(
            _crystalHealth!.HealthPercentage,
            0.0f,
            1.0f
        );

        // Background
        Matrix4 background =
            Matrix4.CreateScale(
                BarWidth + 0.12f,
                BarHeight + 0.10f,
                BarDepth
            ) *
            Matrix4.CreateTranslation(
                0.0f,
                1.8f,
                0.0f
            );

        _renderer!.Draw(
            _healthBarMesh!,
            _hallMaterial!,
            background,
            _camera!
        );

        if (percentage <= 0)
            return;

        // Dynamic health
        float currentWidth =
            BarWidth * percentage;

        float centerX =
            -BarWidth / 2.0f +
            currentWidth / 2.0f;

        Matrix4 fill =
            Matrix4.CreateScale(
                currentWidth,
                BarHeight,
                BarDepth
            ) *
            Matrix4.CreateTranslation(
                centerX,
                1.8f,
                0.15f
            );

        _renderer.Draw(
            _healthBarMesh!,
            _crystalMaterial!,
            fill,
            _camera!
        );
    }

    protected override void Shutdown()
    {
        _crystalMesh?.Dispose();
        _crystalTexture?.Dispose();
        _crystalNormal?.Dispose();

        _hallMesh?.Dispose();
        _hallTexture?.Dispose();
        _hallNormal?.Dispose();

        _enemyMesh?.Dispose();
        _healthBarMesh?.Dispose();

        _renderer?.Dispose();
    }
}
