using AetherBlackbox.Core;
using AetherBlackbox.Events;
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

namespace AetherBlackbox;

public class Plugin : IDalamudPlugin
{

    public static string Name => "Aether Blackbox";    
    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem { get; init; }

    public PositionRecorder PositionRecorder { get; init; }
    public PullManager PullManager { get; init; }

    public AetherBlackboxWindow RecapWindow { get; init; }
    public RecapConfigWindow RecapConfigWindow { get; init; }
    public MainWindow WhiteboardWindow { get; init; }
    public CanvasConfigWindow CanvasConfigWindow { get; init; }
    public AboutWindow AboutWindow { get; init; }
    public ConditionEvaluator ConditionEvaluator { get; init; }

    public CombatEventCapture Capture { get; init; }
    public NotificationHandler NotificationHandler { get; init; }

    public Dictionary<ulong, List<Death>> DeathsPerPlayer { get; } = new();
    public Dictionary<ulong, IPlayerCharacter> Players { get; } = new();

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        Service.Initialize(pluginInterface);

        Configuration = Configuration.Get(pluginInterface);
        ConditionEvaluator = new ConditionEvaluator(this);
        PositionRecorder = new PositionRecorder(this);
        PullManager = new PullManager(this);
        Capture = new CombatEventCapture(this);
        NotificationHandler = new NotificationHandler(this);
        Service.Condition.ConditionChange += OnConditionChange;
        RecapConfigWindow = new RecapConfigWindow(this);
        RecapWindow = new AetherBlackboxWindow(this);

        // Use CanvasConfigWindow for the Canvas config, NOT RecapConfigWindow
        CanvasConfigWindow = new CanvasConfigWindow(this);
        AboutWindow = new AboutWindow();

        WhiteboardWindow = new MainWindow(this);

        WindowSystem = new WindowSystem(Name);
        WindowSystem.AddWindow(RecapConfigWindow);
        WindowSystem.AddWindow(RecapWindow);
        WindowSystem.AddWindow(CanvasConfigWindow);
        WindowSystem.AddWindow(WhiteboardWindow);
        WindowSystem.AddWindow(AboutWindow);

        Service.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi += () => RecapConfigWindow.IsOpen = true;

        Service.CommandManager.AddHandler("/abb", new CommandInfo((_, _) => WhiteboardWindow.IsOpen = true) { HelpMessage = "Open Aether Blackbox" });
        Service.CommandManager.AddHandler("/aetherblackbox", new CommandInfo((_, _) => WhiteboardWindow.IsOpen = true) { HelpMessage = "Open Aether Blackbox" });


    }
    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.InCombat)
        {
            if (value)
            {
                // Entered Combat
                Service.PluginLog.Debug("Combat detected. Starting Pull Session.");
                PullManager.StartSession();
                PositionRecorder.StartRecording();
            }
            else
            {
                // Exited Combat
                Service.PluginLog.Debug("Combat ended. Finalizing Pull Session.");
                PositionRecorder.StopRecording();
                PullManager.EndSession();
            }
        }
    }

    public void Dispose()
    {
        Service.Condition.ConditionChange -= OnConditionChange;
        Service.CommandManager.RemoveHandler("/abb");
        Service.CommandManager.RemoveHandler("/aetherblackbox");

        Service.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        WindowSystem.RemoveAllWindows();
        PositionRecorder.Dispose();
        PullManager.Dispose();
        Capture.Dispose();
        RecapConfigWindow.Dispose();
        CanvasConfigWindow.Dispose();
        WhiteboardWindow.Dispose();
        AboutWindow.Dispose();
    }

}