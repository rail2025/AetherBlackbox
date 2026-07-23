using System;
using System.Collections.Generic;
using System.Linq;

namespace AetherBlackbox.Events
{
    public class CombatHistory
    {
        private readonly Dictionary<ulong, List<CombatEvent>> events = new();
        private readonly Plugin plugin;

        public CombatHistory(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void AddEvent(ulong entityId, CombatEvent evt)
        {
            events.AddEntry(entityId, evt);
        }

        public bool RemoveEvents(ulong entityId, out List<CombatEvent> removedEvents)
        {
            return events.Remove(entityId, out removedEvents);
        }

        public void CleanCombatEvents()
        {
            try
            {
                var entriesToRemove = new List<ulong>();

                foreach (var (id, evts) in events)
                {
                    if (evts.Count == 0 || (DateTime.Now - evts.Last().Snapshot.Time).TotalSeconds > plugin.Configuration.KeepCombatEventsForSeconds)
                    {
                        entriesToRemove.Add(id);
                        continue;
                    }

                    var cutOffTime = DateTime.Now - TimeSpan.FromSeconds(plugin.Configuration.KeepCombatEventsForSeconds);
                    for (var i = 0; i < evts.Count; i++)
                    {
                        if (evts[i].Snapshot.Time > cutOffTime)
                        {
                            evts.RemoveRange(0, i);
                            break;
                        }
                    }
                }

                foreach (var entry in entriesToRemove) events.Remove(entry);
                entriesToRemove.Clear();

                foreach (var (id, death) in plugin.DeathsPerPlayer)
                {
                    if (death.Count == 0 || (DateTime.Now - death.Last().TimeOfDeath).TotalMinutes > plugin.Configuration.KeepDeathsForMinutes)
                    {
                        entriesToRemove.Add(id);
                        continue;
                    }

                    var cutOffTime = DateTime.Now - TimeSpan.FromMinutes(plugin.Configuration.KeepDeathsForMinutes);
                    for (var i = 0; i < death.Count; i++)
                    {
                        if (death[i].TimeOfDeath > cutOffTime)
                        {
                            death.RemoveRange(0, i);
                            break;
                        }
                    }
                }

                foreach (var entry in entriesToRemove) plugin.DeathsPerPlayer.Remove(entry);
            }
            catch (Exception e)
            {
                Service.PluginLog.Error(e, "Error while clearing events");
            }
        }
    }
}