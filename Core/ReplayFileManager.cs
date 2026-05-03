using AetherBlackbox.Events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace AetherBlackbox.Core
{
    public class ReplayFileManager
    {
        private readonly Plugin plugin;

        public ReplayFileManager(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void CleanUpOldReplays()
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
                        if (deleted > 0) Service.PluginLog.Info($"[ReplayFileManager] Auto-cleanup: Deleted {deleted} replay files older than {days} days.");
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

        public class CategoryShortener : Newtonsoft.Json.Serialization.ISerializationBinder
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

        public PullSession? LoadSession(string path, int newPullNumber)
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
                    PullNumber = (uint)newPullNumber,
                    ZoneName = extractedZone,
                    ReplayData = new ReplayRecording
                    {
                        Metadata = body.Metadata ?? new Dictionary<uint, ReplayMetadata>(),
                        Frames = body.Frames ?? new List<ReplayFrame>(),
                        Waymarks = body.Waymarks ?? new List<WaymarkSnapshot>()
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

                return session;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"Failed to load {path}");
                return null;
            }
        }

        public void SaveSession(PullSession session)
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
                    writer.Write(headerJson ?? string.Empty);
                }

                var serializer = new JsonSerializer
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    SerializationBinder = new CategoryShortener()
                };

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
                    serializer.Serialize(jw, body);
                }

                Service.PluginLog.Debug($"[ReplayFileManager] Saved replay: {filename}");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"[ReplayFileManager] Failed to save replay #{session.PullNumber}");
            }
        }
    }
}