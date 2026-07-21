using AetherBlackbox.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public partial class MainWindow
    {
        private Vector2 alliancePanelPosition = new(-1f, -1f);
        private bool isDraggingAlliancePanel = false;

        private void DrawAllianceOverlay(Vector2 canvasStartPos)
        {
            if (ActiveDeathReplay?.ReplayData?.Frames == null || ActiveDeathReplay.ReplayData.Frames.Count == 0) return;

            float scale = ImGuiHelpers.GlobalScale;
            float padding = 10f * scale;
            float rowHeight = 18f * scale;
            float hpBarWidth = 30f * scale;
            float hpBarHeight = 6f * scale;

            float deathTimeOffset = selectedPull != null ? (float)(ActiveDeathReplay.TimeOfDeath - selectedPull.StartTime).TotalSeconds : ActiveDeathReplay.ReplayData.Frames.Last().TimeOffset;
            float targetOffset = deathTimeOffset + replayTimeOffset;

            var allMembers = BuildPartyMemberRows();
            bool hasExplicitParty = allMembers.Any(m => m.TeamTag == "Party");

            var allianceMembers = new List<PartyMemberRowData>();
            var displayStrings = new List<string>();

            var explicitAlliance = allMembers
                .Where(m => m.TeamTag != "Party" && !string.IsNullOrEmpty(m.TeamTag))
                .OrderBy(m => m.TeamTag)
                .ToList();

            foreach (var member in explicitAlliance)
            {
                allianceMembers.Add(member);
                string tagLetter = member.TeamTag.Replace("Alliance ", "").Trim();
                displayStrings.Add($"{member.DisplayName}-{tagLetter}");
            }

            var unclaimedMembers = allMembers
                .Where(m => string.IsNullOrEmpty(m.TeamTag))
                .ToList();

            if (!hasExplicitParty)
            {
                unclaimedMembers = unclaimedMembers.Skip(8).ToList();
            }

            for (int i = 0; i < unclaimedMembers.Count; i++)
            {
                allianceMembers.Add(unclaimedMembers[i]);
                string tagLetter = (i / 8) switch { 0 => "A", 1 => "B", _ => "C" };
                displayStrings.Add($"{unclaimedMembers[i].DisplayName}-{tagLetter}");
            }

            if (allianceMembers.Count == 0) return;

            float widestText = 0f;

            for (int i = 0; i < allianceMembers.Count; i++)
            {
                var member = allianceMembers[i];
                string disp = displayStrings[i];

                float textWidth = ImGui.CalcTextSize(disp).X;
                if (GetActivePhantomJobIconId(member.EntityId, targetOffset) != 0)
                {
                    textWidth += (14f * scale) + (2f * scale);
                }
                widestText = Math.Max(widestText, textWidth);
            }

            float panelWidth = widestText + hpBarWidth + (padding * 3);
            float contentHeight = padding + (allianceMembers.Count * rowHeight) + padding;
            float panelHeight = Math.Min(currentCanvasDrawSize.Y - (80f * scale), contentHeight);

            if (alliancePanelPosition.X < 0f || alliancePanelPosition.Y < 0f)
            {
                alliancePanelPosition = new Vector2(currentCanvasDrawSize.X - panelWidth - padding - (400f * scale), 40f * scale);
            }

            ImGui.SetCursorPos(canvasStartPos + alliancePanelPosition);
            Vector2 panelScreenPos = ImGui.GetCursorScreenPos();
            Vector2 panelScreenEnd = panelScreenPos + new Vector2(panelWidth, panelHeight);

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.12f, 0.8f));
            if (ImGui.BeginChild("AllianceMembersContainer", new Vector2(panelWidth, panelHeight), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                for (int i = 0; i < allianceMembers.Count; i++)
                {
                    var member = allianceMembers[i];
                    string displayText = displayStrings[i];
                    Vector2 rowStart = ImGui.GetCursorScreenPos();

                    if (ImGui.Selectable($"##AllianceRow_{member.EntityId}", selectedEntityId == member.EntityId, ImGuiSelectableFlags.SpanAllColumns, new Vector2(panelWidth, rowHeight)))
                    {
                        selectedEntityId = member.EntityId;
                    }

                    Vector2 nextRowPos = ImGui.GetCursorScreenPos();

                    ImGui.SetCursorScreenPos(rowStart + new Vector2(padding, 1f * scale));

                    uint phId = GetActivePhantomJobIconId(member.EntityId, targetOffset);
                    if (phId != 0)
                    {
                        var sheetStatus = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>().GetRowOrDefault(phId);
                        if (sheetStatus.HasValue && sheetStatus.Value.Icon != 0)
                        {
                            var icon = Service.TextureProvider.GetFromGameIcon(sheetStatus.Value.Icon).GetWrapOrDefault();
                            if (icon != null)
                            {
                                float iconSize = 14f * scale;
                                ImGui.Image(icon.Handle, new Vector2(iconSize, iconSize));
                                ImGui.SameLine(0, 2f * scale);
                            }
                        }
                    }

                    ImGui.TextUnformatted(displayText);

                    if (member.MaxHp > 0)
                    {
                        float hpPct = Math.Clamp((float)member.CurrentHp / member.MaxHp, 0f, 1f);
                        Vector4 hpColor = hpPct <= 0.16f ? new Vector4(0.78f, 0.22f, 0.22f, 1.0f) : hpPct < 0.50f ? new Vector4(0.72f, 0.66f, 0.25f, 1.0f) : new Vector4(0.18f, 0.72f, 0.30f, 1.0f);

                        Vector2 hpPos = new Vector2(rowStart.X + padding + widestText + padding, rowStart.Y + (rowHeight - hpBarHeight) * 0.5f);

                        var draw = ImGui.GetWindowDrawList();
                        draw.AddRectFilled(hpPos, hpPos + new Vector2(hpBarWidth, hpBarHeight), ImGui.GetColorU32(new Vector4(0.16f, 0.16f, 0.18f, 1f)));
                        draw.AddRectFilled(hpPos, hpPos + new Vector2(hpBarWidth * hpPct, hpBarHeight), ImGui.GetColorU32(hpColor));
                    }

                    ImGui.SetCursorScreenPos(nextRowPos);
                }
            }
            ImGui.EndChild();
            ImGui.PopStyleColor();

            HandleAlliancePanelDrag(panelScreenPos, panelScreenEnd, padding, panelWidth, panelHeight);
        }

        private void HandleAlliancePanelDrag(Vector2 panelScreenPos, Vector2 panelScreenEnd, float padding, float panelWidth, float panelHeight)
        {
            if (ImGui.IsMouseHoveringRect(panelScreenPos, panelScreenEnd, true) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                isDraggingAlliancePanel = true;

            if (isDraggingAlliancePanel)
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
                {
                    alliancePanelPosition += ImGui.GetIO().MouseDelta;
                    alliancePanelPosition.X = Math.Clamp(alliancePanelPosition.X, padding, currentCanvasDrawSize.X - panelWidth - padding);
                    alliancePanelPosition.Y = Math.Clamp(alliancePanelPosition.Y, padding, currentCanvasDrawSize.Y - panelHeight - padding);
                }
                else
                {
                    isDraggingAlliancePanel = false;
                }
            }
        }
    }
}