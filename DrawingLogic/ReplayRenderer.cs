using AetherBlackbox.Core;
using AetherBlackbox.Core.Mechanics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Animation;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Common.Lua;
using Lumina.Excel.Sheets;
using Lumina.Excel.Sheets.Experimental;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using static FFXIVClientStructs.FFXIV.Client.LayoutEngine.ILayoutInstance;

namespace AetherBlackbox.DrawingLogic
{
    public class ReplayRenderer
    {
        // Configurable scale: Pixels per In-Game Yard
        public const float DefaultPixelsPerYard = 8f;

        public record ViewContext(Vector2 CanvasOrigin, Vector2 CanvasSize, Vector3 CenterWorldPos, float Zoom, Vector2 PanOffset);

        public static Vector2 WorldToScreen(Vector3 worldPos, ViewContext view)
        {
            var canvasCenter = (view.CanvasOrigin + (view.CanvasSize / 2)) + view.PanOffset;
            float scale = DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * view.Zoom;
            var relPos = worldPos - view.CenterWorldPos;
            return new(canvasCenter.X + (relPos.X * scale), canvasCenter.Y + (relPos.Z * scale));
        }

        public static Vector3 ScreenToWorld(Vector2 screenPos, ViewContext view)
        {
            var canvasCenter = (view.CanvasOrigin + (view.CanvasSize / 2)) + view.PanOffset;
            float scale = DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * view.Zoom;
            float relX = (screenPos.X - canvasCenter.X) / scale;
            float relZ = (screenPos.Y - canvasCenter.Y) / scale;
            return new(relX + view.CenterWorldPos.X, view.CenterWorldPos.Y, relZ + view.CenterWorldPos.Z);
        }

        public void Draw(ImDrawListPtr drawList, ReplayRecording recording, ReplayFrame frame, float targetOffset, Vector2 canvasOrigin, Vector2 canvasSize, Vector3 centerWorldPos, uint territoryTypeId, bool showNpcs, bool showHp, bool anonymizeNames, float zoom, Vector2 panOffset, Configuration config, PresetManager presetManager)
        {
            var view = new ViewContext(canvasOrigin, canvasSize, centerWorldPos, zoom, panOffset);
            DrawInternal(drawList, recording, frame, targetOffset, territoryTypeId, showNpcs, showHp, anonymizeNames, view, config, presetManager);
        }

        private ReplayRecording? _lastRecording = null;

