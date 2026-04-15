using AetherBlackbox.DrawingLogic;
using Dalamud.Interface.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AetherBlackbox.Core;

namespace AetherBlackbox.Windows
{
    public partial class MainWindow
    {
        private void CaptureCurrentState()
        {
            if (ActiveDeathReplay == null) return;

            lock (captureLock)
            {
                var recording = ActiveDeathReplay.ReplayData;
                var centerPos = cachedArenaCenter ?? new Vector3(100, 0, 100);
                var scene = new RenderContext();

                RoleTranslator.CacheRoleMapping(recording);
                var targetOffset = GetDeathTimeOffset() + replayTimeOffset;
                var currentFrame = GetClosestFrame(recording, targetOffset);

                var projector = new CanvasProjector(currentCanvasDrawSize, centerPos, canvasZoom);

                scene.Annotations.AddRange(ExtractUserDrawings(targetOffset));

                if (currentFrame != null)
                {
                    scene.Entities.AddRange(ExtractPlayerPositions(currentFrame, projector));
                }

                if (recording.Waymarks != null)
                {
                    scene.Waymarks.AddRange(ExtractWaymarks(recording.Waymarks, projector));
                }

                string? fallbackImage = GetFallbackImageName();
                string arenaBackground = fallbackImage != null
                    ? $"PluginImages/arenas/{fallbackImage}.webp"
                    : $"PluginImages/arenas/{ActiveDeathReplay.TerritoryTypeId}.webp";

                if (fallbackImage != null)
                {
                    scene.Background = BuildArenaBackground(fallbackImage, scene.Waymarks, scene.Entities);
                }

                var finalDrawables = scene.CompilePipeline();

                System.Drawing.RectangleF bounds = System.Drawing.RectangleF.Empty;
                bool firstBound = true;
                var renderActions = PrepareRenderActions(finalDrawables, ref bounds, ref firstBound);

                ProcessThumbnailAndStage(arenaBackground, finalDrawables, renderActions, bounds, firstBound);
            }
        }

        private class RenderContext
        {
            public BaseDrawable? Background { get; set; }
            public List<BaseDrawable> Waymarks { get; } = new();
            public List<BaseDrawable> Entities { get; } = new();
            public List<BaseDrawable> Annotations { get; } = new();

            public List<BaseDrawable> CompilePipeline()
            {
                var pipeline = new List<BaseDrawable>();
                if (Background != null) pipeline.Add(Background);
                pipeline.AddRange(Waymarks);
                pipeline.AddRange(Entities);
                pipeline.AddRange(Annotations);
                return pipeline;
            }
        }

        private IEnumerable<BaseDrawable> ExtractUserDrawings(float targetOffset)
        {
            var currentDrawables = PageManager.GetCurrentPageDrawables();
            if (currentDrawables == null) yield break;

            foreach (var d in currentDrawables)
            {
                if (targetOffset >= d.StartTime && targetOffset <= d.EndTime)
                {
                    yield return d.Clone();
                }
            }
        }

        private IEnumerable<BaseDrawable> ExtractPlayerPositions(ReplayFrame currentFrame, CanvasProjector projector)
        {
            for (int i = 0; i < currentFrame.Ids.Count; i++)
            {
                if (i >= currentFrame.X.Count || i >= currentFrame.Z.Count) continue;

                ulong entityId = currentFrame.Ids[i];
                if (RoleTranslator.CachedRoleMap.TryGetValue(entityId, out var roleMode))
                {
                    var worldPos = new Vector3(currentFrame.X[i], 0, currentFrame.Z[i]);
                    var canvasPos = projector.WorldToCanvas(worldPos);

                    yield return new DrawableImage(
                        roleMode,
                        GetRoleImagePath(roleMode),
                        canvasPos / ImGuiHelpers.GlobalScale,
                        new Vector2(30f, 30f),
                        new Vector4(1f, 1f, 1f, 1f)
                    );
                }
            }
        }

