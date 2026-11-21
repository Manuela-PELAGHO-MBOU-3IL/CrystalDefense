
using System;
using System.Collections.Generic;
using System.IO;

using CrystalDefenseGame.Enemies;
using CrystalDefense.Core;
using CrystalDefense.Game.Flow;
using CrystalDefense.PlayerAndProjectiles;
using CrystalDefense.Game.UI;

using CrystalEntity = CrystalDefense.Crystal.Crystal;

using Engine;
using Engine.Geometry;
using Engine.Geometry.Loaders;
using Engine.Lighting;
using Engine.Materials;
using Engine.Materials.AmbientDiffuseSpecular;
using Engine.Materials.NormalMapping;
using Engine.Materials.UnlitTexture;
using Engine.OpenGL;

using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CrystalDefense.Game;

internal sealed class CrystalDefenseWindow : Application
{
    // ENGINE
    private OpenGlRenderer _renderer = null!;
    private Camera _camera = null!;
    private readonly List<IDisposable> _resources = new();

    // ARENA AND CRYSTAL
    private ArenaBounds _arenaBounds = null!;

    private readonly List<(
        Mesh Mesh,
        AmbientDiffuseSpecularMaterial Material,
        Matrix4 Model
    )> _arenaObjects = new();

    private Mesh _crystalMesh = null!;
    private Material _crystalMaterial = null!;
    private CrystalEntity _crystal = null!;
    private GameState _gameState = null!;
    private CrystalHealthBar _crystalHealthBar = null!;

    // PLAYER - SARAH
    private Mesh[] _knightParts = null!;
    private Material _knightMaterial = null!;
    private Mesh _projectileMesh = null!;
    private Material _projectileMaterial = null!;

    private Player _player = null!;
    private OrbitCamera _orbitCamera = null!;
    private ProjectileManager _projectiles = null!;
    private readonly PlayerInput _input = new();

    // ENEMIES - ONS
    private Mesh _enemyMesh = null!;
    private Material _enemyMaterial = null!;
    private EnemySpawner _enemySpawner = null!;
    private EnemyWaveManager _enemyWaveManager = null!;
    private CollisionSystem _collisionSystem = null!;

    private CrystalGolemRenderer? _golemRenderer;
    private Dictionary<int, Material>? _golemMaterials;

    // GAME FLOW AND UI - SARAH
    private UiRenderer _uiRenderer = null!;
    private GameUi _gameUi = null!;
    private readonly Random _random = new();

    private GameScreen _screen = GameScreen.MainMenu;
    private float _screenTime;
    private float _waveTime;
    private int _score;
    private int _kills;
    private Upgrade[] _upgradeChoices = Array.Empty<Upgrade>();
    private int _selectedUpgrade;

    public CrystalDefenseWindow(
        GameWindowSettings gameSettings,
        NativeWindowSettings windowSettings
    ) : base(gameSettings, windowSettings)
    {
    }

    protected override void Initialize()
    {
        VSync = VSyncMode.On;
        Title = "Crystal Defense";

        _renderer = new OpenGlRenderer();
        _crystalHealthBar = new CrystalHealthBar();
        _uiRenderer = new UiRenderer();
        _gameUi = new GameUi(_uiRenderer);

        // CAMERA
        _camera = new Camera(
            new Vector3(0.0f, 28.0f, 22.0f),
            Vector3.Zero
        );

        // LIGHTING
        _renderer.DirectionalLight = new DirectionalLight
        {
            Direction = new Vector3(-1.0f, 1.7f, 1.0f),
            AmbientColor = new Vector3(0.3f, 0.3f, 0.3f),
            DiffuseColor = Vector3.One,
            SpecularColor = Vector3.One
        };

        // ARENA
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

        // CRYSTAL
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

        _crystalMesh = Keep(
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

        _crystalMaterial = new AmbientDiffuseSpecularMaterial(
            Keep(Texture.FromFile(crystalTexturePath))
        )
        {
            Shininess = 562.5f
        };

        // KNIGHT - SARAH
        _knightMaterial = new NormalMappingMaterial(
            Keep(LoadTexture("knight_color.png")),
            Keep(LoadTexture("knight_normal.png"))
        )
        {
            Shininess = 40.0f
        };

        _knightParts = new[]
        {
            LoadKnightPart("knight_body.obj"),
            LoadKnightPart("knight_leg_right.obj"),
            LoadKnightPart("knight_leg_left.obj"),
            LoadKnightPart("knight_arm_right.obj"),
            LoadKnightPart("knight_arm_left.obj"),
            LoadKnightPart("knight_cape.obj")
        };

        // PROJECTILES
        string projectilePath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "projectile.obj"
        );

        _projectileMesh = Keep(
            new Mesh(ObjLoader.Load(projectilePath))
        );

        _projectileMaterial = new UnlitTextureMaterial(
            Keep(LoadTexture("projectile.png"))
        );

        // CRYSTAL GOLEM - ONS
        string golemObjPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "CrystalGolem_Animated.obj"
        );

