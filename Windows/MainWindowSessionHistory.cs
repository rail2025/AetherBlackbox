using AetherBlackbox.Events;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.Windows
{
    public partial class MainWindow
    {
        private void DrawSessionHistory()
        {
            ImGui.TextDisabled("SESSION HISTORY");
            ImGui.Separator();
            if (ImGui.TreeNode("Saved Replays (Disk)"))
            {
                if (ImGui.Button("Refresh List")) cachedSavedReplays = plugin.PullManager.GetSavedReplays();
                ImGui.SameLine();
                if (ImGui.Button("Clear All History"))
                {
                    plugin.PullManager.History.Clear();
                    selectedPull = null;
                    var folder = System.IO.Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "replays");
                    if (System.IO.Directory.Exists(folder))
                    {
                        foreach (var f in System.IO.Directory.GetFiles(folder, "*.json.gz"))
                            try { System.IO.File.Delete(f); } catch { }
                    }
                    cachedSavedReplays = plugin.PullManager.GetSavedReplays();
                }
                if (cachedSavedReplays == null) cachedSavedReplays = plugin.PullManager.GetSavedReplays();

                foreach (var file in cachedSavedReplays)
                {
                    if (ImGui.Selectable($"{file.FileName}"))
                    {
                        var loaded = plugin.PullManager.LoadSession(file.FilePath);
                        if (loaded != null) selectedPull = loaded;
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Created: {file.CreationTime}");
                }
                ImGui.TreePop();
            }
            ImGui.Separator();

            var history = plugin.PullManager.History;
            if (history.Count == 0)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "No sessions recorded.");
                return;
            }

            var groups = new System.Collections.Generic.List<(string Name, string Duty, System.Collections.Generic.List<Core.PullSession> Pulls)>();
            foreach (var pull in history)
            {
                int lastParen = pull.ZoneName.LastIndexOf('(');
                string baseName = lastParen > 0 ? pull.ZoneName.Substring(0, lastParen).Trim() : pull.ZoneName;

                int dashIdx = baseName.IndexOf(" - ");
                string dutyName = dashIdx > 0 ? baseName.Substring(0, dashIdx).Trim() : baseName;

                if (groups.Count == 0 || groups.Last().Duty != dutyName) groups.Add((baseName, dutyName, new System.Collections.Generic.List<Core.PullSession>()));
                groups.Last().Pulls.Add(pull);
            }

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                var group = groups[i];
                bool containsSelected = selectedPull != null && group.Pulls.Contains(selectedPull);
                var groupFlags = ImGuiTreeNodeFlags.SpanFullWidth | (containsSelected ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);

                if (ImGui.TreeNodeEx($"##Group_{i}", groupFlags, group.Duty))
                {
                    for (int j = group.Pulls.Count - 1; j >= 0; j--)
                    {
                        var pull = group.Pulls[j];
                        int lastParen = pull.ZoneName.LastIndexOf('(');
                        string hpStr = lastParen > 0 ? pull.ZoneName.Substring(lastParen).Replace("(", "").Replace(")", "").Replace("%%", "%").Trim() : "??%";

                        int dashIdx = pull.ZoneName.IndexOf(" - ");
                        string bossName = dashIdx >= 0 && lastParen > dashIdx
                            ? pull.ZoneName.Substring(dashIdx + 3, lastParen - dashIdx - 3).Trim()
                            : "";

                        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth;
                        if (selectedPull == pull) flags |= ImGuiTreeNodeFlags.DefaultOpen;

                        var title = $"Pull #{pull.PullNumber}, {hpStr} | {bossName} ({pull.Duration:mm\\:ss})" + (pull.IsTruncated ? " [TRUNC]" : "");
                        bool isOpen = ImGui.TreeNodeEx($"##Pull_{pull.PullNumber}", flags, title);

                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton($"##Delete_{pull.PullNumber}", FontAwesomeIcon.Trash))
                        {
                            plugin.PullManager.History.Remove(pull);
                            if (selectedPull == pull) selectedPull = null;
                            continue;
                        }

                        if (ImGui.IsItemClicked())
                        {
                            selectedPull = pull;
                            Service.PluginLog.Info($"User clicked Pull #{pull.PullNumber}. Internal Death Count: {pull.Deaths.Count}");
                        }

                        if (isOpen)
                        {
                            foreach (var death in pull.Deaths)
                            {
                                ImGui.Indent();
                                bool isSelected = (death == ActiveDeathReplay);

                                string playerName = death.ReplayData?.Metadata != null && death.ReplayData.Metadata.TryGetValue(death.PlayerId, out var meta) ? meta.Name : "Unknown";
                                string displayName = GetAnonymizedName(playerName, death.ReplayData);
                                var timeIntoPull = death.TimeOfDeath - pull.StartTime;
                                string deathLabel = $"{displayName} ({timeIntoPull:mm\\:ss})";

                                if (ImGui.Selectable(deathLabel, isSelected))
                                {
                                    OpenReplay(death);
                                }
                                ImGui.Unindent();
                            }
                            ImGui.TreePop();
                        }
                    }
                    ImGui.TreePop();
                }
            }
        }
    }
}