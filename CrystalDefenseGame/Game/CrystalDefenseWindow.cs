
using System;
using System.Collections.Generic;
using System.IO;

using CrystalDefenseGame.Enemies;
using CrystalDefense.Core;
using CrystalDefense.PlayerAndProjectiles;
using CrystalDefense.Game.UI;

using CrystalEntity = CrystalDefense.Crystal.Crystal;

using Engine;
using Engine.Geometry;
using Engine.Geometry.Loaders;
using Engine.Lighting;
using Engine.Materials.AmbientDiffuseSpecular;
using Engine.Materials.UnlitTexture;
using Engine.OpenGL;

using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CrystalDefense.Game;

internal sealed class CrystalDefenseWindow : Application
{
    // =====================================================
    // ENGINE
    // =====================================================

    private OpenGlRenderer _renderer = null!;
    private Camera _camera = null!;

    private readonly List<IDisposable> _resources = new();

    // =====================================================
    // MANUELA - ARENA AND CRYSTAL
    // =====================================================

    private ArenaBounds _arenaBounds = null!;

    private readonly List<(
        Mesh Mesh,
        AmbientDiffuseSpecularMaterial Material,
        Matrix4 Model
    )> _arenaObjects = new();

    private CrystalEntity _crystal = null!;
    private GameState _gameState = null!;
    private CrystalHealthBar _crystalHealthBar = null!;

    // =====================================================
    // SARAH - PLAYER AND PROJECTILES
    // =====================================================

    private Player _player = null!;
    private ProjectileManager _projectiles = null!;

    // =====================================================
    // ONS - ENEMIES AND COLLISIONS
    // =====================================================

    private EnemySpawner _enemySpawner = null!;
    private EnemyWaveManager _enemyWaveManager = null!;
    private CollisionSystem _collisionSystem = null!;

    // =====================================================
    // CONSTRUCTOR
    // =====================================================

    public CrystalDefenseWindow(
        GameWindowSettings gameSettings,
        NativeWindowSettings windowSettings
    ) : base(gameSettings, windowSettings)
    {
    }

    // =====================================================
    // INITIALIZE
    // =====================================================

    protected override void Initialize()
    {
        VSync = VSyncMode.On;

        _renderer = new OpenGlRenderer();
        _crystalHealthBar = new CrystalHealthBar();

        // Camera
        _camera = new Camera(
            new Vector3(0.0f, 18.0f, 16.0f),
            new Vector3(0.0f, 0.0f, 1.0f)
        );

        // Lighting
        _renderer.DirectionalLight = new DirectionalLight
        {
            Direction = new Vector3(-1.0f, 1.7f, 1.0f),
            AmbientColor = new Vector3(0.3f, 0.3f, 0.3f),
            DiffuseColor = new Vector3(1.0f, 1.0f, 1.0f),
            SpecularColor = new Vector3(1.0f, 1.0f, 1.0f)
        };

        // =================================================
        // MANUELA - ARENA
        // =================================================

        _arenaBounds = new ArenaBounds(
            new Vector2(0.0f, 0.0f),
            11.5f
        );

        LoadArenaObject(
            "Arena_Stone.obj",
            "stone_color.png",
            22.5f
        );

        LoadArenaObject(
            "Arena_Floor.obj",
            "floor_color.png",
            10.0f
        );

        LoadArenaObject(
            "Arena_Gates.obj",
            "gates_color.png",
            360.0f
        );

        LoadArenaObject(
            "Arena_Magic.obj",
            "magic_color.png",
            490.0f
        );

        // =================================================
        // MANUELA - CRYSTAL
        // =================================================

        string arenaDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Arena"
        );

        string crystalPath = Path.Combine(
            arenaDirectory,
            "Arena_Crystal.obj"
        );

        if (!File.Exists(crystalPath))
        {
            throw new FileNotFoundException(
                "Crystal model not found.",
                crystalPath
            );
        }

        Mesh crystalMesh = Keep(
            new Mesh(ObjLoader.Load(crystalPath))
        );

        string crystalTexturePath = Path.Combine(
            arenaDirectory,
            "crystal_color.png"
        );

