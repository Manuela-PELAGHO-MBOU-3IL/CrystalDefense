using Engine;
using Engine.Geometry;
using Engine.Geometry.Loaders;
using Engine.Lighting;
using Engine.Materials;
using Engine.Materials.AmbientDiffuseSpecular;
using Engine.Materials.NormalMapping;
using Engine.Materials.ReflectionMapping;
using Engine.Materials.UnlitTexture;
using Engine.Materials.WaveDistortion;
using Engine.OpenGL;
using Example.Materials.NormalColor;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace Example;

internal static class Program
{
    private static void Main()
    {
        var windowSettings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(960, 720),
            Title = "OpenTK — normal mapping",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core
        };

        using var window = new CubeWindow(GameWindowSettings.Default, windowSettings);
        window.Run();
    }
}

internal sealed class CubeWindow : Application
{
    private OpenGlRenderer? _renderer;
    private Camera? _camera;

    // the object in the middle (switchable in Initialize)
    private Mesh? _mesh;
    private Texture? _texture;
    private Texture? _normalMap;
    private Material? _material;
    private Matrix4 _model = Matrix4.Identity;

    // the hall around it, drawn with normal mapping
    private Mesh? _hallMesh;
    private Texture? _hallColorTexture;
    private Texture? _hallNormalMap;
    private NormalMappingMaterial? _hallMaterial;
    private readonly Matrix4 _hallModel = Matrix4.CreateScale(10.0f);

    private float _elapsedTime;

    public CubeWindow(GameWindowSettings gameSettings, NativeWindowSettings windowSettings)
        : base(gameSettings, windowSettings) { }

    protected override void Initialize()
    {
        VSync = VSyncMode.On;
        _renderer = new OpenGlRenderer();
        _camera = new Camera(new Vector3(0, 0, 5), Vector3.Zero);

        // The light of the scene, used by materials with lighting (AmbientDiffuseSpecular, NormalMapping).
        _renderer.DirectionalLight = new DirectionalLight
        {
            Direction = new Vector3(-1.0f, 1.7f, 1.0f),
            AmbientColor = new Vector3(0.3f, 0.3f, 0.3f),
            DiffuseColor = new Vector3(1.0f, 1.0f, 1.0f),
            SpecularColor = new Vector3(1.0f, 1.0f, 1.0f)
        };

        // To switch, comment out the active line and uncomment another one.
        _texture = Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "blur_colors.png"));
        // _texture = Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "duck_texture.png"));
        // _texture = Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "wood_box.png"));
        // _texture = Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "star.png"));
        // _texture = Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "environment.png"));

        // only used by the NormalMappingMaterial
        _normalMap = Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "pattern_normal.png"));

        // _material = new UnlitTextureMaterial(_texture);
        // _material = new WaveDistortionMaterial(_texture);
        // _material = new ReflectionMappingMaterial(_texture);
        // _material = new AmbientDiffuseSpecularMaterial(_texture) { Shininess = 64.0f };
        _material = new NormalMappingMaterial(_texture, _normalMap) { Shininess = 40.0f };
        // _material = new NormalColorMaterial();   // own material in this project, see Materials/NormalColor

        _mesh = new Mesh(Primitives.CreateCube());
        // _mesh = new Mesh(ObjLoader.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "star.obj")));
        // _mesh = new Mesh(ObjLoader.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "duck_flat.obj")));
        // _mesh = new Mesh(ObjLoader.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "duck_smooth.obj")));

        // the hall: color texture and normal map
        _hallMesh = new Mesh(ObjLoader.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "hall.obj")));
        _hallColorTexture = Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "hall_color.png"));
        _hallNormalMap = Texture.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "hall_normal.png"));
        _hallMaterial = new NormalMappingMaterial(_hallColorTexture, _hallNormalMap) { Shininess = 90.0f };
    }

    protected override void FramebufferResized(int width, int height) => _renderer!.Resize(width, height);

    // The animation only changes the state; drawing happens exclusively in Render.
    protected override void Update(float deltaTime)
    {
        _elapsedTime += deltaTime;

        // the object in the middle rotates
        _model = Matrix4.CreateRotationX(_elapsedTime * 0.3f)
               * Matrix4.CreateRotationY(_elapsedTime * 0.7f)
               * Matrix4.CreateRotationZ(_elapsedTime * 0.3f);
        if (_material is WaveDistortionMaterial wave)
            wave.Wobble = _elapsedTime * 5;

        // the camera circles around the middle and moves closer and further away
        float angle = -_elapsedTime * 0.5f;
        float distance = MathF.Sin(_elapsedTime * 0.5f) * 2.0f + 5.0f;
        _camera!.LookAt(new Vector3(MathF.Sin(angle) * distance, 0.0f, MathF.Cos(angle) * distance), Vector3.Zero);
    }

    protected override void Render()
    {
        _renderer!.Clear();
        _renderer.Draw(_hallMesh!, _hallMaterial!, _hallModel, _camera!);
        _renderer.Draw(_mesh!, _material!, _model, _camera!);
    }

    protected override void Shutdown()
    {
        _mesh?.Dispose();
        _texture?.Dispose();
        _normalMap?.Dispose();
        _hallMesh?.Dispose();
        _hallColorTexture?.Dispose();
        _hallNormalMap?.Dispose();
        _renderer?.Dispose();
    }
}
