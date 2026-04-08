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
        public List<PullSession> History { get; private set; } = new();
        public PullSession? CurrentSession { get; private set; }

        private uint nextPullNumber = 1;

        public bool IsInSession => CurrentSession != null;

        public PullManager(Plugin plugin)
        {
            this.plugin = plugin;
            CleanUpOldReplays();
        }
        private void CleanUpOldReplays()
        {
            try
            {
                int days = plugin.Configuration.KeepReplaysForDays;
                if (days <= 0) return;

                var folder = Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "replays");
                if (!Directory.Exists(folder)) return;

                var cutoff = DateTime.Now.AddDays(-days);
                Task.Run(() =>
                {
                    try
                    {
                        var files = Directory.GetFiles(folder, "*.json.gz");
                        int deleted = 0;
                        foreach (var file in files)
                        {
                            if (File.GetCreationTime(file) < cutoff)
                            {
                                try { File.Delete(file); deleted++; }
                                catch (Exception ex) { Service.PluginLog.Warning(ex, $"Failed to delete old replay file: {file}"); }
                            }
                        }
                        if (deleted > 0) Service.PluginLog.Info($"[PullManager] Auto-cleanup: Deleted {deleted} replay files older than {days} days.");
                    }
                    catch (Exception ex)
                    {
                        Service.PluginLog.Error(ex, "Background cleanup task encountered a critical error.");
                    }
                });
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Failed to clean up old replays.");
            }
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
            Task.Run(() => SaveSession(sessionToSave));

            var headerBroadcast = new[] {
                new {
                    Hash = CurrentSession.IdentityHash,
                    EventCount = CurrentSession.EventCount,
                    Date = CurrentSession.StartTime,
                    Zone = CurrentSession.ZoneName
                }
            };

            plugin.NetworkManager.SendHeadersBroadcastAsync(JsonConvert.SerializeObject(headerBroadcast));

            Service.PluginLog.Information($"Session {CurrentSession.PullNumber} ended. Recorded {CurrentSession.Deaths.Count} deaths.");

            CurrentSession = null;
        }

        private class CategoryShortener : Newtonsoft.Json.Serialization.ISerializationBinder
        {
            public void BindToName(Type type, out string? assemblyName, out string? typeName)
            {
                assemblyName = null;
                typeName = type.Name;
            }

            public Type BindToType(string? assemblyName, string typeName)
            {
                return typeName switch
                {
                    "StatusEffect" => typeof(Events.CombatEvent.StatusEffect),
                    "HoT" => typeof(Events.CombatEvent.HoT),
                    "DoT" => typeof(Events.CombatEvent.DoT),
                    "DamageTaken" => typeof(Events.CombatEvent.DamageTaken),
                    "Healed" => typeof(Events.CombatEvent.Healed),
                    _ => typeof(Events.CombatEvent)
                };
            }
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
                        SerializationBinder = new CategoryShortener()
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
        public class ReplayFileHeader
        {
            public string? FilePath;
            public string? FileName;
            public DateTime CreationTime;
            public SearchHeader? Header;
        }
        private class SavedReplayBody
        {
            public Dictionary<uint, ReplayMetadata>? Metadata { get; set; }
            public List<ReplayFrame>? Frames { get; set; }
            public List<WaymarkSnapshot>? Waymarks { get; set; }
            public List<Death>? Deaths { get; set; }
        }

        public List<ReplayFileHeader> GetSavedReplays()
        {
            var results = new List<ReplayFileHeader>();
            var folder = Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "replays");
            if (!Directory.Exists(folder)) return results;

            foreach (var file in Directory.GetFiles(folder, "*.json.gz"))
            {
                try
                {
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
                    using var reader = new BinaryReader(fs);
                    var headerJson = reader.ReadString();
                    var header = JsonConvert.DeserializeObject<SearchHeader>(headerJson);

                    if (header != null)
                    {
                        results.Add(new ReplayFileHeader
                        {
                            FilePath = file,
                            FileName = Path.GetFileName(file),
                            CreationTime = File.GetCreationTime(file),
                            Header = header
                        });
                    }
                }
                catch (Exception) { /* Ignore bad files */ }
            }
            return results.OrderByDescending(r => r.CreationTime).ToList();
        }

        public PullSession? LoadSession(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                using (var reader = new BinaryReader(fs, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    reader.ReadString();
                }

                using var gzip = new GZipStream(fs, CompressionMode.Decompress);
                using var sr = new StreamReader(gzip);
                using var jr = new JsonTextReader(sr);
                var serializer = new JsonSerializer
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    SerializationBinder = new CategoryShortener()
                };
                var body = serializer.Deserialize<SavedReplayBody>(jr);
                if (body == null) return null;

                DateTime calculatedStartTime = File.GetCreationTime(path);
                DateTime? calculatedEndTime = null;
                string extractedZone = "Imported";

                var fileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
                if (fileName.Length > 20)
                {
                    string datePart = fileName.Substring(0, 19);
                    if (DateTime.TryParseExact(datePart, "yyyy-MM-dd_HH-mm-ss", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                    {
                        calculatedStartTime = parsedDate;
                        extractedZone = fileName.Substring(20).Replace("_", " ");
                    }
                }

                if (body.Frames != null && body.Frames.Count > 0)
                {
                    calculatedEndTime = calculatedStartTime.AddSeconds(body.Frames.Last().TimeOffset);
                }

                var session = new PullSession
                {
                    StartTime = calculatedStartTime,
                    EndTime = calculatedEndTime,
                    PullNumber = (uint)(History.Count + 1),
                    ZoneName = extractedZone,
                    ReplayData = new ReplayRecording
                    {
                        Metadata = body.Metadata,
                        Frames = body.Frames,
                        Waymarks = body.Waymarks
                    },
                    Deaths = body.Deaths ?? new List<Death>()
                };

                if (session.Deaths != null)
                {
                    foreach (var death in session.Deaths)
                    {
                        death.ReplayData = session.ReplayData;
                    }
                }
            
                History.Add(session);
                return session;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"Failed to load {path}");
                return null;
            }
        }
        private void SaveSession(PullSession session)
        {
            try
            {
                var folder = Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "replays");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var invalidChars = Path.GetInvalidFileNameChars();
                var cleanZone = new string(session.ZoneName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
                if (string.IsNullOrWhiteSpace(cleanZone) || cleanZone.All(c => c == '_')) cleanZone = "Unknown";

                var filename = $"{session.StartTime:yyyy-MM-dd_HH-mm-ss}_{cleanZone}.json.gz";
                var fullPath = Path.Combine(folder, filename);

                using var fs = new FileStream(fullPath, FileMode.Create);

                using (var writer = new BinaryWriter(fs, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    var headerJson = JsonConvert.SerializeObject(session.ReplayData.Header);
                    writer.Write(headerJson);
                }

                List<Death> safeDeaths;
                lock (session.Deaths)
                {
                    safeDeaths = session.Deaths.ToList();
                }
                var body = new { session.ReplayData.Metadata, session.ReplayData.Frames, session.ReplayData.Waymarks, Deaths = safeDeaths };

                using (var gzip = new GZipStream(fs, CompressionLevel.Optimal))
                using (var sw = new StreamWriter(gzip))
                using (var jw = new JsonTextWriter(sw))
                {
                    var serializer = new JsonSerializer
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        SerializationBinder = new CategoryShortener()
                    };
                    serializer.Serialize(jw, body);
                }

                Service.PluginLog.Debug($"[PullManager] Saved replay: {filename}");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"[PullManager] Failed to save replay #{session.PullNumber}");
            }
        }
        public string GetLastHeadersJson(int count = 20)
        {
            var headers = History.OrderByDescending(h => h.StartTime)
                                 .Take(count)
                                 .Select(h => new {
                                     Hash = h.IdentityHash,
                                     EventCount = h.EventCount,
                                     Date = h.StartTime,
                                     Zone = h.ZoneName
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