        private void BuildTimeline(ReplayRecording recording, uint territoryTypeId)
        {
            Service.PluginLog.Debug($"[ABB Timeline] Manifest size: {recording.Header?.AbilityManifest?.Count ?? -1}");
            recording.ArenaTimeline = new ArenaTimeline();
            var arenaDef = ArenaDatabase.Get(territoryTypeId);
            Service.PluginLog.Debug($"[ABB Timeline] Building for Territory {territoryTypeId}. ArenaDef found: {arenaDef != null}");
            if (arenaDef == null) return;

            recording.ArenaTimeline.AddTransition(0f, "P1");

#if DEBUG
            var loggedActionIds = new HashSet<uint>();
            var loggedTransitions = new HashSet<(string PhaseId, uint ActionId)>();
#endif
            int transitionCount = 0;
            foreach (var f in recording.Frames)
            {
                foreach (var actionId in f.Actions.Concat(f.Casts.Select(c => c.ActionId)))
                {
#if DEBUG
                    if (actionId != 0 && loggedActionIds.Add(actionId))
                    {
                        Service.PluginLog.Debug(
                            $"[ABB Timeline] id={actionId} found={recording.Header.AbilityManifest.ContainsKey(actionId)}");
                    }
#endif

                    if (actionId != 0 && recording.Header.AbilityManifest.TryGetValue(actionId, out var actionName))
                    {
                        foreach (var phase in arenaDef.Phases)
                        {
                            bool match = phase.TriggerNames.Any(name => actionName.Contains(name, StringComparison.OrdinalIgnoreCase));
                            if (match)
                            {
                                Service.PluginLog.Debug($"[ABB Timeline] MATCH FOUND: Phase {phase.PhaseId} triggered by {actionName} at {f.TimeOffset:F2}s");
                                recording.ArenaTimeline.AddTransition(f.TimeOffset, phase.PhaseId);
                                transitionCount++;
                            }


                            if (phase.TriggerType == TriggerType.Ability &&
                                phase.TriggerNames.Any(name => actionName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                            {
                                recording.ArenaTimeline.AddTransition(f.TimeOffset, phase.PhaseId);

#if DEBUG
                                if (loggedTransitions.Add((phase.PhaseId, actionId)))
                                {
                                    Service.PluginLog.Debug(
                                        $"[ABB Timeline] {phase.PhaseId} via {actionName} at {f.TimeOffset:F2}s");
                                }
#endif
                            }
                        }
                    }
                }
            }
            Service.PluginLog.Debug($"[ABB Timeline] Finished. Total Transitions Created: {transitionCount}");
        }

        private void DrawInternal(ImDrawListPtr drawList, ReplayRecording recording, ReplayFrame frame, float targetOffset, uint territoryTypeId, bool showNpcs, bool showHp, bool anonymizeNames, ViewContext view, Configuration config, PresetManager presetManager)
        {
            if (frame == null || frame.Ids.Count == 0) return;
            var canvasCenter = (view.CanvasOrigin + (view.CanvasSize / 2)) + view.PanOffset;
            float scale = DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * view.Zoom;

            if (_lastRecording != recording)
            {
                _lastRecording = recording;
                BuildTimeline(recording, territoryTypeId);
            }

            var activePhaseId = recording.ArenaTimeline?.Resolve(targetOffset) ?? "P1";
            var arenaDef = AetherBlackbox.Core.Mechanics.ArenaDatabase.Get(territoryTypeId);
            var activeVisual = arenaDef?.Visuals.GetValueOrDefault(activePhaseId);

            DrawMapBackground(drawList, territoryTypeId, activeVisual, view, recording.Waymarks, config);

            DrawWaymarks(drawList, recording.Waymarks, view);

            for (int i = 0; i < frame.Ids.Count; i++)
            {
                var id = frame.Ids[i];

                if (!recording.Metadata.TryGetValue(id, out var meta))
                    continue;

                bool isBoss = meta.Type == EntityType.Boss;
                bool isNpc = meta.Type == EntityType.Npc;
                bool isPlayer = meta.ClassJobId != 0;
                bool isPet = meta.Type == EntityType.Pet;

                if (!showNpcs && !isBoss && !isPlayer && !isPet)
                    continue;

                var entityPos = new Vector3(frame.X[i], 0, frame.Z[i]);
                var relPos = entityPos - view.CenterWorldPos;

                var screenX = canvasCenter.X + (relPos.X * scale);
                var screenY = canvasCenter.Y + (relPos.Z * scale);
                var screenPos = new Vector2(screenX, screenY);

                if (screenPos.X < view.CanvasOrigin.X - 50 || screenPos.X > view.CanvasOrigin.X + view.CanvasSize.X + 50 ||
                    screenPos.Y < view.CanvasOrigin.Y - 50 || screenPos.Y > view.CanvasOrigin.Y + view.CanvasSize.Y + 50)
                    continue;

                var state = new ReplayEntityState
                {
                    ObjectId = id,
                    Position = entityPos,
                    Rotation = frame.Rot[i],
                    CurrentHp = frame.Hp[i]
                };

                if (isBoss || isNpc)
                {
                    DrawBossIcon(drawList, state, screenPos);
                }
                else if (isPet)
                {
                    if (meta.OwnerId != 0 && recording.Metadata.TryGetValue(meta.OwnerId, out var ownerMeta))
                    {
                        DrawPlayerIcon(drawList, state, ownerMeta, screenPos, 0.4f);
                    }
                }
                else
                {
                    DrawPlayerIcon(drawList, state, meta, screenPos);
                }

                if (config.ShowReplayStatuses && config.ShowCanvasStatusIcons && isPlayer)
                {
                    DrawPlayerMechanicStatuses(drawList, recording, id, targetOffset, screenPos, config);
                }

                if (showHp && (isPlayer || isBoss))
                    DrawHpBar(drawList, state, meta, screenPos);                
            }

        }

        

        private static readonly HashSet<uint> IgnoredStatuses = new()
        {
            317,  // Fey Illumination
            318,  // Whispering Dawn
            1223, // Fey Union
            1942, // Consolation
            1943, // Seraphic Illumination
            715,  // Excogitation
            1917, // Seraphic Veil
            // todo: add any other specific player/pet buff IDs to ignore
        };

        private void DrawPlayerMechanicStatuses(ImDrawListPtr drawList, ReplayRecording recording, uint entityId, float currentTime, Vector2 screenPos, Configuration config)
        {
            var activeStatuses = new Dictionary<uint, ReplayStatus>();

            for (int i = recording.Frames.Count - 1; i >= 0; i--)
            {
                var f = recording.Frames[i];
                if (f.TimeOffset > currentTime) continue;

                int idx = f.Ids.IndexOf(entityId);
                if (idx != -1 && f.Statuses != null && idx < f.Statuses.Count && f.Statuses[idx] != null)
                {
                    foreach (var status in f.Statuses[idx])
                    {
                        float remaining = status.Duration - (currentTime - f.TimeOffset);
                        if (remaining > 0)
                            activeStatuses[status.Id] = new ReplayStatus { Id = status.Id, Duration = remaining, StackCount = status.StackCount, SourceId = status.SourceId };
                    }
                    break;
                }
            }

            int drawCount = 0;
            float iconWidth = 12f * ImGuiHelpers.GlobalScale;
            float iconHeight = 16f * ImGuiHelpers.GlobalScale;
            Vector2 baseOffset = new Vector2(10f, -20f) * ImGuiHelpers.GlobalScale;

            foreach (var status in activeStatuses.Values)
            {
                if (IgnoredStatuses.Contains(status.Id)) continue;

                var sheetStatus = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRowOrDefault(status.Id);
                if (sheetStatus == null || sheetStatus.Value.Icon == 0) continue;

                // Exclusion Rules
                if (sheetStatus.Value.IsFcBuff || status.Duration > 300f) continue;

                bool isPlayerSourced = false;
                if (status.SourceId != 0 && recording.Metadata.TryGetValue(status.SourceId, out var sourceMeta))
                {
                    if (sourceMeta.Type == EntityType.Pet) continue;
                    if (sourceMeta.Type == EntityType.Player) isPlayerSourced = true;
                }

                // Inclusion Rules
                if (isPlayerSourced)
                {
                    bool isNegative = sheetStatus.Value.StatusCategory == 2;
                    bool isAllowedPositive = config.AllowedPlayerStatuses.Contains(status.Id);

                    if (!isNegative && !isAllowedPositive) continue;
                }

                var iconWrap = Service.TextureProvider.GetFromGameIcon(sheetStatus.Value.Icon).GetWrapOrDefault();
                if (iconWrap != null)
                {
                    Vector2 drawPos = screenPos + baseOffset + new Vector2(drawCount * (iconWidth + 2f), 0);
                    drawList.AddImage(iconWrap.Handle, drawPos, drawPos + new Vector2(iconWidth, iconHeight));
                    drawCount++;

                    if (drawCount >= 4) break;
                }
            }
        }

        private void DrawBossIcon(ImDrawListPtr drawList, ReplayEntityState state, Vector2 center)
        {
            if (_bossIconTexture == null)
                _bossIconTexture = TextureManager.GetTexture("PluginImages/svg/boss.svg");

            if (_bossIconTexture == null)
                return;

            float size = 40f * ImGuiHelpers.GlobalScale;

            float rot = state.Rotation + (float)Math.PI;
            float c = (float)Math.Cos(rot), s = (float)Math.Sin(rot);
            Vector2 Rot(float x, float y) => new Vector2(x * c - y * s, x * s + y * c) + center;

            drawList.AddImageQuad(_bossIconTexture.Handle,
                Rot(-size / 2, -size / 2), Rot(size / 2, -size / 2),
                Rot(size / 2, size / 2), Rot(-size / 2, size / 2),
                new(0, 0), new(1, 0), new(1, 1), new(0, 1),
                0xFFFFFFFF);
            float iconRadius = size / 2;
            var rotX = (float)Math.Sin(state.Rotation);
            var rotY = (float)Math.Cos(state.Rotation);
            var arrowStart = center;
            var arrowEnd = center + (new Vector2(rotX, rotY) * (iconRadius + (5f * ImGuiHelpers.GlobalScale)));

            drawList.AddLine(arrowStart, arrowEnd, ImGui.GetColorU32(new Vector4(1f, 0f, 0f, 0.8f)), 3f * ImGuiHelpers.GlobalScale);
        }

        private void DrawPlayerIcon(ImDrawListPtr drawList, ReplayEntityState state, ReplayMetadata meta, Vector2 screenPos, float scale = 1.0f)
        {
            float iconSize = 28f * ImGuiHelpers.GlobalScale * scale;
            float iconRadius = iconSize / 2;

            if (!_jobIconCache.TryGetValue(meta.ClassJobId, out var texture) || texture == null)
            {
                _jobIconRetries.TryAdd(meta.ClassJobId, 0);
                if (_jobIconRetries[meta.ClassJobId] < 60)
                {
                    string iconName = GetJobIconName(meta.ClassJobId);
                    texture = TextureManager.GetTexture($"PluginImages/toolbar/{iconName}");

                    if (texture != null && texture.Handle != IntPtr.Zero)
                        _jobIconCache[meta.ClassJobId] = texture;
                    else
                        _jobIconRetries[meta.ClassJobId]++;
                }
            }

            if (texture != null && texture.Handle != IntPtr.Zero)
            {
                drawList.AddImage(texture.Handle, screenPos - new Vector2(iconRadius), screenPos + new Vector2(iconRadius));
            }
            else
            {
                drawList.AddCircleFilled(screenPos, iconRadius, ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)));
            }

            var rotX = (float)Math.Sin(state.Rotation);
            var rotY = (float)Math.Cos(state.Rotation);
            var arrowStart = screenPos;
            var arrowEnd = screenPos + (new Vector2(rotX, rotY) * (iconRadius + (5f * ImGuiHelpers.GlobalScale)));

            drawList.AddLine(arrowStart, arrowEnd, ImGui.GetColorU32(new Vector4(1f, 1f, 0f, 0.8f)), 2f * ImGuiHelpers.GlobalScale);
        }

