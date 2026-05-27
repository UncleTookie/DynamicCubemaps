using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using ColossalFramework;
using ColossalFramework.Plugins;
using UnityEngine;

namespace DynamicCubemaps
{
    public static class CubemapController
    {
        public const string Vanilla = "vanilla";
        public const float SunriseStart = 6.0f;
        public const float DayStart = 10.0f;
        public const float SunsetStart = 18.0f;
        public const float NightStart = 21.0f;
        private const float VisualHourOffset = 1.0f;

        private static Cubemap _originalEnv;
        private static Cubemap _originalSpace;
        private static Cubemap _activeEnv;
        private static RenderProperties _render;
        private static DayNightProperties _dayNight;
        private static FogEffect _classicFog;
        private static bool _classicFogCaptured;
        private static bool _classicFogEnabled;
        private static bool _atmosphereCaptured;
        private static bool _nightHazeApplied;
        private static Color _horizonColor;
        private static Color _originalFogColor;
        private static Color _volFogColor;
        private static Color _polluteFogColor;
        private static Color _inscatteringColor;

        private static readonly Color HazeHorizonColor = new Color(0.055f, 0.06f, 0.065f, 1f);
        private static readonly Color HazeFogColor = new Color(0.07f, 0.075f, 0.08f, 1f);
        private static readonly Color HazeVolumeColor = new Color(0.045f, 0.047f, 0.05f, 1f);
        private static readonly Color HazePollutionColor = new Color(0.035f, 0.035f, 0.035f, 1f);
        private static readonly Color HazeInscatteringColor = new Color(0.08f, 0.08f, 0.085f, 1f);

        public enum DayPeriod
        {
            Sunrise,
            Day,
            Sunset,
            Night
        }

        public static DayPeriod GetCurrentPeriod()
        {
            var hour = GetCurrentHour();

            if (hour >= NightStart || hour < SunriseStart)
            {
                return DayPeriod.Night;
            }

            if (hour >= SunsetStart)
            {
                return DayPeriod.Sunset;
            }

            return hour >= DayStart ? DayPeriod.Day : DayPeriod.Sunrise;
        }

        public static int GetCurrentHour()
        {
            var simulation = SimulationManager.instance;
            var frame = (simulation.m_currentFrameIndex + simulation.m_dayTimeOffsetFrames) &
                (SimulationManager.DAYTIME_FRAMES - 1);
            var hour = frame * SimulationManager.DAYTIME_FRAME_TO_HOUR + VisualHourOffset;
            hour %= 24f;
            if (hour < 0f)
            {
                hour += 24f;
            }

            return Mathf.FloorToInt(hour);
        }

        public static void UpdateCubemaps(bool force)
        {
            if (!LoadingExtension.InGame || _dayNight == null)
            {
                return;
            }

            var period = GetCurrentPeriod();
            var useVanillaNight = period == DayPeriod.Night && Options.Current.UseVanillaNight;
            if (useVanillaNight)
            {
                UpdateNightHaze(false);
                ApplyCubemap(_originalEnv, force);
                UpdateClassicFog(true);
            }
            else
            {
                ApplyCubemap(GetCubemap(period), force);
                UpdateNightHaze(period == DayPeriod.Night && Options.Current.FixNightHaze);
                UpdateClassicFog(false);
            }

            if (_originalSpace != null && _dayNight.m_OuterSpaceCubemap != _originalSpace)
            {
                _dayNight.m_OuterSpaceCubemap = _originalSpace;
            }
        }

        public static void Init()
        {
            _render = UnityEngine.Object.FindObjectOfType<RenderProperties>();
            _dayNight = UnityEngine.Object.FindObjectOfType<DayNightProperties>();
            _classicFog = UnityEngine.Object.FindObjectOfType<FogEffect>();

            if (_render != null && _originalEnv == null)
            {
                _originalEnv = _render.m_cubemap;
            }

            if (_dayNight != null && _originalSpace == null)
            {
                _originalSpace = _dayNight.m_OuterSpaceCubemap;
            }

            if (!_classicFogCaptured && _classicFog != null)
            {
                _classicFogEnabled = _classicFog.enabled;
                _classicFogCaptured = true;
            }

            if (!_atmosphereCaptured && _render != null && _dayNight != null)
            {
                _horizonColor = _dayNight.m_NightHorizonColor;
                _originalFogColor = _render.m_fogColor;
                _volFogColor = _render.m_volumeFogColor;
                _polluteFogColor = _render.m_pollutionFogColor;
                _inscatteringColor = _render.m_inscatteringColor;
                _atmosphereCaptured = true;
            }

            CubemapManager.ResetCache();
            CubemapManager.Import();
        }

