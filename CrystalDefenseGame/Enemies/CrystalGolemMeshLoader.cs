
using System;
using System.Collections.Generic;
using Engine.Geometry;
using OpenTK.Mathematics;
using SharpGLTF.Schema2;

namespace CrystalDefenseGame.Enemies;

public static class CrystalGolemMeshLoader
{
    public sealed record GolemPart(
        MeshData Geometry,
        int MaterialIndex,
        Matrix4 Transform,
        int NodeIndex
    );

    public static List<GolemPart> LoadParts(string path)
    {
        ModelRoot model = ModelRoot.Load(path);
        var parts = new List<GolemPart>();

        foreach (var node in model.LogicalNodes)
        {
            if (node.Mesh == null)
                continue;

            foreach (var primitive in node.Mesh.Primitives)
            {
                var positions = primitive
                    .GetVertexAccessor("POSITION")
                    .AsVector3Array();

                var normals = primitive
                    .GetVertexAccessor("NORMAL")
                    ?.AsVector3Array();

                var texCoords = primitive
                    .GetVertexAccessor("TEXCOORD_0")
                    ?.AsVector2Array();

                var vertices = new Vertex[positions.Count];

                for (int i = 0; i < positions.Count; i++)
                {
                    var p = positions[i];

                    Vector3 normal = Vector3.UnitY;

                    if (normals != null)
                    {
                        var n = normals[i];
                        normal = new Vector3(n.X, n.Y, n.Z);
                    }

                    Vector2 uv = Vector2.Zero;

                    if (texCoords != null)
                    {
                        var t = texCoords[i];
                        uv = new Vector2(t.X, t.Y);
                    }

                    vertices[i] = new Vertex(
                        new Vector3(p.X, p.Y, p.Z),
                        normal,
                        uv
                    );
                }

                var indices = new List<uint>();

                foreach (int index in primitive.GetIndices())
                {
                    indices.Add((uint)index);
                }

                if (indices.Count == 0)
                {
                    for (uint i = 0; i < vertices.Length; i++)
                        indices.Add(i);
                }

                if (indices.Count == 0 ||
                    indices.Count % 3 != 0)
                    continue;

                var transform = node.WorldMatrix;

                Matrix4 matrix = new Matrix4(
                    transform.M11, transform.M12,
                    transform.M13, transform.M14,
                    transform.M21, transform.M22,
                    transform.M23, transform.M24,
                    transform.M31, transform.M32,
                    transform.M33, transform.M34,
                    transform.M41, transform.M42,
                    transform.M43, transform.M44
                );

                int materialIndex =
                    primitive.Material?.LogicalIndex ?? -1;

                parts.Add(new GolemPart(
                    new MeshData(
                        vertices,
                        indices.ToArray()
                    ),
                    materialIndex,
                    matrix,
                    node.LogicalIndex
                ));
            }
        }

        Console.WriteLine(
            $"Loaded {parts.Count} positioned GLB parts."
        );

        Console.WriteLine(
            "Golem node indices loaded successfully."
        );

        return parts;
    }
}
