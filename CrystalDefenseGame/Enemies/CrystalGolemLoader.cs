
using System;
using System.IO;
using SharpGLTF.Schema2;

namespace CrystalDefenseGame.Enemies;

public static class CrystalGolemLoader
{
    public static ModelRoot Load()
    {
        // ==========================================
        // CRYSTAL GOLEM GLB PATH
        // ==========================================

        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Models",
            "CrystalGolem.glb"
        );

        // ==========================================
        // CHECK FILE
        // ==========================================

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Crystal Golem GLB not found.",
                path
            );
        }

        // ==========================================
        // LOAD MODEL
        // ==========================================

        ModelRoot model = ModelRoot.Load(path);

        Console.WriteLine();
        Console.WriteLine(
            "========== CRYSTAL GOLEM =========="
        );

        Console.WriteLine(
            "Crystal Golem GLB loaded!"
        );

        Console.WriteLine(
            $"Meshes: {model.LogicalMeshes.Count}"
        );

        Console.WriteLine(
            $"Materials: {model.LogicalMaterials.Count}"
        );

        Console.WriteLine(
            $"Animations: {model.LogicalAnimations.Count}"
        );

        Console.WriteLine(
            "==================================="
        );

        // ==========================================
        // DISPLAY ALL ANIMATION NAMES
        // ==========================================

        Console.WriteLine();
        Console.WriteLine(
            "========== ANIMATION LIST =========="
        );

        foreach (var animation in model.LogicalAnimations)
        {
            Console.WriteLine(
                $"Animation {animation.LogicalIndex}: " +
                $"{animation.Name}"
            );
        }

        Console.WriteLine(
            "===================================="
        );

        // ==========================================
        // DISPLAY ANIMATION DETAILS
        // ==========================================

        Console.WriteLine();
        Console.WriteLine(
            "========== ANIMATION DETAILS =========="
        );

        foreach (var animation in model.LogicalAnimations)
        {
            Console.WriteLine();

            Console.WriteLine(
                $"Animation: {animation.Name}"
            );

            Console.WriteLine(
                $"Channels: {animation.Channels.Count}"
            );

            foreach (var channel in animation.Channels)
            {
                Console.WriteLine(
                    $"  Node: {channel.TargetNode?.Name}"
                );

                Console.WriteLine(
                    $"  Property: {channel.TargetNodePath}"
                );
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            "======================================="
        );

        return model;
    }
}
