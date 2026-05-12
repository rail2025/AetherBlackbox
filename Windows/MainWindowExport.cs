using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace AetherBlackbox.Windows;

public partial class MainWindow
{
    private void DrawExportPreviewUI()
    {
        var slides = ExportManager.StagedSlides.ToList();
        if (slides.Count == 0)
        {
            ImGui.TextWrapped("No slides staged. Open a replay and click the Camera icon to capture a slide.");
            return;
        }

        if (ImGui.BeginChild("SlideList", new Vector2(0, -35 * ImGuiHelpers.GlobalScale), true))
        {
            for (int i = 0; i < slides.Count; i++)
            {
                var slide = slides[i];
                ImGui.PushID($"Slide_{i}");

                var canvasSize = new Vector2(60 * ImGuiHelpers.GlobalScale, 60 * ImGuiHelpers.GlobalScale);
                var cursorScreen = ImGui.GetCursorScreenPos();
                ImGui.InvisibleButton($"prev_{i}", canvasSize);
                var drawList = ImGui.GetWindowDrawList();
                drawList.AddRectFilled(cursorScreen, cursorScreen + canvasSize, ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.1f, 1f)));

                if (slide.ThumbnailBytes.Length > 0)
                {
                    if (!thumbnailCache.ContainsKey(slide) && !thumbnailTasks.ContainsKey(slide))
                    {
                        thumbnailTasks[slide] = Service.TextureProvider.CreateFromImageAsync(slide.ThumbnailBytes);
                    }

                    if (thumbnailTasks.TryGetValue(slide, out var task))
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            thumbnailCache[slide] = task.Result;
                            thumbnailTasks.Remove(slide);
                        }
                    }

                    if (thumbnailCache.TryGetValue(slide, out var texWrap) && texWrap != null)
                    {
                        drawList.AddImage(texWrap.Handle, cursorScreen, cursorScreen + canvasSize);
                    }
                    else
                    {
                        drawList.AddText(cursorScreen + new Vector2(5, 20), ImGui.GetColorU32(new Vector4(1f, 1f, 0f, 1f)), "LOADING...");
                    }
                }
                else
                {
                    drawList.AddText(cursorScreen + new Vector2(5, 20), ImGui.GetColorU32(new Vector4(1f, 1f, 0f, 1f)), "LOADING...");
                }

                var drawables = ExportManager.DeserializeSlide(slides[i]);

                ImGui.SameLine();
                ImGui.BeginGroup();
                ImGui.Text($"Slide #{i + 1}");
                ImGui.Text($"{drawables?.Count ?? 0} objs");
                ImGui.EndGroup();

                ImGui.SameLine(ImGui.GetWindowWidth() - 90 * ImGuiHelpers.GlobalScale);
                ImGui.BeginGroup();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp) && i > 0) ExportManager.SwapSlides(i, i - 1);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown) && i < slides.Count - 1) ExportManager.SwapSlides(i, i + 1);
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) ExportManager.RemoveSlide(i);
                ImGui.EndGroup();

                ImGui.PopID();
                ImGui.Separator();
            }
        }
        ImGui.EndChild();

        if (ImGui.Button("Clear All"))
        {
            ExportManager.Clear();
            foreach (var tex in thumbnailCache.Values) tex?.Dispose();
            thumbnailCache.Clear();
            thumbnailTasks.Clear();
        }
        ImGui.SameLine();
        if (ImGui.Button("Send to AetherDraw", new Vector2(-1, 0)))
        {
            string payload = ExportManager.GenerateIpcPayload();

            try
            {
                var ipcSubscriber = Service.PluginInterface.GetIpcSubscriber<string, bool>("AetherDraw.ImportPlanJson");
                bool result = ipcSubscriber.InvokeFunc(payload);
                if (result)
                    Service.PluginLog.Info("Plan sent to AetherDraw via IPC successfully.");
                else
                    Service.PluginLog.Warning("AetherDraw returned false during IPC import.");
            }
            catch (System.Exception ex)
            {
                Service.PluginLog.Error(ex, "Failed to send IPC to AetherDraw. Is it loaded?");
            }
        }
    }
}