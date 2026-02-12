using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using AetherBlackbox.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AetherBlackbox;

[Serializable]
public class Configuration : IPluginConfiguration
{
    [NonSerialized]
    private IDalamudPluginInterface pluginInterface = null!;

    public CaptureConfig Self { get; set; } = new() { Capture = true, NotificationStyle = NotificationStyle.Popup, OnlyInstances = false, DisableInPvp = false };
    public CaptureConfig Party { get; set; } = new() { Capture = true, NotificationStyle = NotificationStyle.Chat, OnlyInstances = false, DisableInPvp = false };
    public CaptureConfig Others { get; set; } = new() { Capture = false, NotificationStyle = NotificationStyle.Chat, OnlyInstances = true, DisableInPvp = true };

    public bool ShowTip { get; set; } = true;
    public int KeepCombatEventsForSeconds { get; set; } = 60;
    public int KeepDeathsForMinutes { get; set; } = 60;
    public XivChatType ChatType { get; set; } = XivChatType.SystemMessage;
    public EventFilter EventFilter { get; set; } = EventFilter.Default;
    public bool ShowCombatHistogram { get; set; } = false;

    public bool ShowReplayNpcs { get; set; } = true;
    public bool ShowReplayHp { get; set; } = true;

    public bool IsMainWindowMovable { get; set; } = true;
    public float DefaultBrushColorR { get; set; } = 1.0f;
    public float DefaultBrushColorG { get; set; } = 1.0f;
    public float DefaultBrushColorB { get; set; } = 1.0f;
    public float DefaultBrushColorA { get; set; } = 1.0f;
    public float DefaultBrushThickness { get; set; } = 4.0f;

    public bool IsGridVisible { get; set; } = true;
    public float GridSize { get; set; } = 40f;
    public bool IsSnapToGrid { get; set; } = true;
    public int KeepReplaysForDays { get; set; } = 14;

    public bool AnonymizeNames { get; set; } = false;
    public int Version { get; set; } = 2;

    [JsonExtensionData]
    public IDictionary<string, JToken> AdditionalData { get; set; } = new Dictionary<string, JToken>();

    public static Configuration Get(IDalamudPluginInterface pluginInterface)
    {
        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        config.pluginInterface = pluginInterface;
        config.Migrate();

        return config;
    }

    public IEnumerable<(string, CaptureConfig)> EnumCaptureConfigs()
    {
        yield return ("Self", Self);
        yield return ("Party", Party);
        yield return ("Others", Others);
    }

    public void Migrate()
    {
        if (Version == 0)
        {
            foreach (var (k, v) in EnumCaptureConfigs())
            {
                if (AdditionalData.TryGetValue($"Capture{k}", out var capture)) v.Capture = capture.ToObject<bool>();
                if (AdditionalData.TryGetValue($"{k}Notification", out var note)) v.NotificationStyle = note.ToObject<NotificationStyle>();
                if (AdditionalData.TryGetValue($"{k}NotificationOnlyInstances", out var inst)) v.OnlyInstances = inst.ToObject<bool>();
            }
            AdditionalData.Clear();
            Version = 1;
            Save();
        }

        if (Version == 1)
        {
            if (ChatType == XivChatType.Debug)
            {
                ChatType = XivChatType.SystemMessage;
            }
            Version = 2;
            Save();
        }
    }

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }

    [Serializable]
    public class CaptureConfig
    {
        public bool Capture { get; set; }
        public NotificationStyle NotificationStyle { get; set; }
        public bool OnlyInstances { get; set; }
        public bool DisableInPvp { get; set; }
    }
}