        public static void Revert()
        {
            UpdateNightHaze(false);
            UpdateClassicFog(false);

            if (_originalEnv != null)
            {
                if (_render != null)
                {
                    _render.m_cubemap = _originalEnv;
                }

                Shader.SetGlobalTexture("_EnvironmentCubemap", _originalEnv);
                _activeEnv = _originalEnv;
            }

            if (_dayNight != null && _originalSpace != null)
            {
                _dayNight.m_OuterSpaceCubemap = _originalSpace;
                _dayNight.Refresh();
            }
        }

        public static void Release()
        {
            _originalEnv = null;
            _originalSpace = null;
            _activeEnv = null;
            _render = null;
            _dayNight = null;
            _classicFog = null;
            _classicFogCaptured = false;
            _atmosphereCaptured = false;
            _nightHazeApplied = false;
        }

        public static Cubemap GetCubemap(DayPeriod period)
        {
            string code;
            var fallback = _originalEnv ?? (_render != null ? _render.m_cubemap : null);
            switch (period)
            {
                case DayPeriod.Sunrise:
                    code = Options.Current.SunriseCubemap;
                    break;
                case DayPeriod.Day:
                    code = Options.Current.DayCubemap;
                    break;
                case DayPeriod.Sunset:
                    code = Options.Current.SunsetCubemap;
                    break;
                default:
                    code = Options.Current.NightCubemap;
                    break;
            }

            if (code == Vanilla || !LoadingExtension.InGame)
            {
                return fallback;
            }

            var map = CubemapManager.Get(code);
            return map == null ? fallback : map.GetCubemap() ?? fallback;
        }

        public static void ReloadCubemaps()
        {
            CubemapManager.ResetCache();
            CubemapManager.Import();
            _activeEnv = null;
            UpdateCubemaps(true);
        }

        private static void ApplyCubemap(Cubemap cubemap, bool force)
        {
            if (cubemap == null)
            {
                return;
            }

            var renderChanged = false;
            if (_render != null && _render.m_cubemap != cubemap)
            {
                _render.m_cubemap = cubemap;
                renderChanged = true;
            }

            if (force || renderChanged || _activeEnv != cubemap)
            {
                _activeEnv = cubemap;
                Shader.SetGlobalTexture("_EnvironmentCubemap", cubemap);
            }
        }

        private static void UpdateClassicFog(bool disable)
        {
            if (!_classicFogCaptured || _classicFog == null)
            {
                return;
            }

            var enabled = !disable && _classicFogEnabled;
            if (_classicFog.enabled != enabled)
            {
                _classicFog.enabled = enabled;
            }
        }

        private static void UpdateNightHaze(bool enabled)
        {
            if (!_atmosphereCaptured || _render == null || _dayNight == null)
            {
                return;
            }

            if (enabled)
            {
                _dayNight.m_NightHorizonColor = HazeHorizonColor;
                _render.m_fogColor = HazeFogColor;
                _render.m_volumeFogColor = HazeVolumeColor;
                _render.m_pollutionFogColor = HazePollutionColor;
                _render.m_inscatteringColor = HazeInscatteringColor;
                Shader.SetGlobalColor("_EnvironmentFogColor", _render.m_fogColor);
                _nightHazeApplied = true;
                return;
            }

            if (!_nightHazeApplied)
            {
                return;
            }

            _dayNight.m_NightHorizonColor = _horizonColor;
            _render.m_fogColor = _originalFogColor;
            _render.m_volumeFogColor = _volFogColor;
            _render.m_pollutionFogColor = _polluteFogColor;
            _render.m_inscatteringColor = _inscatteringColor;
            Shader.SetGlobalColor("_EnvironmentFogColor", _render.m_fogColor);
            _nightHazeApplied = false;
        }
    }

    public static class CubemapManager
    {
        private const string ConfigFile = "CubemapReplacements.xml";
        private static bool _loaded;

        private static readonly Dictionary<string, CubemapReplacement> _cubemaps =
            new Dictionary<string, CubemapReplacement>();

        public static void ResetCache()
        {
            foreach (var cubemap in _cubemaps.Values)
            {
                cubemap.DestroyCubemap();
            }

            _loaded = false;
            _cubemaps.Clear();
        }

        public static CubemapOption[] GetCubemaps()
        {
            Import();
            var entries = new List<CubemapOption>
            {
                new CubemapOption(CubemapController.Vanilla, "Vanilla"),
            };
            entries.AddRange(_cubemaps.Select(kvp => new CubemapOption(kvp.Key, kvp.Value.Description)));
            return entries.ToArray();
        }

