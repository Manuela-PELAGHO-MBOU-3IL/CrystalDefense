
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace CrystalDefense.Game.UI;

/// <summary>
/// Draws the crystal's health bar and label in screen space using OpenGL.
/// </summary>
public sealed class CrystalHealthBar : IDisposable
{
    private int _shaderProgram;
    private int _vao;
    private int _vbo;
    private int _colorLocation;

    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['C'] = new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" },
        ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        ['Y'] = new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" },
        ['S'] = new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" },
        ['T'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" },
        ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['L'] = new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" },
        ['H'] = new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['P'] = new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" },
        ['0'] = new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
        ['1'] = new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
        ['2'] = new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
        ['3'] = new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
        ['4'] = new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
        ['5'] = new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" },
        ['6'] = new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" },
        ['7'] = new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
        ['8'] = new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
        ['9'] = new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" },
        ['%'] = new[] { "11001", "11010", "00100", "01000", "10110", "00110", "00000" }
    };

    public CrystalHealthBar()
    {
        const string vertexShaderSource = """
            #version 330 core
            layout (location = 0) in vec2 aPosition;

            void main()
            {
                gl_Position = vec4(aPosition, 0.0, 1.0);
            }
            """;

        const string fragmentShaderSource = """
            #version 330 core
            uniform vec4 uColor;
            out vec4 FragColor;

            void main()
            {
                FragColor = uColor;
            }
            """;

        int vertexShader = CompileShader(
            ShaderType.VertexShader, vertexShaderSource);

        int fragmentShader = 0;

        try
        {
            fragmentShader = CompileShader(
                ShaderType.FragmentShader, fragmentShaderSource);

            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vertexShader);
            GL.AttachShader(_shaderProgram, fragmentShader);
            GL.LinkProgram(_shaderProgram);

            GL.GetProgram(
                _shaderProgram,
                GetProgramParameterName.LinkStatus,
                out int success);

            if (success == 0)
                throw new InvalidOperationException(
                    $"Health bar shader link error: {GL.GetProgramInfoLog(_shaderProgram)}");

            _colorLocation = GL.GetUniformLocation(
                _shaderProgram, "uColor");

            CreateRectangleBuffers();
        }
        catch
        {
            if (_shaderProgram != 0)
            {
                GL.DeleteProgram(_shaderProgram);
                _shaderProgram = 0;
            }

            throw;
        }
        finally
        {
            GL.DeleteShader(vertexShader);

            if (fragmentShader != 0)
                GL.DeleteShader(fragmentShader);
        }
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(
            shader,
            ShaderParameter.CompileStatus,
            out int success);

        if (success != 0)
            return shader;

        string error = GL.GetShaderInfoLog(shader);
        GL.DeleteShader(shader);

        throw new InvalidOperationException(
            $"Health bar shader compilation error ({type}): {error}");
    }

    private void CreateRectangleBuffers()
    {
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

        GL.BufferData(
            BufferTarget.ArrayBuffer,
            12 * sizeof(float),
            IntPtr.Zero,
            BufferUsageHint.DynamicDraw);

        GL.VertexAttribPointer(
            0, 2, VertexAttribPointerType.Float,
            false, 2 * sizeof(float), 0);

        GL.EnableVertexAttribArray(0);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    public void Draw(float healthPercentage)
    {
        healthPercentage = Math.Clamp(healthPercentage, 0f, 1f);

        const float barX = -0.92f;
        const float barY = 0.84f;
        const float barWidth = 0.55f;
        const float barHeight = 0.035f;
        const float crystalSize = 0.045f;

        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);

        GL.UseProgram(_shaderProgram);
        GL.BindVertexArray(_vao);

        Vector4 purple = new(0.72f, 0.12f, 1.0f, 1.0f);
        Vector4 lightPurple = new(0.88f, 0.65f, 1.0f, 1.0f);

        // Label above the health bar.
        DrawText("CRYSTAL HP", barX, 0.91f, 0.004f, lightPurple);

        // Health percentage next to the crystal icon.
        string percentage = $"{MathF.Round(healthPercentage * 100):0}%";
        DrawText(
            percentage,
            barX + barWidth + 0.09f,
            barY + 0.012f,
            0.0035f,
            lightPurple);

        // Health bar background.
        DrawRectangle(
            barX, barY, barWidth, barHeight,
            new Vector4(0.16f, 0.04f, 0.22f, 1.0f));

        // Remaining health in crystal purple.
        if (healthPercentage > 0)
        {
            DrawRectangle(
                barX, barY,
                barWidth * healthPercentage, barHeight,
                new Vector4(0.68f, 0.05f, 1.0f, 1.0f));
        }

        // Small crystal marker.
        DrawCrystalIcon(
            barX + barWidth + 0.035f,
            barY + barHeight / 2f,
            crystalSize,
            purple);

        GL.BindVertexArray(0);
        GL.UseProgram(0);

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);
    }

    private void DrawText(
        string text,
        float x,
        float y,
        float pixelSize,
        Vector4 color)
    {
        float cursorX = x;

        foreach (char character in text)
        {
            if (character == ' ')
            {
                cursorX += pixelSize * 6;
                continue;
            }

            if (!Font.TryGetValue(character, out string[]? rows))
                continue;

            for (int row = 0; row < 7; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    if (rows[row][column] != '1')
                        continue;

                    DrawRectangle(
                        cursorX + column * pixelSize,
                        y - row * pixelSize,
                        pixelSize,
                        pixelSize,
                        color);
                }
            }

            cursorX += pixelSize * 6;
        }
    }

    private void DrawCrystalIcon(
        float centerX,
        float centerY,
        float size,
        Vector4 color)
    {
        float[] vertices =
        {
            centerX, centerY + size,
            centerX + size * 0.65f, centerY,
            centerX, centerY,

            centerX, centerY + size,
            centerX, centerY,
            centerX - size * 0.65f, centerY,

            centerX, centerY,
            centerX + size * 0.65f, centerY,
            centerX, centerY - size,

            centerX, centerY,
            centerX, centerY - size,
            centerX - size * 0.65f, centerY
        };

        GL.Uniform4(
            _colorLocation,
            color.X, color.Y, color.Z, color.W);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(
            BufferTarget.ArrayBuffer,
            vertices.Length * sizeof(float),
            vertices,
            BufferUsageHint.DynamicDraw);

        GL.DrawArrays(PrimitiveType.Triangles, 0, 12);
    }

    private void DrawRectangle(
        float x,
        float y,
        float width,
        float height,
        Vector4 color)
    {
        float[] vertices =
        {
            x, y,
            x + width, y,
            x + width, y + height,

            x, y,
            x + width, y + height,
            x, y + height
        };

        GL.Uniform4(
            _colorLocation,
            color.X, color.Y, color.Z, color.W);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

        GL.BufferData(
            BufferTarget.ArrayBuffer,
            vertices.Length * sizeof(float),
            vertices,
            BufferUsageHint.DynamicDraw);

        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    public void Dispose()
    {
        if (_vbo != 0)
            GL.DeleteBuffer(_vbo);

        if (_vao != 0)
            GL.DeleteVertexArray(_vao);

        if (_shaderProgram != 0)
            GL.DeleteProgram(_shaderProgram);

        _vbo = 0;
        _vao = 0;
        _shaderProgram = 0;
    }
}
