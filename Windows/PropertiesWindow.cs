using AetherBlackbox.DrawingLogic;
using AetherBlackbox.Windows.Properties;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;

namespace AetherBlackbox.Windows
{
    public class PropertiesWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        public PropertiesWindow(Plugin plugin)
            : base("Properties###AetherBlackboxProperties")
        {
            this.plugin = plugin;

            SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new System.Numerics.Vector2(250, 300),
                MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            var mainWindow = plugin.MainWindow;

            if (mainWindow == null || !mainWindow.IsOpen)
            {
                IsOpen = false;
                return;
            }

            var selected = mainWindow.SelectedDrawables;

            if (selected == null || selected.Count == 0)
            {
                ImGui.TextDisabled("No objects selected.");
                return;
            }

            CommonProperties.Draw(mainWindow, selected);

            JobSwapProperties.DrawJobSwap(
                mainWindow,
                selected,
                TextureManager.GetTexture);

            ContextProperties.Draw(mainWindow, selected);

            MechanicDefinition.Draw(mainWindow, selected, plugin);

            LayerList.Draw(mainWindow);
        }
    }
}