using AetherBlackbox.DrawingLogic;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AetherBlackbox.Core
{
    public class PluginAssemblyBinder : Newtonsoft.Json.Serialization.ISerializationBinder
    {
        public System.Type BindToType(string assemblyName, string typeName)
        {
            return typeof(PluginAssemblyBinder).Assembly.GetType(typeName) ?? System.Type.GetType(typeName);
        }

        public void BindToName(System.Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = serializedType.FullName;
        }
    }
    public class PlanExportPayload
    {
        public string Version { get; set; } = "1.0";
        public List<PlanPagePayload> Pages { get; set; } = new();
    }

    public class PlanPagePayload
    {
        public string Background { get; set; } = string.Empty;
        public byte[] DrawableData { get; set; } = System.Array.Empty<byte>();
    }

    public class SlideSnapshot
    {
        public string ArenaBackgroundPath { get; set; } = string.Empty;
        public byte[] SerializedDrawables { get; set; } = System.Array.Empty<byte>();
        public byte[] ThumbnailBytes { get; set; } = System.Array.Empty<byte>();
        public int SerializerVersion { get; set; }
    }

    public class PlanExportManager
    {
        public const int CURRENT_SERIALIZER_VERSION = 1;
        private readonly List<SlideSnapshot> _stagedSlides = new();
        private readonly object _slideLock = new object();

        public IReadOnlyList<SlideSnapshot> StagedSlides
        {
            get
            {
                lock (_slideLock)
                {
                    return _stagedSlides.ToList().AsReadOnly();
                }
            }
        }

        public void StageSlide(string arenaBackgroundPath, IEnumerable<BaseDrawable> drawables, byte[] thumbnailBytes)
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new PluginAssemblyBinder()
            };
            var json = JsonConvert.SerializeObject(drawables, settings);
            var bytes = Encoding.UTF8.GetBytes(json);

            lock (_slideLock)
            {
                _stagedSlides.Add(new SlideSnapshot
                {
                    ArenaBackgroundPath = arenaBackgroundPath,
                    SerializedDrawables = bytes,
                    ThumbnailBytes = thumbnailBytes,
                    SerializerVersion = CURRENT_SERIALIZER_VERSION
                });
            }
        }

        public void Clear()
        {
            lock (_slideLock) { _stagedSlides.Clear(); }
        }

        public void SwapSlides(int indexA, int indexB)
        {
            lock (_slideLock)
            {
                if (indexA >= 0 && indexA < _stagedSlides.Count && indexB >= 0 && indexB < _stagedSlides.Count)
                {
                    var temp = _stagedSlides[indexA];
                    _stagedSlides[indexA] = _stagedSlides[indexB];
                    _stagedSlides[indexB] = temp;
                }
            }
        }

        public void RemoveSlide(int index)
        {
            lock (_slideLock)
            {
                if (index >= 0 && index < _stagedSlides.Count)
                {
                    _stagedSlides.RemoveAt(index);
                }
            }
        }
        public List<BaseDrawable>? DeserializeSlide(SlideSnapshot snapshot)
        {
            var json = Encoding.UTF8.GetString(snapshot.SerializedDrawables);
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new PluginAssemblyBinder()
            };

            /*var drawables = JsonConvert.DeserializeObject<List<BaseDrawable>>(json, settings);
            if (drawables != null)
            {
                foreach (var drawable in drawables)
                {
                    drawable.Translate(new System.Numerics.Vector2(0, -32.5f));
                }
            }
            return drawables;*/
            return JsonConvert.DeserializeObject<List<BaseDrawable>>(json, settings);
        }

        public string GenerateExportPayload()
        {
            var plan = new PlanExportPayload { Version = "1.0" };
            lock (_slideLock)
            {
                foreach (var slide in _stagedSlides)
                {
                    plan.Pages.Add(new PlanPagePayload
                    {
                        Background = slide.ArenaBackgroundPath,
                        DrawableData = slide.SerializedDrawables
                    });
                }
            }
            var json = JsonConvert.SerializeObject(plan);
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        }

        public string GenerateIpcPayload()
        {
            var plan = new PlanExportPayload { Version = "1.0" };
            lock (_slideLock)
            {
                foreach (var slide in _stagedSlides)
                {
                    var drawables = DeserializeSlide(slide);
                    byte[] binaryData = System.Array.Empty<byte>();

                    if (drawables != null)
                    {
                        binaryData = Serialization.DrawableSerializer.SerializePageToBytes(drawables);
                    }
                    else
                    {
                        binaryData = Serialization.DrawableSerializer.SerializePageToBytes(new List<BaseDrawable>());
                    }

                    plan.Pages.Add(new PlanPagePayload
                    {
                        Background = slide.ArenaBackgroundPath,
                        DrawableData = binaryData
                    });
                }
            }
            return JsonConvert.SerializeObject(plan);
        }
    }
}