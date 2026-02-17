using AetherBlackbox;
using Dalamud.Interface.Textures.TextureWraps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using Lumina.Data.Files;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Lumina.Data;

namespace AetherBlackbox.DrawingLogic
{
    public static class TextureManager
    {
        private class ThreadSafeStringSet
        {
            private readonly HashSet<string> _set = new();
            public void Add(string item)
            {
                lock (_set) _set.Add(item);
            }
            public void Remove(string item)
            {
                lock (_set) _set.Remove(item);
            }
            public bool Contains(string item)
            {
                lock (_set) return _set.Contains(item);
            }
        }

        private class TextureEntry
        {
            public IDalamudTextureWrap? Texture;
            public Task<IDalamudTextureWrap>? PendingCreationTask;
        }

        private static readonly ConcurrentDictionary<string, TextureEntry> LoadedTextures = new();
        private static readonly ConcurrentDictionary<string, byte[]?> LoadedImageData = new();
        private static readonly ThreadSafeStringSet FailedDownloads = new();
        private static readonly ThreadSafeStringSet PendingDownloads = new();
        private static readonly Dictionary<string, Task<IDalamudTextureWrap>> PendingCreationTasks = new();
        private static readonly ConcurrentQueue<(string resourcePath, byte[] data)> TextureCreationQueue = new();
        private static readonly HttpClient HttpClient = new();

        static TextureManager()
        {
            HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36");
        }

        public static IDalamudTextureWrap? GetTexture(string resourcePath)
        {
            if (Service.TextureProvider == null || string.IsNullOrEmpty(resourcePath)) return null;
            if (FailedDownloads.Contains(resourcePath)) return null;

            if (resourcePath.StartsWith("luminaicon:"))
            {
                try
                {
                    if (uint.TryParse(resourcePath.AsSpan("luminaicon:".Length), out uint iconId))
                    {
                        var iconPath = $"ui/icon/{iconId / 1000 * 1000:000000}/{iconId:000000}.tex";
                        var iconTex = Service.TextureProvider.GetFromGame(iconPath);

                        if (iconTex != null)
                        {
                            var wrappedTex = iconTex.GetWrapOrDefault();
                            return wrappedTex;
                        }
                    }
                    FailedDownloads.Add(resourcePath);
                    return null;
                }
                catch (Exception ex)
                {
                    Service.PluginLog?.Error(ex, $"[TextureManager] Failed to load Lumina icon: {resourcePath}");
                    FailedDownloads.Add(resourcePath);
                    return null;
                }
            }

            if (!LoadedTextures.TryGetValue(resourcePath, out var entry))
            {
                if (!PendingDownloads.Contains(resourcePath) && !PendingCreationTasks.ContainsKey(resourcePath))
                {
                    if (resourcePath.StartsWith("emoji:"))
                    {
                        string emojiChar = resourcePath.Substring("emoji:".Length);
                        if (!string.IsNullOrEmpty(emojiChar))
                        {
                            Service.PluginLog?.Debug($"[TextureManager] Lazy-loading emoji texture: {emojiChar}");
                            PendingDownloads.Add(resourcePath);
                            Task.Run(() => GenerateAndLoadEmojiTexture(emojiChar, resourcePath));
                        }
                    }
                    else
                    {
                        Service.PluginLog?.Debug($"[TextureManager] New texture request. Initiating download for: {resourcePath}");
                        PendingDownloads.Add(resourcePath);
                        Task.Run(() => LoadTextureInBackground(resourcePath));
                    }
                }
                return null;
            }

            if (entry.PendingCreationTask != null)
                return null;

            if (entry.Texture == null || entry.Texture.Handle.Handle.Equals(IntPtr.Zero))
            {
                LoadedTextures.TryRemove(resourcePath, out _);
                entry.Texture?.Dispose();
                return null;
            }

            return entry.Texture;
        }

        public static void PreloadEmojiTexture(string emojiChar)
        {
            if (string.IsNullOrEmpty(emojiChar)) return;
            string resourcePath = "emoji:" + emojiChar;
            if (LoadedTextures.ContainsKey(resourcePath) || PendingDownloads.Contains(resourcePath))
                return;

            Service.PluginLog?.Debug($"[TextureManager] New emoji texture request. Initiating generation for: {emojiChar}");
            PendingDownloads.Add(resourcePath);
            Task.Run(() => GenerateAndLoadEmojiTexture(emojiChar, resourcePath));
        }

        private static async Task GenerateAndLoadEmojiTexture(string emojiChar, string resourcePath)
        {
            try
            {
                byte[] finalUsableBytes = await EmojiRenderer.RenderEmojiToPngAsync(emojiChar);
                LoadedImageData.TryAdd(resourcePath, finalUsableBytes);
                TextureCreationQueue.Enqueue((resourcePath, finalUsableBytes));
            }
            catch (Exception ex)
            {
                Service.PluginLog?.Error(ex, $"[TextureManager] Failed to generate texture for emoji: {emojiChar}");
                FailedDownloads.Add(resourcePath);
                PendingDownloads.Remove(resourcePath);
            }
        }

        public static byte[]? GetImageData(string resourcePath)
        {
            LoadedImageData.TryGetValue(resourcePath, out var data);
            return data;
        }

