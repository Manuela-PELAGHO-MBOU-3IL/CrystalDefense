using Engine;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefense.Core;

// One node of a scene graph: a part of a model that can have child parts.
// A child moves with its parent: its final (world) matrix = its own (local) matrix × the parent's world matrix.
// Example: the knight's arm is a child of the body, so the arm follows the body when the body moves.
public sealed class SceneNode
{
    private readonly List<SceneNode> _children = new();

    public SceneNode(string name, Mesh? mesh = null, Material? material = null, Vector3 offset = default)
    {
        Name = name;
        Mesh = mesh;
        Material = material;
        Offset = offset;
    }

    public string Name { get; }

    // What is drawn for this node (a node without a mesh only groups its children).
    public Mesh? Mesh { get; }
    public Material? Material { get; }

    // Position of the node's joint in the space of its parent (for example the shoulder on the body).
    public Vector3 Offset { get; set; }

    // Animation: rotation of the node around its own joint.
    public Matrix4 Rotation { get; set; } = Matrix4.Identity;

    public IReadOnlyList<SceneNode> Children => _children;

    public SceneNode AddChild(SceneNode child)
    {
        _children.Add(child);
        return child;
    }

    // First rotate around the joint, then move the joint to its place in the parent (OpenTK: left to right).
    public Matrix4 LocalMatrix => Rotation * Matrix4.CreateTranslation(Offset);

    // Draws this node and all its children, recursively.
    public void Draw(OpenGlRenderer renderer, Camera camera, Matrix4 parentWorldMatrix)
    {
        Matrix4 worldMatrix = LocalMatrix * parentWorldMatrix;

        if (Mesh is not null && Material is not null)
            renderer.Draw(Mesh, Material, worldMatrix, camera);

        foreach (SceneNode child in _children)
            child.Draw(renderer, camera, worldMatrix);
    }
}
