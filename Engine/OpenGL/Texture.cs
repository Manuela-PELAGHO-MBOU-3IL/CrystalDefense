
using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace Engine.OpenGL;

public sealed class Texture : IDisposable
{
    private int _handle;

    public int Width { get; }
    public int Height { get; }

    internal int Handle
    {
        get
        {
            if (_handle == 0)
                throw new ObjectDisposedException(nameof(Texture));

            return _handle;
        }
    }

    private Texture(int width, int height, byte[] rgbaPixels)
    {
        Width = width;
        Height = height;

        _handle = GL.GenTexture();

        GL.BindTexture(TextureTarget.Texture2D, _handle);

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba8,
            width,
            height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            rgbaPixels
        );

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.Repeat
        );

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.Repeat
        );

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)TextureMinFilter.LinearMipmapLinear
        );

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)TextureMagFilter.Linear
        );

        GL.GenerateMipmap(
            GenerateMipmapTarget.Texture2D
        );

        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    // Load an image from a file.
    public static Texture FromFile(string path)
    {
        StbImage.stbi_set_flip_vertically_on_load(1);

        using FileStream stream = File.OpenRead(path);

        ImageResult image = ImageResult.FromStream(
            stream,
            ColorComponents.RedGreenBlueAlpha
        );

        return new Texture(
            image.Width,
            image.Height,
            image.Data
        );
    }

    // Load an image embedded inside a GLB file.
    public static Texture FromImageBytes(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            throw new ArgumentException(
                "Image data is empty.",
                nameof(imageBytes)
            );
        }

        StbImage.stbi_set_flip_vertically_on_load(1);

        using var stream = new MemoryStream(imageBytes);

        ImageResult image = ImageResult.FromStream(
            stream,
            ColorComponents.RedGreenBlueAlpha
        );

        return new Texture(
            image.Width,
            image.Height,
            image.Data
        );
    }

    // Create a texture from RGBA pixel data.
    public static Texture FromPixels(
        int width,
        int height,
        byte[] rgbaPixels)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                "Texture dimensions must be positive."
            );
        }

        if (rgbaPixels == null ||
            rgbaPixels.Length != checked(width * height * 4))
        {
            throw new ArgumentException(
                "Invalid RGBA pixel data.",
                nameof(rgbaPixels)
            );
        }

        return new Texture(
            width,
            height,
            rgbaPixels
        );
    }

    // Create a single-color texture.
    public static Texture FromColor(
        byte red,
        byte green,
        byte blue,
        byte alpha = 255)
    {
        return FromPixels(
            1,
            1,
            new byte[]
            {
                red,
                green,
                blue,
                alpha
            }
        );
    }

    public void Dispose()
    {
        if (_handle == 0)
            return;

        GL.DeleteTexture(_handle);
        _handle = 0;
    }
}