        private void DrawHpBar(ImDrawListPtr drawList, ReplayEntityState state, ReplayMetadata meta, Vector2 screenPos)
        {
            if (meta.MaxHp == 0) return;

            float pct = Math.Clamp((float)state.CurrentHp / meta.MaxHp, 0f, 1f);
            float width = 30f * ImGuiHelpers.GlobalScale;
            float height = 4f * ImGuiHelpers.GlobalScale;
            float yOffset = 18f * ImGuiHelpers.GlobalScale;

            var barStart = screenPos + new Vector2(-width / 2, yOffset);
            var barEnd = barStart + new Vector2(width, height);
            var fillEnd = barStart + new Vector2(width * pct, height);

            drawList.AddRectFilled(barStart, barEnd, 0xFF404040);
            drawList.AddRectFilled(barStart, fillEnd, 0xFF00FF00);
            drawList.AddRect(barStart, barEnd, 0xFF000000);
        }
        private uint _lastLoggedTerritory = 0;
        private readonly Dictionary<uint, IDalamudTextureWrap?> _jobIconCache = new();
        private readonly Dictionary<int, IDalamudTextureWrap?> _waymarkIconCache = new();
        private IDalamudTextureWrap? _bossIconTexture;
        private readonly Dictionary<uint, IDalamudTextureWrap?> _mapCache = new();
        private readonly Dictionary<uint, byte> _jobIconRetries = new();
        private readonly Dictionary<int, byte> _waymarkIconRetries = new();
        private string _lastLoggedTexturePath = string.Empty;