        if (!File.Exists(crystalTexturePath))
        {
            throw new FileNotFoundException(
                "Crystal texture not found.",
                crystalTexturePath
            );
        }

        var crystalMaterial =
            new AmbientDiffuseSpecularMaterial(
                Keep(Texture.FromFile(crystalTexturePath))
            )
            {
                Shininess = 562.5f
            };

        _crystal = new CrystalEntity(
            crystalMesh,
            crystalMaterial,
            Vector3.Zero,
            collisionRadius: 3.8f
        );

        _gameState = new GameState();

        // =================================================
        // SARAH - PLAYER
        // =================================================

        // Low-poly knight modelled in Blender (source: Blender/knight.blend).
        // His feet are at y = 0 (the arena floor) and he looks along -Z.
        // All his colors come from one small texture with one color cell per part.
        var playerMesh = Keep(
            new Mesh(ObjLoader.Load(Path.Combine(
                AppContext.BaseDirectory, "Assets", "knight.obj")))
        );

        var playerMaterial =
            new AmbientDiffuseSpecularMaterial(
                Keep(LoadTexture("knight_palette.png"))
            )
            {
                Shininess = 48.0f
            };

        _player = new Player(
            playerMesh,
            playerMaterial,
            new Vector3(0.0f, 0.0f, 6.0f),
            radius: 0.45f
        )
        {
            Scale = 1.2f
        };

        // =================================================
        // SARAH - PROJECTILES
        // =================================================

        // Small low-poly energy orb modelled in Blender (source: Blender/projectile.blend).
        // Unlit, so that it always looks bright.
        var projectileMesh = Keep(
            new Mesh(ObjLoader.Load(Path.Combine(
                AppContext.BaseDirectory, "Assets", "projectile.obj")))
        );

        var projectileMaterial =
            new UnlitTextureMaterial(
                Keep(LoadTexture("projectile.png"))
            );

        _projectiles = new ProjectileManager(
            projectileMesh,
            projectileMaterial
        )
        {
            // Middle height of the enemies (they are 1 unit high, standing on the floor).
            ShootingHeight = 0.6f
        };

        // =================================================
        // ONS - ENEMIES
        // =================================================

        // Temporary cube model.
        // The Crystal Golem will replace it later.
        var enemyMesh = Keep(
            new Mesh(Primitives.CreateCube(1.0f))
        );

        var enemyMaterial =
            new AmbientDiffuseSpecularMaterial(
                Keep(LoadTexture("player.png"))
            )
            {
                Shininess = 32.0f
            };

        // Improved enemy spawner:
        // Different positions and speeds.
        _enemySpawner = new EnemySpawner(
            enemyMesh,
            enemyMaterial
        );

        // Projectile and crystal collisions.
        _collisionSystem = new CollisionSystem();

        // Wave management.
        _enemyWaveManager = new EnemyWaveManager(
            _enemySpawner
        );

        _enemyWaveManager.Start();

        Console.WriteLine(
            "Crystal Defense initialized."
        );

