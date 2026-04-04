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
            ImGui.TextDisabled("INTERACTIVE LOG");
            ImGui.SameLine();
            ImGui.TextDisabled("(Click time to seek)");
            ImGui.Separator();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f * ImGuiHelpers.GlobalScale);
            ImGui.InputTextWithHint("##LogSearch", "Search...", ref logSearchTerm, 100);
            ImGui.Separator();

            if (ActiveDeathReplay == null)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Select an incident.");
                return;
            }

            if (ImGui.BeginTable("InteractiveLogTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.BordersInner | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 50f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 90f * ImGuiHelpers.GlobalScale);
                ImGui.TableSetupColumn("Event", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var evt in ActiveDeathReplay.Events.AsEnumerable().Reverse())
                {
                    if (!string.IsNullOrWhiteSpace(logSearchTerm))
                    {
                        bool match = false;
                        string sSrc = evt switch { CombatEvent.DamageTaken dt => dt.Source, CombatEvent.Healed h => h.Source, CombatEvent.StatusEffect s => s.Source, _ => "" } ?? "";
                        string sAct = evt switch { CombatEvent.DamageTaken dt => dt.Action, CombatEvent.Healed h => h.Action, CombatEvent.StatusEffect s => s.Status, _ => "" } ?? "";

                        if (sSrc.Contains(logSearchTerm, StringComparison.OrdinalIgnoreCase)) match = true;
                        if (sAct.Contains(logSearchTerm, StringComparison.OrdinalIgnoreCase)) match = true;

                        if (!match) continue;
                    }
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    var relativeSeconds = (evt.Snapshot.Time - ActiveDeathReplay.TimeOfDeath).TotalSeconds;

                    if (ImGui.Selectable($"{relativeSeconds:F1}s##{evt.GetHashCode()}", false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        syncTarget = null;
                        replayTimeOffset = (float)relativeSeconds;
                        isPlaybackActive = false;
                        BroadcastTimeSync();
                    }

                    ImGui.TableNextColumn();
                    string source = evt switch
                    {
                        CombatEvent.DamageTaken dt => dt.Source ?? "-",
                        CombatEvent.Healed h => h.Source ?? "-",
                        CombatEvent.StatusEffect s => s.Source ?? "-",
                        _ => "-"
                    };

                    ImGui.Text(GetAnonymizedName(source));

                    ImGui.TableNextColumn();
                    DrawEventCell(evt);
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