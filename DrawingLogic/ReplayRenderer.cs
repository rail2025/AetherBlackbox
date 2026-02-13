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

        public void Draw(ImDrawListPtr drawList, ReplayFrame frame, Dictionary<uint, ReplayMetadata> metadata, List<WaymarkSnapshot> waymarks, Vector2 canvasOrigin, Vector2 canvasSize, Vector3 centerWorldPos, uint territoryTypeId, bool showNpcs, bool showHp, bool anonymizeNames, float zoom, Vector2 panOffset)
        {
            if (frame == null || frame.Ids.Count == 0) return;
            var canvasCenter = (canvasOrigin + (canvasSize / 2)) + panOffset;
            float scale = DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * zoom;

            DrawMapBackground(drawList, territoryTypeId, canvasOrigin, canvasSize, centerWorldPos, zoom, panOffset);
            DrawWaymarks(drawList, waymarks, canvasOrigin, canvasSize, centerWorldPos, zoom, panOffset);

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

                // frame.Z[i] maps to World Z. World Y (height) is ignored unless future fights have overlap vertical?.
                var entityPos = new Vector3(frame.X[i], 0, frame.Z[i]);
                var relPos = entityPos - centerWorldPos;

                var screenX = canvasCenter.X + (relPos.X * scale);
                var screenY = canvasCenter.Y + (relPos.Z * scale);
                var screenPos = new Vector2(screenX, screenY);

                if (screenPos.X < canvasOrigin.X - 50 || screenPos.X > canvasOrigin.X + canvasSize.X + 50 ||
                    screenPos.Y < canvasOrigin.Y - 50 || screenPos.Y > canvasOrigin.Y + canvasSize.Y + 50)
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
            float size = 40f * ImGuiHelpers.GlobalScale;
            var texture = TextureManager.GetTexture("PluginImages/svg/boss.svg");

            if (texture != null)
            {
                float rot = state.Rotation + (float)Math.PI;
                float c = (float)Math.Cos(rot), s = (float)Math.Sin(rot);
                Vector2 Rot(float x, float y) => new Vector2(x * c - y * s, x * s + y * c) + center;

                drawList.AddImageQuad(texture.Handle,
                    Rot(-size / 2, -size / 2), Rot(size / 2, -size / 2),
                    Rot(size / 2, size / 2), Rot(-size / 2, size / 2),
                    new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                    0xFFFFFFFF);
            }
        }

        private void DrawPlayerIcon(ImDrawListPtr drawList, ReplayEntityState state, ReplayMetadata meta, Vector2 screenPos)
        {
            float iconSize = 28f * ImGuiHelpers.GlobalScale;
            float iconRadius = iconSize / 2;

            string iconName = GetJobIconName(meta.ClassJobId);
            var texture = TextureManager.GetTexture($"PluginImages/toolbar/{iconName}");

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

        private void DrawMapBackground(ImDrawListPtr drawList, uint territoryTypeId, Vector2 canvasOrigin, Vector2 canvasSize, Vector3 centerWorldPos, float zoom, Vector2 panOffset)
        {
            var territoryNullable = Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(territoryTypeId);
            if (!territoryNullable.HasValue) return;

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
                            {
                                if (shouldLog) Service.PluginLog.Debug($"[ADR] FOUND 3D Asset: {path}");
                                break;
                            }
                        }
                    }
                }
            }

            if (texture == null)
            {
                var mapRef = territory.Map;
                if (shouldLog) Service.PluginLog.Debug($"3D Asset failed. Trying 2D Map. MapRowId: {mapRef.RowId}");

                if (mapRef.RowId > 0)
                {
                    var mapData = mapRef.Value;
                    var mapIdStr = mapData.Id.ToString();

                    if (shouldLog) Service.PluginLog.Debug($"Map Data ID: '{mapIdStr}'");

                    var split = mapIdStr.Split('/');
                    if (split.Length == 2)
                    {
                        // standard UI map path: ui/map/{region}/{zone}/{region}_{zone}_m.tex maybe
                        string mapTexPath = $"ui/map/{mapIdStr}/{split[0]}_{split[1]}_m.tex";

                        if (shouldLog) Service.PluginLog.Debug($"Constructed Map Path: {mapTexPath}");

                        var mapAsset = Service.TextureProvider.GetFromGame(mapTexPath);
                        if (mapAsset != null)
                        {
                            texture = mapAsset.GetWrapOrDefault();
                            if (shouldLog && texture != null) Service.PluginLog.Debug($"SUCCESS: Loaded 2D Map.");
                        }
                        else if (shouldLog)
                        {
                            Service.PluginLog.Warning($"2D Map file does not exist: {mapTexPath}");
                        }
                    }
                    else if (shouldLog)
                    {
                        Service.PluginLog.Warning($"Map ID '{mapIdStr}' format unexpected (expected 'region/zone').");
                    }
                }
                else if (shouldLog)
                {
                    Service.PluginLog.Warning("Territory has no linked Map row (RowId 0).");
                }
            }

            if (texture != null)
            {
                var canvasCenter = (canvasOrigin + (canvasSize / 2)) + panOffset;
                float mapCurrentSize = 1024f * ImGuiHelpers.GlobalScale * zoom;

                Vector2 mapTopLeft = canvasCenter - new Vector2(mapCurrentSize / 2);
                Vector2 mapBottomRight = canvasCenter + new Vector2(mapCurrentSize / 2);

                drawList.PushClipRect(canvasOrigin, canvasOrigin + canvasSize, true);
                drawList.AddImage(texture.Handle, mapTopLeft, mapBottomRight);
                drawList.PopClipRect();
            }
            else
            {
                var canvasCenter = (canvasOrigin + (canvasSize / 2)) + panOffset;
                float mapCurrentSize = 1024f * ImGuiHelpers.GlobalScale * zoom;

                Vector2 mapTopLeft = canvasCenter - new Vector2(mapCurrentSize / 2);
                Vector2 mapBottomRight = canvasCenter + new Vector2(mapCurrentSize / 2);

                drawList.PushClipRect(canvasOrigin, canvasOrigin + canvasSize, true);
                drawList.AddRectFilled(mapTopLeft, mapBottomRight, 0x1234FFFF);
                drawList.PopClipRect();

                if (shouldLog) Service.PluginLog.Warning($"Failed to resolve ANY background for Territory {territoryTypeId}.");
            }
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
        private void DrawWaymarks(ImDrawListPtr drawList, List<WaymarkSnapshot> waymarks, Vector2 origin, Vector2 size, Vector3 centerWorldPos, float zoom, Vector2 panOffset)
        {
            if (waymarks == null) return;
            var canvasCenter = (origin + (size / 2)) + panOffset;
            float scale = ReplayRenderer.DefaultPixelsPerYard * ImGuiHelpers.GlobalScale * zoom;

            foreach (var wm in waymarks)
            {
                if (!wm.Active) continue;

                var relPos = new Vector3(wm.X, 0, wm.Z) - centerWorldPos;
                var screenX = canvasCenter.X + (relPos.X * scale);
                var screenY = canvasCenter.Y + (relPos.Z * scale);
                var screenPos = new Vector2(screenX, screenY);

                if (screenPos.X < origin.X - 30 || screenPos.X > origin.X + size.X + 30 ||
                    screenPos.Y < origin.Y - 30 || screenPos.Y > origin.Y + size.Y + 30)
                    continue;

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

                var texture = TextureManager.GetTexture($"PluginImages/toolbar/{iconName}");
                if (texture != null)
                {
                    float iconSize = 24f * ImGuiHelpers.GlobalScale;
                    drawList.AddImage(texture.Handle, screenPos - new Vector2(iconSize / 2), screenPos + new Vector2(iconSize / 2));
                }
            }
        }
        
    }
}