        private void DrawMapBackground(ImDrawListPtr drawList, uint territoryTypeId, ArenaVisual? visual, ViewContext view, List<WaymarkSnapshot> waymarks, Configuration config)
        {
            var canvasCenter = (view.CanvasOrigin + (view.CanvasSize / 2)) + view.PanOffset;
            IDalamudTextureWrap? currentTexture = null;
            float finalMapSize = 0f;
            Vector3 mapAnchorPos = view.CenterWorldPos;
            bool isFromDatabase = false;

            bool shouldLog = false;
            string logKey = visual != null ? visual.TexturePath : territoryTypeId.ToString();
            if (_lastLoggedTexturePath != logKey)
            {
                _lastLoggedTexturePath = logKey;
                shouldLog = true;
            }

            if (visual != null && !string.IsNullOrEmpty(visual.TexturePath))
            {
                if (shouldLog) Service.PluginLog.Debug($"[ABB] Requesting database texture: {visual.TexturePath}");
                currentTexture = TextureManager.GetTexture(visual.TexturePath);
                isFromDatabase = true;

                if (shouldLog)
                {
                    if (currentTexture == null) Service.PluginLog.Debug($"[ABB] TextureManager returned null for {visual.TexturePath}");
                    else if (currentTexture.Handle == IntPtr.Zero) Service.PluginLog.Debug($"[ABB] TextureManager returned IntPtr.Zero for {visual.TexturePath}");
                    else Service.PluginLog.Debug($"[ABB] Successfully retrieved handle for {visual.TexturePath}");
                }
            }

            if (currentTexture == null || currentTexture.Handle == IntPtr.Zero)
            {
                if (shouldLog) Service.PluginLog.Debug($"[ABB] Falling back to ResolveMapTexture for Territory {territoryTypeId}");
                if (!_mapCache.TryGetValue(territoryTypeId, out currentTexture) || currentTexture == null || currentTexture.Handle == IntPtr.Zero)
                {
                    currentTexture = ResolveMapTexture(territoryTypeId);
                    if (currentTexture != null) _mapCache[territoryTypeId] = currentTexture;
                }
                isFromDatabase = false;
            }

            if (currentTexture != null && currentTexture.Handle != IntPtr.Zero)
            {
                float scale = DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * view.Zoom;

                if (isFromDatabase && visual != null)
                {
                    finalMapSize = 512f * ImGuiHelpers.GlobalScale * view.Zoom * visual.Scale;

                    if (territoryTypeId == 992 || territoryTypeId == 1321 || territoryTypeId == 1323 || territoryTypeId == 1325 || territoryTypeId == 1327 || territoryTypeId == 755)
                    {
                        finalMapSize *= config.MapScaleMultiplier;
                    }

                    if (visual.AnchorToWaymarks)
                    {
                        var activeWaymarks = waymarks?.Where(w => w.Active).ToList();
                        if (activeWaymarks != null && activeWaymarks.Count > 0)
                        {
                            float avgX = activeWaymarks.Average(w => w.X);
                            float avgZ = activeWaymarks.Average(w => w.Z);
                            mapAnchorPos = new Vector3(avgX, 0, avgZ);
                        }
                    }

                    mapAnchorPos.X += visual.Offset.X + config.MapOffsetX;
                    mapAnchorPos.Z += visual.Offset.Y + config.MapOffsetZ;
                }
                else
                {
                    finalMapSize = 512f * ImGuiHelpers.GlobalScale * view.Zoom;
                }

                var relPos = mapAnchorPos - view.CenterWorldPos;
                var mapScreenCenter = canvasCenter + new Vector2(relPos.X * scale, relPos.Z * scale);

                Vector2 mapTopLeft = mapScreenCenter - new Vector2(finalMapSize / 2);
                Vector2 mapBottomRight = mapScreenCenter + new Vector2(finalMapSize / 2);

                drawList.PushClipRect(view.CanvasOrigin, view.CanvasOrigin + view.CanvasSize, true);
                drawList.AddImage(currentTexture.Handle, mapTopLeft, mapBottomRight);
                drawList.PopClipRect();
            }
            else
            {
                float mapCurrentSize = 1024f * ImGuiHelpers.GlobalScale * view.Zoom;

                Vector2 mapTopLeft = canvasCenter - new Vector2(mapCurrentSize / 2);
                Vector2 mapBottomRight = canvasCenter + new Vector2(mapCurrentSize / 2);

                drawList.PushClipRect(view.CanvasOrigin, view.CanvasOrigin + view.CanvasSize, true);
                drawList.AddRectFilled(mapTopLeft, mapBottomRight, 0x1234FFFF);
                drawList.PopClipRect();
            }
        }

