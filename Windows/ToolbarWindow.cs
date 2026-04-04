using AetherBlackbox.DrawingLogic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public class ToolbarGroup
    {
        public DrawMode Primary { get; set; }
        public List<DrawMode> SubModes { get; set; } = new();
        public string Tooltip { get; set; } = "";
    }
    public class ToolbarWindow : IDisposable
    {
        private readonly Plugin plugin;
        private DrawMode activeMode = DrawMode.Pen;

        private readonly List<ToolbarGroup> toolGroups;
        private readonly Dictionary<DrawMode, DrawMode> activeSubModeMap;
        private readonly Dictionary<DrawMode, string> toolDisplayNames;
        private readonly Dictionary<DrawMode, string> iconPaths;
        private static readonly float[] ThicknessPresets = { 1.5f, 4f, 7f, 10f };
        private static readonly Vector4[] ColorPalette = {
            new(1.0f,1.0f,1.0f,1.0f), new(0.0f,0.0f,0.0f,1.0f),
            new(1.0f,0.0f,0.0f,1.0f), new(0.0f,1.0f,0.0f,1.0f),
            new(0.0f,0.0f,1.0f,1.0f), new(1.0f,1.0f,0.0f,1.0f),
            new(1.0f,0.0f,1.0f,1.0f), new(0.0f,1.0f,1.0f,1.0f),
            new(0.5f,0.5f,0.5f,1.0f), new(0.8f,0.4f,0.0f,1.0f)
        };

        public ToolbarWindow(Plugin plugin)
        {
            this.plugin = plugin;
            
            toolGroups = new List<ToolbarGroup>
            {
                new() { Primary = DrawMode.Pen, SubModes = new List<DrawMode> { DrawMode.Pen, DrawMode.StraightLine, DrawMode.Dash }, Tooltip = "Pen Tools" },
                new() { Primary = DrawMode.Rectangle, SubModes = new List<DrawMode> { DrawMode.Rectangle, DrawMode.Circle, DrawMode.Donut, DrawMode.Arrow, DrawMode.Cone, DrawMode.Triangle, DrawMode.Pie }, Tooltip = "Shape Tools" },
                new() { Primary = DrawMode.StackImage, SubModes = new List<DrawMode> { DrawMode.StackImage, DrawMode.SpreadImage, DrawMode.LineStackImage, DrawMode.FlareImage, DrawMode.DonutAoEImage, DrawMode.CircleAoEImage, DrawMode.BossImage, DrawMode.GazeImage, DrawMode.TowerImage, DrawMode.ExasImage, DrawMode.Starburst}, Tooltip = "Mechanic Icons" },
                new() { Primary = DrawMode.TextTool, SubModes = new List<DrawMode>(), Tooltip = "Text Tool" },
                new() { Primary = DrawMode.Image, SubModes = new List<DrawMode>(), Tooltip = "Image" }
            };

            activeSubModeMap = new Dictionary<DrawMode, DrawMode>();
            foreach (var group in toolGroups)
            {
                activeSubModeMap[group.Primary] = group.Primary;
            }

            toolDisplayNames = new Dictionary<DrawMode, string>
            {
                { DrawMode.Pen, "Pen" }, { DrawMode.StraightLine, "Line" }, { DrawMode.Dash, "Dash" },
                { DrawMode.Rectangle, "Rect" }, { DrawMode.Circle, "Circle" }, { DrawMode.Donut, "Donut" },
                { DrawMode.Arrow, "Arrow" }, { DrawMode.Cone, "Cone" }, { DrawMode.Triangle, "Triangle" },
                { DrawMode.Pie, "Pie" }, { DrawMode.Starburst, "Star" },
                { DrawMode.TextTool, "TEXT" }, { DrawMode.Image, "IMG" }
            };

            iconPaths = new Dictionary<DrawMode, string>
            {
                { DrawMode.Pen, "" }, { DrawMode.StraightLine, "" }, { DrawMode.Dash, "" },
                { DrawMode.Rectangle, "" }, { DrawMode.Circle, "" }, { DrawMode.Donut, "" },
                { DrawMode.Arrow, "" }, { DrawMode.Cone, "" }, { DrawMode.Triangle, "" },
                { DrawMode.Pie, "" }, { DrawMode.Starburst, "PluginImages.svg.starburst.png" },
                { DrawMode.StackImage, "PluginImages.svg.stack.svg" },
                { DrawMode.SpreadImage, "PluginImages.svg.spread.svg" },
                { DrawMode.LineStackImage, "PluginImages.svg.line_stack.svg" },
                { DrawMode.FlareImage, "PluginImages.svg.flare.svg" },
                { DrawMode.DonutAoEImage, "PluginImages.svg.donut.svg" },
                { DrawMode.CircleAoEImage, "PluginImages.svg.prox_aoe.svg" },
                { DrawMode.BossImage, "PluginImages.svg.boss.svg" },
                { DrawMode.GazeImage, "PluginImages.svg.gaze.png" },
                { DrawMode.TowerImage, "PluginImages.svg.tower.png" },
                { DrawMode.ExasImage, "PluginImages.svg.exas.svg" },
                { DrawMode.TextTool, "" }, { DrawMode.Image, "PluginImages.toolbar.Square.png" }
            };
        }
        public void Dispose() { }

        public void Draw()
        {
            float buttonSize = 45 * ImGuiHelpers.GlobalScale;
            Vector2 iconButtonSize = new Vector2(buttonSize, buttonSize);
            Vector2 popupIconButtonSize = new Vector2(28 * ImGuiHelpers.GlobalScale, 28 * ImGuiHelpers.GlobalScale);

            var style = ImGui.GetStyle();
            float windowVisibleX2 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;

            for (int i = 0; i < toolGroups.Count; i++)
            {
                var group = toolGroups[i];

                if (i > 0)
                {
                    float lastButtonX2 = ImGui.GetItemRectMax().X;
                    float nextButtonX2 = lastButtonX2 + style.ItemSpacing.X + iconButtonSize.X;
                    if (nextButtonX2 < windowVisibleX2) ImGui.SameLine();
                }

                DrawMode activeModeInGroup = activeSubModeMap.GetValueOrDefault(group.Primary, group.Primary);
                string activePath = iconPaths.GetValueOrDefault(activeModeInGroup, "");
                var tex = activePath != "" ? TextureManager.GetTexture(activePath) : null;
                var drawList = ImGui.GetWindowDrawList();

                bool isGroupActive = activeMode == group.Primary || (group.SubModes.Any() && group.SubModes.Contains(activeMode));

                using (isGroupActive ? ImRaii.PushColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]) : null)
                {
                    if (ImGui.Button($"##{group.Primary}", iconButtonSize))
                    {
                        if (group.SubModes.Any()) activeMode = activeModeInGroup;
                        else activeMode = group.Primary;
                        plugin.MainWindow.CurrentDrawMode = activeMode;
                        plugin.MainWindow.IsDrawingMode = true;
                    }

                    var min = ImGui.GetItemRectMin();
                    var max = ImGui.GetItemRectMax();
                    var center = (min + max) / 2;

                    if (tex != null) drawList.AddImage(tex.Handle, min, max);
                    else
                    {
                        var color = ImGui.GetColorU32(ImGuiCol.Text);
                        var graphicCenter = new Vector2(center.X, min.Y + iconButtonSize.Y * 0.35f);

                        if (group.Primary == DrawMode.Pen)
                            drawList.AddLine(graphicCenter - new Vector2(iconButtonSize.Y * 0.4f, iconButtonSize.Y * 0.15f), graphicCenter + new Vector2(iconButtonSize.Y * 0.4f, iconButtonSize.Y * 0.15f), color, 2f);
                        else if (group.Primary == DrawMode.Rectangle)
                        {
                            drawList.AddRect(graphicCenter - new Vector2(iconButtonSize.Y * 0.25f, iconButtonSize.Y * 0.25f), graphicCenter + new Vector2(iconButtonSize.Y * 0.25f, iconButtonSize.Y * 0.25f), color, 0f, ImDrawFlags.None, 2f);
                            drawList.AddCircle(graphicCenter, iconButtonSize.Y * 0.35f, color, 0, 2f);
                        }
                        else if (group.Primary == DrawMode.TextTool || group.Primary == DrawMode.Image)
                        {
                            var activeToolName = toolDisplayNames.GetValueOrDefault(activeModeInGroup, group.Primary == DrawMode.TextTool ? "TEXT" : "IMG");
                            var textSize = ImGui.CalcTextSize(activeToolName);
                            drawList.AddText(new Vector2(center.X - textSize.X / 2, center.Y - textSize.Y / 2), color, activeToolName);
                        }

                        if (group.Primary != DrawMode.TextTool && group.Primary != DrawMode.Image)
                        {
                            var activeToolName = toolDisplayNames.GetValueOrDefault(activeModeInGroup, activeModeInGroup.ToString());
                            var textSize = ImGui.CalcTextSize(activeToolName);
                            drawList.AddText(new Vector2(center.X - textSize.X / 2, max.Y - textSize.Y - (iconButtonSize.Y * 0.1f)), color, activeToolName);
                        }
                    }

                    if (group.SubModes.Any())
                    {
                        var arrowSize = 6f * ImGuiHelpers.GlobalScale;
                        var padding = 4f * ImGuiHelpers.GlobalScale;
                        Vector2 p1 = new Vector2(max.X - arrowSize - padding, max.Y - arrowSize - padding);
                        Vector2 p2 = new Vector2(max.X - padding, max.Y - arrowSize - padding);
                        Vector2 p3 = new Vector2(max.X - arrowSize * 0.5f - padding, max.Y - padding);
                        drawList.AddTriangleFilled(p1, p2, p3, ImGui.GetColorU32(ImGuiCol.Text));
                    }
                }

                if (ImGui.IsItemHovered()) ImGui.SetTooltip(group.Tooltip);

                if (group.SubModes.Any() && ImGui.BeginPopupContextItem($"popup_{group.Primary}", ImGuiPopupFlags.MouseButtonLeft))
                {
                    int cols = Math.Min(3, group.SubModes.Count);
                    if (ImGui.BeginTable($"##table_{group.Primary}", cols, ImGuiTableFlags.SizingFixedFit))
                    {
                        foreach (var subMode in group.SubModes)
                        {
                            ImGui.TableNextColumn();
                            string subPath = iconPaths.GetValueOrDefault(subMode, "");
                            var subTex = subPath != "" ? TextureManager.GetTexture(subPath) : null;

                            if (subTex != null)
                            {
                                if (ImGui.ImageButton(subTex.Handle, popupIconButtonSize))
                                {
                                    activeMode = subMode;
                                    activeSubModeMap[group.Primary] = subMode;
                                    plugin.MainWindow.CurrentDrawMode = activeMode;
                                    plugin.MainWindow.IsDrawingMode = true;
                                    ImGui.CloseCurrentPopup();
                                }
                            }
                            else
                            {
                                var subDisplayName = toolDisplayNames.GetValueOrDefault(subMode, subMode.ToString());
                                if (ImGui.Selectable(subDisplayName, activeMode == subMode))
                                {
                                    activeMode = subMode;
                                    activeSubModeMap[group.Primary] = subMode;
                                    plugin.MainWindow.CurrentDrawMode = activeMode;
                                    plugin.MainWindow.IsDrawingMode = true;
                                    ImGui.CloseCurrentPopup();
                                }
                            }
                        }
                        ImGui.EndTable();
                    }
                    ImGui.EndPopup();
                }
            }
            ImGui.Separator();

            float btnWidthHalf = (iconButtonSize.X * 2 + style.ItemSpacing.X) / 2f;
            if (ImGui.Button("Select", new Vector2(btnWidthHalf, 0))) { plugin.MainWindow.CurrentDrawMode = DrawMode.Select; plugin.MainWindow.IsDrawingMode = true; }
            ImGui.SameLine();
            if (ImGui.Button("Eraser", new Vector2(btnWidthHalf, 0))) { plugin.MainWindow.CurrentDrawMode = DrawMode.Eraser; plugin.MainWindow.IsDrawingMode = true; }

            if (ImGui.Button("Undo", new Vector2(iconButtonSize.X * 2 + style.ItemSpacing.X, 0))) plugin.MainWindow.PerformUndo();
            if (ImGui.Button("Props", new Vector2(iconButtonSize.X * 2 + style.ItemSpacing.X, 0))) plugin.PropertiesWindow.IsOpen = !plugin.PropertiesWindow.IsOpen;

            ImGui.Separator();

            var isFilled = plugin.MainWindow.CurrentShapeFilled;
            if (ImGui.Checkbox("Fill Shape", ref isFilled)) plugin.MainWindow.CurrentShapeFilled = isFilled;

            ImGui.Separator();
            ImGui.Text("Thickness:");
            float thicknessButtonWidth = ((iconButtonSize.X * 2 + style.ItemSpacing.X) - style.ItemSpacing.X * (ThicknessPresets.Length - 1)) / ThicknessPresets.Length;
            foreach (var t in ThicknessPresets)
            {
                if (t != ThicknessPresets[0]) ImGui.SameLine();
                if (ImGui.Selectable($"{t:0}", Math.Abs(plugin.MainWindow.CurrentBrushThickness - t) < 0.01f, 0, new Vector2(thicknessButtonWidth, 0)))
                    plugin.MainWindow.CurrentBrushThickness = t;
            }

            ImGui.Separator();
            int colorsPerRow = 5;
            float smallColorButtonSize = ((iconButtonSize.X * 2 + style.ItemSpacing.X) - (style.ItemSpacing.X * (colorsPerRow - 1))) / colorsPerRow;
            Vector2 colorButtonDimensions = new(smallColorButtonSize, smallColorButtonSize);
            var paletteDrawList = ImGui.GetWindowDrawList();
            for (int i = 0; i < ColorPalette.Length; i++)
            {
                if (i > 0 && i % colorsPerRow != 0) ImGui.SameLine();
                if (ImGui.ColorButton($"##ColorPaletteButton{i}", ColorPalette[i], ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoAlpha, colorButtonDimensions))
                    plugin.MainWindow.CurrentBrushColor = ColorPalette[i];
                if (ColorPalette[i] == plugin.MainWindow.CurrentBrushColor)
                    paletteDrawList.AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(new Vector4(1, 1, 0, 1)), 0, ImDrawFlags.None, 2f);
            }
        }
    }
}