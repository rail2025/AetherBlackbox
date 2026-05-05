using AetherBlackbox.Core;
using AetherBlackbox.Events;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public partial class MainWindow
    {
        // Row data for a single party member in the overlay
        private sealed class PartyMemberRowData
        {
            public required ulong EntityId { get; init; }
            public required string DisplayName { get; init; }
            public required uint MaxHp { get; init; }
            public required uint CurrentHp { get; init; }
            public required uint ClassJobId { get; init; }
            public required List<(uint Id, float Remaining)> Debuffs { get; init; }
        }

        // Main function of the overlay: builds party member data and layout, renders rows, and handles dragging with RMB
        private void DrawPartyMembersPanel(Vector2 canvasStartPos)
        {
            float scale = ImGuiHelpers.GlobalScale;
            float padding = 10f * scale;
            float iconSize = 17.5f * scale;

            var members = BuildPartyMemberRows();
            if (members.Count == 0) return;

            int targetRows = Math.Max(members.Count, 8);

            float panelWidth = ComputePartyPanelWidth(members, iconSize, scale);
            var layout = ComputePartyPanelLayout(panelWidth, targetRows, scale, padding);

            EnsurePartyPanelPositionInitialized(layout.DefaultX, layout.DefaultY);
            partyPanelPosition.X = Math.Clamp(partyPanelPosition.X, padding, layout.MaxX);
            partyPanelPosition.Y = Math.Clamp(partyPanelPosition.Y, padding, layout.MaxY);

            float panelX = partyPanelPosition.X;
            float panelY = partyPanelPosition.Y;

            ImGui.SetCursorPos(canvasStartPos + new Vector2(panelX, panelY));
            Vector2 panelScreenPos = ImGui.GetCursorScreenPos();
            Vector2 panelScreenEnd = panelScreenPos + new Vector2(panelWidth, layout.PanelHeight);

            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

            if (ImGui.BeginChild("PartyMembersContainer", new Vector2(panelWidth, layout.PanelHeight), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground))
            {
                DrawPartyMemberRows(members, layout.RowHeight, layout.BarHeight, iconSize, scale);
            }

            ImGui.EndChild();

            HandlePartyPanelDrag(panelScreenPos, panelScreenEnd, padding, layout.MaxX, layout.MaxY);

            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }

        // Builds party rows from replay metadata: resolves HP from the closest frame, gets debuffs, and applies anonymization if checked
        private List<PartyMemberRowData> BuildPartyMemberRows()
        {
            if (ActiveDeathReplay?.ReplayData == null) return new();

            var recording = ActiveDeathReplay.ReplayData;
            var statusSheet = Service.DataManager.GetExcelSheet<Status>();

            ReplayFrame? closestFrame = null;
            float targetOffset = 0f;
            if (recording.Frames.Count > 0)
            {
                float deathTimeOffset = selectedPull != null
                    ? (float)(ActiveDeathReplay.TimeOfDeath - selectedPull.StartTime).TotalSeconds
                    : recording.Frames.Last().TimeOffset;
                targetOffset = deathTimeOffset + replayTimeOffset;
                closestFrame = GetClosestFrame(recording, targetOffset);
            }

            List<PartyMemberRowData> rows = [];
            foreach (var kvp in recording.Metadata.Where(kvp => kvp.Value.ClassJobId != 0 && !string.IsNullOrWhiteSpace(kvp.Value.Name)))
            {
                uint currentHp = 0;
                if (closestFrame?.Hp != null)
                {
                    int idx = closestFrame.Ids.IndexOf(kvp.Key);
                    if (idx >= 0 && idx < closestFrame.Hp.Count)
                        currentHp = closestFrame.Hp[idx];
                }

                var debuffs = GetActiveStatuses(recording, kvp.Key, targetOffset)
                    .Where(s =>
                    {
                        var row = statusSheet.GetRowOrDefault(s.Id);
                        return row.HasValue && row.Value.StatusCategory == (uint)StatusCategory.Detrimental;
                    })
                    .OrderBy(s => s.RemainingDuration)
                    .Select(s => (s.Id, s.RemainingDuration))
                    .ToList();

                string displayName = kvp.Value.Name;
                if (configuration.AnonymizeNames)
                {
                    var jobRow = Service.DataManager.GetExcelSheet<ClassJob>().GetRowOrDefault(kvp.Value.ClassJobId);
                    if (jobRow.HasValue) displayName = jobRow.Value.Abbreviation.ToString();
                }

                rows.Add(new PartyMemberRowData
                {
                    EntityId = kvp.Key,
                    DisplayName = displayName,
                    MaxHp = kvp.Value.MaxHp,
                    CurrentHp = currentHp,
                    ClassJobId = kvp.Value.ClassJobId,
                    Debuffs = debuffs
                });
            }

            return rows.OrderBy(m => GetPartyRolePriority(m.ClassJobId)).ThenBy(m => m.DisplayName).ToList();
        }

        // Calculates panel width based on the longest display name plus space for 5 debuff icons
        private static float ComputePartyPanelWidth(List<PartyMemberRowData> members, float iconSize, float scale)
        {
            float longestNameWidth = 0f;
            foreach (var member in members)
            {
                float width = ImGui.CalcTextSize(member.DisplayName).X;
                if (width > longestNameWidth) longestNameWidth = width;
            }

            float iconSpacing = 2f * scale;
            float iconsWidth = (5f * iconSize) + (4f * iconSpacing);
            float badgeWidth = 18f * scale;
            float horizontalPadding = 24f * scale;

            return Math.Clamp(longestNameWidth + iconsWidth + badgeWidth + horizontalPadding, 220f * scale, 360f * scale);
        }

        // Computes vertical sizing (row height, HP bar height, total panel height)
        private (float PanelHeight, float RowHeight, float BarHeight, float DefaultX, float DefaultY, float MaxX, float MaxY)
            ComputePartyPanelLayout(float panelWidth, int targetRows, float scale, float padding)
        {
            float panelY = (isExportPreviewOpen ? 420f : 40f) * scale;
            float availableHeight = Math.Max(80f * scale, currentCanvasDrawSize.Y - panelY - padding);

            float nameHeight = ImGui.GetTextLineHeight();
            float rowHeight = Math.Max((nameHeight * 2f) + (1f * scale), 20f * scale);
            float barHeight = Math.Max(6f * scale, nameHeight - (1f * scale));
            float panelHeight = Math.Min(availableHeight, (rowHeight * targetRows) + (6f * scale));

            float defaultX = currentCanvasDrawSize.X - panelWidth - padding;
            float maxX = Math.Max(padding, defaultX);
            float maxY = Math.Max(padding, currentCanvasDrawSize.Y - panelHeight - padding);

            return (panelHeight, rowHeight, barHeight, defaultX, panelY, maxX, maxY);
        }

        // Restores saved panel position from config if applicable
        private void EnsurePartyPanelPositionInitialized(float defaultX, float defaultY)
        {
            if (partyPanelPosition.X >= 0f && partyPanelPosition.Y >= 0f) return;

            if (configuration.PartyMemberListOffsetX >= 0f && configuration.PartyMemberListOffsetY >= 0f)
            {
                partyPanelPosition = new Vector2(configuration.PartyMemberListOffsetX, configuration.PartyMemberListOffsetY);
            }
            else
            {
                partyPanelPosition = new Vector2(defaultX, defaultY);
            }
        }

        // Renders each party member as its own "card": Name, up to 5 debuff icons, and a color-coded HP bar
        private void DrawPartyMemberRows(List<PartyMemberRowData> members, float rowHeight, float barHeight, float iconSize, float scale)
        {
            var statusSheet = Service.DataManager.GetExcelSheet<Status>();

            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(2f * scale, 0f));
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];

                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.12f, 0.12f, 0.14f, 0.95f));
                ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4f * scale);
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f * scale, 0f));

                if (ImGui.BeginChild($"PartyMemberRow_{i}", new Vector2(-1, rowHeight), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        selectedEntityId = member.EntityId;
                    }
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(member.DisplayName);

                    int shownDebuffs = 0;
                    for (int d = 0; d < member.Debuffs.Count && shownDebuffs < 5; d++)
                    {
                        var debuff = member.Debuffs[d];
                        var sheetStatus = statusSheet.GetRowOrDefault(debuff.Id);
                        if (!sheetStatus.HasValue || sheetStatus.Value.Icon == 0) continue;

                        var icon = Service.TextureProvider.GetFromGameIcon(sheetStatus.Value.Icon).GetWrapOrDefault();
                        if (icon == null) continue;

                        ImGui.SameLine(0, 2f * scale);
                        ImGui.Image(icon.Handle, new Vector2(iconSize, iconSize));
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{sheetStatus.Value.Name}\n{debuff.Remaining:F1}s");
                        shownDebuffs++;
                    }

                    int hiddenDebuffs = member.Debuffs.Count - shownDebuffs;
                    if (hiddenDebuffs > 0)
                    {
                        ImGui.SameLine(0, 2f * scale);
                        ImGui.TextDisabled($"+{hiddenDebuffs}");
                    }

                    if (member.MaxHp > 0)
                    {
                        float hpPct = Math.Clamp((float)member.CurrentHp / member.MaxHp, 0f, 1f);
                        Vector4 hpColor = hpPct <= 0.16f
                            ? new Vector4(0.78f, 0.22f, 0.22f, 1.0f)
                            : hpPct < 0.50f
                                ? new Vector4(0.72f, 0.66f, 0.25f, 1.0f)
                                : new Vector4(0.18f, 0.72f, 0.30f, 1.0f);

                        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.16f, 0.16f, 0.18f, 1.0f));
                        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, hpColor);
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.94f, 0.97f, 0.94f, 1.0f));
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f * scale, 0f));
                        ImGui.ProgressBar(hpPct, new Vector2(-1, barHeight), $"{member.CurrentHp}/{member.MaxHp}");
                        ImGui.PopStyleVar();
                        ImGui.PopStyleColor(3);
                    }
                    else
                    {
                        ImGui.Dummy(new Vector2(0, barHeight));
                    }
                }

                ImGui.EndChild();
                ImGui.PopStyleVar(2);
                ImGui.PopStyleColor();
            }
            ImGui.PopStyleVar();
        }

        // Right-click drag to reposition the panel
        private void HandlePartyPanelDrag(Vector2 panelScreenPos, Vector2 panelScreenEnd, float padding, float maxX, float maxY)
        {
            bool hoveredPanel = ImGui.IsMouseHoveringRect(panelScreenPos, panelScreenEnd, true);
            if (hoveredPanel && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                isDraggingPartyPanel = true;
            }

            if (!isDraggingPartyPanel) return;

            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                partyPanelPosition += ImGui.GetIO().MouseDelta;
                partyPanelPosition.X = Math.Clamp(partyPanelPosition.X, padding, maxX);
                partyPanelPosition.Y = Math.Clamp(partyPanelPosition.Y, padding, maxY);
            }
            else
            {
                isDraggingPartyPanel = false;
                configuration.PartyMemberListOffsetX = partyPanelPosition.X;
                configuration.PartyMemberListOffsetY = partyPanelPosition.Y;
                configuration.Save();
            }
        }

        // Sorts the party list by tank > healer > melee > ranged > caster
        private static int GetPartyRolePriority(uint classJobId)
        {
            return classJobId switch
            {
                19 or 21 or 32 or 37 => 1, // Tanks
                24 or 28 or 33 or 40 => 2, // Healers
                20 or 22 or 30 or 34 or 39 or 41 => 3, // Melee DPS
                23 or 31 or 38 => 4, // Physical ranged DPS
                25 or 27 or 35 or 42 => 5, // Magical ranged DPS
                _ => 99
            };
        }
    }
}