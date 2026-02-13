using AetherBlackbox.Core;
using AetherBlackbox.DrawingLogic;
using AetherBlackbox.Events;
using AetherBlackbox.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace AetherBlackbox.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private float canvasZoom = 1.0f;
        private Vector2 canvasPanOffset = Vector2.Zero;
        private readonly Plugin plugin;
        private readonly Configuration configuration;
        private readonly CanvasController canvasController;
        private readonly PageManager pageManager;
        private List<PullManager.ReplayFileHeader>? cachedSavedReplays;
        private string logSearchTerm = "";
        private readonly ReplayRenderer replayRenderer;

        // Replay
        public Death? ActiveDeathReplay { get; set; }
        private bool isReplayMode = false;
        private float replayTimeOffset = 0f;
        private bool isPlaybackActive = false;
        private Vector3? cachedArenaCenter = null;

        // Canvas
        private ulong selectedEntityId = 0;
        private BaseDrawable? hoveredDrawable = null;
        private List<BaseDrawable> selectedDrawables = new List<BaseDrawable>();
        private List<BaseDrawable> clipboard = new List<BaseDrawable>();
        private DrawMode currentDrawMode = DrawMode.Pen;
        private Vector4 currentBrushColor;
        private float currentBrushThickness;
        private bool currentShapeFilled = false;
        private Vector2 currentCanvasDrawSize;

        private List<Lumina.Excel.Sheets.Status> statusSearchResults = new();

        private PullSession? selectedPull;
        private const float SidebarWidth = 350f;

        public interface IPlanAction
        {
            void Undo(MainWindow window);
            string Description { get; }
        }

        private readonly Stack<IPlanAction> planUndoStack = new();
        private int previousSelectionCount = 0;
        private readonly Vector4 ColorHealing = new(0.6f, 1.0f, 0.8f, 1.0f);
        private readonly Vector4 ColorDamagePhysical = new(0.3f, 0.6f, 1.0f, 1.0f);
        private readonly Vector4 ColorDamageMagic = new(0.7f, 0.6f, 0.1f, 1.0f);
        private readonly Vector4 ColorNeutral = new(0.8f, 0.8f, 0.8f, 1.0f);
        public List<BaseDrawable> SelectedDrawables => selectedDrawables;
        public PageManager PageManager => pageManager;

        public MainWindow(Plugin plugin, string id = "") : base($"Aether Blackbox v1.0.0 - Powered by Death Recap by Kouzukii and AetherDraw by rail{id}###Aether Blackbox MainWindow{id}")
        {
            this.plugin = plugin;
            this.configuration = plugin.Configuration;
            this.pageManager = new PageManager();

            this.replayRenderer = new ReplayRenderer();

            this.canvasController = new CanvasController(
                this.pageManager,
                () => currentDrawMode, (newMode) => currentDrawMode = newMode,
                () => currentBrushColor, () => currentBrushThickness, () => currentShapeFilled,
                selectedDrawables, () => hoveredDrawable, (newHovered) => hoveredDrawable = newHovered,
                this.configuration, this.plugin
            );

            this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(1000f * ImGuiHelpers.GlobalScale, 600f * ImGuiHelpers.GlobalScale), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
            this.RespectCloseHotkey = true;
            this.currentBrushColor = new Vector4(this.configuration.DefaultBrushColorR, this.configuration.DefaultBrushColorG, this.configuration.DefaultBrushColorB, this.configuration.DefaultBrushColorA);
            var initialThicknessPresets = new float[] { 1.5f, 4f, 7f, 10f };
            this.currentBrushThickness = initialThicknessPresets.Contains(this.configuration.DefaultBrushThickness) ? this.configuration.DefaultBrushThickness : initialThicknessPresets[1];
        }

        public void Dispose()
        { }

        public void OpenReplay(Death death)
        {
            selectedPull = plugin.PullManager.History.FirstOrDefault(p => p.Deaths.Contains(death));
            ActiveDeathReplay = death;
            isReplayMode = true;
            replayTimeOffset = 0f;
            isPlaybackActive = false;
            cachedArenaCenter = null;
            IsOpen = true;
        }

        public override void PreDraw() => Flags = configuration.IsMainWindowMovable ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoMove;

        public override void Draw()
        {
            TextureManager.DoMainThreadWork();

            var contentRegion = ImGui.GetContentRegionAvail();

            ImGui.BeginGroup();
            {
                if (ImGui.BeginChild("SessionHistoryPane", new Vector2(SidebarWidth * ImGuiHelpers.GlobalScale, contentRegion.Y * 0.5f), true))
                {
                    DrawSessionHistory();
                }
                ImGui.EndChild();

                float remainingHeight = ImGui.GetContentRegionAvail().Y;
                if (ImGui.BeginChild("InteractiveLogPane", new Vector2(SidebarWidth * ImGuiHelpers.GlobalScale, remainingHeight), true))
                {
                    DrawInteractiveLog();
                }
                ImGui.EndChild();
            }
            ImGui.EndGroup();

            ImGui.SameLine();

            using (var rightPaneRaii = ImRaii.Child("RightPane", Vector2.Zero, false, ImGuiWindowFlags.None))
            {
                if (rightPaneRaii)
                {
                    DrawOriginalRightPane();
                }
            }

            if (this.selectedDrawables.Count > 0 && this.previousSelectionCount == 0)
            { }
            this.previousSelectionCount = this.selectedDrawables.Count;
        }

        private void DrawSessionHistory()
        {
            ImGui.TextDisabled("SESSION HISTORY");
            ImGui.Separator();
            if (ImGui.TreeNode("Saved Replays (Disk)"))
            {
                if (ImGui.Button("Refresh List")) cachedSavedReplays = plugin.PullManager.GetSavedReplays();
                ImGui.SameLine();
                if (ImGui.Button("Clear All History"))
                {
                    plugin.PullManager.History.Clear();
                    selectedPull = null;
                    var folder = System.IO.Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "replays");
                    if (System.IO.Directory.Exists(folder))
                    {
                        foreach (var f in System.IO.Directory.GetFiles(folder, "*.json.gz"))
                            try { System.IO.File.Delete(f); } catch { }
                    }
                    cachedSavedReplays = plugin.PullManager.GetSavedReplays();
                }
                if (cachedSavedReplays == null) cachedSavedReplays = plugin.PullManager.GetSavedReplays();

                foreach (var file in cachedSavedReplays)
                {
                    if (ImGui.Selectable($"{file.FileName}"))
                    {
                        var loaded = plugin.PullManager.LoadSession(file.FilePath);
                        if (loaded != null) selectedPull = loaded;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Created: {file.CreationTime}");
                }
                ImGui.TreePop();
            }
            ImGui.Separator();

            var history = plugin.PullManager.History;
            if (history.Count == 0)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "No sessions recorded.");
                return;
            }

            for (int i = history.Count - 1; i >= 0; i--)
            {
                var pull = history[i];
                var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth;
                if (selectedPull == pull) flags |= ImGuiTreeNodeFlags.DefaultOpen;

                var title = pull.DisplayTitle + (pull.IsTruncated ? " [TRUNC]" : "");
                bool isOpen = ImGui.TreeNodeEx($"##Pull_{pull.PullNumber}", flags, title);

                ImGui.SameLine();
                if (ImGuiComponents.IconButton($"##Delete_{pull.PullNumber}", FontAwesomeIcon.Trash))
                {
                    plugin.PullManager.History.Remove(pull);
                    if (selectedPull == pull) selectedPull = null;
                    continue;
                }

                if (ImGui.IsItemClicked())
                {
                    selectedPull = pull;
                    Service.PluginLog.Info($"User clicked Pull #{pull.PullNumber}. Internal Death Count: {pull.Deaths.Count}");
                }

                if (isOpen)
                {
                    foreach (var death in pull.Deaths)
                    {
                        ImGui.Indent();
                        bool isSelected = (death == ActiveDeathReplay);

                        string displayName = GetAnonymizedName(death.PlayerName, death.ReplayData);
                        var timeIntoPull = death.TimeOfDeath - pull.StartTime;
                        string deathLabel = $"{displayName} ({timeIntoPull:mm\\:ss})";

                        if (ImGui.Selectable(deathLabel, isSelected))
                        {
                            OpenReplay(death);
                        }
                        ImGui.Unindent();
                    }
                    ImGui.TreePop();
                }
            }
        }

        private void DrawInteractiveLog()
        {
            ImGui.TextDisabled("INTERACTIVE LOG");
            ImGui.SameLine();
            ImGui.TextDisabled("(Click time to seek)");
            ImGui.Separator();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("##LogSearch", "Search...", ref logSearchTerm, 100);
            ImGui.Separator();

            if (ActiveDeathReplay == null)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Select an incident.");
                return;
            }

            if (ImGui.BeginTable("InteractiveLogTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInner | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 50f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 90f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Event", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var evt in ActiveDeathReplay.Events.AsEnumerable().Reverse())
                {
                    if (!string.IsNullOrWhiteSpace(logSearchTerm))
                    {
                        bool match = false;
                        string sSrc = evt switch { CombatEvent.DamageTaken dt => dt.Source, CombatEvent.Healed h => h.Source, CombatEvent.StatusEffect s => s.Source, _ => "" } ?? "";
                        string sAct = evt switch { CombatEvent.DamageTaken dt => dt.Action, CombatEvent.Healed h => h.Action, CombatEvent.StatusEffect s => s.Status, _ => "" } ?? "";

                        if (sSrc.Contains(logSearchTerm, StringComparison.OrdinalIgnoreCase)) match = true;
                        if (sAct.Contains(logSearchTerm, StringComparison.OrdinalIgnoreCase)) match = true;

                        if (!match) continue;
                    }
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    var relativeSeconds = (evt.Snapshot.Time - ActiveDeathReplay.TimeOfDeath).TotalSeconds;

                    if (ImGui.Selectable($"{relativeSeconds:F1}s##{evt.GetHashCode()}", false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        replayTimeOffset = (float)relativeSeconds;
                        isPlaybackActive = false;
                    }

                    ImGui.TableNextColumn();
                    string source = evt switch
                    {
                        CombatEvent.DamageTaken dt => dt.Source ?? "-",
                        CombatEvent.Healed h => h.Source ?? "-",
                        CombatEvent.StatusEffect s => s.Source ?? "-",
                        _ => "-"
                    };

                    ImGui.Text(GetAnonymizedName(source));

                    ImGui.TableNextColumn();
                    DrawEventCell(evt);
                }
                ImGui.EndTable();
            }
        }

        private void DrawEventCell(CombatEvent evt)
        {
            switch (evt)
            {
                case CombatEvent.DamageTaken dt:
                    var col = dt.DamageType == DamageType.Magic ? ColorDamageMagic : ColorDamagePhysical;
                    if (GetIconImage(dt.Icon) is { } dtIcon) InlineIcon(dtIcon);
                    ImGui.TextColored(col, $"-{dt.Amount:N0} {dt.Action}");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{dt.DamageType} {(dt.Crit ? "(Crit)" : "")} {(dt.DirectHit ? "(DH)" : "")}");
                    break;

                case CombatEvent.Healed h:
                    if (GetIconImage(h.Icon) is { } hIcon) InlineIcon(hIcon);
                    ImGui.TextColored(ColorHealing, $"+{h.Amount:N0} {h.Action}");
                    break;

                case CombatEvent.StatusEffect s:
                    if (GetIconImage(s.Icon) is { } sIcon) InlineIcon(sIcon);
                    ImGui.TextColored(ColorNeutral, $"{s.Status} ({s.Duration:F0}s)");
                    break;

                default:
                    ImGui.Text(evt.GetType().Name);
                    break;
            }
        }

        private void DrawOriginalRightPane()
        {
            if (ActiveDeathReplay != null)
            {
                ImGui.SameLine();
                if (ImGui.Button(isPlaybackActive ? "Pause" : "Play"))
                {
                    isPlaybackActive = !isPlaybackActive;
                    if (isPlaybackActive && replayTimeOffset >= 0f) replayTimeOffset = -20f;
                }
                ImGui.SameLine();

                if (ImGuiComponents.IconButton("##ZoomOut", FontAwesomeIcon.SearchMinus))
                    canvasZoom = Math.Clamp(canvasZoom - 0.25f, 0.1f, 5.0f);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Zoom Out");

                ImGui.SameLine();
                if (ImGuiComponents.IconButton("##ZoomIn", FontAwesomeIcon.SearchPlus))
                    canvasZoom = Math.Clamp(canvasZoom + 0.25f, 0.1f, 5.0f);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Zoom In");

                ImGui.SameLine();
                if (ImGuiComponents.IconButton("##ResetView", FontAwesomeIcon.CompressArrowsAlt))
                {
                    canvasZoom = 1.0f;
                    canvasPanOffset = Vector2.Zero;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Reset View");

                ImGui.SameLine();

                DrawTimeline();

                ImGui.SameLine();

                if (ImGuiComponents.IconButton("OpenAbout", FontAwesomeIcon.InfoCircle))
                {
                    plugin.AboutWindow.IsOpen = !plugin.AboutWindow.IsOpen;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("About Aether Blackbox");

                ImGui.SameLine();

                if (ImGuiComponents.IconButton("OpenConfig", FontAwesomeIcon.Cog))
                {
                    plugin.RecapConfigWindow.IsOpen = !plugin.RecapConfigWindow.IsOpen;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open Aether Blackbox Settings");

                ImGui.SameLine();

                if (ImGuiComponents.IconButton("ReplaySettings", FontAwesomeIcon.Eye))
                    ImGui.OpenPopup("replay_settings_popup");

                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Replay Visibility Settings");

                if (ImGui.BeginPopup("replay_settings_popup"))
                {
                    ImGui.TextUnformatted("Replay Visualization");
                    ImGui.Separator();

                    var showNpcs = configuration.ShowReplayNpcs;
                    if (ImGui.Checkbox("Show NPCs/Objects", ref showNpcs))
                    {
                        configuration.ShowReplayNpcs = showNpcs;
                        configuration.Save();
                    }

                    var showHp = configuration.ShowReplayHp;
                    if (ImGui.Checkbox("Show HP Bars", ref showHp))
                    {
                        configuration.ShowReplayHp = showHp;
                        configuration.Save();
                    }

                    ImGui.EndPopup();
                }

                if (isPlaybackActive)
                {
                    replayTimeOffset += ImGui.GetIO().DeltaTime;
                    if (replayTimeOffset >= 0f) { replayTimeOffset = 0f; isPlaybackActive = false; }
                }
            }

            float selectionInfoHeight = (selectedEntityId != 0 && ActiveDeathReplay != null) ? (140f * ImGuiHelpers.GlobalScale) : (ImGui.GetStyle().WindowPadding.Y * 2);
            float canvasAvailableHeight = ImGui.GetContentRegionAvail().Y - selectionInfoHeight;
            canvasAvailableHeight = Math.Max(canvasAvailableHeight, 50f * ImGuiHelpers.GlobalScale);

            if (ImGui.BeginChild("CanvasDrawingArea", new Vector2(0, canvasAvailableHeight), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                currentCanvasDrawSize = ImGui.GetContentRegionAvail();
                DrawCanvas();
                ImGui.EndChild();
            }

            if (selectedEntityId != 0 && ActiveDeathReplay != null)
            {
                if (ImGui.BeginChild("SelectionInfoArea", new Vector2(0, selectionInfoHeight), false, ImGuiWindowFlags.NoScrollbar))
                {
                    DrawSelectionInfo();
                    ImGui.EndChild();
                }
            }
        }

        private void DrawSelectionInfo()
        {
            if (ActiveDeathReplay == null || ActiveDeathReplay.ReplayData.Frames.Count == 0) return;

            var recording = ActiveDeathReplay.ReplayData;
            var targetOffset = recording.Frames.Last().TimeOffset + replayTimeOffset;
            var closestFrame = recording.Frames.MinBy(f => Math.Abs(f.TimeOffset - targetOffset));
            if (closestFrame == null) return;

            int idx = closestFrame.Ids.IndexOf((uint)selectedEntityId);
            if (idx == -1) return;

            if (!recording.Metadata.TryGetValue((uint)selectedEntityId, out var meta)) return;

            ImGui.Separator();

            if (ImGui.BeginTable("SelectionInfoTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Self", ImGuiTableColumnFlags.WidthStretch, 2f);
                ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                string displayName = meta.Name;
                if (configuration.AnonymizeNames)
                {
                    var jobRow = Service.DataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(meta.ClassJobId);
                    displayName = jobRow.HasValue ? jobRow.Value.Abbreviation.ToString() : "Job";
                }
                ImGui.Text($"{displayName}");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), $"(HP: {closestFrame.Hp[idx]} / {meta.MaxHp})");
                ImGui.SameLine(250);

                uint upcoming = GetActionInRange(targetOffset, 0.1f, 1.5f);
                ImGui.Text("Next:"); ImGui.SameLine();
                DrawActionIconSmall(upcoming, 0.5f);

                ImGui.SameLine();

                uint current = GetActionInRange(targetOffset, -2.0f, 0.1f);
                ImGui.Text("Used:"); ImGui.SameLine();
                DrawActionIconSmall(current, 1.0f);

                if (closestFrame.Statuses != null && idx < closestFrame.Statuses.Count && closestFrame.Statuses[idx].Count > 0)
                {
                    foreach (var status in closestFrame.Statuses[idx])
                    {
                        var sheetStatus = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>().GetRowOrDefault(status.Id);
                        if (sheetStatus == null) continue;

                        var icon = Service.TextureProvider.GetFromGameIcon(sheetStatus.Value.Icon).GetWrapOrDefault();
                        if (icon != null)
                        {
                            ImGui.Image(icon.Handle, new Vector2(24, 24) * ImGuiHelpers.GlobalScale);
                            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{sheetStatus.Value.Name}\n{status.Duration:F0}s");
                            ImGui.SameLine();
                        }
                    }
                    ImGui.NewLine();
                }

                bool isCasting = false;
                if (closestFrame.Casts != null && idx < closestFrame.Casts.Count)
                {
                    var cast = closestFrame.Casts[idx];
                    if (cast.ActionId != 0 && cast.Total > 0)
                    {
                        isCasting = true;
                        var action = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(cast.ActionId);
                        string actionName = action?.Name.ToString() ?? "Unknown";
                        float pct = Math.Clamp(cast.Current / cast.Total, 0f, 1f);

                        ImGui.AlignTextToFramePadding();
                        ImGui.Text($"{actionName}:");
                        ImGui.SameLine();

                        ImGui.ProgressBar(pct, new Vector2(ImGui.GetContentRegionAvail().X, 15 * ImGuiHelpers.GlobalScale), $"{cast.Current:F1}s");
                    }
                }
                if (!isCasting)
                {
                    float height = Math.Max(ImGui.GetTextLineHeight(), 15 * ImGuiHelpers.GlobalScale);
                    ImGui.Dummy(new Vector2(1, height));
                }
                ImGui.TableSetColumnIndex(1);

                if (closestFrame.Targets != null && idx < closestFrame.Targets.Count)
                {
                    ulong targetId = closestFrame.Targets[idx];
                    if (targetId != 0 && targetId != 0xE0000000)
                    {
                        int targetIdx = closestFrame.Ids.IndexOf((uint)targetId);
                        if (targetIdx != -1 && recording.Metadata.TryGetValue((uint)targetId, out var targetMeta))
                        {
                            ImGui.TextDisabled("Targeting:");
                            string targetDisplayName = targetMeta.Name;
                            if (configuration.AnonymizeNames)
                            {
                                var jobRow = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>()
                                    .GetRowOrDefault(targetMeta.ClassJobId);
                                targetDisplayName = jobRow.HasValue ? jobRow.Value.Abbreviation.ToString() : "Job";
                            }
                            ImGui.Text($"{targetDisplayName}");

                            float targetHpPct = (float)closestFrame.Hp[targetIdx] / targetMeta.MaxHp;
                            ImGui.ProgressBar(targetHpPct, new Vector2(ImGui.GetContentRegionAvail().X, 15 * ImGuiHelpers.GlobalScale), $"{targetHpPct * 100:F1}%");
                        }
                        else
                        {
                            ImGui.TextDisabled("Targeting:");
                            ImGui.Text("Unknown Entity");
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("No Target");
                    }
                }

                ImGui.EndTable();
            }
        }

        private void DrawCanvas()
        {
            Vector2 canvasSizeForImGuiDrawing = ImGui.GetContentRegionAvail();
            currentCanvasDrawSize = canvasSizeForImGuiDrawing;
            if (canvasSizeForImGuiDrawing.X < 50f * ImGuiHelpers.GlobalScale) canvasSizeForImGuiDrawing.X = 50f * ImGuiHelpers.GlobalScale;
            if (canvasSizeForImGuiDrawing.Y < 50f * ImGuiHelpers.GlobalScale) canvasSizeForImGuiDrawing.Y = 50f * ImGuiHelpers.GlobalScale;
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            Vector2 canvasOriginScreen = ImGui.GetCursorScreenPos();

            ReplayFrame? closestFrame = null;
            ReplayRecording? recording = null;
            Vector3 centerPos = new Vector3(100, 0, 100);

            if (isReplayMode && ActiveDeathReplay != null && ActiveDeathReplay.ReplayData.Frames.Count > 0)
            {
                recording = ActiveDeathReplay.ReplayData;
                var targetOffset = recording.Frames.Last().TimeOffset + replayTimeOffset;
                closestFrame = recording.Frames.MinBy(f => Math.Abs(f.TimeOffset - targetOffset));

                if (cachedArenaCenter == null && recording.Frames.Count > 0 && recording.Frames[0].Ids.Count > 0)
                {
                    var firstFrame = recording.Frames[0];
                    Vector3 sum = Vector3.Zero;
                    for (int i = 0; i < firstFrame.Ids.Count; i++) sum += new Vector3(firstFrame.X[i], 0, firstFrame.Z[i]);
                    cachedArenaCenter = sum / firstFrame.Ids.Count;
                }
                centerPos = cachedArenaCenter ?? new Vector3(100, 0, 100);
            }

            ImGui.SetCursorScreenPos(canvasOriginScreen);

            ImGui.InvisibleButton("##CanvasInput", canvasSizeForImGuiDrawing);

            if (ImGui.IsItemHovered())
            {
                float wheel = ImGui.GetIO().MouseWheel;
                if (wheel != 0) canvasZoom = Math.Clamp(canvasZoom + (wheel * 0.1f), 0.1f, 5.0f);
            }

            if (ImGui.IsItemActive())
            {
                // Pan (Drag)
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                {
                    canvasPanOffset += ImGui.GetIO().MouseDelta;
                }
                // Click (Select) - Only if NOT dragging
                else if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && closestFrame != null && recording != null)
                {
                    selectedEntityId = 0;
                    float bestDist = float.MaxValue;

                    var effectiveCanvasCenter = (canvasOriginScreen + currentCanvasDrawSize / 2) + canvasPanOffset;
                    float effectiveScale = 8f * ImGuiHelpers.GlobalScale * canvasZoom;

                    for (int i = 0; i < closestFrame.Ids.Count; i++)
                    {
                        if (i >= closestFrame.X.Count || i >= closestFrame.Z.Count) continue;
                        var entityPos = new Vector3(closestFrame.X[i], 0, closestFrame.Z[i]);
                        var relPos = entityPos - centerPos;
                        var screenX = effectiveCanvasCenter.X + (relPos.X * effectiveScale);
                        var screenY = effectiveCanvasCenter.Y + (relPos.Z * effectiveScale);

                        float dist = Vector2.Distance(ImGui.GetMousePos(), new Vector2(screenX, screenY));
                        if (dist < 25f * ImGuiHelpers.GlobalScale * canvasZoom && dist < bestDist)
                        {
                            bestDist = dist;
                            selectedEntityId = closestFrame.Ids[i];
                        }
                    }
                }
            }

            drawList.AddRectFilled(canvasOriginScreen, canvasOriginScreen + canvasSizeForImGuiDrawing, ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.17f, 1.0f)));
            if (configuration.IsGridVisible)
            {
                float scaledGridCellSize = configuration.GridSize * ImGuiHelpers.GlobalScale;
                if (scaledGridCellSize > 2)
                {
                    var gridColor = ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1.0f));
                    for (float x = scaledGridCellSize; x < canvasSizeForImGuiDrawing.X; x += scaledGridCellSize)
                        drawList.AddLine(new Vector2(canvasOriginScreen.X + x, canvasOriginScreen.Y), new Vector2(canvasOriginScreen.X + x, canvasOriginScreen.Y + canvasSizeForImGuiDrawing.Y), gridColor, Math.Max(1f, 1.0f * ImGuiHelpers.GlobalScale));
                    for (float y = scaledGridCellSize; y < canvasSizeForImGuiDrawing.Y; y += scaledGridCellSize)
                        drawList.AddLine(new Vector2(canvasOriginScreen.X, canvasOriginScreen.Y + y), new Vector2(canvasOriginScreen.X + canvasSizeForImGuiDrawing.X, canvasOriginScreen.Y + y), gridColor, Math.Max(1f, 1.0f * ImGuiHelpers.GlobalScale));
                }
            }
            drawList.AddRect(canvasOriginScreen - Vector2.One, canvasOriginScreen + canvasSizeForImGuiDrawing + Vector2.One, ImGui.GetColorU32(new Vector4(0.4f, 0.4f, 0.45f, 1f)), 0f, ImDrawFlags.None, Math.Max(1f, 1.0f * ImGuiHelpers.GlobalScale));

            if (closestFrame != null && recording != null)
            {
                replayRenderer.Draw(
                    drawList,
                    closestFrame,
                    recording.Metadata,
                    recording.Waymarks,
                    canvasOriginScreen,
                    currentCanvasDrawSize,
                    centerPos,
                    ActiveDeathReplay.TerritoryTypeId,
                    plugin.Configuration.ShowReplayNpcs,
                    plugin.Configuration.ShowReplayHp,
                    plugin.Configuration.AnonymizeNames,
                    canvasZoom,
                    canvasPanOffset
                );

                // Draw Selection Circle
                if (selectedEntityId != 0)
                {
                    int selIdx = closestFrame.Ids.IndexOf((uint)selectedEntityId);
                    if (selIdx != -1)
                    {
                        var entityPos = new Vector3(closestFrame.X[selIdx], 0, closestFrame.Z[selIdx]);
                        var relPos = entityPos - centerPos;
                        var canvasCenter = (canvasOriginScreen + currentCanvasDrawSize / 2) + canvasPanOffset;
                        float scale = 8f * ImGuiHelpers.GlobalScale * canvasZoom;

                        var screenX = canvasCenter.X + (relPos.X * scale);
                        var screenY = canvasCenter.Y + (relPos.Z * scale);

                        drawList.AddCircle(new Vector2(screenX, screenY), 22f * ImGuiHelpers.GlobalScale, 0xFF00D7FF, 0, 3f);
                    }
                }
            }

            // User Drawings
            ImGui.PushClipRect(canvasOriginScreen, canvasOriginScreen + canvasSizeForImGuiDrawing, true);
            var drawablesSnapshot = pageManager.GetCurrentPageDrawables()?.ToList();
            if (drawablesSnapshot != null && drawablesSnapshot.Any())
            {
                foreach (var drawable in drawablesSnapshot) drawable.Draw(drawList, canvasOriginScreen);
            }
            canvasController.GetCurrentDrawingObjectForPreview()?.Draw(drawList, canvasOriginScreen);
            ImGui.PopClipRect();
        }

        private int GetLayerPriority(DrawMode mode)
        {
            return mode switch
            {
                DrawMode.TextTool => 10,
                DrawMode.EmojiImage => 6,
                DrawMode.Image => 0,
                DrawMode.Waymark1Image or DrawMode.Waymark2Image or DrawMode.Waymark3Image or DrawMode.Waymark4Image or DrawMode.WaymarkAImage or DrawMode.WaymarkBImage or DrawMode.WaymarkCImage or DrawMode.WaymarkDImage or DrawMode.RoleTankImage or DrawMode.RoleHealerImage or DrawMode.RoleMeleeImage or DrawMode.RoleRangedImage or DrawMode.Party1Image or DrawMode.Party2Image or DrawMode.Party3Image or DrawMode.Party4Image or DrawMode.Party5Image or DrawMode.Party6Image or DrawMode.Party7Image or DrawMode.Party8Image or DrawMode.SquareImage or DrawMode.CircleMarkImage or DrawMode.TriangleImage or DrawMode.PlusImage or DrawMode.StackIcon or DrawMode.SpreadIcon or DrawMode.TetherIcon or DrawMode.BossIconPlaceholder or DrawMode.AddMobIcon or DrawMode.Dot1Image or DrawMode.Dot2Image or DrawMode.Dot3Image or DrawMode.Dot4Image or DrawMode.Dot5Image or DrawMode.Dot6Image or DrawMode.Dot7Image or DrawMode.Dot8Image => 5,
                DrawMode.BossImage or DrawMode.CircleAoEImage or DrawMode.DonutAoEImage or DrawMode.FlareImage or DrawMode.LineStackImage or DrawMode.SpreadImage or DrawMode.StackImage => 3,
                DrawMode.Pen or DrawMode.StraightLine or DrawMode.Rectangle or DrawMode.Circle or DrawMode.Arrow or DrawMode.Cone or DrawMode.Dash or DrawMode.Donut => 2,
                _ => 1,
            };
        }
        private void DrawTimeline()
        {
            if (ActiveDeathReplay == null) return;

            var drawList = ImGui.GetWindowDrawList();
            var cursor = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X - (140 * ImGuiHelpers.GlobalScale);
            var height = ImGui.GetFrameHeight();

            float minTime = -20f;
            float maxTime = 5f;
            float totalRange = maxTime - minTime;

            drawList.AddRectFilled(cursor, cursor + new Vector2(width, height), ImGui.GetColorU32(ImGuiCol.FrameBg), 4f);

            ImGui.InvisibleButton("##TimelineScrubber", new Vector2(width, height));
            if (ImGui.IsItemActive())
            {
                isPlaybackActive = false;
                float mouseRatio = Math.Clamp((ImGui.GetMousePos().X - cursor.X) / width, 0f, 1f);
                replayTimeOffset = minTime + (mouseRatio * totalRange);
            }

            foreach (var evt in ActiveDeathReplay.Events)
            {
                float relativeTime = (float)(evt.Snapshot.Time - ActiveDeathReplay.TimeOfDeath).TotalSeconds;
                if (relativeTime < minTime || relativeTime > maxTime) continue;

                float ratio = (relativeTime - minTime) / totalRange;
                float x = cursor.X + (ratio * width);

                uint color = 0x50FFFFFF;
                if (evt is CombatEvent.DamageTaken) color = 0xA05050FF;
                else if (evt is CombatEvent.Healed) color = 0xA050FF50;

                drawList.AddLine(new Vector2(x, cursor.Y), new Vector2(x, cursor.Y + height), color, 1f);
            }

            float currentRatio = Math.Clamp((replayTimeOffset - minTime) / totalRange, 0f, 1f);
            float playheadX = cursor.X + (currentRatio * width);

            drawList.AddLine(new Vector2(playheadX, cursor.Y), new Vector2(playheadX, cursor.Y + height), 0xFFFFFFFF, 2f);
            drawList.AddCircleFilled(new Vector2(playheadX, cursor.Y + height / 2), 4f, 0xFFFFFFFF);

            string timeText = $"{replayTimeOffset:F1}s";
            drawList.AddText(cursor + new Vector2(5, 2), 0xFFFFFFFF, timeText);
        }

        private static void InlineIcon(IDalamudTextureWrap img)
        {
            float s = ImGui.GetTextLineHeight();
            ImGui.Image(img.Handle, new Vector2(s, s));
            ImGui.SameLine();
        }

        private static IDalamudTextureWrap? GetIconImage(uint? icon, uint stackCount = 0)
        {
            if (icon is not { } idx) return null;
            if (stackCount > 1) idx += stackCount - 1;
            return Service.TextureProvider.TryGetIconPath(idx, out var path)
                ? Service.TextureProvider.GetFromGame(path).GetWrapOrDefault()
                : null;
        }
        private string GetAnonymizedName(string name, ReplayRecording? recording = null)
        {
            if (!configuration.AnonymizeNames) return name;

            var data = recording ?? ActiveDeathReplay?.ReplayData;

            if (data != null && !string.IsNullOrEmpty(name))
            {
                var meta = data.Metadata.Values.FirstOrDefault(m => m.Name == name);
                if (meta != null && meta.ClassJobId != 0)
                {
                    var jobSheet = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.ClassJob>();
                    if (jobSheet != null)
                    {
                        var job = jobSheet.GetRowOrDefault(meta.ClassJobId);
                        if (job.HasValue) return job.Value.Abbreviation.ToString();
                    }
                }
            }
            return name;
        }
        private uint GetActionInRange(float currentTime, float startOffset, float endOffset)
        {
            if (ActiveDeathReplay == null) return 0;
            var recording = ActiveDeathReplay.ReplayData;

            foreach (var f in recording.Frames)
            {
                if (f.TimeOffset >= currentTime + startOffset && f.TimeOffset <= currentTime + endOffset)
                {
                    int idx = f.Ids.IndexOf((uint)selectedEntityId);
                    if (idx != -1 && f.Actions != null && f.Actions.Count > idx && f.Actions[idx] != 0)
                        return f.Actions[idx];
                }
            }
            return 0;
        }

        private void DrawActionIconSmall(uint actionId, float alpha)
        {
            if (actionId == 0) { ImGui.Dummy(new Vector2(24, 24) * ImGuiHelpers.GlobalScale); return; }

            var action = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(actionId);
            if (!action.HasValue) return;

            var iconWrap = TextureManager.GetTexture($"luminaicon:{action.Value.Icon}");
            if (iconWrap != null)
            {
                ImGui.Image(iconWrap.Handle, new Vector2(24, 24) * ImGuiHelpers.GlobalScale, Vector2.Zero, Vector2.One, new Vector4(1, 1, 1, alpha));
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(action.Value.Name.ToString());
            }
        }

    }
}