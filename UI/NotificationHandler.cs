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
    private Dalamud.Interface.Textures.ISharedImmediateTexture? pluginIconNode;

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

        if (pluginIconNode == null)
        {
            pluginIconNode = Service.TextureProvider.GetFromManifestResource(typeof(Plugin).Assembly, "AetherBlackbox.PluginImages.icon.png");
        }
        var pluginIcon = pluginIconNode.GetWrapOrDefault();
        windowWasMoved = ImGui.GetWindowPos() != initialPos;

        WindowName = windowWasMoved ? "###AetherBlackboxPopup" : "(Drag me somewhere)###AetherBlackboxPopup";

        var elapsed = (DateTime.Now - popupDeath?.TimeOfDeath)?.TotalSeconds;
        if (!plugin.MainWindow.IsOpen && elapsed < 30)
        {
            var labelTop = $"Show Aether Blackbox ({30 - elapsed:N0}s)";
            var playerName = popupDeath != null ? GetDeathPlayerName(popupDeath) : "";

            var cursorPos = ImGui.GetCursorScreenPos();
            bool buttonClicked = ImGui.Button("###AetherBlackboxButton", new Vector2(-1, -1));
            var buttonSize = ImGui.GetItemRectSize();
            var drawList = ImGui.GetWindowDrawList();

            var textTopSize = ImGui.CalcTextSize(labelTop);
            var iconSize = pluginIcon != null ? new Vector2(24 * ImGuiHelpers.GlobalScale, 24 * ImGuiHelpers.GlobalScale) : Vector2.Zero;
            var textNameSize = ImGui.CalcTextSize(playerName);
            var padding = pluginIcon != null && !string.IsNullOrEmpty(playerName) ? 5f * ImGuiHelpers.GlobalScale : 0f;

            var totalBottomWidth = iconSize.X + padding + textNameSize.X;
            var topTextY = cursorPos.Y + (buttonSize.Y - (textTopSize.Y + iconSize.Y + 5 * ImGuiHelpers.GlobalScale)) / 2;
            var bottomY = topTextY + textTopSize.Y + 5 * ImGuiHelpers.GlobalScale;

            var topTextPos = new Vector2(cursorPos.X + (buttonSize.X - textTopSize.X) / 2, topTextY);
            drawList.AddText(topTextPos, ImGui.GetColorU32(ImGuiCol.Text), labelTop);

            if (!string.IsNullOrEmpty(playerName))
            {
                var startX = cursorPos.X + (buttonSize.X - totalBottomWidth) / 2;
                if (pluginIcon != null)
                {
                    drawList.AddImage(pluginIcon.Handle, new Vector2(startX, bottomY), new Vector2(startX + iconSize.X, bottomY + iconSize.Y));
                    startX += iconSize.X + padding;
                }
                var textNamePos = new Vector2(startX, bottomY + (iconSize.Y - textNameSize.Y) / 2);
                drawList.AddText(textNamePos, ImGui.GetColorU32(ImGuiCol.Text), playerName);
            }

            if (buttonClicked)
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
