using AetherBlackbox.DrawingLogic;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AetherBlackbox.Windows.Properties
{
    internal static class JobSwapProperties
    {
        private static readonly Dictionary<DrawMode, List<DrawMode>> RoleToJobMap = new()
        {
            { DrawMode.RoleTankImage,   new List<DrawMode> { DrawMode.RoleTank1Image, DrawMode.RoleTank2Image, DrawMode.JobPldImage, DrawMode.JobWarImage, DrawMode.JobDrkImage, DrawMode.JobGnbImage } },
            { DrawMode.RoleHealerImage, new List<DrawMode> { DrawMode.RoleHealer1Image, DrawMode.RoleHealer2Image, DrawMode.JobWhmImage, DrawMode.JobSchImage, DrawMode.JobAstImage, DrawMode.JobSgeImage } },
            { DrawMode.RoleMeleeImage,  new List<DrawMode> { DrawMode.RoleMelee1Image, DrawMode.RoleMelee2Image, DrawMode.JobMnkImage, DrawMode.JobDrgImage, DrawMode.JobNinImage, DrawMode.JobSamImage, DrawMode.JobRprImage, DrawMode.JobVprImage } },
            { DrawMode.RoleRangedImage, new List<DrawMode> { DrawMode.RoleRanged1Image, DrawMode.RoleRanged2Image, DrawMode.JobBrdImage, DrawMode.JobMchImage, DrawMode.JobDncImage } },
            { DrawMode.RoleCasterImage, new List<DrawMode> { DrawMode.JobBlmImage, DrawMode.JobSmnImage, DrawMode.JobRdmImage, DrawMode.JobPctImage } }
        };

        internal static List<DrawMode>? GetJobListForMode(DrawMode mode)
        {
            if (RoleToJobMap.TryGetValue(mode, out var direct))
                return direct;

            foreach (var kvp in RoleToJobMap)
                if (kvp.Value.Contains(mode))
                    return kvp.Value;

            return null;
        }

        internal static string GetIconPath(DrawMode mode)
        {
            if (mode == DrawMode.RoleTank1Image) return "PluginImages.toolbar.tank_1.png";
            if (mode == DrawMode.RoleTank2Image) return "PluginImages.toolbar.tank_2.png";
            if (mode == DrawMode.RoleHealer1Image) return "PluginImages.toolbar.healer_1.png";
            if (mode == DrawMode.RoleHealer2Image) return "PluginImages.toolbar.healer_2.png";
            if (mode == DrawMode.RoleMelee1Image) return "PluginImages.toolbar.melee_1.png";
            if (mode == DrawMode.RoleMelee2Image) return "PluginImages.toolbar.melee_2.png";
            if (mode == DrawMode.RoleRanged1Image) return "PluginImages.toolbar.ranged_dps_1.png";
            if (mode == DrawMode.RoleRanged2Image) return "PluginImages.toolbar.ranged_dps_2.png";
            if (mode == DrawMode.RoleCasterImage) return "PluginImages.toolbar.caster.png";

            string name = mode.ToString()
                .Replace("Job", "")
                .Replace("Image", "")
                .ToLower();

            return $"PluginImages.toolbar.{name}.png";
        }

        internal static void DrawJobSwap(
            MainWindow mainWindow,
            List<BaseDrawable> selected,
            Func<string, IDalamudTextureWrap> textureGetter)
        {
            if (selected == null || selected.Count != 1)
                return;

            if (selected[0] is not DrawableImage dImg)
                return;

            var jobList = GetJobListForMode(dImg.ObjectDrawMode);
            if (jobList == null)
                return;

            ImGui.Separator();
            ImGui.Text("Swap Job Icon");

            var style = ImGui.GetStyle();
            float windowVisibleX2 = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
            float buttonSize = 40f * ImGuiHelpers.GlobalScale;
            var btnVec = new Vector2(buttonSize, buttonSize);

            for (int i = 0; i < jobList.Count; i++)
            {
                var jobMode = jobList[i];

                if (i > 0)
                {
                    float lastX = ImGui.GetItemRectMax().X;
                    float nextX = lastX + style.ItemSpacing.X + buttonSize;

                    if (nextX < windowVisibleX2)
                        ImGui.SameLine();
                }

                var tex = textureGetter(GetIconPath(jobMode));
                if (tex != null)
                {
                    if (ImGui.ImageButton((ImTextureID)tex.Handle, btnVec))
                    {
                        dImg.ObjectDrawMode = jobMode;
                        dImg.ImageResourcePath = GetIconPath(jobMode);

                        mainWindow.CanvasController.UndoManager.RecordAction(
                            mainWindow.PageManager.GetCurrentPageDrawables(),
                            "Swap Job Icon");

                        mainWindow.CanvasController.InteractionHandler.CommitObjectChanges(
                            new List<BaseDrawable>(selected));
                    }

                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(jobMode.ToString()
                            .Replace("Job", "")
                            .Replace("Image", ""));
                    }
                }
            }
        }
    }
}