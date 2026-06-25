//TODO: finish the class rewrites when the stars align
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

namespace AetherBlackbox.Windows;

public partial class MainWindow : Window, IDisposable
{
    private float canvasZoom = 1.0f;
    private Vector2 canvasPanOffset = Vector2.Zero;
    private readonly Plugin plugin;
    private readonly Configuration configuration;
    private readonly CanvasController canvasController;
    private readonly PageManager pageManager;
    private List<ReplayFileManager.ReplayFileHeader>? cachedSavedReplays;
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
    public float CurrentAbsoluteTime => GetDeathTimeOffset() + replayTimeOffset;

    // Canvas
    private ulong selectedEntityId = 0;
    private BaseDrawable? hoveredDrawable = null;
    private List<BaseDrawable> selectedDrawables = [];
    private List<BaseDrawable> clipboard = [];

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
    
    private List<Lumina.Excel.Sheets.Status> statusSearchResults = new();

    public bool IsNetworkHost { get; private set; } = false;
    private float lastTimeSyncBroadcast = 0f;
 
    private Dictionary<string, float> userMarkers = new();
    private Dictionary<string, (float Time, int TotalViewers)> activePings = new();
    private HashSet<string> connectedUsers = new();
    private string? syncTarget = null;
    private float lastPingTime = 0f;

    private Vector2 partyPanelPosition = new(-1f, -1f);
    private bool isDraggingPartyPanel = false;

    private readonly uint[] Palette = [0xFFFFB358, 0xFF727BFF, 0xFFB4F5AF, 0xFF2299D2, 0xFFFF8CBC, 0xFF57A6FF];
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
        this.currentBrushColor = new(this.configuration.DefaultBrushColorR, this.configuration.DefaultBrushColorG, this.configuration.DefaultBrushColorB, this.configuration.DefaultBrushColorA);
        float[] initialThicknessPresets = [1.5f, 4f, 7f, 10f];
        this.currentBrushThickness = initialThicknessPresets.Contains(this.configuration.DefaultBrushThickness) ? this.configuration.DefaultBrushThickness : initialThicknessPresets[1];

