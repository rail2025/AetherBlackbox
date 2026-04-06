using AetherBlackbox.Core;
using AetherBlackbox.DrawingLogic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Dalamud.Interface.Utility.Raii.ImRaii;

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
                new() { Primary = DrawMode.SquareImage, SubModes = new List<DrawMode> { DrawMode.SquareImage, DrawMode.CircleMarkImage, DrawMode.TriangleImage, DrawMode.PlusImage }, Tooltip = "Placeable Shapes" },
                new() { Primary = DrawMode.RoleTankImage, SubModes = new List<DrawMode> { DrawMode.RoleTankImage, DrawMode.RoleHealerImage, DrawMode.RoleMeleeImage, DrawMode.RoleRangedImage, DrawMode.RoleCasterImage }, Tooltip = "Role Icons" },
                new() { Primary = DrawMode.Party1Image, SubModes = new List<DrawMode> { DrawMode.Party1Image, DrawMode.Party2Image, DrawMode.Party3Image, DrawMode.Party4Image, DrawMode.Party5Image, DrawMode.Party6Image, DrawMode.Party7Image, DrawMode.Party8Image,DrawMode.Bind1Image, DrawMode.Bind2Image, DrawMode.Bind3Image, DrawMode.Ignore1Image, DrawMode.Ignore2Image }, Tooltip = "Party Number Icons" },
                new() { Primary = DrawMode.Dot3Image, SubModes = new List<DrawMode> { DrawMode.Dot1Image, DrawMode.Dot2Image, DrawMode.Dot3Image, DrawMode.Dot4Image, DrawMode.Dot5Image, DrawMode.Dot6Image, DrawMode.Dot7Image, DrawMode.Dot8Image }, Tooltip = "Colored Dots" },
                new() { Primary = DrawMode.StackImage, SubModes = new List<DrawMode> { DrawMode.StackImage, DrawMode.SpreadImage, DrawMode.LineStackImage, DrawMode.FlareImage, DrawMode.DonutAoEImage, DrawMode.CircleAoEImage, DrawMode.BossImage, DrawMode.GazeImage, DrawMode.TowerImage, DrawMode.ExasImage, DrawMode.Starburst}, Tooltip = "Mechanic Icons" },
                new() { Primary = DrawMode.TextTool, SubModes = new List<DrawMode>(), Tooltip = "Text Tool" },
                new() { Primary = DrawMode.Image, SubModes = new List<DrawMode>(), Tooltip = "Image" }
            };

            activeSubModeMap = new Dictionary<DrawMode, DrawMode>();
            foreach (var group in toolGroups)
            {
                activeSubModeMap[group.Primary] = group.Primary;
            }
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
                string activePath = ToolRegistry.Tools.TryGetValue(activeModeInGroup, out var m) ? m.IconPath : "";
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
                            var activeToolName = ToolRegistry.Tools.TryGetValue(activeModeInGroup, out var m1) && !string.IsNullOrEmpty(m1.DisplayName) ? m1.DisplayName : (group.Primary == DrawMode.TextTool ? "TEXT" : "IMG");
                            var textSize = ImGui.CalcTextSize(activeToolName);
                            drawList.AddText(new Vector2(center.X - textSize.X / 2, center.Y - textSize.Y / 2), color, activeToolName);
                        }

                        if (group.Primary != DrawMode.TextTool && group.Primary != DrawMode.Image)
                        {
                            var activeToolName = ToolRegistry.Tools.TryGetValue(activeModeInGroup, out var m2) && !string.IsNullOrEmpty(m2.DisplayName) ? m2.DisplayName : activeModeInGroup.ToString();
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
                    int cols = Math.Min(4, group.SubModes.Count);
                    if (ImGui.BeginTable($"##table_{group.Primary}", cols, ImGuiTableFlags.SizingFixedFit))
                    {
                        foreach (var subMode in group.SubModes)
                        {
                            ImGui.TableNextColumn();
                            string subPath = ToolRegistry.Tools.TryGetValue(subMode, out var mSub) ? mSub.IconPath : "";
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
                                var subDisplayName = ToolRegistry.Tools.TryGetValue(subMode, out var mSubName) && !string.IsNullOrEmpty(mSubName.DisplayName) ? mSubName.DisplayName : subMode.ToString();
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

            bool gridVisible = plugin.Configuration.IsGridVisible;
            if (ImGui.Checkbox("Grid", ref gridVisible))
            {
                plugin.Configuration.IsGridVisible = gridVisible;
                plugin.Configuration.Save();
            }

            ImGui.SameLine();
            ImGui.Text("size");
            ImGui.SameLine();

            int gridSizeInt = (int)plugin.Configuration.GridSize;
            float availableWidth = iconButtonSize.X * 2 + style.ItemSpacing.X;
            float labelWidth = ImGui.CalcTextSize("size").X;
            float checkboxWidth = ImGui.GetItemRectSize().X;
            ImGui.SetNextItemWidth(availableWidth - checkboxWidth - labelWidth - style.ItemSpacing.X * 2);

            if (ImGui.InputInt("##GridSpacingInput", ref gridSizeInt, 0))
            {
                plugin.Configuration.GridSize = Math.Clamp(gridSizeInt, 10, 200);
                plugin.Configuration.Save();
            }

            bool snapToGrid = plugin.Configuration.SnapToGrid;
            if (ImGui.Checkbox("Snap to Grid", ref snapToGrid))
            {
                plugin.Configuration.SnapToGrid = snapToGrid;
                plugin.Configuration.Save();
            }

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

            ImGui.Separator();

            float btnWidthFull = iconButtonSize.X * 2 + style.ItemSpacing.X;
            float availableHeight = ImGui.GetContentRegionAvail().Y;
            float bugReportButtonHeight = ImGui.CalcTextSize("Bug report/\nFeature request").Y + ImGui.GetStyle().FramePadding.Y * 2.0f;
            float kofiButtonHeight = ImGui.GetFrameHeight();
            float footerButtonsTotalHeight = bugReportButtonHeight + kofiButtonHeight + ImGui.GetStyle().ItemSpacing.Y;

            if (availableHeight > footerButtonsTotalHeight)
            {
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + availableHeight - footerButtonsTotalHeight);
            }

            using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.1f, 0.4f, 0.1f, 1.0f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, new Vector4(0.1f, 0.5f, 0.1f, 1.0f)))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, new Vector4(0.2f, 0.6f, 0.2f, 1.0f)))
            {
                if (ImGui.Button("Bug report/\nFeature request", new Vector2(btnWidthFull, bugReportButtonHeight)))
                    Util.OpenLink("https://github.com/rail2025/AetherBlackbox/issues");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Opens the GitHub Issues page in your browser.");

            using (ImRaii.PushColor(ImGuiCol.Button, 0xFF000000 | 0xFF312B))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, 0xDD000000 | 0xFF312B))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0xFF312B))
            {
                if (ImGui.Button("Support on Ko-Fi", new Vector2(btnWidthFull, 0)))
                    Util.OpenLink("https://ko-fi.com/rail2025");
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Buy me a coffee if this plugin helped your prog!");
            }
        }
    }
}