        Console.WriteLine(
            $"Crystal health: {_crystal.Health}/{CrystalEntity.MaxHealth}"
        );
    }

    // =====================================================
    // LOAD ARENA OBJECT
    // =====================================================

    private void LoadArenaObject(
        string fileName,
        string textureName,
        float shininess
    )
    {
        string arenaDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Arena"
        );

        string arenaPath = Path.Combine(
            arenaDirectory,
            fileName
        );

        if (!File.Exists(arenaPath))
        {
            throw new FileNotFoundException(
                $"Arena object not found: {fileName}",
                arenaPath
            );
        }

        Mesh mesh = Keep(
            new Mesh(ObjLoader.Load(arenaPath))
        );

        string texturePath = Path.Combine(
            arenaDirectory,
            textureName
        );

        if (!File.Exists(texturePath))
        {
            throw new FileNotFoundException(
                $"Arena texture not found: {textureName}",
                texturePath
            );
        }

        var material =
            new AmbientDiffuseSpecularMaterial(
                Keep(Texture.FromFile(texturePath))
            )
            {
                Shininess = shininess
            };

        _arenaObjects.Add((
            mesh,
            material,
            Matrix4.Identity
        ));

        Console.WriteLine(
            $"Arena object loaded: {fileName}"
        );
    }

    // =====================================================
    // RESIZE
    // =====================================================

    protected override void FramebufferResized(
        int width,
        int height
    )
    {
        _renderer.Resize(width, height);
    }

    // =====================================================
    // UPDATE
    // =====================================================

    protected override void Update(float deltaTime)
    {
        if (KeyboardState.IsKeyPressed(Keys.Escape))
        {
            Close();
            return;
        }

        // Stop gameplay after victory or defeat.
        if (_gameState.IsGameOver)
            return;

        // =================================================
        // SARAH - PLAYER
        // =================================================

        _player.Update(
            KeyboardState,
            deltaTime,
            _arenaBounds,
            _crystal.Position,
            _crystal.Radius
        );

        // =================================================
        // SARAH - PROJECTILES
        // =================================================

        _projectiles.Update(
            KeyboardState,
            deltaTime,
            _player,
            _arenaBounds
        );

        // =================================================
        // ONS - ENEMY WAVES
        // =================================================

        // Manage the current wave.
        _enemyWaveManager.Update(deltaTime);

        // =================================================
        // ONS - ENEMY MOVEMENT
        // =================================================

        // Enemies move toward the crystal.
        // Different spawn positions and speeds.
        // Separation is handled inside EnemySpawner.
        _enemySpawner.Update(
            deltaTime,
            _crystal.Position,
            _crystal.Radius
        );

        // =================================================
        // ONS - PROJECTILE COLLISIONS
        // =================================================

        _collisionSystem.CheckProjectileEnemyCollisions(
            _enemySpawner.Enemies,
            _projectiles.Projectiles
        );

        // =================================================
        // ONS - CRYSTAL COLLISIONS
        // =================================================

        // Each enemy attacks every 3 seconds.
        // Enemies stay alive until defeated.
        _collisionSystem.CheckEnemyCrystalCollisions(
            _enemySpawner.Enemies,
            _crystal,
            deltaTime
        );

        // Remove defeated enemies.
        _enemySpawner.RemoveDeadEnemies();

        // =================================================
        // MANUELA - GAME STATE
        // =================================================

        _gameState.Update(
            _crystal,
            allWavesCompleted:
                _enemyWaveManager.AllWavesCompleted
        );
    }

    // =====================================================
    // RENDER
    // =====================================================

    protected override void Render()
    {
        _renderer.Clear();

        // =================================================
        // MANUELA - ARENA
        // =================================================

        foreach (var arenaObject in _arenaObjects)
        {
            _renderer.Draw(
                arenaObject.Mesh,
                arenaObject.Material,
                arenaObject.Model,
                _camera
            );
        }

        // =================================================
        // MANUELA - CRYSTAL
        // =================================================

        _crystal.Draw(
            _renderer,
            _camera
        );

        // =================================================
        // SARAH - PLAYER
        // =================================================

        _player.Draw(
            _renderer,
            _camera
        );

        // =================================================
        // SARAH - PROJECTILES
        // =================================================

        _projectiles.Draw(
            _renderer,
            _camera
        );

        // =================================================
        // ONS - ENEMIES
        // =================================================

        _enemySpawner.Draw(
            _renderer,
            _camera
        );

        // =================================================
        // MANUELA - CRYSTAL HEALTH BAR
        // =================================================

        _crystalHealthBar.Draw(
            _crystal.HealthPercentage
        );
    }

    // =====================================================
    // SHUTDOWN
    // =====================================================

    protected override void Shutdown()
    {
        foreach (IDisposable resource in _resources)
        {
            resource.Dispose();
        }

        _crystalHealthBar.Dispose();
        _renderer.Dispose();
    }

    // =====================================================
    // TEXTURES
    // =====================================================

    private static Texture LoadTexture(
        string fileName
    )
    {
        string texturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            fileName
        );

        if (!File.Exists(texturePath))
        {
            throw new FileNotFoundException(
                $"Texture not found: {fileName}",
                texturePath
            );
        }

        return Texture.FromFile(texturePath);
    }

    // =====================================================
    // RESOURCE MANAGEMENT
    // =====================================================

    private T Keep<T>(T resource)
        where T : IDisposable
    {
        _resources.Add(resource);
        return resource;
    }
}
