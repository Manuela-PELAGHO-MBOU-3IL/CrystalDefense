using CrystalDefense.Core;
using CrystalDefense.PlayerAndProjectiles;
using Engine;
using Engine.Geometry;
using Engine.Lighting;
using Engine.Materials.AmbientDiffuseSpecular;
using Engine.Materials.UnlitTexture;
using Engine.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace CrystalDefense.Game;

// The main window of the game. It only creates the parts of the game and calls them in the right order;
// the logic itself lives in the classes of each part (player, projectiles, enemies, arena).
// SHARED FILE: everybody adds the calls of her own part here.
internal sealed class CrystalDefenseWindow : Application
{
    // created in Initialize (null! tells the compiler: "it will not be null when it is used")
    private OpenGlRenderer _renderer = null!;
    private Camera _camera = null!;
    private readonly List<IDisposable> _resources = new();   // meshes and textures, released in Shutdown

    // --- TEMPORARY until the arena (Manuela) is merged: a flat floor of 20 × 20 units ---
    private ArenaBounds _arenaBounds = null!;
    private Mesh _floorMesh = null!;
    private AmbientDiffuseSpecularMaterial _floorMaterial = null!;
    private readonly Matrix4 _floorModel = Matrix4.CreateScale(10.0f, 0.1f, 10.0f) * Matrix4.CreateTranslation(0.0f, -0.1f, 0.0f);

    // --- Player and projectiles (Sarah) ---
    private Player _player = null!;
    private ProjectileManager _projectiles = null!;

    public CrystalDefenseWindow(GameWindowSettings gameSettings, NativeWindowSettings windowSettings)
        : base(gameSettings, windowSettings) { }

    protected override void Initialize()
    {
        VSync = VSyncMode.On;
        _renderer = new OpenGlRenderer();

        // Camera above the arena, slightly behind it, looking at the center.
        _camera = new Camera(new Vector3(0.0f, 18.0f, 16.0f), new Vector3(0.0f, 0.0f, 1.0f));

        _renderer.DirectionalLight = new DirectionalLight
        {
            Direction = new Vector3(-1.0f, 1.7f, 1.0f),
            AmbientColor = new Vector3(0.3f, 0.3f, 0.3f),
            DiffuseColor = new Vector3(1.0f, 1.0f, 1.0f),
            SpecularColor = new Vector3(1.0f, 1.0f, 1.0f)
        };

        // --- TEMPORARY floor ---
        _arenaBounds = new ArenaBounds(-10.0f, 10.0f, -10.0f, 10.0f);
        _floorMesh = Keep(new Mesh(Primitives.CreateCube()));
        _floorMaterial = new AmbientDiffuseSpecularMaterial(Keep(LoadTexture("floor_placeholder.png"))) { Shininess = 8.0f };

        // --- Player: a blue cube of size 1 for now (later a model from Blender) ---
        var playerMesh = Keep(new Mesh(Primitives.CreateCube(1.0f)));
        var playerMaterial = new AmbientDiffuseSpecularMaterial(Keep(LoadTexture("player.png"))) { Shininess = 32.0f };
        _player = new Player(playerMesh, playerMaterial, new Vector3(0.0f, 0.5f, 5.0f), radius: 0.5f);

        // --- Projectiles: small yellow cubes without lighting, so that they look bright ---
        var projectileMesh = Keep(new Mesh(Primitives.CreateCube(0.3f)));
        var projectileMaterial = new UnlitTextureMaterial(Keep(LoadTexture("projectile.png")));
        _projectiles = new ProjectileManager(projectileMesh, projectileMaterial);
    }

    protected override void FramebufferResized(int width, int height) => _renderer.Resize(width, height);

    // Game logic, once per frame. The order matters: first move, then check what touches what.
    protected override void Update(float deltaTime)
    {
        if (KeyboardState.IsKeyPressed(Keys.Escape)) Close();

        _player.Update(KeyboardState, deltaTime, _arenaBounds);                  // Sarah
        _projectiles.Update(KeyboardState, deltaTime, _player, _arenaBounds);    // Sarah
        // _enemies.Update(deltaTime, _crystal);                                 // Ons
        // _collisions.Check(_projectiles.Projectiles, _enemies, _crystal);      // Ons
        // _gameState.Update(_crystal, _enemies);                                // Manuela
    }

    // Drawing only, no changes of the game state.
    protected override void Render()
    {
        _renderer.Clear();
        _renderer.Draw(_floorMesh, _floorMaterial, _floorModel, _camera);   // TEMPORARY floor
        _player.Draw(_renderer, _camera);
        _projectiles.Draw(_renderer, _camera);
    }

    protected override void Shutdown()
    {
        foreach (IDisposable resource in _resources) resource.Dispose();
        _renderer.Dispose();
    }

    private static Texture LoadTexture(string fileName) =>
        Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", fileName));

    // Remembers a GPU resource so that Shutdown releases it, and returns it unchanged.
    private T Keep<T>(T resource) where T : IDisposable
    {
        _resources.Add(resource);
        return resource;
    }
}
