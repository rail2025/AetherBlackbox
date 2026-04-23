using AetherBlackbox.Events;
using AetherBlackbox.Networking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace AetherBlackbox.Core
{
    public class PullManager : IDisposable
    {
        private readonly Plugin plugin;
        public ReplayFileManager FileManager { get; private set; }
        public List<PullSession> History { get; private set; } = new();
        public PullSession? CurrentSession { get; private set; }

        private uint nextPullNumber = 1;

        public bool IsInSession => CurrentSession != null;

        public PullManager(Plugin plugin)
        {
            this.plugin = plugin;
            this.FileManager = new ReplayFileManager(plugin);
            this.FileManager.CleanUpOldReplays();
        }

        public void StartSession()
        {
            if (CurrentSession != null)
            {
                EndSession();
            }
            var lastSession = History.LastOrDefault();
            if (lastSession != null && lastSession.Deaths.Count == 0 && lastSession.DamageByTarget.Values.Sum() == 0)
            {
                History.Remove(lastSession);
                Service.PluginLog.Info($"[PullManager] Removed junk session #{lastSession.PullNumber}");
            }
            CurrentSession = new PullSession
            {
                PullNumber = (uint)(History.Count + 1),
                StartTime = DateTime.Now
            };

            Service.PluginLog.Info($"Session #{CurrentSession.PullNumber} Started at {CurrentSession.StartTime:HH:mm:ss.fff}");

            plugin.PositionRecorder.StartRecording();
        }

        public void EndSession(bool isWipe = false)
        {
            if (CurrentSession == null) return;

            CurrentSession.EndTime = DateTime.Now;
            Service.PluginLog.Info($"EndSession: CurrentSession has {CurrentSession.Deaths.Count} deaths BEFORE filter.");
            plugin.PositionRecorder.StopRecording();
            CurrentSession.ReplayData = plugin.PositionRecorder.GetReplayData();

            long totalDamage = CurrentSession.DamageByTarget.Values.Sum();
            
            // DEBUG: keep sessions even if empty
            //Service.PluginLog.Warning($"[DEBUG] totalDamage={totalDamage}, deaths={CurrentSession.Deaths.Count}");

            lock (CurrentSession.Deaths)
            {
                var sorted = CurrentSession.Deaths.OrderByDescending(d => d.TimeOfDeath).ToList();
                CurrentSession.Deaths.Clear();
                foreach (var death in sorted)
                {
                    death.ReplayData = CurrentSession.ReplayData;
                    CurrentSession.Deaths.Add(death);
                }
            }

            // Determine Boss Name (highest max HP in metadata)
            string bossName = "Unknown Boss";
            float hpPct = 0f;

            if (CurrentSession.ReplayData?.Metadata != null && CurrentSession.ReplayData.Metadata.Count > 0)
            {
                var bossKvp = CurrentSession.ReplayData.Metadata
                    .OrderByDescending(kvp => kvp.Value.MaxHp)
                    .FirstOrDefault();

                if (bossKvp.Key != 0 && !string.IsNullOrEmpty(bossKvp.Value.Name))
                {
                    bossName = bossKvp.Value.Name;
                    uint bossHp = 0;
                    uint bossMaxHp = bossKvp.Value.MaxHp;

                    if (CurrentSession.ReplayData.Frames != null && CurrentSession.ReplayData.Frames.Count > 0)
                    {
                        var lastFrame = CurrentSession.ReplayData.Frames.Last();
                        int idx = lastFrame.Ids.IndexOf(bossKvp.Key);
                        if (idx != -1 && lastFrame.Hp.Count > idx)
                        {
                            bossHp = lastFrame.Hp[idx];
                        }
                    }

                    if (bossMaxHp > 0)
                        hpPct = ((float)bossHp / bossMaxHp) * 100f;
                }
            }

            var terriRow = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(Service.ClientState.TerritoryType);
            string terriName = (terriRow.HasValue && terriRow.Value.PlaceName.RowId != 0)
                ? (terriRow.Value.PlaceName.ValueNullable?.Name.ToString() ?? "Unknown Zone") : "Unknown Zone";

            CurrentSession.ZoneName = $"{terriName} - {bossName} ({hpPct:F1}%%)";
        

            if (CurrentSession.ReplayData?.Header != null)
            {
                CurrentSession.ReplayData.Header.DeathLog = CurrentSession.Deaths
                    .Select(d => $"{d.PlayerName} ({(d.TimeOfDeath - CurrentSession.StartTime):mm\\:ss})")
                    .ToList();
            }

            History.Add(CurrentSession);
            var sessionToSave = CurrentSession;
            Task.Run(() => FileManager.SaveSession(sessionToSave));

            var headerBroadcast = new[] {
                new {
                    Hash = CurrentSession.IdentityHash,
                    EventCount = CurrentSession.EventCount,
                    Date = CurrentSession.StartTime,
                    Zone = CurrentSession.ZoneName,
                    ZoneName = terriName,
                    BossName = bossName,
                    LowestHpPercent = hpPct
                }
            };

            plugin.NetworkManager.SendHeadersBroadcastAsync(JsonConvert.SerializeObject(headerBroadcast));

            Service.PluginLog.Information($"Session {CurrentSession.PullNumber} ended. Recorded {CurrentSession.Deaths.Count} deaths.");

            CurrentSession = null;
        }

        public void AddDeath(Death death)
        {
            if (IsInSession && CurrentSession != null)
            {
                lock (CurrentSession.Deaths)
                {
                    CurrentSession.Deaths.Add(death);
                }
                return;
            }

            var lastSession = History.LastOrDefault();
            if (lastSession != null && lastSession.EndTime.HasValue)
            {
                // 15-second grace period for late packets
                if ((death.TimeOfDeath - lastSession.EndTime.Value).TotalSeconds <= 15)
                {
                    lock (lastSession.Deaths)
                    {
                        lastSession.Deaths.Add(death);
                    }
                    Service.PluginLog.Information($"Late arrival: Attached death of {death.PlayerName} to finished Session #{lastSession.PullNumber}");
                }
            }
        }
        public void UploadReplayByHash(string hash)
        {
            var session = History.FirstOrDefault(h => h.IdentityHash == hash);
            if (session == null) return;

            Service.PluginLog.Info($"Web Client requested replay: {hash}. Uploading EncounterSync...");

            
            byte[] replayBinary = Array.Empty<byte>();
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    var headerJson = JsonConvert.SerializeObject(session.ReplayData?.Header);
                    writer.Write(headerJson ?? "");
                }

                List<Death> safeDeaths;
                lock (session.Deaths)
                {
                    safeDeaths = session.Deaths.ToList();
                }
                //var body = new { session.ReplayData?.Metadata, session.ReplayData?.Frames, session.ReplayData?.Waymarks, Deaths = safeDeaths };
                uint bossId = 0;
                if (session.ReplayData?.Metadata != null)
                {
                    bossId = session.ReplayData.Metadata
                        .OrderByDescending(kvp => kvp.Value.MaxHp)
                        .Select(kvp => kvp.Key)
                        .FirstOrDefault();
                }

                var body = new
                {
                    session.ReplayData?.Metadata,
                    session.ReplayData?.Frames,
                    session.ReplayData?.Waymarks,
                    Deaths = safeDeaths,
                    BossId = bossId,
                    ZoneName = session.ZoneName
                };
                using (var gzip = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                using (var sw = new StreamWriter(gzip))
                using (var jw = new JsonTextWriter(sw))
                {
                    var serializer = new JsonSerializer
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        SerializationBinder = new ReplayFileManager.CategoryShortener()
                    };
                    serializer.Serialize(jw, body);
                }
                replayBinary = ms.ToArray();
            }

            var payload = new NetworkPayload
            {
                PageIndex = -1,
                Action = PayloadActionType.EncounterSync,
                Data = replayBinary
            };

            plugin.NetworkManager.SendStateUpdateAsync(payload);
            Service.PluginLog.Info($"[PullManager] Finished uploading EncounterSync for {hash}. Size: {replayBinary.Length / 1024} KB");
        }
        public List<ReplayFileManager.ReplayFileHeader> GetSavedReplays()
        {
            return FileManager.GetSavedReplays();
        }

        public PullSession? LoadSession(string path)
        {
            var session = FileManager.LoadSession(path, History.Count + 1);
            if (session != null) History.Add(session);
            return session;
        }
        public string GetLastHeadersJson(int count = 20)
        {
            var headers = History.OrderByDescending(h => h.StartTime)
                                 .Take(count)
                                 .Select(h => {
                                     string zone = h.ZoneName;
                                     string boss = "Unknown";
                                     float hp = 0f;
                                     if (!string.IsNullOrEmpty(h.ZoneName) && h.ZoneName.Contains(" - "))
                                     {
                                         var parts = h.ZoneName.Split(new[] { " - " }, 2, StringSplitOptions.None);
                                         zone = parts[0];
                                         int pctIndex = parts[1].LastIndexOf(" (");
                                         if (pctIndex > 0)
                                         {
                                             boss = parts[1].Substring(0, pctIndex);
                                             string pctStr = parts[1].Substring(pctIndex + 2).Replace("%", "").Replace(")", "");
                                             float.TryParse(pctStr, out hp);
                                         }
                                         else
                                         {
                                             boss = parts[1];
                                         }
                                     }
                                     return new
                                     {
                                         Hash = h.IdentityHash,
                                         EventCount = h.EventCount,
                                         Date = h.StartTime,
                                         Zone = h.ZoneName,
                                         ZoneName = zone,
                                         BossName = boss,
                                         LowestHpPercent = hp
                                     };
                                 }).ToList();

            return JsonConvert.SerializeObject(headers);
        }
        public void Dispose()
        {
            History.Clear();
            CurrentSession = null;
        }
    }
}