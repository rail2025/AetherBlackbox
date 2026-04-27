using AetherBlackbox.Events;
using AetherBlackbox.Game;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;
using System.Text;

namespace AetherBlackbox.UI;

public class NotificationHandler : Window {
    private readonly DalamudLinkPayload chatLinkPayload;
    private readonly Plugin plugin;
    private Death? popupDeath;
    private bool windowWasMoved;
    private readonly Vector2 initialPos;

    public NotificationHandler(Plugin plugin) : base("###AetherBlackboxPopup",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoFocusOnAppearing) {
        this.plugin = plugin;

        ref var viewportPosition = ref ImGui.GetMainViewport().WorkPos;
        ref var viewportSize = ref ImGui.GetMainViewport().WorkSize;

        Size = new Vector2(200, 80);
        PositionCondition = ImGuiCond.FirstUseEver;
        Position = initialPos = viewportPosition + (viewportSize - Size.Value * ImGuiHelpers.GlobalScale) * .5f;

        chatLinkPayload = Service.ChatGui.AddChatLinkHandler(0, OnChatLinkClick);
    }

    private string GetDeathPlayerName(Death death)
    {
        if (death.ReplayData?.Metadata != null && death.ReplayData.Metadata.TryGetValue(death.PlayerId, out var meta))
            return meta.Name;
        if (plugin.PullManager.CurrentSession?.Metadata != null && plugin.PullManager.CurrentSession.Metadata.TryGetValue(death.PlayerId, out var currentMeta))
            return currentMeta.Name;
        return "Unknown";
    }

    private void OnChatLinkClick(uint cmdId, SeString msg)
    {
        if (msg.Payloads is [.., RawPayload p, _] && DeathNotificationPayload.Decode(p) is { } payload
                                                  && plugin.DeathsPerPlayer.TryGetValue(payload.PlayerId, out var deaths)) {
            var death = deaths.FirstOrDefault(d => d.TimeOfDeath.Ticks == payload.DeathTimestamp);
            if (death != null)
            {
                plugin.MainWindow.OpenReplay(death);
            }
        }
    }

    public override void Draw() {
        windowWasMoved = ImGui.GetWindowPos() != initialPos;

        WindowName = windowWasMoved ? "###AetherBlackboxPopup" : "(Drag me somewhere)###AetherBlackboxPopup";

        var elapsed = (DateTime.Now - popupDeath?.TimeOfDeath)?.TotalSeconds;
        if (!plugin.MainWindow.IsOpen && elapsed < 30)
        {
            var label = $"Show Aether Blackbox ({30 - elapsed:N0}s)";
            if (popupDeath != null)
            {
                var playerName = GetDeathPlayerName(popupDeath);
                label = AppendCenteredPlayerName(label, playerName);
            }

            if (ImGui.Button(label, new Vector2(-1, -1)))
            {
                if (popupDeath != null)
                {
                    plugin.MainWindow.OpenReplay(popupDeath);
                }
                popupDeath = null;
                IsOpen = false;
            }
        } else {
            IsOpen = false;
        }
    }

    private static string AppendCenteredPlayerName(string label, string pname) {
        var length = ImGui.CalcTextSize(label).X;
        var spclength = ImGui.CalcTextSize(" ").X;
        var namelength = ImGui.CalcTextSize(pname).X;
        var spccount = (int)Math.Round((namelength - length) / 2f / spclength);
        if (spccount == 0)
            return label + "\n" + pname;
        if (spccount > 0) {
            var strbld = new StringBuilder(spccount * 2 + label.Length + pname.Length + 1);
            strbld.Append(' ', spccount);
            strbld.Append(label);
            strbld.Append(' ', spccount);
            strbld.Append('\n');
            strbld.Append(pname);
            return strbld.ToString();
        } else {
            var strbld = new StringBuilder(-spccount * 2 + label.Length + pname.Length + 1);
            strbld.Append(label);
            strbld.Append('\n');
            strbld.Append(' ', -spccount);
            strbld.Append(pname);
            strbld.Append(' ', -spccount);
            return strbld.ToString();
        }
    }

    public void DisplayDeath(Death death)
    {
        var displayType = plugin.ConditionEvaluator.GetNotificationType(death.PlayerId);
        var playerName = GetDeathPlayerName(death);
        switch (displayType)
        {
            case NotificationStyle.Popup:
                popupDeath = death;
                IsOpen = true;
                break;
            case NotificationStyle.Chat:
                var chatMsg = HasAuthor(plugin.Configuration.ChatType)
                    ? new SeString(chatLinkPayload, new TextPayload(" has died "), new UIForegroundPayload(710), new TextPayload("[ Show Aether Blackbox ]"),
                        new UIForegroundPayload(0), new DeathNotificationPayload(death.TimeOfDeath.Ticks, death.PlayerId), RawPayload.LinkTerminator)
                    : new SeString(chatLinkPayload, new UIForegroundPayload(1), new TextPayload(playerName), new UIForegroundPayload(0),
                        new TextPayload(" has died "), new UIForegroundPayload(710), new TextPayload("[ Show Aether Blackbox ]"), new UIForegroundPayload(0),
                        new DeathNotificationPayload(death.TimeOfDeath.Ticks, death.PlayerId), RawPayload.LinkTerminator);
                Service.ChatGui.Print(new XivChatEntry { Message = chatMsg, Type = plugin.Configuration.ChatType, Name = playerName });
                break;
            case NotificationStyle.OpenAetherBlackbox:
                plugin.MainWindow.OpenReplay(death);
                break;
        }
    }

    private static bool HasAuthor(XivChatType chatType) =>
        chatType switch {
            XivChatType.None => false,
            XivChatType.Debug => false,
            XivChatType.Urgent => false,
            XivChatType.Notice => false,
            XivChatType.StandardEmote => false,
            XivChatType.Echo => false,
            XivChatType.SystemError => false,
            XivChatType.SystemMessage => false,
            XivChatType.GatheringSystemMessage => false,
            XivChatType.ErrorMessage => false,
            XivChatType.RetainerSale => false,
            _ => true
        };
}