        string glbPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Models",
            "CrystalGolem.glb"
        );

        if (File.Exists(golemObjPath))
        {
            _enemyMesh = Keep(
                new Mesh(ObjLoader.Load(golemObjPath))
            );
        }
        else
        {
            _enemyMesh = Keep(
                new Mesh(Primitives.CreateCube(1.0f))
            );
        }

        _enemyMaterial = new AmbientDiffuseSpecularMaterial(
            Keep(Texture.FromColor(120, 115, 135))
        )
        {
            Shininess = 32.0f
        };

        if (File.Exists(glbPath))
        {
            var golemModel = CrystalGolemLoader.Load();

            var golemParts =
                CrystalGolemMeshLoader.LoadParts(glbPath);

            _golemRenderer = Keep(
                new CrystalGolemRenderer(golemParts)
            );

            _golemMaterials =
                CrystalGolemMaterialLoader.LoadMaterials(
                    golemModel,
                    texture => Keep(texture)
                );

            Console.WriteLine(
                $"Crystal Golem loaded: {golemParts.Count} parts"
            );
        }

        StartNewGame();
        ShowScreen(GameScreen.MainMenu);

        Console.WriteLine("Crystal Defense initialized.");
    }

    // START OR RESTART GAME
    private void StartNewGame()
    {
        _crystal = new CrystalEntity(
            _crystalMesh,
            _crystalMaterial,
            Vector3.Zero,
            collisionRadius: 3.8f
        );

        _gameState = new GameState();

        var knightModel = new KnightModel(
            _knightParts[0],
            _knightParts[1],
            _knightParts[2],
            _knightParts[3],
            _knightParts[4],
            _knightParts[5],
            _knightMaterial
        );

        _player = new Player(
            knightModel,
            _knightParts[0],
            _knightMaterial,
            new Vector3(0.0f, 0.0f, 6.5f),
            radius: 0.6f
        )
        {
            Scale = 1.2f
        };

        _projectiles = new ProjectileManager(
            _projectileMesh,
            _projectileMaterial
        )
        {
            ShootingHeight = 0.6f
        };

        _orbitCamera = new OrbitCamera(
            _camera,
            _player.Position
        );

        _enemySpawner = new EnemySpawner(
            _enemyMesh,
            _enemyMaterial
        );

        if (_golemRenderer != null && _golemMaterials != null)
        {
            _enemySpawner.SetGolemRenderer(
                _golemRenderer,
                _golemMaterials
            );
        }

        _collisionSystem = new CollisionSystem();

        _enemyWaveManager = new EnemyWaveManager(
            _enemySpawner
        )
        {
            AutoStartWaves = false
        };

        _enemyWaveManager.Start();

        _score = 0;
        _kills = 0;
        _waveTime = 0.0f;
    }

    private void ShowScreen(GameScreen screen)
    {
        _screen = screen;
        _screenTime = 0.0f;
    }

    // LOAD ARENA OBJECT
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

        var material = new AmbientDiffuseSpecularMaterial(
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
    }

    protected override void FramebufferResized(
        int width,
        int height
    )
    {
        _renderer.Resize(width, height);
    }

    // UPDATE GAME
    protected override void Update(float deltaTime)
    {
        _screenTime += deltaTime;

        switch (_screen)
        {
            case GameScreen.MainMenu:
                UpdateMainMenu(deltaTime);
                break;

            case GameScreen.Playing:
                UpdatePlaying(deltaTime);
                break;

            case GameScreen.Paused:
                UpdatePaused(deltaTime);
                break;

            case GameScreen.ChoosingUpgrade:
                UpdateUpgradeChoice(deltaTime);
                break;

            case GameScreen.Victory:
            case GameScreen.Defeat:
                UpdateEndScreen(deltaTime);
                break;
        }
    }

    private void UpdateMainMenu(float deltaTime)
    {
        if (KeyboardState.IsKeyPressed(Keys.Escape))
        {
            Close();
            return;
        }

        if (
            KeyboardState.IsKeyPressed(Keys.Enter) ||
            KeyboardState.IsKeyPressed(Keys.KeyPadEnter) ||
            KeyboardState.IsKeyPressed(Keys.Space) ||
            MouseState.IsButtonPressed(MouseButton.Left)
        )
        {
            ShowScreen(GameScreen.Playing);
            return;
        }

        _orbitCamera.Rotate(deltaTime * 0.15f);
        UpdateCamera(deltaTime);
    }

    private void UpdatePaused(float deltaTime)
    {
        if (
            KeyboardState.IsKeyPressed(Keys.Escape) ||
            KeyboardState.IsKeyPressed(Keys.P)
        )
        {
            ShowScreen(GameScreen.Playing);
            return;
        }

        if (KeyboardState.IsKeyPressed(Keys.M))
        {
            StartNewGame();
            ShowScreen(GameScreen.MainMenu);
        }
    }

    private void UpdateEndScreen(float deltaTime)
    {
        if (_screenTime > 0.8f)
        {
            if (
                KeyboardState.IsKeyPressed(Keys.Enter) ||
                KeyboardState.IsKeyPressed(Keys.KeyPadEnter)
            )
            {
                StartNewGame();
                ShowScreen(GameScreen.Playing);
                return;
            }

            if (KeyboardState.IsKeyPressed(Keys.Escape))
            {
                StartNewGame();
                ShowScreen(GameScreen.MainMenu);
                return;
            }
        }

        _orbitCamera.Rotate(deltaTime * 0.1f);
        UpdateCamera(deltaTime);
    }

    // MAIN GAMEPLAY
    private void UpdatePlaying(float deltaTime)
    {
        if (
            KeyboardState.IsKeyPressed(Keys.Escape) ||
            KeyboardState.IsKeyPressed(Keys.P)
        )
        {
            ShowScreen(GameScreen.Paused);
            return;
        }

        _waveTime += deltaTime;

        // PLAYER INPUT
        _input.Read(
            KeyboardState,
            MouseState,
            ClientSize,
            _orbitCamera,
            _player.Position,
            _projectiles.ShootingHeight,
            _enemySpawner.Enemies,
            deltaTime
        );

        // PLAYER MOVEMENT
        _player.Update(
            _input,
            deltaTime,
            _arenaBounds,
            _crystal.Position,
            _crystal.Radius
        );

        // PROJECTILES
        _projectiles.Update(
            _input.IsShooting,
            deltaTime,
            _player,
            _arenaBounds
        );

        // ENEMY WAVES
        _enemyWaveManager.Update(deltaTime);

        // ENEMY MOVEMENT
        _enemySpawner.Update(
            deltaTime,
            _crystal.Position,
            _crystal.Radius
        );

        // PROJECTILE COLLISIONS
        _collisionSystem.CheckProjectileEnemyCollisions(
            _enemySpawner.Enemies,
            _projectiles.Projectiles
        );

        // CRYSTAL COLLISIONS
        _collisionSystem.CheckEnemyCrystalCollisions(
            _enemySpawner.Enemies,
            _crystal,
            deltaTime
        );

        // SWORD COLLISIONS
        _collisionSystem.CheckSwordEnemyCollisions(
            _player,
            _enemySpawner.Enemies
        );

        // ENEMY-PLAYER COLLISIONS
        _collisionSystem.CheckEnemyPlayerCollisions(
            _enemySpawner.Enemies,
            _player
        );

        // SCORE AND DEAD ENEMIES
        CountDefeatedEnemies();
        _enemySpawner.RemoveDeadEnemies();

        // CAMERA
        if (_player.WasHitThisFrame)
        {
            _orbitCamera.Shake(0.35f);
        }

        UpdateCamera(deltaTime);

        // GAME STATE
        _gameState.Update(
            _crystal,
            allWavesCompleted:
                _enemyWaveManager.AllWavesCompleted
        );

        if (_gameState.Status == GameStatus.Defeat)
        {
            ShowScreen(GameScreen.Defeat);
        }
        else if (_gameState.Status == GameStatus.Victory)
        {
            _score += (int)_crystal.Health * 10;
            ShowScreen(GameScreen.Victory);
        }
        else if (_enemyWaveManager.IsCurrentWaveCleared)
        {
            _score += 100 * _enemyWaveManager.CurrentWave;
            OfferUpgrades();
        }
    }

    // SCORE
    private void CountDefeatedEnemies()
    {
        foreach (Enemy enemy in _enemySpawner.Enemies)
        {
            if (!enemy.IsAlive)
            {
                _kills++;

                _score +=
                    10 *
                    Math.Max(1, _enemyWaveManager.CurrentWave) *
                    (enemy.IsBoss ? 5 : 1);
            }
        }
    }

    // UPGRADES
    private UpgradeTargets CurrentUpgradeTargets =>
        new(_player, _projectiles, _crystal);

    private void OfferUpgrades()
    {
        _upgradeChoices = UpgradeCatalog.PickThree(
            CurrentUpgradeTargets,
            _random
        );

        _selectedUpgrade = 0;

        if (_upgradeChoices.Length == 0)
        {
            _enemyWaveManager.StartNextWaveNow();
            return;
        }

        ShowScreen(GameScreen.ChoosingUpgrade);
    }

    private void UpdateUpgradeChoice(float deltaTime)
    {
        int count = _upgradeChoices.Length;

        for (int i = 0; i < count; i++)
        {
            if (
                KeyboardState.IsKeyPressed(Keys.D1 + i) ||
                KeyboardState.IsKeyPressed(Keys.KeyPad1 + i)
            )
            {
                ChooseUpgrade(i);
                return;
            }
        }

        if (
            KeyboardState.IsKeyPressed(Keys.Left) ||
            KeyboardState.IsKeyPressed(Keys.A)
        )
        {
            _selectedUpgrade =
                (_selectedUpgrade + count - 1) % count;
        }

        if (
            KeyboardState.IsKeyPressed(Keys.Right) ||
            KeyboardState.IsKeyPressed(Keys.D)
        )
        {
            _selectedUpgrade =
                (_selectedUpgrade + 1) % count;
        }

        bool canConfirm = _screenTime > 0.4f;

        if (
            canConfirm &&
            (
                KeyboardState.IsKeyPressed(Keys.Enter) ||
                KeyboardState.IsKeyPressed(Keys.KeyPadEnter)
            )
        )
        {
            ChooseUpgrade(_selectedUpgrade);
            return;
        }

        UiRect[] cards = _gameUi.GetUpgradeCardRects(count);

        for (int i = 0; i < count; i++)
        {
            if (!cards[i].Contains(MouseState.Position))
            {
                continue;
            }

            if (MouseState.Delta.LengthSquared > 0.0f)
            {
                _selectedUpgrade = i;
            }

            if (
                canConfirm &&
                MouseState.IsButtonPressed(MouseButton.Left)
            )
            {
                ChooseUpgrade(i);
                return;
            }
        }

        UpdateCamera(deltaTime);
    }

    private void ChooseUpgrade(int index)
    {
        Upgrade upgrade = _upgradeChoices[index];

        upgrade.Apply(CurrentUpgradeTargets);

        Console.WriteLine(
            $"Upgrade chosen: {upgrade.Title}"
        );

        _enemyWaveManager.StartNextWaveNow();

        _waveTime = 0.0f;

        ShowScreen(GameScreen.Playing);
    }

    private void UpdateCamera(float deltaTime)
    {
        _orbitCamera.Update(
            deltaTime,
            KeyboardState,
            MouseState,
            _player.Position
        );
    }

    // RENDER
    protected override void Render()
    {
        _renderer.Clear();

        // ARENA
        foreach (var arenaObject in _arenaObjects)
        {
            _renderer.Draw(
                arenaObject.Mesh,
                arenaObject.Material,
                arenaObject.Model,
                _camera
            );
        }

        // CRYSTAL
        _crystal.Draw(_renderer, _camera);

        // PLAYER
        _player.Draw(_renderer, _camera);

        // PROJECTILES
        _projectiles.Draw(_renderer, _camera);

        // ENEMIES
        _enemySpawner.Draw(_renderer, _camera);

        bool inGame =
            _screen is
                GameScreen.Playing or
                GameScreen.Paused or
                GameScreen.ChoosingUpgrade;

        // CRYSTAL HEALTH BAR
        if (inGame)
        {
            _crystalHealthBar.Draw(
                _crystal.HealthPercentage
            );
        }

        // UI
        _uiRenderer.Begin(ClientSize);

        if (inGame)
        {
            _gameUi.DrawHud(CurrentHudInfo());
        }

        if (_screen == GameScreen.Playing)
        {
            _gameUi.DrawWaveStart(
                _enemyWaveManager.CurrentWave,
                _waveTime,
                _input.Mode == PlayerInput.AimMode.Mouse
            );
        }

        switch (_screen)
        {
            case GameScreen.MainMenu:
                _gameUi.DrawMainMenu(_screenTime);
                break;

            case GameScreen.Paused:
                _gameUi.DrawPause();
                break;

            case GameScreen.ChoosingUpgrade:
                _gameUi.DrawUpgradeChoice(
                    _enemyWaveManager.CurrentWave,
                    _upgradeChoices,
                    _selectedUpgrade
                );
                break;

            case GameScreen.Victory:
            case GameScreen.Defeat:
                _gameUi.DrawEndScreen(
                    _screen == GameScreen.Victory,
                    _score,
                    _enemyWaveManager.CurrentWave,
                    _enemyWaveManager.TotalWaveCount,
                    _kills
                );
                break;
        }

        _uiRenderer.End();
    }

    // HUD
    private HudInfo CurrentHudInfo()
    {
        int alive = 0;

        foreach (Enemy enemy in _enemySpawner.Enemies)
        {
            if (enemy.IsAlive)
            {
                alive++;
            }
        }

        return new HudInfo(
            _player.Health,
            _player.MaxHealth,
            _player.IsKnockedOut,
            _player.DashCharge,
            _enemyWaveManager.CurrentWave,
            _enemyWaveManager.TotalWaveCount,
            _score,
            alive,
            _input.Mode == PlayerInput.AimMode.Mouse
        );
    }

    // SHUTDOWN
    protected override void Shutdown()
    {
        foreach (IDisposable resource in _resources)
        {
            resource.Dispose();
        }

        _crystalHealthBar.Dispose();
        _uiRenderer.Dispose();
        _renderer.Dispose();
    }

    // HELPERS
    private Mesh LoadKnightPart(string fileName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Knight",
            fileName
        );

        return Keep(new Mesh(ObjLoader.Load(path)));
    }

    private static Texture LoadTexture(string fileName)
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

    private T Keep<T>(T resource)
        where T : IDisposable
    {
        _resources.Add(resource);
        return resource;
    }
}