        public static void DoMainThreadWork()
        {
            if (Service.TextureProvider == null) return;

            while (TextureCreationQueue.TryDequeue(out var item))
            {
                Service.PluginLog?.Debug($"[TextureManager] Dequeued data for {item.resourcePath}. Starting texture creation task.");
                var creationTask = Service.TextureProvider.CreateFromImageAsync(item.data);
                PendingCreationTasks[item.resourcePath] = creationTask;

                if (!LoadedTextures.TryGetValue(item.resourcePath, out var entry))
                {
                    entry = new TextureEntry();
                    LoadedTextures[item.resourcePath] = entry;
                }
                entry.PendingCreationTask = creationTask;
            }

            if (PendingCreationTasks.Any())
            {
                var completedTasks = PendingCreationTasks.Where(kvp => kvp.Value.IsCompleted).ToList();
                foreach (var completed in completedTasks)
                {
                    var resourcePath = completed.Key;
                    var task = completed.Value;
                    Service.PluginLog?.Debug($"[TextureManager] Task for {resourcePath} completed with status: {task.Status}");
                    try
                    {
                        if (task.IsCompletedSuccessfully && LoadedTextures.TryGetValue(resourcePath, out var entry))
                        {
                            entry.Texture = task.Result;
                            entry.PendingCreationTask = null;
                            Service.PluginLog?.Info($"[TextureManager] Successfully created and cached texture for: {resourcePath}");
                        }
                        else
                        {
                            if (task.Exception != null)
                                Service.PluginLog?.Error(task.Exception, $"[TextureManager] Texture creation task faulted for {resourcePath}");
                            FailedDownloads.Add(resourcePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        Service.PluginLog?.Error(ex, $"[TextureManager] Error processing completed texture task for {resourcePath}");
                        FailedDownloads.Add(resourcePath);
                    }
                    finally
                    {
                        PendingCreationTasks.Remove(resourcePath);
                        PendingDownloads.Remove(resourcePath);
                    }
                }
            }
        }

        private static async Task LoadTextureInBackground(string resourcePath)
        {
            try
            {
                byte[]? rawImageBytes = null;
                if (resourcePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, resourcePath);
                    if (resourcePath.Contains("raidplan.io"))
                    {
                        request.Headers.Referrer = new Uri("https://raidplan.io/");
                    }
                    var response = await HttpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    rawImageBytes = await response.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var targetName = $"{assembly.GetName().Name}.{resourcePath.Replace("\\", ".").Replace("/", ".")}";

                    var foundResource = assembly.GetManifestResourceNames()
                        .FirstOrDefault(n => n.Equals(targetName, StringComparison.OrdinalIgnoreCase));

                    using var resourceStream = foundResource != null ? assembly.GetManifestResourceStream(foundResource) : null;
                    if (resourceStream != null) rawImageBytes = ReadStream(resourceStream);
                }

                if (rawImageBytes == null) throw new Exception("Image byte data was null.");

                byte[]? finalUsableBytes = rawImageBytes;

                if (resourcePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    Service.PluginLog?.Debug($"[TextureManager] Rasterizing SVG for: {resourcePath}");
                    using var stream = new MemoryStream(rawImageBytes);
                    finalUsableBytes = RasterizeSvg(stream);
                }

                else if (resourcePath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    Service.PluginLog?.Debug($"[TextureManager] Converting WebP to PNG for: {resourcePath}");
                    using var image = Image.Load(rawImageBytes);
                    using var ms = new MemoryStream();
                    image.SaveAsPng(ms);
                    finalUsableBytes = ms.ToArray();
                }

                if (finalUsableBytes != null)
                {
                    LoadedImageData.TryAdd(resourcePath, finalUsableBytes);
                    TextureCreationQueue.Enqueue((resourcePath, finalUsableBytes));
                }
                else
                {
                    throw new Exception($"Image processing for {resourcePath} resulted in null byte data.");
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog?.Error(ex, $"[TextureManager] Download/processing failed for: {resourcePath}");
                FailedDownloads.Add(resourcePath);
                PendingDownloads.Remove(resourcePath);
            }
        }

        private static byte[]? RasterizeSvg(Stream svgStream)
        {
            using var svg = new SKSvg();
            if (svg.Load(svgStream) is { } && svg.Picture != null)
            {
                var svgSize = svg.Picture.CullRect;
                var width = (int)Math.Ceiling(svgSize.Width);
                var height = (int)Math.Ceiling(svgSize.Height);

                if (width <= 0) width = 64;
                if (height <= 0) height = 64;

                var info = new SKImageInfo(width, height);

                using var surface = SKSurface.Create(info);
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);

                var matrix = SKMatrix.CreateScale(
                    (float)info.Width / svgSize.Width,
                    (float)info.Height / svgSize.Height);
                canvas.DrawPicture(svg.Picture, in matrix);

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            }
            return null;
        }

        private static byte[] ReadStream(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        public static void Dispose()
        {
            foreach (var texPair in LoadedTextures)
            {
                texPair.Value.Texture?.Dispose();
            }
            LoadedTextures.Clear();
            HttpClient.Dispose();
        }
    }
}