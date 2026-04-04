using System;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;

namespace AetherBlackbox.UI;

public class RecapConfigWindow : Window
{
    private readonly Plugin plugin;

    public RecapConfigWindow(Plugin plugin) : base("Aether Blackbox Config", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
        Size = new Vector2(580, 340);
    }

    public void Dispose() { }

    public override void Draw()
    {
        var conf = plugin.Configuration;

        ImGui.TextUnformatted("Capture Settings");
        ImGui.Separator();
        ImGui.Columns(3);
        foreach (var (k, v) in conf.EnumCaptureConfigs())
        {
            ImGui.PushID(k);
            var bCapture = v.Capture;
            if (ImGui.Checkbox($"Capture {k}", ref bCapture))
            {
                v.Capture = bCapture;
                conf.Save();
            }

            var notificationStyle = (int)v.NotificationStyle;
            ImGui.TextUnformatted("On Death");
            if (ImGui.Combo("##2", ref notificationStyle, ["Do Nothing", "Chat Message", "Show Popup", "Open Blackbox"]))
            {
                v.NotificationStyle = (NotificationStyle)notificationStyle;
                conf.Save();
            }

            var bOnlyInstances = v.OnlyInstances;
            if (ImGui.Checkbox("Only in instances", ref bOnlyInstances))
            {
                v.OnlyInstances = bOnlyInstances;
                conf.Save();
            }

            OnlyInInstancesTooltip();

            var bDisableInPvp = v.DisableInPvp;
            if (ImGui.Checkbox("Disable in PvP", ref bDisableInPvp))
            {
                v.DisableInPvp = bDisableInPvp;
                conf.Save();
            }

            ImGui.PopID();
            ImGui.NextColumn();
        }

        ImGui.Columns();
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextUnformatted("General Settings");
        ImGui.Spacing();
        
        var bAnonymize = conf.AnonymizeNames;
        if (ImGui.Checkbox("Streamer Mode (Anonymize Names)", ref bAnonymize))
        {
            conf.AnonymizeNames = bAnonymize;
            conf.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Replaces player names with their Job Abbreviation (e.g., 'WAR', 'WHM') in replays.");

        var chatTypes = Enum.GetValues<XivChatType>();
        var chatType = Array.IndexOf(chatTypes, conf.ChatType);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Chat Message Type");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150 * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##3", ref chatType, chatTypes.Select(t => t.GetAttribute<XivChatTypeInfoAttribute>()?.FancyName ?? t.ToString()).ToImmutableList(),
                10))
        {
            conf.ChatType = chatTypes[chatType];
            conf.Save();
        }

        ChatMessageTypeTooltip();

        var bShowTip = conf.ShowTip;
        if (ImGui.Checkbox("Show chat tip", ref bShowTip))
        {
            conf.ShowTip = bShowTip;
            conf.Save();
        }

        ChatTipTooltip();
        var keepEventsFor = conf.KeepCombatEventsForSeconds;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Keep Events for (sec)");
        ImGui.SameLine(ImGuiHelpers.GlobalScale * 140);
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 150);
        if (ImGui.InputInt("##4", ref keepEventsFor, 10))
        {
            conf.KeepCombatEventsForSeconds = keepEventsFor;
            conf.Save();
        }
        var keepReplaysFor = conf.KeepReplaysForDays;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Keep Replays for (days)");
        ImGui.SameLine(ImGuiHelpers.GlobalScale * 140);
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 150);
        if (ImGui.InputInt("##6", ref keepReplaysFor, 1))
        {
            conf.KeepReplaysForDays = keepReplaysFor;
            conf.Save();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Automatically deletes replay files older than this (in days) upon plugin startup.\nSet to 0 to disable auto-cleanup.\nReplays are located in AppData-Roaming-XIVLauncher-pluginconfigs-AetherBlackbox");

        var keepDeathsFor = conf.KeepDeathsForMinutes;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Keep Deaths for (min)");
        ImGui.SameLine(ImGuiHelpers.GlobalScale * 140);
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 150);
        if (ImGui.InputInt("##5", ref keepDeathsFor, 10))
        {
            conf.KeepDeathsForMinutes = keepDeathsFor;
            conf.Save();
        }

    }

    private static void ChatMessageTypeTooltip()
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Filter category of the \"Chat Message\" death notification.\n" +
                             "\"Debug\" will show up in all chat tabs regardless of configuration.\n" +
                             "Note that this will only affect the way the notification is displayed to you. They will never be visible to others.");
        }
    }

    private static void ChatTipTooltip()
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Prints the command in the chat to reopen the window the first time you close.");
        }
    }

    private static void OnlyInInstancesTooltip()
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Will only show a death notification when in an instance (e.g. a Dungeon)");
        }
    }
}