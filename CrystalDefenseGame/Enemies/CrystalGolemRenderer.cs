
using System;
using System.Collections.Generic;
using System.IO;

using Engine.OpenGL;
using OpenTK.Mathematics;

using SharpGLTF.Schema2;
using SharpGLTF.Animations;

// ==========================================
// ALIASES TO AVOID AMBIGUOUS REFERENCES
// ==========================================

using GameMesh = Engine.OpenGL.Mesh;
using GameMaterial = Engine.Materials.Material;
using GameCamera = Engine.Camera;

using NumericsMatrix = System.Numerics.Matrix4x4;
using NumericsVector3 = System.Numerics.Vector3;
using NumericsQuaternion = System.Numerics.Quaternion;

namespace CrystalDefenseGame.Enemies;

public sealed class CrystalGolemRenderer : IDisposable
{
    // ==========================================
    // RENDER PART
    // ==========================================

    private sealed record RenderPart(
        GameMesh Mesh,
        int NodeIndex,
        int MaterialIndex
    );

    // ==========================================
    // ANIMATED NODE
    // ==========================================

    private sealed class AnimatedNode
    {
        public NumericsMatrix BaseMatrix;

        public int ParentIndex = -1;

        public ICurveSampler<NumericsVector3>? Translation;

        public ICurveSampler<NumericsQuaternion>? Rotation;

        public ICurveSampler<NumericsVector3>? Scale;
    }

    // ==========================================
    // FIELDS
    // ==========================================

    private readonly List<RenderPart> _parts = new();

    private readonly AnimatedNode[] _nodes;

    private readonly float _animationDuration;

    // ==========================================
    // CONSTRUCTOR
    // ==========================================

