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
        private Dictionary<char, Vector2> allianceGroupPositions = [];
        private char? draggingAllianceGroup = null;
        private HashSet<char> draggingCluster = [];

        private void DrawAllianceOverlay(Vector2 canvasStartPos)
        {
            if (ActiveDeathReplay?.ReplayData?.Frames == null || ActiveDeathReplay.ReplayData.Frames.Count == 0) return;

            float scale = ImGuiHelpers.GlobalScale;
            float padding = 10f * scale;
            float rowHeight = 18f * scale;
            float hpBarWidth = 30f * scale;
            float hpBarHeight = 6f * scale;
            float headerHeight = ImGui.GetTextLineHeightWithSpacing() + (8f * scale);

            float deathTimeOffset = selectedPull != null ? (float)(ActiveDeathReplay.TimeOfDeath - selectedPull.StartTime).TotalSeconds : ActiveDeathReplay.ReplayData.Frames.Last().TimeOffset;
            float targetOffset = deathTimeOffset + replayTimeOffset;

            var allMembers = BuildPartyMemberRows();
            var groupedMembers = GroupAllianceMembers(allMembers);

            if (groupedMembers.Count == 0) return;

            var groupSizes = ComputeAllianceGroupSizes(groupedMembers, targetOffset, padding, rowHeight, hpBarWidth, headerHeight, scale);
            EnsureAllianceGroupPositionsInitialized(groupSizes, padding, scale);

            char? hoveredGroup = DrawAllianceGroups(groupedMembers, groupSizes, targetOffset, padding, rowHeight, hpBarWidth, hpBarHeight, scale, canvasStartPos);

            HandleAllianceSnappingAndDrag(groupSizes, padding, hoveredGroup);
        }

        private Dictionary<char, List<PartyMemberRowData>> GroupAllianceMembers(List<PartyMemberRowData> allMembers)
        {
            Dictionary<char, List<PartyMemberRowData>> groupedMembers = [];

            foreach (var member in allMembers)
            {
                if (string.IsNullOrEmpty(member.TeamTag) || member.TeamTag.StartsWith("Party")) continue;

                char tagLetter = '\0';
                if (member.TeamTag.StartsWith("Alliance ") && member.TeamTag.Length > 9)
                {
                    tagLetter = member.TeamTag[9];
                }

                if (tagLetter != '\0')
                {
                    if (!groupedMembers.ContainsKey(tagLetter)) groupedMembers[tagLetter] = [];
                    groupedMembers[tagLetter].Add(member);
                }
            }

            // Fallback for un-updated replays using old strings
            if (groupedMembers.Count == 0)
            {
                var oldAllianceMembers = allMembers.Where(m => m.TeamTag == "Alliance").ToList();
                for (int i = 0; i < oldAllianceMembers.Count; i++)
                {
                    char tagLetter = (char)('A' + (i / 8) + 1);
                    if (!groupedMembers.ContainsKey(tagLetter)) groupedMembers[tagLetter] = [];
                    groupedMembers[tagLetter].Add(oldAllianceMembers[i]);
                }
            }

            return groupedMembers;
        }

        private Dictionary<char, Vector2> ComputeAllianceGroupSizes(Dictionary<char, List<PartyMemberRowData>> groupedMembers, float targetOffset, float padding, float rowHeight, float hpBarWidth, float headerHeight, float scale)
        {
            Dictionary<char, Vector2> groupSizes = [];

            foreach (var kvp in groupedMembers.OrderBy(x => x.Key))
            {
                float widestText = 0f;
                foreach (var member in kvp.Value)
                {
                    float textWidth = ImGui.CalcTextSize(member.DisplayName).X;
                    if (GetActivePhantomJobIconId(member.EntityId, targetOffset) != 0) textWidth += (16f * scale);
                    widestText = Math.Max(widestText, textWidth);
                }

                float width = widestText + hpBarWidth + (padding * 3);
                float height = padding + headerHeight + (kvp.Value.Count * rowHeight) + padding;
                groupSizes[kvp.Key] = new Vector2(width, height);
            }

            return groupSizes;
        }

        private void EnsureAllianceGroupPositionsInitialized(Dictionary<char, Vector2> groupSizes, float padding, float scale)
        {
            float startX = currentCanvasDrawSize.X - padding;
            float currentY = 40f * scale;

            foreach (var kvp in groupSizes.OrderBy(x => x.Key))
            {
                if (!allianceGroupPositions.ContainsKey(kvp.Key))
                {
                    startX -= (kvp.Value.X + padding);
                    if (startX < padding)
                    {
                        startX = currentCanvasDrawSize.X - padding - kvp.Value.X;
                        currentY += kvp.Value.Y + padding;
                    }
                    allianceGroupPositions[kvp.Key] = new Vector2(Math.Max(startX, padding), currentY);
                }
            }
        }

        private char? DrawAllianceGroups(Dictionary<char, List<PartyMemberRowData>> groupedMembers, Dictionary<char, Vector2> groupSizes, float targetOffset, float padding, float rowHeight, float hpBarWidth, float hpBarHeight, float scale, Vector2 canvasStartPos)
        {
            char? hoveredGroup = null;

            foreach (var kvp in groupedMembers.OrderBy(x => x.Key))
            {
                char groupLetter = kvp.Key;
                var members = kvp.Value;
                Vector2 pos = allianceGroupPositions[groupLetter];
                Vector2 size = groupSizes[groupLetter];

                ImGui.SetCursorPos(canvasStartPos + pos);
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.12f, 0.8f));

                if (ImGui.BeginChild($"AllianceGroup_{groupLetter}", size, true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    ImGui.TextDisabled($"Alliance {groupLetter}");
                    ImGui.Separator();

                    foreach (var member in members)
                    {
                        Vector2 rowStart = ImGui.GetCursorScreenPos();
                        if (ImGui.Selectable($"##AllianceRow_{member.EntityId}", selectedEntityId == member.EntityId, ImGuiSelectableFlags.SpanAllColumns, new Vector2(size.X, rowHeight)))
                            selectedEntityId = member.EntityId;

                        Vector2 nextRowPos = ImGui.GetCursorScreenPos();
                        ImGui.SetCursorScreenPos(rowStart + new Vector2(padding, 1f * scale));

                        uint phIconId = GetActivePhantomJobIconId(member.EntityId, targetOffset);
                        if (phIconId != 0)
                        {
                            var icon = Service.TextureProvider.GetFromGameIcon(phIconId).GetWrapOrDefault();
                            if (icon != null)
                            {
                                ImGui.Image(icon.Handle, new Vector2(14f * scale, 14f * scale));
                                ImGui.SameLine(0, 2f * scale);
                            }
                        }

                        ImGui.TextUnformatted(member.DisplayName);

                        if (member.MaxHp > 0)
                        {
                            float hpPct = Math.Clamp((float)member.CurrentHp / member.MaxHp, 0f, 1f);
                            Vector4 hpColor = hpPct <= 0.16f ? new Vector4(0.78f, 0.22f, 0.22f, 1.0f) : hpPct < 0.50f ? new Vector4(0.72f, 0.66f, 0.25f, 1.0f) : new Vector4(0.18f, 0.72f, 0.30f, 1.0f);
                            Vector2 hpPos = new Vector2(rowStart.X + size.X - hpBarWidth - padding, rowStart.Y + (rowHeight - hpBarHeight) * 0.5f);

                            var draw = ImGui.GetWindowDrawList();
                            draw.AddRectFilled(hpPos, hpPos + new Vector2(hpBarWidth, hpBarHeight), ImGui.GetColorU32(new Vector4(0.16f, 0.16f, 0.18f, 1f)));
                            draw.AddRectFilled(hpPos, hpPos + new Vector2(hpBarWidth * hpPct, hpBarHeight), ImGui.GetColorU32(hpColor));
                        }
                        ImGui.SetCursorScreenPos(nextRowPos);
                    }
                    if (ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
                    {
                        hoveredGroup = groupLetter;
                    }
                }
                ImGui.EndChild();
                ImGui.PopStyleColor();
            }

            return hoveredGroup;
        }

        private void HandleAllianceSnappingAndDrag(Dictionary<char, Vector2> sizes, float padding, char? hoveredGroup)
        {
            if (hoveredGroup != null && draggingAllianceGroup == null)
            {
                ImGui.SetTooltip("Right click and drag to move this overlay. Moves full snapped group if top-left is dragged.");

                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    char group = hoveredGroup.Value;
                    draggingAllianceGroup = group;
                    draggingCluster.Clear();

                    var toProcess = new Queue<char>();
                    toProcess.Enqueue(group);
                    draggingCluster.Add(group);

                    while (toProcess.Count > 0)
                    {
                        char curr = toProcess.Dequeue();
                        Vector2 cPos = allianceGroupPositions[curr];
                        Vector2 cSize = sizes[curr];

                        foreach (var other in allianceGroupPositions.Keys)
                        {
                            if (draggingCluster.Contains(other)) continue;
                            Vector2 oPos = allianceGroupPositions[other];
                            Vector2 oSize = sizes[other];

                            bool touchingX = Math.Abs((cPos.X + cSize.X) - oPos.X) <= 5f || Math.Abs((oPos.X + oSize.X) - cPos.X) <= 5f;
                            bool overlapY = cPos.Y < oPos.Y + oSize.Y && cPos.Y + cSize.Y > oPos.Y;

                            bool touchingY = Math.Abs((cPos.Y + cSize.Y) - oPos.Y) <= 5f || Math.Abs((oPos.Y + oSize.Y) - cPos.Y) <= 5f;
                            bool overlapX = cPos.X < oPos.X + oSize.X && cPos.X + cSize.X > oPos.X;

                            if ((touchingX && overlapY) || (touchingY && overlapX))
                            {
                                draggingCluster.Add(other);
                                toProcess.Enqueue(other);
                            }
                        }
                    }

                    char topLeft = draggingCluster.OrderBy(g => allianceGroupPositions[g].X + allianceGroupPositions[g].Y).First();
                    if (group != topLeft)
                    {
                        draggingCluster.Clear();
                        draggingCluster.Add(group);
                    }
                }
            }

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                if (draggingAllianceGroup != null)
                {
                    // todo: update configuration for persistence later
                }
                draggingAllianceGroup = null;
                return;
            }

            if (draggingAllianceGroup != null)
            {
                Vector2 delta = ImGui.GetIO().MouseDelta;

                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;

                foreach (char g in draggingCluster)
                {
                    Vector2 pos = allianceGroupPositions[g];
                    Vector2 size = sizes[g];
                    minX = Math.Min(minX, pos.X);
                    minY = Math.Min(minY, pos.Y);
                    maxX = Math.Max(maxX, pos.X + size.X);
                    maxY = Math.Max(maxY, pos.Y + size.Y);
                }

                float newMinX = minX + delta.X;
                float newMinY = minY + delta.Y;
                float newMaxX = maxX + delta.X;
                float newMaxY = maxY + delta.Y;

                float correctionX = 0f;
                float correctionY = 0f;

                if (newMinX < padding) correctionX = padding - newMinX;
                else if (newMaxX > currentCanvasDrawSize.X - padding) correctionX = (currentCanvasDrawSize.X - padding) - newMaxX;

                if (newMinY < padding) correctionY = padding - newMinY;
                else if (newMaxY > currentCanvasDrawSize.Y - padding) correctionY = (currentCanvasDrawSize.Y - padding) - newMaxY;

                Vector2 appliedDelta = delta + new Vector2(correctionX, correctionY);

                foreach (char g in draggingCluster)
                {
                    allianceGroupPositions[g] += appliedDelta;
                }

                if (draggingCluster.Count == 1)
                {
                    char dragG = draggingAllianceGroup.Value;
                    Vector2 dPos = allianceGroupPositions[dragG];
                    Vector2 dSize = sizes[dragG];

                    foreach (var other in allianceGroupPositions.Keys)
                    {
                        if (draggingCluster.Contains(other)) continue;
                        Vector2 oPos = allianceGroupPositions[other];
                        Vector2 oSize = sizes[other];

                        bool snapLeft = Math.Abs(dPos.X - (oPos.X + oSize.X)) <= 5f;
                        bool snapRight = Math.Abs((dPos.X + dSize.X) - oPos.X) <= 5f;
                        bool overlapY = dPos.Y >= oPos.Y - dSize.Y && dPos.Y <= oPos.Y + oSize.Y;

                        bool snapTop = Math.Abs(dPos.Y - (oPos.Y + oSize.Y)) <= 5f;
                        bool snapBottom = Math.Abs((dPos.Y + dSize.Y) - oPos.Y) <= 5f;
                        bool overlapX = dPos.X >= oPos.X - dSize.X && dPos.X <= oPos.X + oSize.X;

                        if (snapLeft && overlapY)
                            allianceGroupPositions[dragG] = new Vector2(oPos.X + oSize.X, dPos.Y);
                        else if (snapRight && overlapY)
                            allianceGroupPositions[dragG] = new Vector2(oPos.X - dSize.X, dPos.Y);
                        else if (snapTop && overlapX)
                            allianceGroupPositions[dragG] = new Vector2(dPos.X, oPos.Y + oSize.Y);
                        else if (snapBottom && overlapX)
                            allianceGroupPositions[dragG] = new Vector2(dPos.X, oPos.Y - dSize.Y);
                    }

                    Vector2 finalPos = allianceGroupPositions[dragG];
                    finalPos.X = Math.Clamp(finalPos.X, padding, currentCanvasDrawSize.X - dSize.X - padding);
                    finalPos.Y = Math.Clamp(finalPos.Y, padding, currentCanvasDrawSize.Y - dSize.Y - padding);
                    allianceGroupPositions[dragG] = finalPos;
                }
            }
        }
    }
}