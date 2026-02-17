using System;
using System.Collections.Generic;
using System.Linq;
using AetherBlackbox.Events;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
            Service.PluginLog.Info($"EndSession: CurrentSession has {CurrentSession.Deaths.Count} deaths BEFORE filter. Window: {CurrentSession.StartTime:HH:mm:ss.fff} to {CurrentSession.EndTime:Value:HH:mm:ss.fff}");
            plugin.PositionRecorder.StopRecording();
            CurrentSession.ReplayData = plugin.PositionRecorder.GetReplayData();

            lock (CurrentSession.Deaths)
            {
                foreach (var death in CurrentSession.Deaths)
                {
                    death.ReplayData = CurrentSession.ReplayData;
                }

                var sortedDeaths = CurrentSession.Deaths.OrderByDescending(d => d.TimeOfDeath).ToList();
                CurrentSession.Deaths.Clear();
                CurrentSession.Deaths.AddRange(sortedDeaths);
            }

            // Determine Boss Name (highest damage taken)
            if (CurrentSession.DamageByTarget.Count > 0)
            {
                var bossName = CurrentSession.DamageByTarget.OrderByDescending(x => x.Value).First().Key;
                CurrentSession.ZoneName = bossName;
            }

            if (CurrentSession.ReplayData?.Header != null)
            {
                CurrentSession.ReplayData.Header.DeathLog = CurrentSession.Deaths
                    .Select(d => $"{d.PlayerName} ({(d.TimeOfDeath - CurrentSession.StartTime):mm\\:ss})")
                    .ToList();
            }

            History.Add(CurrentSession);
            var sessionToSave = CurrentSession;
            Task.Run(() => SaveSession(sessionToSave));

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
                return Type.GetType($"AetherBlackbox.Events.CombatEvent+{typeName}") ?? typeof(Events.CombatEvent);
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
                using var reader = new BinaryReader(fs);

                reader.ReadString();

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

                var session = new PullSession
                {
                    StartTime = File.GetCreationTime(path),
                    PullNumber = (uint)(History.Count + 1),
                    ZoneName = "Imported",
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

                using var gzip = new GZipStream(fs, CompressionLevel.Optimal);
                using var sw = new StreamWriter(gzip);
                using var jw = new JsonTextWriter(sw);
                var serializer = new JsonSerializer
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    SerializationBinder = new CategoryShortener()
                };
                serializer.Serialize(jw, body);

                Service.PluginLog.Debug($"[PullManager] Saved replay: {filename}");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"[PullManager] Failed to save replay #{session.PullNumber}");
            }
        }
        public void Dispose()
        {
            History.Clear();
            CurrentSession = null;
        }
    }
}