        private IDalamudTextureWrap? ResolveMapTexture(uint territoryTypeId)
        {
            var territoryNullable = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(territoryTypeId);
            if (!territoryNullable.HasValue) return null;

            var territory = territoryNullable.Value;
            string bgPath = territory.Bg.ToString();
            IDalamudTextureWrap? texture = null;
            bool shouldLog = _lastLoggedTerritory != territoryTypeId;

            if (shouldLog)
            {
                _lastLoggedTerritory = territoryTypeId;
                Service.PluginLog.Debug($"[ABB] Resolving Background for Territory {territoryTypeId}...");
            }

            if (!string.IsNullOrEmpty(bgPath))
            {
                var lastSlash = bgPath.LastIndexOf('/');
                if (lastSlash != -1)
                {
                    var folderPath = bgPath.Substring(0, lastSlash);
                    var assetName = bgPath.Substring(lastSlash + 1);

                    if (folderPath.EndsWith("/level", StringComparison.Ordinal))
                        folderPath = folderPath[..^6];

                    string[] searchPaths =
                    {
                        $"{folderPath}/bgpart/floor_a.tex",
                        $"{folderPath}/bgpart/arena_a.tex",
                        $"{folderPath}/bgpart/{assetName}_floor_a.tex",
                        $"{folderPath}/bgpart/{assetName}_arena_a.tex",
                        $"{folderPath}/bgpart/{assetName}/floor_a.tex",
                        $"{folderPath}/bgpart/{assetName}/arena_a.tex",
                    };

                    foreach (var path in searchPaths)
                    {
                        var asset = Service.TextureProvider.GetFromGame(path);
                        if (asset != null)
                        {
                            texture = asset.GetWrapOrDefault();
                            if (texture != null)
                            {
                                if (shouldLog) Service.PluginLog.Debug($"[ABB] Found texture at: {path}");
                                break;
                            }
                        }
                    }
                }
            }

            if (texture == null)
            {
                var mapRef = territory.Map;
                if (mapRef.RowId > 0)
                {
                    var mapData = mapRef.Value;
                    var mapIdStr = mapData.Id.ToString();
                    var split = mapIdStr.Split('/');
                    if (split.Length == 2)
                    {
                        string mapTexPath = $"ui/map/{mapIdStr}/{split[0]}_{split[1]}_m.tex";
                        var mapAsset = Service.TextureProvider.GetFromGame(mapTexPath);
                        if (mapAsset != null) texture = mapAsset.GetWrapOrDefault();
                    }
                }
            }

            if (texture == null && shouldLog)
                Service.PluginLog.Warning($"Failed to resolve ANY background for Territory {territoryTypeId}.");

            return texture;
        }

        

