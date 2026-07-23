using System;
using AetherBlackbox.Core;

namespace AetherBlackbox.Events
{
    public class DeathRecorder
    {
        private readonly Plugin plugin;
        private readonly CombatHistory history;

        public DeathRecorder(Plugin plugin, CombatHistory history)
        {
            this.plugin = plugin;
            this.history = history;
        }

        public void ProcessDeath(uint entityId)
        {
            Service.PluginLog.Info($"Packet: Death received for EntityId {entityId}");

            if (history.RemoveEvents(entityId, out var events))
            {
                var replayData = plugin.PositionRecorder.GetReplayData();
                var death = new Death
                {
                    PlayerId = entityId,
                    TimeOfDeath = DateTime.Now,
                    Events = events,
                    ReplayData = replayData,
                    TerritoryTypeId = Service.ClientState.TerritoryType
                };

                Service.PluginLog.Info($"Capture: Created Death object for {entityId} at {death.TimeOfDeath:HH:mm:ss.fff}");
                plugin.DeathsPerPlayer.AddEntry(entityId, death);
                plugin.NotificationHandler.DisplayDeath(death);
                plugin.PullManager.AddDeath(death);
            }
            else
            {
                Service.PluginLog.Info($"Capture: Death packet received but no combat events found for {entityId}");
            }
        }
    }
}