        private IEnumerable<BaseDrawable> ExtractWaymarks(List<AetherBlackbox.Core.WaymarkSnapshot> waymarks, CanvasProjector projector)
        {
            foreach (var wm in waymarks)
            {
                if (!wm.Active) continue;
                var worldPos = new Vector3(wm.X, 0, wm.Z);
                var canvasPos = projector.WorldToCanvas(worldPos);

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

                yield return new DrawableImage(DrawMode.Image, $"PluginImages/toolbar/{iconName}", canvasPos / ImGuiHelpers.GlobalScale, new Vector2(24f, 24f), new Vector4(1f,1f, 1f, 1f));
            }
        }

        private string? GetFallbackImageName()
        {
            if (ActiveDeathReplay.TerritoryTypeId is 992 or 1321) return "m9";
            if (ActiveDeathReplay.TerritoryTypeId == 1323) return "m10";
            if (ActiveDeathReplay.TerritoryTypeId == 1325) return "m11p1";
            if (ActiveDeathReplay.TerritoryTypeId == 1327) return "m12p1";
            if (ActiveDeathReplay.TerritoryTypeId == 1238) return "fru";
            if (ActiveDeathReplay.TerritoryTypeId == 1317)
            {
                var boss = ActiveDeathReplay.ReplayData.Metadata.Values.FirstOrDefault(m => m.Type == EntityType.Boss);
                return boss?.Name switch
                {
                    "Darya the Sea-Maid" => "tmtboss1_arena",
                    "Lone Swordmaster" => "tmtboss2_arena",
                    "Pari of Plenty" => "tmtboss3_arena",
                    _ => "tmtboss1_arena"
                };
            }
            return null;
        }

        private BaseDrawable BuildArenaBackground(string fallbackImage, List<BaseDrawable> waymarks, List<BaseDrawable> entities)
        {
            string adExportPath = fallbackImage.StartsWith("tmt")
                ? $"PluginImages.toolbar.{fallbackImage}.jpg"
                : $"PluginImages.toolbar.{fallbackImage}.png";

            DrawMode arenaMode = fallbackImage switch
            {
                "m9" => DrawMode.ArenaM9,
                "m10" => DrawMode.ArenaM10,
                "m11p1" => DrawMode.ArenaM11P1,
                "m12p1" => DrawMode.ArenaM12P1,
                "fru" => DrawMode.ArenaFRU,
                _ => DrawMode.Image
            };

            Vector2 correctCenter = Vector2.Zero;

            if (waymarks.Count > 0)
            {
                float sumX = 0, sumY = 0;
                foreach (var w in waymarks)
                {
                    var box = w.GetBoundingBox();
                    sumX += box.X + (box.Width / 2f);
                    sumY += box.Y + (box.Height / 2f);
                }
                correctCenter = new Vector2(sumX / waymarks.Count, sumY / waymarks.Count);
            }
            else if (entities.Count > 0)
            {
                var box = entities.Last().GetBoundingBox();
                correctCenter = new Vector2(box.X + (box.Width / 2f), box.Y + (box.Height / 2f));
            }

            float normalizedScale = 8f * canvasZoom;
            correctCenter.X += configuration.MapOffsetX * normalizedScale;
            correctCenter.Y += configuration.MapOffsetZ * normalizedScale;

            float arenaSize = 512f * canvasZoom * configuration.MapScaleMultiplier;

            return new DrawableImage(
                arenaMode,
                adExportPath,
                correctCenter,
                new Vector2(arenaSize, arenaSize),
                new Vector4(1f, 1f, 1f, 1f),
                0f
            );
        }

        private List<System.Action<IImageProcessingContext, Vector2, float>> PrepareRenderActions(List<BaseDrawable> combinedDrawables, ref System.Drawing.RectangleF bounds, ref bool firstBound)
        {
            var renderActions = new List<System.Action<IImageProcessingContext, Vector2, float>>();

            foreach (var d in combinedDrawables)
            {
                try
                {
                    if (d is DrawableText textDrawable)
                    {
                        textDrawable.PerformLayout();
                    }
                    var capturedDrawable = d.Clone();

                    var b = capturedDrawable.GetBoundingBox();
                    if (!b.IsEmpty)
                    {
                        if (firstBound) { bounds = b; firstBound = false; }
                        else { bounds = System.Drawing.RectangleF.Union(bounds, b); }
                    }

                    renderActions.Add((ctx, offset, scale) => capturedDrawable.DrawToImage(ctx, offset, scale));
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, $"Failed to prepare render data for: {d.GetType().Name}");
                }
            }

