using System.Numerics;

namespace AetherBlackbox.Windows.Properties
{
    internal static class PropertyConstants
    {
        // from ToolbarDrawer to ensure consistency in presets
        internal static readonly float[] ThicknessPresets = { 1.5f, 4f, 7f, 10f };

        internal static readonly Vector4[] ColorPalette =
        {
            new(1.0f,1.0f,1.0f,1.0f),
            new(0.0f,0.0f,0.0f,1.0f),
            new(1.0f,0.0f,0.0f,1.0f),
            new(0.0f,1.0f,0.0f,1.0f),
            new(0.0f,0.0f,1.0f,1.0f),
            new(1.0f,1.0f,0.0f,1.0f),
            new(1.0f,0.0f,1.0f,1.0f),
            new(0.0f,1.0f,1.0f,1.0f),
            new(0.5f,0.5f,0.5f,1.0f),
            new(0.8f,0.4f,0.0f,1.0f)
        };
    }
}