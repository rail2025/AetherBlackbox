using AetherBlackbox.Core;
using AetherBlackbox.Events;
using AetherBlackbox.Core.Mechanics;
using AetherBlackbox.Game;
using AetherBlackbox.UI;
using AetherBlackbox.Windows;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AetherBlackbox.Networking;

namespace AetherBlackbox;

public class Plugin : IDalamudPlugin
{

    public static string Name => "Aether Blackbox";
    public Configuration Configuration { get; private set; }
    public WindowSystem WindowSystem { get; private set; }

    public PositionRecorder PositionRecorder { get; private set; }
    public PullManager PullManager { get; private set; }

    public RecapConfigWindow RecapConfigWindow { get; private set; }
    public MainWindow MainWindow { get; private set; }
    public CanvasConfigWindow CanvasConfigWindow { get; private set; }
    public ToolbarWindow ToolbarWindow { get; private set; }
    public PropertiesWindow PropertiesWindow { get; private set; }
    public AboutWindow AboutWindow { get; private set; }
    public LiveSessionWindow LiveSessionWindow { get; private set; }
    public ConditionEvaluator ConditionEvaluator { get; private set; }
    public SessionMechanicsWindow SessionMechanicsWindow { get; private set; }
    public MechanicLibraryWindow MechanicLibraryWindow { get; private set; }
    public PresetManager PresetManager { get; private set; }
    public PresetStorageService StorageService { get; private set; }

    public CombatEventCapture Capture { get; private set; }
    public NotificationHandler NotificationHandler { get; private set; }
    public NetworkManager NetworkManager { get; private set; }
    public Dictionary<ulong, List<Death>> DeathsPerPlayer { get; } = new();
    public Dictionary<ulong, IPlayerCharacter> Players { get; } = new();
    private Dalamud.Plugin.Ipc.ICallGateProvider<string>? pullMetadataIpcProvider;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Service.Initialize(pluginInterface);
        Configuration = Configuration.Get(pluginInterface);

        InitializeReplaySystem();
        InitializeDrawingSystem();

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        Service.PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;

        Service.CommandManager.AddHandler("/abb", new CommandInfo((_, _) => MainWindow.IsOpen = true) { HelpMessage = "Open Aether Blackbox" });
        Service.CommandManager.AddHandler("/aetherblackbox", new CommandInfo((_, _) => MainWindow.IsOpen = true) { HelpMessage = "Open Aether Blackbox" });
    }

    private void InitializeReplaySystem()
    {
        ConditionEvaluator = new ConditionEvaluator(this);
        PositionRecorder = new PositionRecorder(this);
        PullManager = new PullManager(this);
        Capture = new CombatEventCapture(this);
        NotificationHandler = new NotificationHandler(this);
        NetworkManager = new NetworkManager();
        PresetManager = new PresetManager();
        StorageService = new PresetStorageService(Service.PluginInterface.ConfigDirectory.FullName);

        void BroadcastHeaders()
        {
            var headers = PullManager.GetLastHeadersJson();
            if (headers != "[]")
                NetworkManager.SendHeadersBroadcastAsync(headers);
        }

        NetworkManager.OnConnected += BroadcastHeaders;
        NetworkManager.OnReplayRequested += hash => PullManager.UploadReplayByHash(hash);
        NetworkManager.OnHeadersRequested += BroadcastHeaders;
        Service.Condition.ConditionChange += OnConditionChange;

        try
        {
            pullMetadataIpcProvider = Service.PluginInterface.GetIpcProvider<string>("AetherBlackbox.GetLastPullMetadata");
            pullMetadataIpcProvider.RegisterFunc(GetPullMetadataPayload);
        }
        catch (System.Exception ex)
        {
            Service.PluginLog.Error(ex, "Failed to register IPC provider.");
        }
    }

    private void InitializeDrawingSystem()
    {
        WindowSystem = new WindowSystem(Name);

        RecapConfigWindow = new RecapConfigWindow(this);
        CanvasConfigWindow = new CanvasConfigWindow(this);
        ToolbarWindow = new ToolbarWindow(this);
        PropertiesWindow = new PropertiesWindow(this);
        AboutWindow = new AboutWindow();
        MainWindow = new MainWindow(this);
        LiveSessionWindow = new LiveSessionWindow(this);
        SessionMechanicsWindow = new SessionMechanicsWindow(this);
        MechanicLibraryWindow = new MechanicLibraryWindow(this);

        WindowSystem.AddWindow(RecapConfigWindow);
        WindowSystem.AddWindow(CanvasConfigWindow);
        WindowSystem.AddWindow(PropertiesWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(AboutWindow);
        WindowSystem.AddWindow(LiveSessionWindow);
        WindowSystem.AddWindow(NotificationHandler);
        WindowSystem.AddWindow(SessionMechanicsWindow);
        WindowSystem.AddWindow(MechanicLibraryWindow);
    }
    private void OnOpenConfigUi() => RecapConfigWindow.IsOpen = true;
    private void OnOpenMainUi() => MainWindow.IsOpen = true;
    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.InCombat)
        {
            if (value)
            {
                Service.PluginLog.Debug("Combat detected. Starting Pull Session.");
                PullManager.StartSession();
                PositionRecorder.StartRecording();
            }
            else
            {
                Service.PluginLog.Debug("Combat ended. Finalizing Pull Session.");
                PositionRecorder.StopRecording();
                PullManager.EndSession();
            }
        }
    }

    public void Dispose()
    {
        pullMetadataIpcProvider?.UnregisterFunc();
        Service.Condition.ConditionChange -= OnConditionChange;
        Service.CommandManager.RemoveHandler("/abb");
        Service.CommandManager.RemoveHandler("/aetherblackbox");

        Service.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        Service.PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;

        WindowSystem.RemoveAllWindows();
        PositionRecorder.Dispose();
        PullManager.Dispose();
        Capture.Dispose();
        NetworkManager.Dispose();
        DrawingLogic.TextureManager.Dispose();
        RecapConfigWindow.Dispose();
        CanvasConfigWindow.Dispose();
        ToolbarWindow.Dispose();
        PropertiesWindow.Dispose();
        MainWindow.Dispose();
        LiveSessionWindow.Dispose();
        AboutWindow.Dispose();
        SessionMechanicsWindow.Dispose();
        MechanicLibraryWindow.Dispose();
    }
    private string GetPullMetadataPayload()
    {
        try
        {
            return PullManager.GetLastHeadersJson();
        }
        catch (System.Exception ex)
        {
            Service.PluginLog.Error(ex, "Error getting pull metadata for IPC.");
            return string.Empty;
        }
    }
}