            return renderActions;
        }

        private System.Threading.CancellationTokenSource? _captureCts;

        private void ProcessThumbnailAndStage(string arenaBackground, List<BaseDrawable> combinedDrawables, List<System.Action<IImageProcessingContext, Vector2, float>> renderActions, System.Drawing.RectangleF bounds, bool firstBound)
        {
            _captureCts?.Cancel();
            _captureCts = new System.Threading.CancellationTokenSource();
            var token = _captureCts.Token;

            System.Threading.Tasks.Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;

                byte[] thumbnailBytes = Array.Empty<byte>();
                try
                {
                    using var thumb = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(120, 120);
                    thumb.Mutate(ctx =>
                    {
                        ctx.Clear(Color.DarkGray);

                        try
                        {
                            byte[]? arenaBytes = TextureManager.GetImageData(arenaBackground);
                            if (arenaBytes != null)
                            {
                                using var arenaImg = Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(arenaBytes);
                                arenaImg.Mutate(x => x.Resize(120, 120));
                                ctx.DrawImage(arenaImg, new Point(0, 0), 1f);
                            }
                        }
                        catch (Exception ex)
                        {
                            Service.PluginLog.Error(ex, "Failed to draw arena background to thumbnail.");
                        }

                        float padding = 10f;
                        float exportScale = 1f;
                        var offset = new Vector2(60, 60);

                        if (!firstBound && bounds.Width > 0 && bounds.Height > 0)
                        {
                            float scaleX = (120f - padding * 2) / bounds.Width;
                            float scaleY = (120f - padding * 2) / bounds.Height;
                            exportScale = Math.Min(scaleX, scaleY);

                            offset = new Vector2(
                                -bounds.X * exportScale + padding,
                                -bounds.Y * exportScale + padding
                            );
                        }

                        foreach (var action in renderActions)
                        {
                            if (token.IsCancellationRequested) return;
                            try
                            {
                                action(ctx, offset, exportScale);
                            }
                            catch (Exception ex)
                            {
                                Service.PluginLog.Error(ex, "Drawable failed to render to thumbnail.");
                            }
                        }
                    });

                    if (token.IsCancellationRequested) return;

                    using var ms = new System.IO.MemoryStream();
                    thumb.SaveAsWebp(ms);
                    thumbnailBytes = ms.ToArray();
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Failed to render/encode thumbnail in background thread.");
                }

                try
                {
                    if (token.IsCancellationRequested) return;
                    ExportManager.StageSlide(arenaBackground, combinedDrawables, thumbnailBytes);
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Failed to stage slide in background thread.");
                }
            }, token);
        }

        private string GetRoleImagePath(DrawMode roleMode)
        {
            return roleMode switch
            {
                DrawMode.RoleTank1Image => "PluginImages/toolbar/tank_1.png",
                DrawMode.RoleTank2Image => "PluginImages/toolbar/tank_2.png",
                DrawMode.RoleHealer1Image => "PluginImages/toolbar/healer_1.png",
                DrawMode.RoleHealer2Image => "PluginImages/toolbar/healer_2.png",
                DrawMode.RoleMelee1Image => "PluginImages/toolbar/melee_1.png",
                DrawMode.RoleMelee2Image => "PluginImages/toolbar/melee_2.png",
                DrawMode.RoleRanged1Image => "PluginImages/toolbar/ranged_dps_1.png",
                DrawMode.RoleRanged2Image => "PluginImages/toolbar/ranged_dps_2.png",
                DrawMode.BossImage => "PluginImages/svg/boss.svg",
                _ => "PluginImages/toolbar/StatusPlaceholder.png"
            };
        }
    }
}