        this.plugin.NetworkManager.OnHostStatusReceived += OnHostStatusReceived;
        this.plugin.NetworkManager.OnStateUpdateReceived += OnStateUpdateReceived;
        this.plugin.NetworkManager.OnUserTimeReceived += OnUserTimeReceived;
        this.plugin.NetworkManager.OnUserPingReceived += OnUserPingReceived;
        this.plugin.NetworkManager.OnUserJoined += OnUserJoined;
        this.plugin.NetworkManager.OnUserLeft += OnUserLeft;
        this.plugin.NetworkManager.OnConnected += OnNetworkConnected;
        this.plugin.NetworkManager.OnDisconnected += OnNetworkDisconnected;
    }

    private void OnNetworkConnected() => pageManager.EnterLiveMode();
    private void OnNetworkDisconnected() => pageManager.ExitLiveMode();

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
        this.IsNetworkHost = status;
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

    public void Dispose()
    {
        this.plugin.NetworkManager.OnHostStatusReceived -= OnHostStatusReceived;
        this.plugin.NetworkManager.OnStateUpdateReceived -= OnStateUpdateReceived;
        this.plugin.NetworkManager.OnUserTimeReceived -= OnUserTimeReceived;
        this.plugin.NetworkManager.OnUserPingReceived -= OnUserPingReceived;
        this.plugin.NetworkManager.OnUserJoined -= OnUserJoined;
        this.plugin.NetworkManager.OnUserLeft -= OnUserLeft;
        this.plugin.NetworkManager.OnConnected -= OnNetworkConnected;
        this.plugin.NetworkManager.OnDisconnected -= OnNetworkDisconnected;
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

        if (plugin.NetworkManager.IsConnected && IsNetworkHost && selectedPull != null)
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
                if (IsNetworkHost)
                    ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.2f, 1.0f), "Live: HOST");
                else
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.2f, 1.0f), "Live: VIEWER");
                ImGui.SameLine();
            }

            bool isHostOrOffline = IsNetworkHost || !plugin.NetworkManager.IsConnected;
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

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("OpenMechanicLibrary", FontAwesomeIcon.Database))
                plugin.MechanicLibraryWindow.IsOpen = !plugin.MechanicLibraryWindow.IsOpen;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open Mechanic Library (Saved Presets)");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("OpenSessionMechanics", FontAwesomeIcon.ListUl))
                plugin.SessionMechanicsWindow.IsOpen = !plugin.SessionMechanicsWindow.IsOpen;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open Active Session Mechanics");

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

                var showStatuses = configuration.ShowReplayStatuses;
                if (ImGui.Checkbox("Show Statuses", ref showStatuses))
                {
                    configuration.ShowReplayStatuses = showStatuses;
                    configuration.Save();
                }

                ImGui.EndPopup();
            }

            float rightAlignOffset = ImGui.GetContentRegionAvail().X - (130f * ImGuiHelpers.GlobalScale);
            if (rightAlignOffset > 0) ImGui.SameLine(rightAlignOffset);
            else ImGui.SameLine();

            if (ImGuiComponents.IconButton("##TimeNudgeLeft", FontAwesomeIcon.ChevronLeft))
            {
                syncTarget = null;
                isPlaybackActive = false;
                replayTimeOffset = Math.Max(timelineMin, replayTimeOffset - 0.1f);
                BroadcastTimeSync();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Nudge Left (-0.1s)");

            ImGui.SameLine();
            float absTimeInput = currentDeathTime + replayTimeOffset;
            ImGui.SetNextItemWidth(55f * ImGuiHelpers.GlobalScale);
            if (ImGui.InputFloat("##ExactTime", ref absTimeInput, 0f, 0f, "%.1f", ImGuiInputTextFlags.EnterReturnsTrue))
            {
                syncTarget = null;
                isPlaybackActive = false;
                replayTimeOffset = Math.Clamp(absTimeInput - currentDeathTime, timelineMin, 5f);
                BroadcastTimeSync();
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("##TimeNudgeRight", FontAwesomeIcon.ChevronRight))
            {
                syncTarget = null;
                isPlaybackActive = false;
                replayTimeOffset = Math.Min(5f, replayTimeOffset + 0.1f);
                BroadcastTimeSync();
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Nudge Right (+0.1s)");

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
            DrawMapCalibrationPanel();                
        }

        if (ImGui.BeginChild("CanvasDrawingArea", new Vector2(0, 0), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            var canvasStartPos = ImGui.GetCursorPos();
            currentCanvasDrawSize = ImGui.GetContentRegionAvail();
            if (currentCanvasDrawSize.X > 0 && currentCanvasDrawSize.Y > 0)
            {
                DrawCanvas();

                ImGui.SetCursorPos(canvasStartPos + new Vector2(10 * ImGuiHelpers.GlobalScale, 10 * ImGuiHelpers.GlobalScale));

                bool isToolbarVisible = configuration.IsToolbarVisible;
                bool isSelectionVisible = selectedEntityId != 0 && ActiveDeathReplay != null;

                float bottomPadding = isSelectionVisible ? 150f * ImGuiHelpers.GlobalScale : 20f * ImGuiHelpers.GlobalScale;
                float childHeight = isToolbarVisible ? (currentCanvasDrawSize.Y - bottomPadding) : (35 * ImGuiHelpers.GlobalScale);

                float childWidth = 115f * ImGuiHelpers.GlobalScale;
                if (isToolbarVisible)
                    childWidth += ImGui.GetStyle().ScrollbarSize;

                ImGui.PushStyleColor(ImGuiCol.ChildBg, isToolbarVisible ? new Vector4(0.12f, 0.12f, 0.14f, 0.95f) : new Vector4(0, 0, 0, 0));

                if (ImGui.BeginChild("ToolbarContainer", new Vector2(childWidth, childHeight), isToolbarVisible, ImGuiWindowFlags.None))
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
                if (selectedEntityId != 0 && ActiveDeathReplay != null)
                {
                    ImGui.SetCursorPos(canvasStartPos + new Vector2(0, currentCanvasDrawSize.Y - (140f * ImGuiHelpers.GlobalScale)));
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.12f, 0.12f, 0.14f, 0.95f));
                    if (ImGui.BeginChild("SelectionInfoArea", new Vector2(0, 140f * ImGuiHelpers.GlobalScale), true, ImGuiWindowFlags.NoScrollbar))
                    {
                        DrawSelectionInfo();
                    }
                    ImGui.EndChild();
                    ImGui.PopStyleColor();
                }
                if (configuration.ShowPartyMemberList)
                {
                    DrawPartyMembersPanel(canvasStartPos);
                }
            }
            ImGui.EndChild();
        }
    }


    // might use this again 
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
                var jobSheet = Service.DataManager.GetExcelSheet<ClassJob>();
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