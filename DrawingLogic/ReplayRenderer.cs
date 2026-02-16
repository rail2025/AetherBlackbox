using AetherBlackbox.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets;
using Lumina.Excel.Sheets.Experimental;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AetherBlackbox.DrawingLogic
{
    public class ReplayRenderer
    {
        // Configurable scale: Pixels per In-Game Yard
        public const float DefaultPixelsPerYard = 8f;

        public record ViewContext(Vector2 CanvasOrigin, Vector2 CanvasSize, Vector3 CenterWorldPos, float Zoom, Vector2 PanOffset);

        public void Draw(ImDrawListPtr drawList, ReplayFrame frame, Dictionary<uint, ReplayMetadata> metadata, List<WaymarkSnapshot> waymarks, Vector2 canvasOrigin, Vector2 canvasSize, Vector3 centerWorldPos, uint territoryTypeId, bool showNpcs, bool showHp, bool anonymizeNames, float zoom, Vector2 panOffset, Configuration config)
        {
            var view = new ViewContext(canvasOrigin, canvasSize, centerWorldPos, zoom, panOffset);
            DrawInternal(drawList, frame, metadata, waymarks, territoryTypeId, showNpcs, showHp, anonymizeNames, view, config);
        }

        private void DrawInternal(ImDrawListPtr drawList, ReplayFrame frame, Dictionary<uint, ReplayMetadata> metadata, List<WaymarkSnapshot> waymarks, uint territoryTypeId, bool showNpcs, bool showHp, bool anonymizeNames, ViewContext view, Configuration config)
        {
            if (frame == null || frame.Ids.Count == 0) return;
            var canvasCenter = (view.CanvasOrigin + (view.CanvasSize / 2)) + view.PanOffset;
            float scale = DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * view.Zoom;

            DrawMapBackground(drawList, territoryTypeId, view, waymarks, config);
            DrawWaymarks(drawList, waymarks, view);

            for (int i = 0; i < frame.Ids.Count; i++)
            {
                var id = frame.Ids[i];

                if (!metadata.TryGetValue(id, out var meta))
                    continue;

                bool isBoss = meta.Type == EntityType.Boss;
                bool isNpc = meta.Type == EntityType.Npc;
                bool isPlayer = meta.ClassJobId != 0;

                if (!showNpcs && !isBoss && !isPlayer)
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
                    DrawBossIcon(drawList, state, screenPos);
                else
                    DrawPlayerIcon(drawList, state, meta, screenPos);

                if (showHp && (isPlayer || isBoss))
                    DrawHpBar(drawList, state, meta, screenPos);                
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
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                0xFFFFFFFF);
        }

        private void DrawPlayerIcon(ImDrawListPtr drawList, ReplayEntityState state, ReplayMetadata meta, Vector2 screenPos)
        {
            float iconSize = 28f * ImGuiHelpers.GlobalScale;
            float iconRadius = iconSize / 2;

            if (!_jobIconCache.TryGetValue(meta.ClassJobId, out var texture) || texture == null)
            {
                string iconName = GetJobIconName(meta.ClassJobId);
                texture = TextureManager.GetTexture($"PluginImages/toolbar/{iconName}");

                if (texture != null && texture.Handle != IntPtr.Zero)
                    _jobIconCache[meta.ClassJobId] = texture;
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

        private void DrawMapBackground(ImDrawListPtr drawList, uint territoryTypeId, ViewContext view, List<WaymarkSnapshot> waymarks, Configuration config)
        {
            if (!_mapCache.TryGetValue(territoryTypeId, out var texture) || texture == null)
            {
                texture = ResolveMapTexture(territoryTypeId);

                if (texture != null && texture.Handle != IntPtr.Zero)
                    _mapCache[territoryTypeId] = texture;
            }

            var canvasCenter = (view.CanvasOrigin + (view.CanvasSize / 2)) + view.PanOffset;

            if (texture != null)
            {
                float scale = DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * view.Zoom;
                Vector3 mapAnchorPos = view.CenterWorldPos;
                float finalMapSize = 512f * ImGuiHelpers.GlobalScale * view.Zoom;

                if (territoryTypeId == 992 || territoryTypeId == 1321 || territoryTypeId == 1323 || territoryTypeId == 1325 || territoryTypeId == 1327)
                {
                    finalMapSize *= config.MapScaleMultiplier;

                    var activeWaymarks = waymarks?.Where(w => w.Active).ToList();
                    if (activeWaymarks != null && activeWaymarks.Count > 0)
                    {
                        float avgX = activeWaymarks.Average(w => w.X);
                        float avgZ = activeWaymarks.Average(w => w.Z);
                        mapAnchorPos = new Vector3(avgX, 0, avgZ);
                    }

                    mapAnchorPos.X += config.MapOffsetX;
                    mapAnchorPos.Z += config.MapOffsetZ;
                }

                var relPos = mapAnchorPos - view.CenterWorldPos;
                var mapScreenCenter = canvasCenter + new Vector2(relPos.X * scale, relPos.Z * scale);

                Vector2 mapTopLeft = mapScreenCenter - new Vector2(finalMapSize / 2);
                Vector2 mapBottomRight = mapScreenCenter + new Vector2(finalMapSize / 2);

                drawList.PushClipRect(view.CanvasOrigin, view.CanvasOrigin + view.CanvasSize, true);
                drawList.AddImage(texture.Handle, mapTopLeft, mapBottomRight);
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
                Service.PluginLog.Debug($"[ADR] Resolving Background for Territory {territoryTypeId}...");
            }

            if (!string.IsNullOrEmpty(bgPath))
            {
                var lastSlash = bgPath.LastIndexOf('/');
                if (lastSlash != -1)
                {
                    var folderPath = bgPath.Substring(0, lastSlash);
                    var assetName = bgPath.Substring(lastSlash + 1);
                    var rootPath = folderPath.Replace("/level", "").Replace("/bgpart", "");

                    string[] searchPaths = {
                $"{rootPath}/bgpart/{assetName}_floor_a.tex",
                $"{rootPath}/bgpart/{assetName}_arena_a.tex",
                $"{rootPath}/texture/{assetName}_floor_a.tex",
                $"{rootPath}/texture/{assetName}_a.tex",
                $"{rootPath}/texture/{assetName}_d.tex"
            };

                    foreach (var path in searchPaths)
                    {
                        var asset = Service.TextureProvider.GetFromGame(path);
                        if (asset != null)
                        {
                            texture = asset.GetWrapOrDefault();
                            if (texture != null)
                                break;
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
                        if (mapAsset != null)
                            texture = mapAsset.GetWrapOrDefault();
                    }
                }
            }

            if (texture == null)
            {
                string? fallbackImage = territoryTypeId switch
                {
                    992 or 1321 => "m9.webp",
                    1323 => "m10.webp",
                    1325 => "m11p1.webp",
                    1327 => "m12p1.webp",
                    _ => null
                };

                if (fallbackImage != null)
                    texture = TextureManager.GetTexture($"PluginImages/arenas/{fallbackImage}");
            }

            if (texture == null && shouldLog)
                Service.PluginLog.Warning($"Failed to resolve ANY background for Territory {territoryTypeId}.");

            return texture;
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