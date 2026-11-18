using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CrystalDefense.Game.UI;

// Draws the 2D interface (rectangles and pixel text) on top of the 3D scene.
// Positions are in screen pixels, (0, 0) is the top-left corner of the window.
// Everything of one frame is collected between Begin() and End() and drawn with a single draw call.
public sealed class UiRenderer : IDisposable
{
    private const int FloatsPerVertex = 6;   // x, y, r, g, b, a

    private readonly List<float> _vertices = new();
    private readonly int _shaderProgram;
    private readonly int _vao;
    private readonly int _vbo;
    private Vector2 _screenSize = Vector2.One;

    public UiRenderer()
    {
        const string vertexSource = """
            #version 330 core
            layout (location = 0) in vec2 aPosition;
            layout (location = 1) in vec4 aColor;
            out vec4 vColor;
            void main()
            {
                vColor = aColor;
                gl_Position = vec4(aPosition, 0.0, 1.0);
            }
            """;
        const string fragmentSource = """
            #version 330 core
            in vec4 vColor;
            out vec4 FragColor;
            void main()
            {
                FragColor = vColor;
            }
            """;

        int vertexShader = Compile(ShaderType.VertexShader, vertexSource);
        int fragmentShader = Compile(ShaderType.FragmentShader, fragmentSource);
        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vertexShader);
        GL.AttachShader(_shaderProgram, fragmentShader);
        GL.LinkProgram(_shaderProgram);
        GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
            throw new InvalidOperationException("UI shader link error: " + GL.GetProgramInfoLog(_shaderProgram));
        GL.DetachShader(_shaderProgram, vertexShader);
        GL.DetachShader(_shaderProgram, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        int stride = FloatsPerVertex * sizeof(float);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    public Vector2 ScreenSize => _screenSize;

    public void Begin(Vector2i screenSize)
    {
        _screenSize = new Vector2(Math.Max(1, screenSize.X), Math.Max(1, screenSize.Y));
        _vertices.Clear();
    }

    public void Rect(float x, float y, float width, float height, Vector4 color)
    {
        AddVertex(x, y, color);
        AddVertex(x + width, y, color);
        AddVertex(x + width, y + height, color);
        AddVertex(x, y, color);
        AddVertex(x + width, y + height, color);
        AddVertex(x, y + height, color);
    }

    // A rectangle outline of the given thickness.
    public void Frame(float x, float y, float width, float height, float thickness, Vector4 color)
    {
        Rect(x, y, width, thickness, color);
        Rect(x, y + height - thickness, width, thickness, color);
        Rect(x, y, thickness, height, color);
        Rect(x + width - thickness, y, thickness, height, color);
    }

    // Draws text with its top-left corner at (x, y). pixelSize = size of one font pixel on screen.
    public void Text(string text, float x, float y, float pixelSize, Vector4 color)
    {
        string normalized = BitmapFont.Normalize(text);
        float cursor = x;
        foreach (char c in normalized)
        {
            if (BitmapFont.TryGetGlyph(c, out string[] rows))
            {
                for (int row = 0; row < BitmapFont.GlyphHeight; row++)
                    for (int column = 0; column < BitmapFont.GlyphWidth; column++)
                        if (rows[row][column] == '1')
                            Rect(cursor + column * pixelSize, y + row * pixelSize, pixelSize, pixelSize, color);
            }
            cursor += BitmapFont.Advance * pixelSize;
        }
    }

    // Text with a dark shadow, easier to read on top of the 3D scene.
    public void TextWithShadow(string text, float x, float y, float pixelSize, Vector4 color)
    {
        Text(text, x + pixelSize * 0.6f, y + pixelSize * 0.6f, pixelSize, new Vector4(0, 0, 0, 0.75f * color.W));
        Text(text, x, y, pixelSize, color);
    }

    public void TextCentered(string text, float centerX, float y, float pixelSize, Vector4 color) =>
        TextWithShadow(text, centerX - Measure(text, pixelSize) / 2, y, pixelSize, color);

    public float Measure(string text, float pixelSize) => BitmapFont.Measure(BitmapFont.Normalize(text), pixelSize);

    public void End()
    {
        if (_vertices.Count == 0)
            return;

        // The interface is drawn above everything, with transparency.
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        GL.UseProgram(_shaderProgram);
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        float[] data = _vertices.ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StreamDraw);
        GL.DrawArrays(PrimitiveType.Triangles, 0, data.Length / FloatsPerVertex);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
        GL.UseProgram(0);

        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.CullFace);
        GL.Enable(EnableCap.DepthTest);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shaderProgram);
    }

    // Screen pixels → normalized device coordinates (-1 … 1, y upwards).
    private void AddVertex(float x, float y, Vector4 color)
    {
        _vertices.Add(x / _screenSize.X * 2.0f - 1.0f);
        _vertices.Add(1.0f - y / _screenSize.Y * 2.0f);
        _vertices.Add(color.X);
        _vertices.Add(color.Y);
        _vertices.Add(color.Z);
        _vertices.Add(color.W);
    }

    private static int Compile(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException($"UI {type} compile error: " + GL.GetShaderInfoLog(shader));
        return shader;
    }
}
