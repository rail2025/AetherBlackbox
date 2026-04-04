using AetherBlackbox.DrawingLogic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public partial class MainWindow
    {
        private void DrawMapCalibrationPanel()
        {
            ImGui.Separator();
            ImGui.Text("Arena Calibration:");
            ImGui.SameLine();

            bool mapLocked = configuration.IsMapLocked;
            if (ImGui.Checkbox("Lock", ref mapLocked))
            {
                configuration.IsMapLocked = mapLocked;
                configuration.Save();
            }

            ImGui.BeginDisabled(mapLocked);

            float buttonSize = ImGui.GetFrameHeight();
            var mapControls = new (string Label, System.Action OffsetAction)[]
            {
                ("Up", () => configuration.MapOffsetZ -= 1f),
                ("Down", () => configuration.MapOffsetZ += 1f),
                ("Left", () => configuration.MapOffsetX -= 1f),
                ("Right", () => configuration.MapOffsetX += 1f)
            };

            for (int i = 0; i < mapControls.Length; i++)
            {
                if (ImGui.Button(mapControls[i].Label, new Vector2(0, buttonSize)))
                {
                    mapControls[i].OffsetAction.Invoke();
                    configuration.Save();
                }

                if (i < mapControls.Length - 1)
                    ImGui.SameLine();
            }

            ImGui.SameLine(0, 20f * ImGuiHelpers.GlobalScale);

            float mapScale = configuration.MapScaleMultiplier;
            ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
            if (ImGui.SliderFloat("Scale", ref mapScale, 0.5f, 2.0f))
            {
                configuration.MapScaleMultiplier = mapScale;
                configuration.Save();
            }

            ImGui.EndDisabled();

            ImGui.SameLine();

            bool isSelect = !IsDrawingMode;
            if (isSelect) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            if (ImGui.Button("Select")) IsDrawingMode = false;
            if (isSelect) ImGui.PopStyleColor();

            ImGui.SameLine();
            bool isLaser = IsDrawingMode && currentDrawMode == DrawMode.Laser;
            if (isLaser) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            if (ImGui.Button("Laser")) { IsDrawingMode = true; currentDrawMode = DrawMode.Laser; }
            if (isLaser) ImGui.PopStyleColor();

            ImGui.SameLine();

            if (plugin.NetworkManager.IsConnected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(new Vector4(0.8f, 0.2f, 0.2f, 1.0f)));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGui.GetColorU32(new Vector4(1.0f, 0.3f, 0.3f, 1.0f)));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGui.GetColorU32(new Vector4(0.6f, 0.1f, 0.1f, 1.0f)));
                if (ImGui.Button("Disconnect"))
                {
                    _ = plugin.NetworkManager.DisconnectAsync();
                }
                ImGui.PopStyleColor(3);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Leave the Live Session");
            }
            else
            {
                if (ImGui.Button("Live"))
                {
                    plugin.LiveSessionWindow.IsOpen = !plugin.LiveSessionWindow.IsOpen;
                }
            }
        }
    }
}