        public static CubemapReplacement Get(string code)
        {
            Import();
            CubemapReplacement map;
            return _cubemaps.TryGetValue(code, out map) ? map : null;
        }

        public static void Import()
        {
            if (_loaded)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plugins = Singleton<PluginManager>.instance.GetPluginsInfo().ToArray();
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var plugin in plugins)
            {
                if (string.IsNullOrEmpty(plugin.modPath))
                {
                    continue;
                }

                var parent = Directory.GetParent(plugin.modPath);
                if (parent != null && string.Equals(parent.Name, "255710", StringComparison.OrdinalIgnoreCase))
                {
                    roots.Add(parent.FullName);
                }

                if (File.Exists(Path.Combine(plugin.modPath, ConfigFile)) ||
                    IsPack(plugin.name) ||
                    IsPack(plugin.modPath))
                {
                    foreach (var xml in FindConfigFiles(plugin.modPath))
                    {
                        Import(Path.GetDirectoryName(xml), plugin.name, seen);
                    }
                }
            }

            var gameDir = Directory.GetParent(Application.dataPath);
            var steamApps = gameDir != null && gameDir.Parent != null ? gameDir.Parent.Parent : null;
            if (steamApps != null)
            {
                var workshop = Path.Combine(
                    Path.Combine(
                        Path.Combine(steamApps.FullName, "workshop"),
                        "content"),
                    "255710");
                if (Directory.Exists(workshop))
                {
                    roots.Add(workshop);
                }
            }

            foreach (var root in roots)
            {
                string[] dirs;
                try
                {
                    dirs = Directory.GetDirectories(root);
                }
                catch
                {
                    continue;
                }

                foreach (var dir in dirs)
                {
                    foreach (var xml in FindConfigFiles(dir))
                    {
                        Import(Path.GetDirectoryName(xml), Path.GetFileName(dir), seen);
                    }
                }
            }

            _loaded = true;
        }

        private static IEnumerable<string> FindConfigFiles(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                yield break;
            }

            var rootXml = Path.Combine(dir, ConfigFile);
            if (File.Exists(rootXml))
            {
                yield return rootXml;
                yield break;
            }

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch
            {
                yield break;
            }

            foreach (var subdir in subdirs)
            {
                var subdirXml = Path.Combine(subdir, ConfigFile);
                if (File.Exists(subdirXml))
                {
                    yield return subdirXml;
                }
            }
        }

        private static bool IsPack(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf("hdri", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("cubemap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("skybox", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Import(string dir, string src, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                return;
            }

            var path = Path.GetFullPath(dir);
            if (!seen.Add(path))
            {
                return;
            }

            var xml = Path.Combine(path, ConfigFile);
            if (!File.Exists(xml))
            {
                return;
            }

            try
            {
                var maps = ReadConfig(xml);
                foreach (var map in maps)
                {
                    Add(src, path, map);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "DynamicCubemaps: Error while parsing CubemapReplacements.xml of " + src + ": " + e.Message);
            }
        }

        private static List<CubemapReplacement> ReadConfig(string xml)
        {
            using (var stream = new FileStream(xml, FileMode.Open))
            {
                var serializer = new XmlSerializer(typeof(Config));
                var config = serializer.Deserialize(stream) as Config;
                return config == null || config.Maps == null ? new List<CubemapReplacement>() : config.Maps;
            }
        }

        private static void Add(string src, string dir, CubemapReplacement map)
        {
            var code = map.Code == null ? null : map.Code.Trim();
            var desc = map.Description == null ? null : map.Description.Trim();

            if (string.IsNullOrEmpty(code))
            {
                LogInvalidConfig(src, "replacement code is empty");
                return;
            }

            if (string.IsNullOrEmpty(desc))
            {
                LogInvalidConfig(src, "replacement description is empty");
                return;
            }

            map.Code = code;
            map.Description = desc;
            map.Dir = dir;

            if (_cubemaps.ContainsKey(map.Code))
            {
                LogInvalidConfig(src, "replacement code is already present");
                return;
            }

            _cubemaps.Add(map.Code, map);
        }

        private static void LogInvalidConfig(string src, string reason)
        {
            Debug.LogError("DynamicCubemaps: Invalid CubemapReplacements.xml of " + src + ": " + reason + "!");
        }

        [XmlRoot("CubemapReplacementsConfig")]
        public class Config
        {
            [XmlArray("CubemapReplacements")]
            [XmlArrayItem("CubemapReplacement")]
            public List<CubemapReplacement> Maps { get; set; } = new List<CubemapReplacement>();
        }
    }

    public struct CubemapOption
    {
        public CubemapOption(string code, string description)
        {
            Code = code;
            Description = description;
        }

        public string Code { get; }
        public string Description { get; }
    }
}
