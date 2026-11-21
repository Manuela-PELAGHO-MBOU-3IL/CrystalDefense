
using System;
using System.Collections.Generic;
using System.Numerics;

using Engine.Materials.AmbientDiffuseSpecular;
using SharpGLTF.Schema2;

using Material = Engine.Materials.Material;
using Texture = Engine.OpenGL.Texture;

namespace CrystalDefenseGame.Enemies;

public static class CrystalGolemMaterialLoader
{
    public static Dictionary<int, Material> LoadMaterials(
        ModelRoot model,
        Func<Texture, Texture> keepTexture)
    {
        var materials = new Dictionary<int, Material>();

        Console.WriteLine(
            "========== GOLEM MATERIALS =========="
        );

        foreach (var gltfMaterial in model.LogicalMaterials)
        {
            var channel = gltfMaterial.FindChannel("BaseColor");

            Vector4 baseColor =
                channel?.Color ?? Vector4.One;

            bool hasTexture =
                channel.HasValue &&
                channel.Value.Texture != null;

            // ======================================
            // FIX MISSING BLENDER COLORS
            // ======================================

            // Some procedural Blender materials
            // are exported as white without textures.
            //
            // Use approximate dark stone colors
            // for these specific materials.
            //
            // Purple crystal materials remain unchanged.

            if (!hasTexture)
            {
                baseColor = gltfMaterial.Name switch
                {
                    // Main stone body
                    "Balanced_Basalt" =>
                        new Vector4(
                            0.22f,
                            0.19f,
                            0.26f,
                            1.0f
                        ),

                    // Stone edges
                    "Balanced_Stone_Edges" =>
                        new Vector4(
                            0.32f,
                            0.28f,
                            0.36f,
                            1.0f
                        ),

                    // Dark armor parts
                    "Balanced_Dark_Stone" =>
                        new Vector4(
                            0.12f,
                            0.10f,
                            0.16f,
                            1.0f
                        ),

                    // Joints between body parts
                    "REF_Joints" =>
                        new Vector4(
                            0.10f,
                            0.08f,
                            0.13f,
                            1.0f
                        ),

                    // Obsidian claws
                    "Beast_Obsidian_Claws" =>
                        new Vector4(
                            0.15f,
                            0.12f,
                            0.20f,
                            1.0f
                        ),

                    // Keep the original GLB colors
                    // for all other materials.
                    _ => baseColor
                };
            }

            // ======================================
            // MATERIAL DIAGNOSTIC
            // ======================================

            Console.WriteLine(
                $"Material: {gltfMaterial.Name}, " +
                $"BaseColor: {baseColor}, " +
                $"HasTexture: {hasTexture}"
            );

            Texture texture;

            // ======================================
            // LOAD EMBEDDED GLB TEXTURE
            // ======================================

            if (hasTexture)
            {
                var image =
                    channel.Value.Texture!.PrimaryImage;

                byte[] imageBytes =
                    image.Content.Content.ToArray();

                texture = keepTexture(
                    Texture.FromImageBytes(imageBytes)
                );

                Console.WriteLine(
                    $"  -> Embedded GLB texture loaded: " +
                    $"{gltfMaterial.Name}"
                );
            }
            else
            {
                // ==================================
                // CREATE TEXTURE FROM MATERIAL COLOR
                // ==================================

                texture = keepTexture(
                    Texture.FromColor(
                        ToByte(baseColor.X),
                        ToByte(baseColor.Y),
                        ToByte(baseColor.Z),
                        ToByte(baseColor.W)
                    )
                );

                Console.WriteLine(
                    $"  -> Material color applied: " +
                    $"{gltfMaterial.Name}"
                );
            }

            // ======================================
            // CREATE ENGINE MATERIAL
            // ======================================

            var material =
                new AmbientDiffuseSpecularMaterial(texture)
                {
                    Shininess = 32.0f
                };

            materials[gltfMaterial.LogicalIndex] = material;
        }

        Console.WriteLine(
            $"Total GLB materials loaded: {materials.Count}"
        );

        Console.WriteLine(
            "====================================="
        );

        return materials;
    }

    // ==========================================
    // CONVERT FLOAT COLOR TO BYTE
    // ==========================================

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp(
            (int)MathF.Round(value * 255.0f),
            0,
            255
        );
    }
}
