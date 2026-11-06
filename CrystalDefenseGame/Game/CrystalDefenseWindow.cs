
using System;
using System.Collections.Generic;
using System.IO;

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
    // MANUELA - ARENA
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

        // Lighting - Blinn-Phong
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

        // Load static arena objects.
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

        var crystalMaterial = new AmbientDiffuseSpecularMaterial(
            Keep(Texture.FromFile(crystalTexturePath))
        )
        {
            Shininess = 562.5f
        };

        // Collision zone of the crystal and pedestal.
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

        var playerMesh = Keep(
            new Mesh(Primitives.CreateCube(1.0f))
        );

        var playerMaterial = new AmbientDiffuseSpecularMaterial(
            Keep(LoadTexture("player.png"))
        )
        {
            Shininess = 32.0f
        };

        _player = new Player(
            playerMesh,
            playerMaterial,
            new Vector3(0.0f, 0.5f, 5.0f),
            radius: 0.5f
        );

        // =================================================
        // SARAH - PROJECTILES
        // =================================================

        var projectileMesh = Keep(
            new Mesh(Primitives.CreateCube(0.3f))
        );

        var projectileMaterial = new UnlitTextureMaterial(
            Keep(LoadTexture("projectile.png"))
        );

        _projectiles = new ProjectileManager(
            projectileMesh,
            projectileMaterial
        );

        Console.WriteLine("Crystal Defense initialized.");

        Console.WriteLine(
            $"Crystal health: {_crystal.Health}/{CrystalEntity.MaxHealth}"
        );
    }

    // =====================================================
    // LOAD ONE STATIC ARENA OBJECT
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

        // Sarah - Player.
        _player.Update(
            KeyboardState,
            deltaTime,
            _arenaBounds,
            _crystal.Position,
            _crystal.Radius
        );

        // Sarah - Projectiles.
        _projectiles.Update(
            KeyboardState,
            deltaTime,
            _player,
            _arenaBounds
        );

        // Ons - Enemies and collisions.
        // Enemy damage will be connected here.

        // Manuela - Game state.
        // Waiting for Ons's wave system.
        _gameState.Update(
            _crystal,
            allWavesCompleted: false
        );
    }

    // =====================================================
    // RENDER
    // =====================================================

    protected override void Render()
    {
        _renderer.Clear();

        // Manuela - Static Blender arena objects.
        foreach (var arenaObject in _arenaObjects)
        {
            _renderer.Draw(
                arenaObject.Mesh,
                arenaObject.Material,
                arenaObject.Model,
                _camera
            );
        }

        // Manuela - Crystal.
        _crystal.Draw(
            _renderer,
            _camera
        );

        // Sarah - Player.
        _player.Draw(
            _renderer,
            _camera
        );

        // Sarah - Projectiles.
        _projectiles.Draw(
            _renderer,
            _camera
        );

        // Manuela - Crystal health bar (2D HUD).
        _crystalHealthBar.Draw(_crystal.HealthPercentage);
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

    // =====================================================
    // RESOURCE MANAGEMENT
    // =====================================================

    private T Keep<T>(T resource) where T : IDisposable
    {
        _resources.Add(resource);
        return resource;
    }
}
