
using CrystalDefense.Game;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace CrystalDefense;

internal static class Program
{
    private static void Main()
    {
        var windowSettings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(1280, 720),
            Title = "Crystal Defense",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core
        };

        using var window = new CrystalDefenseWindow(
            GameWindowSettings.Default,
            windowSettings
        );

        window.Run();
    }
}