        public void Dispose()
        {
            foreach (var tex in _jobIconCache.Values) tex?.Dispose();
            foreach (var tex in _waymarkIconCache.Values) tex?.Dispose();
            foreach (var tex in _mapCache.Values) tex?.Dispose();
            _bossIconTexture?.Dispose();

            _jobIconCache.Clear();
            _waymarkIconCache.Clear();
            _mapCache.Clear();
            _bossIconTexture = null;
        }

        private string GetJobIconName(uint jobId)
        {
            return jobId switch
            {
                19 => "pld.png",
                20 => "mnk.png",
                21 => "war.png",
                22 => "drg.png",
                23 => "brd.png",
                24 => "whm.png",
                25 => "blm.png",
                26 => "acr.png",
                27 => "smn.png",
                28 => "sch.png",
                29 => "rog.png",
                30 => "nin.png",
                31 => "mch.png",
                32 => "drk.png",
                33 => "ast.png",
                34 => "sam.png",
                35 => "rdm.png",
                36 => "blu.png",
                37 => "gnb.png",
                38 => "dnc.png",
                39 => "rpr.png",
                40 => "sge.png",
                41 => "vpr.png",
                42 => "pct.png",
                _ => "caster.png" // Fallback
            };
        }
        private void DrawWaymarks(ImDrawListPtr drawList, List<WaymarkSnapshot> waymarks, ViewContext view)
        {
            if (waymarks == null) return;

            var canvasCenter = (view.CanvasOrigin + (view.CanvasSize / 2)) + view.PanOffset;
            float scale = DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * view.Zoom;

            foreach (var wm in waymarks)
            {
                if (!wm.Active) continue;

                var relPos = new Vector3(wm.X, 0, wm.Z) - view.CenterWorldPos;
                var screenX = canvasCenter.X + (relPos.X * scale);
                var screenY = canvasCenter.Y + (relPos.Z * scale);
                var screenPos = new Vector2(screenX, screenY);

                if (screenPos.X < view.CanvasOrigin.X - 30 || screenPos.X > view.CanvasOrigin.X + view.CanvasSize.X + 30 ||
                    screenPos.Y < view.CanvasOrigin.Y - 30 || screenPos.Y > view.CanvasOrigin.Y + view.CanvasSize.Y + 30)
                    continue;

                if (!_waymarkIconCache.TryGetValue(wm.ID, out var texture) || texture == null)
                {
                    _waymarkIconRetries.TryAdd(wm.ID, 0);
                    if (_waymarkIconRetries[wm.ID] < 60)
                    {
                        string iconName = wm.ID switch
                        {
                            0 => "A.png",
                            1 => "B.png",
                            2 => "C.png",
                            3 => "D.png",
                            4 => "1_waymark.png",
                            5 => "2_waymark.png",
                            6 => "3_waymark.png",
                            7 => "4_waymark.png",
                            _ => "A.png"
                        };

                        texture = TextureManager.GetTexture($"PluginImages/toolbar/{iconName}");

                        if (texture != null && texture.Handle != IntPtr.Zero)
                            _waymarkIconCache[wm.ID] = texture;
                        else
                            _waymarkIconRetries[wm.ID]++;
                    }
                }


                if (texture != null)
                {
                    float iconSize = 24f * ImGuiHelpers.GlobalScale;
                    drawList.AddImage(texture.Handle, screenPos - new Vector2(iconSize / 2), screenPos + new Vector2(iconSize / 2));
                }
            }
        }
    }
}