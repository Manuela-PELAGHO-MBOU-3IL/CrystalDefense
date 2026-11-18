
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

// The main window. It loads the models once (Initialize), creates a fresh game when needed (StartNewGame)
// and switches between the screens of the game: menu, playing, pause, upgrade choice, victory, defeat.
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

    private Mesh _crystalMesh = null!;
    private Material _crystalMaterial = null!;
    private CrystalEntity _crystal = null!;
    private GameState _gameState = null!;
    private CrystalHealthBar _crystalHealthBar = null!;

    // =====================================================
    // SARAH - PLAYER AND PROJECTILES
    // =====================================================

    private Mesh[] _knightParts = null!;   // body, right leg, left leg, right arm, left arm, cape
    private Material _knightMaterial = null!;
    private Mesh _projectileMesh = null!;
    private Material _projectileMaterial = null!;

    private Player _player = null!;
    private ProjectileManager _projectiles = null!;
    private OrbitCamera _orbitCamera = null!;
    private readonly PlayerInput _input = new();

    // =====================================================
    // ONS - ENEMIES AND COLLISIONS
    // =====================================================

    private Mesh _enemyMesh = null!;
    private Material _enemyMaterial = null!;
    private EnemySpawner _enemySpawner = null!;
    private EnemyWaveManager _enemyWaveManager = null!;
    private CollisionSystem _collisionSystem = null!;

    // =====================================================
    // SARAH - GAME FLOW AND INTERFACE
    // =====================================================

    private UiRenderer _uiRenderer = null!;
    private GameUi _gameUi = null!;
    private readonly Random _random = new();

    private GameScreen _screen = GameScreen.MainMenu;
    private float _screenTime;            // seconds since the current screen was opened (blinking texts)
    private float _waveTime;              // seconds played since the current wave started (wave banner, tips)
    private int _score;
    private int _kills;
    private Upgrade[] _upgradeChoices = Array.Empty<Upgrade>();
    private int _selectedUpgrade;

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
    // INITIALIZE: load everything once
    // =====================================================

    protected override void Initialize()
    {
        VSync = VSyncMode.On;
        Title = "Crystal Defense";

        _renderer = new OpenGlRenderer();
        _crystalHealthBar = new CrystalHealthBar();
        _uiRenderer = new UiRenderer();
        _gameUi = new GameUi(_uiRenderer);

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

        _crystalMesh = crystalMesh;
        _crystalMaterial = crystalMaterial;

        // =================================================
        // SARAH - KNIGHT AND PROJECTILES
        // =================================================

        // Knight modelled in Blender (source: Blender/knight.blend), made of six parts that are
        // animated as a scene graph (see KnightModel). All parts share one UV atlas with two textures
        // baked in Blender: the colours (worn metal, striped cloth, leather...) and a normal map for
        // the small reliefs (scratches, hammered metal, woven cloth), drawn with normal mapping.
        _knightMaterial =
            new NormalMappingMaterial(
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
            LoadKnightPart("knight_cape.obj"),
        };

        // Small low-poly energy orb modelled in Blender (source: Blender/projectile.blend).
        // Unlit, so that it always looks bright.
        _projectileMesh = Keep(
            new Mesh(ObjLoader.Load(Path.Combine(
                AppContext.BaseDirectory, "Assets", "projectile.obj")))
        );

        _projectileMaterial =
            new UnlitTextureMaterial(
                Keep(LoadTexture("projectile.png"))
            );

        // =================================================
        // ONS - ENEMIES
        // =================================================

        // Temporary cube model.
        // The Crystal Golem will replace it later.
        _enemyMesh = Keep(
            new Mesh(Primitives.CreateCube(1.0f))
        );

        _enemyMaterial =
            new AmbientDiffuseSpecularMaterial(
                Keep(LoadTexture("player.png"))
            )
            {
                Shininess = 32.0f
            };

        // A fresh game is ready behind the main menu.
        StartNewGame();
        ShowScreen(GameScreen.MainMenu);

        Console.WriteLine(
            "Crystal Defense initialized."
        );
    }

    // =====================================================
    // SARAH - START A NEW GAME (also used by "play again")
    // =====================================================

    // Creates everything that changes during a game; the loaded models are reused.
    private void StartNewGame()
    {
        // Manuela - crystal and game state.
        _crystal = new CrystalEntity(
            _crystalMesh,
            _crystalMaterial,
            Vector3.Zero,
            collisionRadius: 3.8f
        );

        _gameState = new GameState();

        // Sarah - knight, projectiles and camera.
        var knightModel = new KnightModel(
            _knightParts[0], _knightParts[1], _knightParts[2],
            _knightParts[3], _knightParts[4], _knightParts[5],
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
            Scale = 1.2f   // the model is about 1.9 high in Blender: about 2.3 in the game
        };

        _projectiles = new ProjectileManager(
            _projectileMesh,
            _projectileMaterial
        )
        {
            // Middle height of the enemies (they are 1 unit high, standing on the floor).
            ShootingHeight = 0.6f
        };

        _orbitCamera = new OrbitCamera(
            _camera,
            _player.Position
        );

        // Ons - enemies, collisions and waves.
        _enemySpawner = new EnemySpawner(
            _enemyMesh,
            _enemyMaterial
        );

        _collisionSystem = new CollisionSystem();

        // The next wave starts only after the upgrade choice (see UpdatePlaying / ChooseUpgrade).
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
    // UPDATE: depends on the current screen
    // =====================================================

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

        if (KeyboardState.IsKeyPressed(Keys.Enter) ||
            KeyboardState.IsKeyPressed(Keys.KeyPadEnter) ||
            KeyboardState.IsKeyPressed(Keys.Space) ||
            MouseState.IsButtonPressed(MouseButton.Left))
        {
            ShowScreen(GameScreen.Playing);
            return;
        }

        // The camera turns slowly around the knight behind the menu.
        _orbitCamera.Rotate(deltaTime * 0.15f);
        UpdateCamera(deltaTime);
    }

    private void UpdatePaused(float deltaTime)
    {
        if (KeyboardState.IsKeyPressed(Keys.Escape) || KeyboardState.IsKeyPressed(Keys.P))
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
        // A short delay, so that a key held during the last fight does not skip this screen.
        if (_screenTime > 0.8f)
        {
            if (KeyboardState.IsKeyPressed(Keys.Enter) || KeyboardState.IsKeyPressed(Keys.KeyPadEnter))
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

    private void UpdatePlaying(float deltaTime)
    {
        if (KeyboardState.IsKeyPressed(Keys.Escape) || KeyboardState.IsKeyPressed(Keys.P))
        {
            ShowScreen(GameScreen.Paused);
            return;
        }

        _waveTime += deltaTime;

        // =================================================
        // SARAH - PLAYER
        // =================================================

        // Keyboard only or keyboard + mouse: PlayerInput turns the keys and the mouse into actions.
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

        _player.Update(
            _input,
            deltaTime,
            _arenaBounds,
            _crystal.Position,
            _crystal.Radius
        );

        // =================================================
        // SARAH - PROJECTILES
        // =================================================

        _projectiles.Update(
            _input.IsShooting,
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

        // =================================================
        // SARAH - SWORD AND DAMAGE TO THE PLAYER
        // =================================================

        _collisionSystem.CheckSwordEnemyCollisions(
            _player,
            _enemySpawner.Enemies
        );

        _collisionSystem.CheckEnemyPlayerCollisions(
            _enemySpawner.Enemies,
            _player
        );

        // =================================================
        // SARAH - SCORE (before the defeated enemies are removed)
        // =================================================

        CountDefeatedEnemies();

        // Remove defeated enemies.
        _enemySpawner.RemoveDeadEnemies();

        // =================================================
        // SARAH - CAMERA
        // =================================================

        if (_player.WasHitThisFrame)
            _orbitCamera.Shake(0.35f);

        UpdateCamera(deltaTime);

        // =================================================
        // MANUELA - GAME STATE
        // =================================================

        _gameState.Update(
            _crystal,
            allWavesCompleted:
                _enemyWaveManager.AllWavesCompleted
        );

        // =================================================
        // SARAH - END OF THE GAME OR END OF A WAVE
        // =================================================

        if (_gameState.Status == GameStatus.Defeat)
        {
            ShowScreen(GameScreen.Defeat);
        }
        else if (_gameState.Status == GameStatus.Victory)
        {
            _score += (int)_crystal.Health * 10;   // bonus for the health left in the crystal
            ShowScreen(GameScreen.Victory);
        }
        else if (_enemyWaveManager.IsCurrentWaveCleared)
        {
            _score += 100 * _enemyWaveManager.CurrentWave;   // bonus for each finished wave
            OfferUpgrades();
        }
    }

    // =====================================================
    // SARAH - SCORE AND UPGRADES
    // =====================================================

    // Every enemy defeated in this frame gives points; later waves give more.
    private void CountDefeatedEnemies()
    {
        foreach (Enemy enemy in _enemySpawner.Enemies)
        {
            if (!enemy.IsAlive)
            {
                _kills++;
                _score += 10 * Math.Max(1, _enemyWaveManager.CurrentWave) * (enemy.IsBoss ? 5 : 1);
            }
        }
    }

    private UpgradeTargets CurrentUpgradeTargets => new(_player, _projectiles, _crystal);

    private void OfferUpgrades()
    {
        _upgradeChoices = UpgradeCatalog.PickThree(CurrentUpgradeTargets, _random);
        _selectedUpgrade = 0;

        if (_upgradeChoices.Length == 0)
        {
            _enemyWaveManager.StartNextWaveNow();   // nothing to offer: go on directly
            return;
        }

        ShowScreen(GameScreen.ChoosingUpgrade);
    }

    private void UpdateUpgradeChoice(float deltaTime)
    {
        int count = _upgradeChoices.Length;

        // keyboard: 1, 2, 3 choose directly; left / right + Enter
        for (int i = 0; i < count; i++)
        {
            if (KeyboardState.IsKeyPressed(Keys.D1 + i) || KeyboardState.IsKeyPressed(Keys.KeyPad1 + i))
            {
                ChooseUpgrade(i);
                return;
            }
        }

        if (KeyboardState.IsKeyPressed(Keys.Left) || KeyboardState.IsKeyPressed(Keys.A))
            _selectedUpgrade = (_selectedUpgrade + count - 1) % count;
        if (KeyboardState.IsKeyPressed(Keys.Right) || KeyboardState.IsKeyPressed(Keys.D))
            _selectedUpgrade = (_selectedUpgrade + 1) % count;

        // a short delay, so that Enter (sword) or a click held during the fight does not choose by accident
        bool canConfirm = _screenTime > 0.4f;
        if (canConfirm && (KeyboardState.IsKeyPressed(Keys.Enter) || KeyboardState.IsKeyPressed(Keys.KeyPadEnter)))
        {
            ChooseUpgrade(_selectedUpgrade);
            return;
        }

        // mouse: hovering selects a card, clicking chooses it
        UiRect[] cards = _gameUi.GetUpgradeCardRects(count);
        for (int i = 0; i < count; i++)
        {
            if (!cards[i].Contains(MouseState.Position))
                continue;

            if (MouseState.Delta.LengthSquared > 0.0f)
                _selectedUpgrade = i;

            if (canConfirm && MouseState.IsButtonPressed(MouseButton.Left))
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
        Console.WriteLine($"Upgrade chosen: {upgrade.Title}");

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

    // =====================================================
    // RENDER: the 3D scene, then the interface of the current screen
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
        // MANUELA - CRYSTAL HEALTH BAR (during the game only)
        // =================================================

        bool inGame = _screen is GameScreen.Playing or GameScreen.Paused or GameScreen.ChoosingUpgrade;
        if (inGame)
        {
            _crystalHealthBar.Draw(
                _crystal.HealthPercentage
            );
        }

        // =================================================
        // SARAH - INTERFACE
        // =================================================

        _uiRenderer.Begin(ClientSize);

        if (inGame)
            _gameUi.DrawHud(CurrentHudInfo());

        if (_screen == GameScreen.Playing)
            _gameUi.DrawWaveStart(_enemyWaveManager.CurrentWave, _waveTime, _input.Mode == PlayerInput.AimMode.Mouse);

        switch (_screen)
        {
            case GameScreen.MainMenu:
                _gameUi.DrawMainMenu(_screenTime);
                break;
            case GameScreen.Paused:
                _gameUi.DrawPause();
                break;
            case GameScreen.ChoosingUpgrade:
                _gameUi.DrawUpgradeChoice(_enemyWaveManager.CurrentWave, _upgradeChoices, _selectedUpgrade);
                break;
            case GameScreen.Victory:
            case GameScreen.Defeat:
                _gameUi.DrawEndScreen(
                    _screen == GameScreen.Victory,
                    _score,
                    _enemyWaveManager.CurrentWave,
                    _enemyWaveManager.TotalWaveCount,
                    _kills);
                break;
        }

        _uiRenderer.End();
    }

    private HudInfo CurrentHudInfo()
    {
        int alive = 0;
        foreach (Enemy enemy in _enemySpawner.Enemies)
        {
            if (enemy.IsAlive)
                alive++;
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
            _input.Mode == PlayerInput.AimMode.Mouse);
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
        _uiRenderer.Dispose();
        _renderer.Dispose();
    }

    // =====================================================
    // SARAH - HELPERS
    // =====================================================

    private Mesh LoadKnightPart(string fileName) =>
        Keep(new Mesh(ObjLoader.Load(Path.Combine(
            AppContext.BaseDirectory, "Assets", "Knight", fileName))));

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
