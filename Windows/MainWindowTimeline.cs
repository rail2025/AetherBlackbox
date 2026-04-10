using AetherBlackbox.DrawingLogic;
using AetherBlackbox.Events;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public partial class MainWindow
    {
        private void DrawTimeline()
        {
            if (ActiveDeathReplay == null) return;

            var drawList = ImGui.GetWindowDrawList();
            var cursor = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X - (10 * ImGuiHelpers.GlobalScale);
            var height = ImGui.GetFrameHeight();

            float deathTimeOffset = GetDeathTimeOffset();
            float minTime = -deathTimeOffset;
            if (minTime > -20f) minTime = -20f;
            float maxTime = 5f;
            float totalRange = maxTime - minTime;

            drawList.AddRectFilled(cursor, cursor + new Vector2(width, height), ImGui.GetColorU32(ImGuiCol.FrameBg), 4f);

            ImGui.InvisibleButton("##TimelineScrubber", new Vector2(width, height));
            if (ImGui.IsItemActive())
            {
                syncTarget = null;
                isPlaybackActive = false;
                float mouseRatio = Math.Clamp((ImGui.GetMousePos().X - cursor.X) / width, 0f, 1f);
                replayTimeOffset = minTime + (mouseRatio * totalRange);
                lastTimeSyncBroadcast += ImGui.GetIO().DeltaTime;
                if (lastTimeSyncBroadcast >= 0.1f)
                {
                    BroadcastTimeSync();
                    lastTimeSyncBroadcast = 0f;
                }
            }
            if (ImGui.IsItemDeactivated()) BroadcastTimeSync();

            foreach (var evt in ActiveDeathReplay.Events)
            {
                float relativeTime = (float)(evt.Snapshot.Time - ActiveDeathReplay.TimeOfDeath).TotalSeconds;
                if (relativeTime < minTime || relativeTime > maxTime) continue;

                float ratio = (relativeTime - minTime) / totalRange;
                float x = cursor.X + (ratio * width);

                if (evt is CombatEvent.DamageTaken || evt is CombatEvent.Healed)
                {
                    uint fadedColor = (evt is CombatEvent.DamageTaken) ? (uint)0x305050FF : (uint)0x3050FF50;
                    drawList.AddLine(new Vector2(x, cursor.Y), new Vector2(x, cursor.Y + height * 0.5f), fadedColor, 1f);
                    continue;
                }

                if (evt is CombatEvent.StatusEffect status && (status.Id == 148 || status.Id == 1140))
                {
                    var resIcon = AetherBlackbox.DrawingLogic.TextureManager.GetTexture($"luminaicon:{status.Id}");
                    if (resIcon != null)
                    {
                        Vector2 iconSize = new Vector2(16, 16) * ImGuiHelpers.GlobalScale;
                        drawList.AddImage(resIcon.Handle,
                            new Vector2(x - iconSize.X / 2, cursor.Y),
                            new Vector2(x + iconSize.X / 2, cursor.Y + iconSize.Y),
                            Vector2.Zero, Vector2.One, 0xFF00FFFF);
                    }
                }

                drawList.AddLine(new Vector2(x, cursor.Y), new Vector2(x, cursor.Y + height), 0xFF0000FF, 1f);
            }

            if (selectedPull?.Deaths != null)
            {
                foreach (var d in selectedPull.Deaths)
                {
                    float dRelTime = (float)(d.TimeOfDeath - ActiveDeathReplay.TimeOfDeath).TotalSeconds;
                    if (dRelTime < minTime || dRelTime > maxTime) continue;

                    float dRatio = (dRelTime - minTime) / totalRange;
                    float dX = cursor.X + (dRatio * width);

                    if (ActiveDeathReplay.ReplayData?.Metadata != null && ActiveDeathReplay.ReplayData.Metadata.TryGetValue(d.PlayerId, out var dMeta))
                    {
                        uint dJobIconId = 62100 + dMeta.ClassJobId;
                        var dJobIcon = TextureManager.GetTexture($"luminaicon:{dJobIconId}");

                        if (dJobIcon != null)
                        {
                            Vector2 iconSize = new Vector2(16, 16) * ImGuiHelpers.GlobalScale;
                            drawList.AddImage(dJobIcon.Handle,
                                new Vector2(dX - iconSize.X / 2, cursor.Y + height + 5),
                                new Vector2(dX + iconSize.X / 2, cursor.Y + height + 5 + iconSize.Y),
                                Vector2.Zero, Vector2.One, 0xFFFFFFFF);
                        }
                    }
                        drawList.AddLine(new Vector2(dX, cursor.Y + height), new Vector2(dX, cursor.Y + height + 5), 0xFF0000FF, 2f);
                    }
                }

                float absoluteCurrentTime = deathTimeOffset + replayTimeOffset;

            foreach (var kvp in userMarkers)
            {
                float relTime = kvp.Value - deathTimeOffset;
                float ratio = Math.Clamp((relTime - minTime) / totalRange, 0f, 1f);
                float x = cursor.X + (ratio * width);
                uint color = GetUserColor(kvp.Key);
                drawList.AddTriangleFilled(new Vector2(x, cursor.Y + height), new Vector2(x - 5, cursor.Y + height + 8), new Vector2(x + 5, cursor.Y + height + 8), color);
            }

            uint pingColor = ImGui.GetColorU32(new Vector4(0.97f, 0.32f, 0.29f, 1.0f));
            uint syncedTextColor = ImGui.GetColorU32(new Vector4(0.69f, 0.96f, 0.71f, 1.0f));
            Vector2 endOfTimelineCursorPos = ImGui.GetCursorScreenPos();

            System.Collections.Generic.List<float> drawnPingTimes = new System.Collections.Generic.List<float>();
            string primaryId = syncTarget ?? plugin.NetworkManager.LocalClientId;

            var orderedIds = new System.Collections.Generic.List<string>();
            if (activePings.ContainsKey(primaryId)) orderedIds.Add(primaryId);
            foreach (var key in activePings.Keys) { if (key != primaryId) orderedIds.Add(key); }

            foreach (var id in orderedIds)
            {
                if (id != plugin.NetworkManager.LocalClientId && !connectedUsers.Contains(id)) continue;
                if (syncTarget != null && id == plugin.NetworkManager.LocalClientId) continue;

                var ping = activePings[id];

                float pingAbsTime = ping.Time;
                if (id == plugin.NetworkManager.LocalClientId) pingAbsTime = absoluteCurrentTime;
                else if (userMarkers.TryGetValue(id, out float uTime)) pingAbsTime = uTime;

                bool isOverlapping = false;
                foreach (var drawnTime in drawnPingTimes)
                {
                    if (Math.Abs(drawnTime - pingAbsTime) < 1.5f) { isOverlapping = true; break; }
                }
                if (isOverlapping) continue;

                drawnPingTimes.Add(pingAbsTime);

                float relPingTime = pingAbsTime - deathTimeOffset;
                float ratio = Math.Clamp((relPingTime - minTime) / totalRange, 0f, 1f);
                float x = cursor.X + (ratio * width);

                int syncedCount = 0;
                if (Math.Abs(absoluteCurrentTime - pingAbsTime) < 1.5f) syncedCount++;
                foreach (var marker in userMarkers)
                {
                    if (connectedUsers.Contains(marker.Key) && Math.Abs(marker.Value - pingAbsTime) < 1.5f)
                        syncedCount++;
                }

                int totalCount = connectedUsers.Count + 1;

                drawList.AddTriangleFilled(new Vector2(x, cursor.Y - 2), new Vector2(x - 5, cursor.Y - 10), new Vector2(x + 5, cursor.Y - 10), pingColor);

                string iconText = Dalamud.Interface.FontAwesomeIcon.Eye.ToIconString();
                string normalText = $" {syncedCount}/{totalCount}" + (syncTarget == id ? " [Synced]" : "");

                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                float iconWidth = ImGui.CalcTextSize(iconText).X;
                ImGui.PopFont();

                float textHeight = ImGui.CalcTextSize(normalText).Y;
                float totalWidth = iconWidth + ImGui.CalcTextSize(normalText).X;

                ImGui.SetCursorScreenPos(new Vector2(x - 10, cursor.Y - 10 - textHeight));
                if (ImGui.InvisibleButton($"##ping_{id}", new Vector2(Math.Max(20, totalWidth), textHeight + 10)))
                {
                    syncTarget = id;
                    replayTimeOffset = pingAbsTime - deathTimeOffset;
                    BroadcastTimeSync();
                }

                ImGui.SetCursorScreenPos(new Vector2(x - (totalWidth / 2), cursor.Y - 10 - textHeight));
                ImGui.PushStyleColor(ImGuiCol.Text, syncTarget == id ? syncedTextColor : 0xFFD9D1C9);

                ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
                ImGui.Text(iconText);
                ImGui.PopFont();

                ImGui.SameLine(0, 0);
                ImGui.Text(normalText);
                ImGui.PopStyleColor();
            }

            ImGui.SetCursorScreenPos(endOfTimelineCursorPos);

            float currentRatio = Math.Clamp((replayTimeOffset - minTime) / totalRange, 0f, 1f);
            float playheadX = cursor.X + (currentRatio * width);

            drawList.AddLine(new Vector2(playheadX, cursor.Y), new Vector2(playheadX, cursor.Y + height), 0xFFFFFFFF, 2f);
            drawList.AddCircleFilled(new Vector2(playheadX, cursor.Y + height / 2), 4f, 0xFFFFFFFF);

            string timeText = $"{absoluteCurrentTime:F1}s";
            drawList.AddText(cursor + new Vector2(5, 2), 0xFFFFFFFF, timeText);
        }
    }
}