    public CrystalGolemRenderer(
        IReadOnlyList<CrystalGolemMeshLoader.GolemPart> parts)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Models",
            "CrystalGolem.glb"
        );

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Crystal Golem GLB not found.",
                path
            );
        }

        ModelRoot model = ModelRoot.Load(path);

        // ======================================
        // CREATE NODES
        // ======================================

        _nodes = new AnimatedNode[
            model.LogicalNodes.Count
        ];

        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i] = new AnimatedNode
            {
                BaseMatrix =
                    model.LogicalNodes[i].LocalMatrix
            };
        }

        // ======================================
        // BUILD NODE HIERARCHY
        // ======================================

        foreach (var node in model.LogicalNodes)
        {
            foreach (var child in node.VisualChildren)
            {
                _nodes[
                    child.LogicalIndex
                ].ParentIndex = node.LogicalIndex;
            }
        }

        // ======================================
        // LOAD ANIMATIONS
        // ======================================

        float duration = 0.0f;

        foreach (var animation in model.LogicalAnimations)
        {
            duration = MathF.Max(
                duration,
                animation.Duration
            );

            foreach (var channel in animation.Channels)
            {
                if (channel.TargetNode == null)
                    continue;

                int index =
                    channel.TargetNode.LogicalIndex;

                AnimatedNode target = _nodes[index];

                switch (channel.TargetNodePath)
                {
                    case PropertyPath.translation:

                        target.Translation =
                            channel.GetTranslationSampler()?
                                .CreateCurveSampler(true);

                        break;

                    case PropertyPath.rotation:

                        target.Rotation =
                            channel.GetRotationSampler()?
                                .CreateCurveSampler(true);

                        break;

                    case PropertyPath.scale:

                        target.Scale =
                            channel.GetScaleSampler()?
                                .CreateCurveSampler(true);

                        break;
                }
            }
        }

        _animationDuration = duration;

        // ======================================
        // CREATE RENDER MESHES
        // ======================================

        foreach (var part in parts)
        {
            _parts.Add(
                new RenderPart(
                    new GameMesh(part.Geometry),
                    part.NodeIndex,
                    part.MaterialIndex
                )
            );
        }

        Console.WriteLine(
            $"Golem renderer initialized: {_parts.Count} parts."
        );

        Console.WriteLine(
            $"Animations loaded: {model.LogicalAnimations.Count}"
        );

        Console.WriteLine(
            $"Animation duration: {_animationDuration:F2}s"
        );
    }

    // ==========================================
    // MATRIX CONVERSION
    // ==========================================

    private static Matrix4 ConvertMatrix(
        NumericsMatrix matrix)
    {
        return new Matrix4(
            matrix.M11,
            matrix.M12,
            matrix.M13,
            matrix.M14,

            matrix.M21,
            matrix.M22,
            matrix.M23,
            matrix.M24,

            matrix.M31,
            matrix.M32,
            matrix.M33,
            matrix.M34,

            matrix.M41,
            matrix.M42,
            matrix.M43,
            matrix.M44
        );
    }

    // ==========================================
    // LOCAL ANIMATED TRANSFORM
    // ==========================================

    private NumericsMatrix GetLocalMatrix(
        AnimatedNode node,
        float time,
        bool walking)
    {
        if (!walking)
        {
            return node.BaseMatrix;
        }

        bool success = NumericsMatrix.Decompose(
            node.BaseMatrix,
            out NumericsVector3 scale,
            out NumericsQuaternion rotation,
            out NumericsVector3 translation
        );

        if (!success)
        {
            return node.BaseMatrix;
        }

        if (node.Translation != null)
        {
            translation =
                node.Translation.GetPoint(time);
        }

        if (node.Rotation != null)
        {
            rotation =
                node.Rotation.GetPoint(time);
        }

        if (node.Scale != null)
        {
            scale =
                node.Scale.GetPoint(time);
        }

        NumericsMatrix scaleMatrix =
            NumericsMatrix.CreateScale(scale);

        NumericsMatrix rotationMatrix =
            NumericsMatrix.CreateFromQuaternion(
                rotation
            );

        NumericsMatrix translationMatrix =
            NumericsMatrix.CreateTranslation(
                translation
            );

        return
            scaleMatrix *
            rotationMatrix *
            translationMatrix;
    }

    // ==========================================
    // WORLD TRANSFORM
    // ==========================================

    private NumericsMatrix GetWorldMatrix(
        int nodeIndex,
        float time,
        bool walking,
        NumericsMatrix[] cache,
        bool[] calculated)
    {
        if (calculated[nodeIndex])
        {
            return cache[nodeIndex];
        }

        AnimatedNode node =
            _nodes[nodeIndex];

        NumericsMatrix localMatrix =
            GetLocalMatrix(
                node,
                time,
                walking
            );

        if (node.ParentIndex >= 0)
        {
            NumericsMatrix parentMatrix =
                GetWorldMatrix(
                    node.ParentIndex,
                    time,
                    walking,
                    cache,
                    calculated
                );

            localMatrix *= parentMatrix;
        }

        cache[nodeIndex] = localMatrix;

        calculated[nodeIndex] = true;

        return localMatrix;
    }

    // ==========================================
    // DRAW GOLEM
    // ==========================================

    public void Draw(
        OpenGlRenderer renderer,
        GameCamera camera,
        IReadOnlyDictionary<int, GameMaterial> materials,
        GameMaterial fallbackMaterial,
        Matrix4 modelMatrix,
        float animationTime,
        bool walking)
    {
        float time = 0.0f;

        if (_animationDuration > 0.0f)
        {
            time =
                animationTime % _animationDuration;
        }

        NumericsMatrix[] cache =
            new NumericsMatrix[_nodes.Length];

        bool[] calculated =
            new bool[_nodes.Length];

        foreach (RenderPart part in _parts)
        {
            GameMaterial material;

            if (
                materials.TryGetValue(
                    part.MaterialIndex,
                    out GameMaterial? found
                )
                && found != null
            )
            {
                material = found;
            }
            else
            {
                material = fallbackMaterial;
            }

            NumericsMatrix worldMatrix =
                GetWorldMatrix(
                    part.NodeIndex,
                    time,
                    walking,
                    cache,
                    calculated
                );

            Matrix4 finalMatrix =
                ConvertMatrix(worldMatrix) *
                modelMatrix;

            renderer.Draw(
                part.Mesh,
                material,
                finalMatrix,
                camera
            );
        }
    }

    // ==========================================
    // DISPOSE
    // ==========================================

    public void Dispose()
    {
        foreach (RenderPart part in _parts)
        {
            part.Mesh.Dispose();
        }

        _parts.Clear();
    }
}
