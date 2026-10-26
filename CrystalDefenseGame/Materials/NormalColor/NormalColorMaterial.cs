using Engine.Materials;
using Engine.OpenGL;

namespace Example.Materials.NormalColor;

// Example of a material that lives in the application project instead of the engine:
// it shows the world space normal as a color (x = red, y = green, z = blue), which is
// useful to check the normals of a model.
//
// To write your own material in your own project:
// - derive from Engine.Materials.Material and override ShaderSource and ApplyUniforms
// - put the .glsl files next to the class and embed them (see EmbeddedResource in Example.csproj)
public sealed class NormalColorMaterial : Material
{
    private static readonly ShaderSource Source =
        ShaderSource.FromEmbeddedResources(typeof(NormalColorMaterial), "normal_color.vert.glsl", "normal_color.frag.glsl");

    protected override ShaderSource ShaderSource => Source;

    // This material has no values of its own; uModel, uView and uProjection are set by the renderer.
    protected override void ApplyUniforms(ShaderProgram shader) { }
}
