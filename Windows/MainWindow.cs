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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AetherBlackbox.Windows
{
    public partial class MainWindow : Window, IDisposable
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

        private readonly object captureLock = new();
        public PlanExportManager ExportManager { get; } = new();
        public RoleTranslator RoleTranslator { get; } = new();
        private bool isExportPreviewOpen = false;
        private Dictionary<SlideSnapshot, IDalamudTextureWrap> thumbnailCache = new();
        private Dictionary<SlideSnapshot, System.Threading.Tasks.Task<IDalamudTextureWrap>> thumbnailTasks = new();

        private ReplayFrame? GetClosestFrame(ReplayRecording recording, float targetOffset)
        {
            if (recording.Frames.Count == 0) return null;
            int left = 0, right = recording.Frames.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (recording.Frames[mid].TimeOffset == targetOffset) return recording.Frames[mid];
                if (recording.Frames[mid].TimeOffset < targetOffset) left = mid + 1;
                else right = mid - 1;
            }
            if (left >= recording.Frames.Count) return recording.Frames[right];
            if (right < 0) return recording.Frames[left];
            return Math.Abs(recording.Frames[left].TimeOffset - targetOffset) < Math.Abs(recording.Frames[right].TimeOffset - targetOffset) ? recording.Frames[left] : recording.Frames[right];
        }

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

        public bool IsDrawingMode { get; set; } = false;
        public DrawMode CurrentDrawMode { get => currentDrawMode; set { currentDrawMode = value; IsDrawingMode = true; } }
        public Vector4 CurrentBrushColor { get => currentBrushColor; set => currentBrushColor = value; }
        public float CurrentBrushThickness { get => currentBrushThickness; set => currentBrushThickness = value; }
        public bool CurrentShapeFilled { get => currentShapeFilled; set => currentShapeFilled = value; }

        private DrawMode currentDrawMode = DrawMode.Pen;
        private Vector4 currentBrushColor;
        private float currentBrushThickness;
        private bool currentShapeFilled = false;
        private Vector2 currentCanvasDrawSize;

        private bool isLaserMode = false;
        public void PerformUndo() => canvasController.Undo();
        private DrawableLaser? currentLaser = null;

        private List<Lumina.Excel.Sheets.Status> statusSearchResults = new();

        public bool isNetworkHost { get; private set; } = false;
        private float lastTimeSyncBroadcast = 0f;
        private float lastLaserBroadcast = 0f;
        private List<DrawableLaser> remoteLasers = new();

        private Dictionary<string, float> userMarkers = new();
        private Dictionary<string, (float Time, int TotalViewers)> activePings = new();
        private HashSet<string> connectedUsers = new();
        private string? syncTarget = null;
        private float lastPingTime = 0f;

        private readonly uint[] Palette = new uint[] { 0xFFFFB358, 0xFF727BFF, 0xFFB4F5AF, 0xFF2299D2, 0xFFFF8CBC, 0xFF57A6FF };
        private uint GetUserColor(string id)
        {
            int hash = 0;
            foreach (char c in id) hash += c;
            return Palette[Math.Abs(hash) % Palette.Length];
        }

        private PullSession? selectedPull;
        private const float SidebarWidth = 350f;
        private const ulong EmptyTargetID = 0xE0000000;

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
        public CanvasController CanvasController => canvasController;

        public MainWindow(Plugin plugin, string id = "") : base($"Aether Blackbox v{typeof(Plugin).Assembly.GetName().Version} - Powered by Death Recap by Kouzukii and AetherDraw by rail{id}###Aether Blackbox MainWindow{id}")
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

            this.plugin.NetworkManager.OnHostStatusReceived += OnHostStatusReceived;
            this.plugin.NetworkManager.OnStateUpdateReceived += OnStateUpdateReceived;
            this.plugin.NetworkManager.OnUserTimeReceived += OnUserTimeReceived;
            this.plugin.NetworkManager.OnUserPingReceived += OnUserPingReceived;
            this.plugin.NetworkManager.OnUserJoined += OnUserJoined;
            this.plugin.NetworkManager.OnUserLeft += OnUserLeft;
        }

        private void OnUserTimeReceived(string senderId, float time) => userMarkers[senderId] = time;
        private void OnUserPingReceived(string senderId, float time) => activePings[senderId] = (time, connectedUsers.Count + 1);
        private void OnUserJoined(string senderId) => connectedUsers.Add(senderId);
        private void OnUserLeft(string senderId)
        {
            connectedUsers.Remove(senderId);
            userMarkers.Remove(senderId);
            activePings.Remove(senderId);
            if (syncTarget == senderId) syncTarget = null;
        }

        private void OnHostStatusReceived(bool status)
        {
            this.isNetworkHost = status;
        }
        private void OnStateUpdateReceived(Networking.NetworkPayload payload)
        {
            if (payload.Action == Networking.PayloadActionType.EncounterSync)
            {
                var (territoryType, pullNumber, activeDeathId) = Serialization.PayloadSerializer.DeserializeEncounterSync(payload.Data!);
                if (selectedPull?.PullNumber != pullNumber || (ActiveDeathReplay != null && (ulong)ActiveDeathReplay.TimeOfDeath.Ticks != activeDeathId))
                {
                    var newPull = plugin.PullManager.History.FirstOrDefault(p => p.PullNumber == pullNumber);
                    if (newPull != null)
                    {
                        var targetDeath = newPull.Deaths.FirstOrDefault(d => (ulong)d.TimeOfDeath.Ticks == activeDeathId) ?? newPull.Deaths.FirstOrDefault();
                        if (targetDeath != null) OpenReplay(targetDeath);
                    }
                }
            }
            else if (payload.Action == Networking.PayloadActionType.TimeSync)
            { }
            else if (payload.Action == Networking.PayloadActionType.DrawLaser)
            {
                Service.PluginLog.Info($"[LASER IN] Received laser payload. Total data bytes: {payload.Data?.Length ?? 0}");
                var (mapPoints, color) = Serialization.PayloadSerializer.DeserializeDrawLaser(payload.Data!);

                var effectiveCanvasCenter = (currentCanvasDrawSize / 2) + canvasPanOffset;
                float effectiveScale = 8f * ImGuiHelpers.GlobalScale * canvasZoom;

                var screenPoints = mapPoints.Select(p => new Vector2(
                    effectiveCanvasCenter.X + (p.X * effectiveScale),
                    effectiveCanvasCenter.Y + (p.Y * effectiveScale)
                )).ToList();

                remoteLasers.Add(new DrawableLaser(screenPoints, color, 2f));
            }
        }

        private float GetDeathTimeOffset()
        {
            if (ActiveDeathReplay == null) return 0f;
            if (selectedPull != null) return (float)(ActiveDeathReplay.TimeOfDeath - selectedPull.StartTime).TotalSeconds;
            if (ActiveDeathReplay.ReplayData.Frames.Count > 0) return ActiveDeathReplay.ReplayData.Frames.Last().TimeOffset;
            return 0f;
        }

        private void BroadcastTimeSync()
        {
            if (!plugin.NetworkManager.IsConnected) return;
            float absTime = GetDeathTimeOffset() + replayTimeOffset;
            _ = plugin.NetworkManager.BroadcastUserTimeAsync(absTime);
        }
        private void BroadcastLaser()
        {
            if (currentLaser == null || !plugin.NetworkManager.IsConnected) return;
            var screenPoints = currentLaser.GetPoints();
            if (screenPoints.Count == 0) return;

            var effectiveCanvasCenter = (currentCanvasDrawSize / 2) + canvasPanOffset;
            float effectiveScale = 8f * ImGuiHelpers.GlobalScale * canvasZoom;

            var mapPoints = screenPoints.Select(p => new Vector2(
                (p.X - effectiveCanvasCenter.X) / effectiveScale,
                (p.Y - effectiveCanvasCenter.Y) / effectiveScale
            )).ToList();

            var payload = new Networking.NetworkPayload
            {
                Action = Networking.PayloadActionType.DrawLaser,
                Data = Serialization.PayloadSerializer.SerializeDrawLaser(mapPoints, new Vector4(0.718f, 0.973f, 0.718f, 1.0f))
            };
            _ = plugin.NetworkManager.SendStateUpdateAsync(payload);
        }

        public void Dispose()
        {
            this.plugin.NetworkManager.OnHostStatusReceived -= OnHostStatusReceived;
            this.plugin.NetworkManager.OnStateUpdateReceived -= OnStateUpdateReceived;
            this.plugin.NetworkManager.OnUserTimeReceived -= OnUserTimeReceived;
            this.plugin.NetworkManager.OnUserPingReceived -= OnUserPingReceived;
            this.plugin.NetworkManager.OnUserJoined -= OnUserJoined;
            this.plugin.NetworkManager.OnUserLeft -= OnUserLeft;
        }

        public void OpenReplay(Death death)
        {
            selectedPull = plugin.PullManager.History.FirstOrDefault(p => p.Deaths.Contains(death));
            ActiveDeathReplay = death;
            isReplayMode = true;
            replayTimeOffset = 0f;
            activePings.Clear();
            syncTarget = null;
            isPlaybackActive = false;
            cachedArenaCenter = null;
            IsOpen = true;

            if (plugin.NetworkManager.IsConnected && isNetworkHost && selectedPull != null)
            {
                var payload = new Networking.NetworkPayload
                {
                    Action = Networking.PayloadActionType.EncounterSync,
                    Data = Serialization.PayloadSerializer.SerializeEncounterSync(death.TerritoryTypeId, (int)selectedPull.PullNumber, (ulong)death.TimeOfDeath.Ticks)
                };
                _ = plugin.NetworkManager.SendStateUpdateAsync(payload);
            }
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

            this.previousSelectionCount = this.selectedDrawables.Count;
        }

        private void DrawOriginalRightPane()
        {
            if (ActiveDeathReplay != null)
            {
                if (plugin.NetworkManager.IsConnected)
                {
                    if (isNetworkHost)
                        ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), "Live: HOST");
                    else
                        ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.2f, 1.0f), "Live: VIEWER");
                    ImGui.SameLine();
                }

                bool isHostOrOffline = isNetworkHost || !plugin.NetworkManager.IsConnected;
                float currentDeathTime = GetDeathTimeOffset();
                float timelineMin = -currentDeathTime < -20f ? -currentDeathTime : -20f;

                if (ImGui.Button(isPlaybackActive ? "Pause" : "Play"))
                {
                    syncTarget = null;
                    isPlaybackActive = !isPlaybackActive;
                    if (isPlaybackActive && replayTimeOffset >= 5f) replayTimeOffset = timelineMin;
                }
                ImGui.SameLine();
                if (ImGui.Button("Ping View"))
                {
                    var now = (float)ImGui.GetTime();
                    if (now - lastPingTime > 5.0f)
                    {
                        lastPingTime = now;
                        float absTime = GetDeathTimeOffset() + replayTimeOffset;
                        _ = plugin.NetworkManager.BroadcastUserPingAsync(absTime);
                        activePings[plugin.NetworkManager.LocalClientId] = (absTime, connectedUsers.Count + 1);
                    }
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Share your timeline position");

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
                if (ImGuiComponents.IconButton("OpenAbout", FontAwesomeIcon.InfoCircle))
                    plugin.AboutWindow.IsOpen = !plugin.AboutWindow.IsOpen;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("About Aether Blackbox");

                ImGui.SameLine();
                if (ImGuiComponents.IconButton("OpenConfig", FontAwesomeIcon.Cog))
                    plugin.RecapConfigWindow.IsOpen = !plugin.RecapConfigWindow.IsOpen;
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

                ImGui.Spacing();
                ImGui.Dummy(new Vector2(0, 15f * ImGuiHelpers.GlobalScale));
                DrawTimeline();
                ImGui.Dummy(new Vector2(0, 10f * ImGuiHelpers.GlobalScale));

                if (syncTarget != null)
                {
                    if (userMarkers.TryGetValue(syncTarget, out float targetAbsTime))
                    {
                        replayTimeOffset = targetAbsTime - GetDeathTimeOffset();
                    }
                }
                else if (isPlaybackActive)
                {
                    replayTimeOffset += ImGui.GetIO().DeltaTime;
                    if (replayTimeOffset >= 5f)
                    {
                        replayTimeOffset = 5f;
                        isPlaybackActive = false;
                    }
                    lastTimeSyncBroadcast += ImGui.GetIO().DeltaTime;
                    if (lastTimeSyncBroadcast >= 0.5f)
                    {
                        BroadcastTimeSync();
                        lastTimeSyncBroadcast = 0f;
                    }
                }

                if (ActiveDeathReplay.TerritoryTypeId == 992 || ActiveDeathReplay.TerritoryTypeId == 1321 ||
                    ActiveDeathReplay.TerritoryTypeId == 1323 || ActiveDeathReplay.TerritoryTypeId == 1325 ||
                    ActiveDeathReplay.TerritoryTypeId == 1327)
                {
                    DrawMapCalibrationPanel();
                }
            }

            float selectionInfoHeight = (selectedEntityId != 0 && ActiveDeathReplay != null) ? (140f * ImGuiHelpers.GlobalScale) : (ImGui.GetStyle().WindowPadding.Y * 2);

            if (ImGui.BeginChild("CanvasDrawingArea", new Vector2(0, -selectionInfoHeight), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                var canvasStartPos = ImGui.GetCursorPos();
                currentCanvasDrawSize = ImGui.GetContentRegionAvail();
                if (currentCanvasDrawSize.X > 0 && currentCanvasDrawSize.Y > 0)
                {
                    DrawCanvas();

                    ImGui.SetCursorPos(canvasStartPos + new Vector2(10 * ImGuiHelpers.GlobalScale, 10 * ImGuiHelpers.GlobalScale));

                    bool isToolbarVisible = configuration.IsToolbarVisible;
                    float childHeight = isToolbarVisible ? (currentCanvasDrawSize.Y - 20 * ImGuiHelpers.GlobalScale) : (35 * ImGuiHelpers.GlobalScale);

                    ImGui.PushStyleColor(ImGuiCol.ChildBg, isToolbarVisible ? new Vector4(0.12f, 0.12f, 0.14f, 0.95f) : new Vector4(0, 0, 0, 0));

                    if (ImGui.BeginChild("ToolbarContainer", new Vector2(115 * ImGuiHelpers.GlobalScale, childHeight), isToolbarVisible, ImGuiWindowFlags.NoScrollbar))
                    {
                        if (ImGui.Button(isToolbarVisible ? "<< Close" : "Draw >>", new Vector2(-1, 0)))
                        {
                            configuration.IsToolbarVisible = !isToolbarVisible;
                            configuration.Save();

                            if (!configuration.IsToolbarVisible)
                            {
                                IsDrawingMode = false;
                            }
                        }

                        if (isToolbarVisible)
                        {
                            ImGui.Dummy(new Vector2(0, 5 * ImGuiHelpers.GlobalScale));
                            plugin.ToolbarWindow.Draw();
                        }
                    }
                    ImGui.EndChild();
                    ImGui.PopStyleColor();

                    ImGui.SetCursorPos(canvasStartPos + new Vector2(currentCanvasDrawSize.X - 310 * ImGuiHelpers.GlobalScale, 10 * ImGuiHelpers.GlobalScale));
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.12f, 0.12f, 0.14f, 0.95f));
                    if (ImGui.BeginChild("ExportContainer", new Vector2(300 * ImGuiHelpers.GlobalScale, isExportPreviewOpen ? 400 * ImGuiHelpers.GlobalScale : 35 * ImGuiHelpers.GlobalScale), true, ImGuiWindowFlags.NoScrollbar))
                    {
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Camera)) CaptureCurrentState();
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Capture current state as a slide");
                        ImGui.SameLine();
                        if (ImGui.Button($"Export Plan ({ExportManager.StagedSlides.Count})", new Vector2(-1, 0))) isExportPreviewOpen = !isExportPreviewOpen;

                        if (isExportPreviewOpen)
                        {
                            ImGui.Dummy(new Vector2(0, 5 * ImGuiHelpers.GlobalScale));
                            DrawExportPreviewUI();
                        }
                    }
                    ImGui.EndChild();
                    ImGui.PopStyleColor();
                }
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
            float targetOffset = 0f;

            if (isReplayMode && ActiveDeathReplay != null && ActiveDeathReplay.ReplayData.Frames.Count > 0)
            {
                recording = ActiveDeathReplay.ReplayData;
                var deathTimeOffset = selectedPull != null ? (float)(ActiveDeathReplay.TimeOfDeath - selectedPull.StartTime).TotalSeconds : recording.Frames.Last().TimeOffset;
                targetOffset = deathTimeOffset + replayTimeOffset;
                closestFrame = GetClosestFrame(recording, targetOffset);

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

            bool hovered = ImGui.IsItemHovered();
            bool active = ImGui.IsItemActive();

            if (hovered)
            {
                float wheel = ImGui.GetIO().MouseWheel;
                if (wheel != 0) canvasZoom = Math.Clamp(canvasZoom + (wheel * 0.1f), 0.1f, 5.0f);
            }

            bool isLMBDown = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            bool isLMBClicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            bool isLMBReleased = ImGui.IsMouseReleased(ImGuiMouseButton.Left);
            bool isLMBDoubleClicked = ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
            bool isRMBDragging = ImGui.IsMouseDragging(ImGuiMouseButton.Right);

            if (hovered || active || isLMBReleased)
            {
                if (isRMBDragging)
                {
                    canvasPanOffset += ImGui.GetIO().MouseDelta;
                }

                if (IsDrawingMode)
                {
                    var viewContext = new ReplayRenderer.ViewContext(canvasOriginScreen, currentCanvasDrawSize, centerPos, canvasZoom, canvasPanOffset);
                    var mousePosScreen = ImGui.GetMousePos();
                    var mousePosLogical = mousePosScreen - canvasOriginScreen;

                    if (active || isLMBReleased)
                    {
                        canvasController.ProcessCanvasInteraction(
                            mousePosLogical, mousePosScreen, canvasOriginScreen, drawList,
                            isLMBDown, isLMBClicked, isLMBReleased, isLMBDoubleClicked,
                            () => currentDrawMode, () => currentBrushColor, () => currentBrushThickness, () => currentShapeFilled,
                            viewContext, targetOffset);
                    }

                    if (currentDrawMode == DrawMode.Laser && plugin.NetworkManager.IsConnected && active)
                    {
                        lastLaserBroadcast += ImGui.GetIO().DeltaTime;
                        if (lastLaserBroadcast >= 0.05f)
                        {
                            BroadcastLaser();
                            lastLaserBroadcast = 0f;
                        }
                    }
                }
                else if (active && !isRMBDragging)
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
                    recording,
                    closestFrame,
                    targetOffset,
                    canvasOriginScreen,
                    currentCanvasDrawSize,
                    centerPos,
                    ActiveDeathReplay.TerritoryTypeId,
                    plugin.Configuration.ShowReplayNpcs,
                    plugin.Configuration.ShowReplayHp,
                    plugin.Configuration.AnonymizeNames,
                    canvasZoom,
                    canvasPanOffset,
                    plugin.Configuration
                );

                // Draw Selection Circle
                if (selectedEntityId != 0)
                {
                    int selIdx = closestFrame.Ids.IndexOf((uint)selectedEntityId);
                    if (selIdx != -1 && closestFrame.X != null && selIdx < closestFrame.X.Count && closestFrame.Z != null && selIdx < closestFrame.Z.Count)
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
                var sortedDrawables = drawablesSnapshot.OrderBy(d => GetLayerPriority(d.ObjectDrawMode));
                foreach (var drawable in sortedDrawables) drawable.Draw(drawList, canvasOriginScreen);
            }
            canvasController.GetCurrentDrawingObjectForPreview()?.Draw(drawList, canvasOriginScreen);

            if (currentLaser != null)
            {
                currentLaser.Draw(drawList, canvasOriginScreen);
                if ((DateTime.Now - currentLaser.LastUpdateTime).TotalSeconds > 1.2)
                {
                    currentLaser = null;
                }
            }
            for (int i = remoteLasers.Count - 1; i >= 0; i--)
            {
                var laser = remoteLasers[i];
                laser.Draw(drawList, canvasOriginScreen);
                if ((DateTime.Now - laser.LastUpdateTime).TotalSeconds > 1.2)
                {
                    remoteLasers.RemoveAt(i);
                }
            }

            ImGui.PopClipRect();

            // Draw the text editor UI over the canvas if active
            canvasController.inPlaceTextEditor?.DrawEditorUI();
        }

        private int GetLayerPriority(DrawMode mode)
        {
            string modeName = mode.ToString();

            if (modeName == nameof(DrawMode.TextTool)) return 10;
            if (modeName == nameof(DrawMode.EmojiImage)) return 6;
            if (modeName == nameof(DrawMode.Image)) return 0;

            if (modeName.StartsWith("Waymark") ||
                modeName.StartsWith("Role") ||
                modeName.StartsWith("Party") ||
                modeName.StartsWith("Dot") ||
                modeName.EndsWith("Icon") ||
                modeName is "SquareImage" or "CircleMarkImage" or "TriangleImage" or "PlusImage")
            {
                return 5;
            }

            if (modeName.EndsWith("AoEImage") ||
                modeName is "BossImage" or "FlareImage" or "LineStackImage" or "SpreadImage" or "StackImage")
            {
                return 3;
            }

            return mode switch
            {
                DrawMode.Pen or DrawMode.StraightLine or DrawMode.Rectangle or DrawMode.Circle or
                DrawMode.Arrow or DrawMode.Cone or DrawMode.Dash or DrawMode.Donut => 2,
                _ => 1,
            };
        }

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

        private Vector3 CalculateCenterFromWaymarks(ReplayRecording recording)
        {
            if (recording.Waymarks == null || recording.Waymarks.Count == 0) return new Vector3(100, 0, 100);
            float sumX = 0, sumZ = 0;
            foreach (var w in recording.Waymarks)
            {
                sumX += w.X;
                sumZ += w.Z;
            }
            return new Vector3(sumX / recording.Waymarks.Count, 0, sumZ / recording.Waymarks.Count);
        }

        private void CaptureCurrentState()
        {
            if (ActiveDeathReplay == null) return;

            lock (captureLock)
            {
                var recording = ActiveDeathReplay.ReplayData;
                var centerPos = CalculateCenterFromWaymarks(recording);
                var combinedDrawables = new List<BaseDrawable>();

                var currentDrawables = PageManager.GetCurrentPageDrawables();
                if (currentDrawables != null)
                {
                    combinedDrawables.AddRange(currentDrawables.Select(d => d.Clone()));
                }

                RoleTranslator.CacheRoleMapping(recording);
                var targetOffset = GetDeathTimeOffset() + replayTimeOffset;
                var currentFrame = GetClosestFrame(recording, targetOffset);

                if (currentFrame != null)
                {
                    float effectiveScale = 8f * ImGuiHelpers.GlobalScale * canvasZoom;
                    var effectiveCanvasCenter = (currentCanvasDrawSize / 2) + canvasPanOffset;

                    for (int i = 0; i < currentFrame.Ids.Count; i++)
                    {
                        if (i >= currentFrame.X.Count || i >= currentFrame.Z.Count) continue;

                        ulong entityId = currentFrame.Ids[i];
                        if (RoleTranslator.CachedRoleMap.TryGetValue(entityId, out var roleMode))
                        {
                            var relPos = new Vector3(currentFrame.X[i], 0, currentFrame.Z[i]) - centerPos;
                            float screenX = effectiveCanvasCenter.X + (relPos.X * effectiveScale);
                            float screenY = effectiveCanvasCenter.Y + (relPos.Z * effectiveScale);

                            string rolePath = roleMode switch
                            {
                                DrawMode.RoleTank1Image => "PluginImages/toolbar/tank_1.png",
                                DrawMode.RoleTank2Image => "PluginImages/toolbar/tank_2.png",
                                DrawMode.RoleHealer1Image => "PluginImages/toolbar/healer_1.png",
                                DrawMode.RoleHealer2Image => "PluginImages/toolbar/healer_2.png",
                                DrawMode.RoleMelee1Image => "PluginImages/toolbar/melee_1.png",
                                DrawMode.RoleMelee2Image => "PluginImages/toolbar/melee_2.png",
                                DrawMode.RoleRanged1Image => "PluginImages/toolbar/ranged_dps_1.png",
                                DrawMode.RoleRanged2Image => "PluginImages/toolbar/ranged_dps_2.png",
                                DrawMode.BossImage => "PluginImages/svg/boss.svg",
                                _ => "PluginImages/toolbar/StatusPlaceholder.png"
                            };

                            combinedDrawables.Add(new DrawableImage(
                                roleMode,
                                rolePath,
                                new Vector2(screenX, screenY) / ImGuiHelpers.GlobalScale,
                                new Vector2(30f, 30f),
                                new Vector4(1f, 1f, 1f, 1f)
                            ));
                        }
                    }
                }

                if (recording.Waymarks != null)
                {
                    float effectiveScale = 8f * ImGuiHelpers.GlobalScale * canvasZoom;
                    var effectiveCanvasCenter = (currentCanvasDrawSize / 2) + canvasPanOffset;

                    foreach (var wm in recording.Waymarks)
                    {
                        if (!wm.Active) continue;

                        var relPos = new Vector3(wm.X, 0, wm.Z) - centerPos;
                        float screenX = effectiveCanvasCenter.X + (relPos.X * effectiveScale);
                        float screenY = effectiveCanvasCenter.Y + (relPos.Z * effectiveScale);

                        string iconName = wm.ID switch
                        {
                            0 => "A.png",
                            1 => "B.png",
                            2 => "C.png",
                            3 => "D.png",
                            4 => "1_waymark.png",
                            5 => "2_waymark.png",
                            6 => "3_waymark.png",
                            7 => "4_waymark.png",
                            _ => "A.png"
                        };

                        combinedDrawables.Add(new DrawableImage(
                            DrawMode.Image,
                            $"PluginImages/toolbar/{iconName}",
                            new Vector2(screenX, screenY) / ImGuiHelpers.GlobalScale,
                            new Vector2(24f, 24f),
                            new Vector4(1f, 1f, 1f, 1f)
                        ));
                    }
                }

                string? fallbackImage = ActiveDeathReplay.TerritoryTypeId switch
                {
                    992 or 1321 => "m9.webp",
                    1323 => "m10.webp",
                    1325 => "m11p1.webp",
                    1327 => "m12p1.webp",
                    _ => null
                };
                string arenaBackground = fallbackImage != null ? $"PluginImages/arenas/{fallbackImage}" : $"PluginImages/arenas/{ActiveDeathReplay.TerritoryTypeId}.webp";

                System.Drawing.RectangleF bounds = System.Drawing.RectangleF.Empty;
                bool firstBound = true;

                var renderActions = new System.Collections.Generic.List<System.Action<SixLabors.ImageSharp.Processing.IImageProcessingContext, System.Numerics.Vector2, float>>();

                foreach (var d in combinedDrawables)
                {
                    try
                    {
                        if (d is AetherBlackbox.DrawingLogic.DrawableText textDrawable)
                        {
                            textDrawable.PerformLayout();
                        }
                        var capturedDrawable = d.Clone();

                        var b = capturedDrawable.GetBoundingBox();
                        if (!b.IsEmpty)
                        {
                            if (firstBound) { bounds = b; firstBound = false; }
                            else { bounds = System.Drawing.RectangleF.Union(bounds, b); }
                        }

                        renderActions.Add((ctx, offset, scale) => capturedDrawable.DrawToImage(ctx, offset, scale));
                    }
                    catch (System.Exception ex)
                    {
                        Service.PluginLog.Error(ex, $"Failed to prepare render data for: {d.GetType().Name}");
                    }
                }

                System.Threading.Tasks.Task.Run(() =>
                {
                    byte[] thumbnailBytes = System.Array.Empty<byte>();
                    try
                    {
                        using var thumb = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(120, 120);
                        thumb.Mutate(ctx =>
                        {
                            ctx.Clear(SixLabors.ImageSharp.Color.DarkGray);

                            try
                            {
                                byte[]? arenaBytes = TextureManager.GetImageData(arenaBackground);
                                if (arenaBytes != null)
                                {
                                    using var arenaImg = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(arenaBytes);
                                    arenaImg.Mutate(x => x.Resize(120, 120));
                                    ctx.DrawImage(arenaImg, new SixLabors.ImageSharp.Point(0, 0), 1f);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Service.PluginLog.Error(ex, "Failed to draw arena background to thumbnail.");
                            }

                            float padding = 10f;
                            float exportScale = 1f;
                            var offset = new System.Numerics.Vector2(60, 60);

                            if (!firstBound && bounds.Width > 0 && bounds.Height > 0)
                            {
                                float scaleX = (120f - padding * 2) / bounds.Width;
                                float scaleY = (120f - padding * 2) / bounds.Height;
                                exportScale = Math.Min(scaleX, scaleY);

                                offset = new System.Numerics.Vector2(
                                    -bounds.X * exportScale + padding,
                                    -bounds.Y * exportScale + padding
                                );
                            }

                            foreach (var action in renderActions)
                            {
                                try
                                {
                                    action(ctx, offset, exportScale);
                                }
                                catch (System.Exception ex)
                                {
                                    Service.PluginLog.Error(ex, "Drawable failed to render to thumbnail.");
                                }
                            }
                        });

                        using var ms = new System.IO.MemoryStream();
                        thumb.SaveAsWebp(ms);
                        thumbnailBytes = ms.ToArray();
                    }
                    catch (System.Exception ex)
                    {
                        Service.PluginLog.Error(ex, "Failed to render/encode thumbnail in background thread.");
                    }

                    try
                    {
                        ExportManager.StageSlide(arenaBackground, combinedDrawables, thumbnailBytes);
                    }
                    catch (System.Exception ex)
                    {
                        Service.PluginLog.Error(ex, "Failed to stage slide in background thread.");
                    }
                });
            }
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
    }
}