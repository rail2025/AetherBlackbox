using AetherBlackbox.Events;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using System;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public partial class MainWindow
    {
        private void DrawInteractiveLog()
        {
            var logConfig = configuration.InteractiveLog;

            ImGui.TextDisabled("INTERACTIVE LOG");
            ImGui.SameLine();
            ImGui.TextDisabled("(Click time to seek)");
            ImGui.Separator();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("##LogSearch", "Search...", ref logSearchTerm, 100);

            ImGui.SameLine();
            if (ImGui.SmallButton("Columns/Filters"))
            {
                ImGui.OpenPopup("InteractiveLogOptionsPopup");
            }

            if (ImGui.BeginPopup("InteractiveLogOptionsPopup"))
            {
                var showTime = logConfig.ShowTimeColumn;
                if (ImGui.Checkbox("Show Time", ref showTime))
                {
                    logConfig.ShowTimeColumn = showTime;
                    configuration.Save();
                }

                var showHp = logConfig.ShowHpColumn;
                if (ImGui.Checkbox("Show HP", ref showHp))
                {
                    logConfig.ShowHpColumn = showHp;
                    configuration.Save();
                }

                var showSource = logConfig.ShowSourceColumn;
                if (ImGui.Checkbox("Show Source", ref showSource))
                {
                    logConfig.ShowSourceColumn = showSource;
                    configuration.Save();
                }

                var showEvent = logConfig.ShowEventColumn;
                if (ImGui.Checkbox("Show Event", ref showEvent))
                {
                    logConfig.ShowEventColumn = showEvent;
                    configuration.Save();
                }

                ImGui.Separator();

                var onlyDamaging = logConfig.OnlyDamagingEvents;
                if (ImGui.Checkbox("Only Damaging Events", ref onlyDamaging))
                {
                    logConfig.OnlyDamagingEvents = onlyDamaging;
                    configuration.Save();
                }

                var useAbsoluteTime = logConfig.UseAbsolutePullTime;
                if (ImGui.Checkbox("Use Absolute Pull Time", ref useAbsoluteTime))
                {
                    logConfig.UseAbsolutePullTime = useAbsoluteTime;
                    configuration.Save();
                }

                ImGui.EndPopup();
            }
            ImGui.Separator();

            if (ActiveDeathReplay == null)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Select an incident.");
                return;
            }

            bool showTimeCol = logConfig.ShowTimeColumn;
            bool showHpCol = logConfig.ShowHpColumn;
            bool showSourceCol = logConfig.ShowSourceColumn;
            bool showEventCol = logConfig.ShowEventColumn;

            if (!showTimeCol && !showHpCol && !showSourceCol && !showEventCol)
                showEventCol = true;

            int columnCount = 0;
            if (showTimeCol) columnCount++;
            if (showHpCol) columnCount++;
            if (showSourceCol) columnCount++;
            if (showEventCol) columnCount++;

            if (ImGui.BeginTable("InteractiveLogTable", columnCount, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInner | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit))
            {
                if (showTimeCol) ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 50f * ImGuiHelpers.GlobalScale);
                if (showHpCol) ImGui.TableSetupColumn("HP", ImGuiTableColumnFlags.WidthFixed);
                if (showSourceCol) ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 90f * ImGuiHelpers.GlobalScale);
                if (showEventCol) ImGui.TableSetupColumn("Event", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var evt in ActiveDeathReplay.Events.AsEnumerable().Reverse())
                {
                    if (logConfig.OnlyDamagingEvents && !InteractiveLogUtilities.IsDamagingEvent(evt)) continue;
                    if (!InteractiveLogUtilities.MatchesSearch(evt, logSearchTerm, ActiveDeathReplay.ReplayData)) continue;

                    ImGui.TableNextRow();

                    float relativeSeconds = (float)(evt.Snapshot.Time - ActiveDeathReplay.TimeOfDeath).TotalSeconds;
                    float deathOffset = GetDeathTimeOffset();
                    float absoluteSeconds = deathOffset + relativeSeconds;

                    if (showTimeCol)
                    {
                        ImGui.TableNextColumn();
                        float shownTime = logConfig.UseAbsolutePullTime ? absoluteSeconds : relativeSeconds;

                        if (ImGui.Selectable($"{shownTime:F1}s##{evt.GetHashCode()}", false, ImGuiSelectableFlags.SpanAllColumns))
                        {
                            syncTarget = null;
                            replayTimeOffset = logConfig.UseAbsolutePullTime ? shownTime - deathOffset : shownTime;
                            isPlaybackActive = false;
                            BroadcastTimeSync();
                        }
                    }

                    if (showHpCol)
                    {
                        ImGui.TableNextColumn();
                        float hpPct = evt.Snapshot.MaxHp > 0 ? (float)evt.Snapshot.CurrentHp / evt.Snapshot.MaxHp : 0f;
                        Vector4 hpColor = hpPct <= 0.16f
                            ? new Vector4(0.78f, 0.22f, 0.22f, 1.0f)
                            : hpPct < 0.50f
                                ? new Vector4(0.72f, 0.66f, 0.25f, 1.0f)
                                : ColorHealing;
                        ImGui.TextColored(hpColor, $"{evt.Snapshot.CurrentHp:N0}");
                    }

                    if (showSourceCol)
                    {
                        ImGui.TableNextColumn();
                        string source = InteractiveLogUtilities.GetSource(evt, ActiveDeathReplay.ReplayData);
                        ImGui.Text(GetAnonymizedName(source));
                    }

                    if (showEventCol)
                    {
                        ImGui.TableNextColumn();
                        DrawEventCell(evt);
                    }
                }
                ImGui.EndTable();
            }
        }

        private void DrawEventCell(CombatEvent evt)
        {
            switch (evt)
            {
                case CombatEvent.DamageTaken dt:
                    var col = dt.DamageType == DamageType.Magic ? ColorDamageMagic : ColorDamagePhysical;
                    if (GetIconImage(dt.Icon) is { } dtIcon) InlineIcon(dtIcon);
                    ImGui.TextColored(col, $"-{dt.Amount:N0} {dt.Action}");
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{dt.DamageType} {(dt.Crit ? "(Crit)" : "")} {(dt.DirectHit ? "(DH)" : "")}");
                    break;

                case CombatEvent.Healed h:
                    if (GetIconImage(h.Icon) is { } hIcon) InlineIcon(hIcon);
                    ImGui.TextColored(ColorHealing, $"+{h.Amount:N0} {h.Action}");
                    break;

                case CombatEvent.StatusEffect s:
                    if (GetIconImage(s.Icon) is { } sIcon) InlineIcon(sIcon);
                    ImGui.TextColored(ColorNeutral, $"{s.Status} ({s.Duration:F0}s)");
                    break;

                default:
                    ImGui.Text(evt.GetType().Name);
                    break;
            }
        }
    }
}