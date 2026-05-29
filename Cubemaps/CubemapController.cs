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
    public static class DynamicCubemaps
    {
        public const string Vanilla = "vanilla";
        public const float SunriseStart = 6.0f;
        public const float DayStart = 10.0f;
        public const float SunsetStart = 18.0f;
        public const float NightStart = 21.0f;
        private const float VisualHourOffset = 1.0f;

        private static Cubemap originalEnv;
        private static Cubemap originalSpace;
        private static Cubemap activeEnv;
        private static bool atmosphereCaptured;
        private static bool nightHazeApplied;
        private static Color horizonColor;
        private static Color originalFogColor;
        private static Color volFogColor;
        private static Color polluteFogColor;
        private static Color inscatteringColor;

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
            var hour = SimulationManager.instance.m_currentDayTimeHour + VisualHourOffset;
            hour %= 24f;
            if (hour < 0f)
            {
                hour += 24f;
            }

            var starts = new[] { SunriseStart, DayStart, SunsetStart, NightStart };
            var periods = new[] { DayPeriod.Sunrise, DayPeriod.Day, DayPeriod.Sunset, DayPeriod.Night };
            var selectedStart = float.MinValue;
            var selectedPeriod = DayPeriod.Night;

            for (var i = 0; i < starts.Length; i++)
            {
                var start = starts[i] <= hour ? starts[i] : starts[i] - 24f;
                if (start >= selectedStart)
                {
                    selectedStart = start;
                    selectedPeriod = periods[i];
                }
            }

            return selectedPeriod;
        }

        public static void UpdateCubemaps()
        {
            UpdateCubemaps(false);
        }

        public static void UpdateCubemaps(bool forceRefresh)
        {
            if (!LoadingExtension.inGame)
            {
                return;
            }

            var dayNight = UnityEngine.Object.FindObjectOfType<DayNightProperties>();
            if (dayNight == null)
            {
                Debug.LogError("DynamicCubemaps: DayNightProperties not found");
                return;
            }

            var period = GetCurrentPeriod();
            var render = UnityEngine.Object.FindObjectOfType<RenderProperties>();
            var env = GetCubemap(period);
            var space = period == DayPeriod.Night ? env : originalSpace;

            if (env != null)
            {
                if (render != null && render.m_cubemap != env)
                {
                    render.m_cubemap = env;
                }

                if (forceRefresh || activeEnv != env)
                {
                    activeEnv = env;
                    Shader.SetGlobalTexture("_EnvironmentCubemap", env);
                }
            }

            if (space != null && dayNight.m_OuterSpaceCubemap != space)
            {
                dayNight.m_OuterSpaceCubemap = space;
            }

            UpdateNightHaze(period == DayPeriod.Night && Options.Current.FixNightHaze);
        }

        public static void Initialize()
        {
            var render = UnityEngine.Object.FindObjectOfType<RenderProperties>();
            var dayNight = UnityEngine.Object.FindObjectOfType<DayNightProperties>();

            if (render != null && originalEnv == null)
            {
                originalEnv = render.m_cubemap;
            }

            if (dayNight != null && originalSpace == null)
            {
                originalSpace = dayNight.m_OuterSpaceCubemap;
            }

            CaptureAtmosphere(render, dayNight);

            CubemapManager.ResetCache();
            CubemapManager.ImportFromMods();
        }

        public static void Revert()
        {
            UpdateNightHaze(false);

            var render = UnityEngine.Object.FindObjectOfType<RenderProperties>();
            var dayNight = UnityEngine.Object.FindObjectOfType<DayNightProperties>();

            if (originalEnv != null)
            {
                if (render != null)
                {
                    render.m_cubemap = originalEnv;
                }

                Shader.SetGlobalTexture("_EnvironmentCubemap", originalEnv);
                activeEnv = originalEnv;
            }

            if (dayNight != null && originalSpace != null)
            {
                dayNight.m_OuterSpaceCubemap = originalSpace;
                dayNight.Refresh();
            }
        }

        private static Cubemap GetFallback()
        {
            if (originalEnv != null)
            {
                return originalEnv;
            }

            var render = UnityEngine.Object.FindObjectOfType<RenderProperties>();
            return render != null ? render.m_cubemap : null;
        }

        public static Cubemap GetCubemap(DayPeriod period)
        {
            string code;
            Cubemap fallback;
            switch (period)
            {
                case DayPeriod.Sunrise:
                    code = Options.Current.SunriseCubemap;
                    fallback = GetFallback();
                    break;
                case DayPeriod.Day:
                    code = Options.Current.DayCubemap;
                    fallback = GetFallback();
                    break;
                case DayPeriod.Sunset:
                    code = Options.Current.SunsetCubemap;
                    fallback = GetFallback();
                    break;
                default:
                    code = Options.Current.NightCubemap;
                    fallback = originalSpace;
                    break;
            }

            if (code == Vanilla || !LoadingExtension.inGame)
            {
                return fallback;
            }

            try
            {
                var replacement = CubemapManager.GetReplacement(period, code);
                return replacement == null ? fallback : replacement.GetCubemap() ?? fallback;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return fallback;
            }
        }

        public static void ReloadCubemaps()
        {
            Revert();
            CubemapManager.ResetCache();
            CubemapManager.ImportFromMods();
            activeEnv = null;
            UpdateCubemaps(true);
        }

        private static void CaptureAtmosphere(RenderProperties render, DayNightProperties dayNight)
        {
            if (atmosphereCaptured || render == null || dayNight == null)
            {
                return;
            }

            horizonColor = dayNight.m_NightHorizonColor;
            originalFogColor = render.m_fogColor;
            volFogColor = render.m_volumeFogColor;
            polluteFogColor = render.m_pollutionFogColor;
            inscatteringColor = render.m_inscatteringColor;
            atmosphereCaptured = true;
        }

        private static void UpdateNightHaze(bool enabled)
        {
            var render = UnityEngine.Object.FindObjectOfType<RenderProperties>();
            var dayNight = UnityEngine.Object.FindObjectOfType<DayNightProperties>();
            CaptureAtmosphere(render, dayNight);

            if (!atmosphereCaptured || render == null || dayNight == null)
            {
                return;
            }

            if (enabled)
            {
                dayNight.m_NightHorizonColor = HazeHorizonColor;
                render.m_fogColor = HazeFogColor;
                render.m_volumeFogColor = HazeVolumeColor;
                render.m_pollutionFogColor = HazePollutionColor;
                render.m_inscatteringColor = HazeInscatteringColor;
                Shader.SetGlobalColor("_EnvironmentFogColor", render.m_fogColor);
                nightHazeApplied = true;
                return;
            }

            if (!nightHazeApplied)
            {
                return;
            }

            dayNight.m_NightHorizonColor = horizonColor;
            render.m_fogColor = originalFogColor;
            render.m_volumeFogColor = volFogColor;
            render.m_pollutionFogColor = polluteFogColor;
            render.m_inscatteringColor = inscatteringColor;
            Shader.SetGlobalColor("_EnvironmentFogColor", render.m_fogColor);
            nightHazeApplied = false;
        }
    }

    public static class CubemapManager
    {
        private const string ConfigFile = "CubemapReplacements.xml";
        private static bool loaded;

        private static readonly Dictionary<string, CubemapReplacement> Sunrise =
            new Dictionary<string, CubemapReplacement>();

        private static readonly Dictionary<string, CubemapReplacement> Day =
            new Dictionary<string, CubemapReplacement>();

        private static readonly Dictionary<string, CubemapReplacement> Sunset =
            new Dictionary<string, CubemapReplacement>();

        private static readonly Dictionary<string, CubemapReplacement> Night =
            new Dictionary<string, CubemapReplacement>();

        public static void ResetCache()
        {
            Destroy(Sunrise);
            Destroy(Day);
            Destroy(Sunset);
            Destroy(Night);

            loaded = false;
            Sunrise.Clear();
            Day.Clear();
            Sunset.Clear();
            Night.Clear();
        }

        private static void Destroy(Dictionary<string, CubemapReplacement> group)
        {
            foreach (var cubemap in group.Values)
            {
                cubemap.DestroyCubemap();
            }
        }

        public static CubemapOption[] GetSunriseCubemaps()
        {
            return List(DynamicCubemaps.DayPeriod.Sunrise);
        }

        public static CubemapOption[] GetDayCubemaps()
        {
            return List(DynamicCubemaps.DayPeriod.Day);
        }

        public static CubemapOption[] GetSunsetCubemaps()
        {
            return List(DynamicCubemaps.DayPeriod.Sunset);
        }

        public static CubemapOption[] GetNightCubemaps()
        {
            return List(DynamicCubemaps.DayPeriod.Night);
        }

        private static CubemapOption[] List(DynamicCubemaps.DayPeriod period)
        {
            ImportFromMods();
            var entries = new List<CubemapOption>
            {
                new CubemapOption(DynamicCubemaps.Vanilla, "Vanilla"),
            };
            entries.AddRange(GetGroup(period).Select(kvp => new CubemapOption(kvp.Key, kvp.Value.Description)));
            return entries.ToArray();
        }

        public static CubemapReplacement GetReplacement(DynamicCubemaps.DayPeriod period, string code)
        {
            ImportFromMods();
            CubemapReplacement replacement;
            return GetGroup(period).TryGetValue(code, out replacement) ? replacement : null;
        }

        public static void ImportFromMods()
        {
            if (loaded)
            {
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var plugins = Singleton<PluginManager>.instance.GetPluginsInfo().ToArray();
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var plugin in plugins.Where(plugin => plugin.isEnabled))
            {
                Import(plugin.modPath, plugin.name, seen);
            }

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
            }

            var gameDirectory = Directory.GetParent(Application.dataPath);
            var steamAppsDirectory = gameDirectory != null && gameDirectory.Parent != null ? gameDirectory.Parent.Parent : null;
            if (steamAppsDirectory != null)
            {
                var workshopRoot = Path.Combine(
                    Path.Combine(
                        Path.Combine(steamAppsDirectory.FullName, "workshop"),
                        "content"),
                    "255710");
                if (Directory.Exists(workshopRoot))
                {
                    roots.Add(workshopRoot);
                }
            }

            foreach (var root in roots)
            {
                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(root);
                }
                catch
                {
                    continue;
                }

                foreach (var directory in directories)
                {
                    if (File.Exists(Path.Combine(directory, ConfigFile)))
                    {
                        Import(directory, Path.GetFileName(directory), seen);
                    }
                }
            }

            loaded = true;
        }

        private static void Import(string directory, string source, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            var dir = Path.GetFullPath(directory);
            if (!seen.Add(dir))
            {
                return;
            }

            var xml = Path.Combine(dir, ConfigFile);
            if (!File.Exists(xml))
            {
                return;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(ReplacementConfig));
                ReplacementConfig config;
                using (var stream = new FileStream(xml, FileMode.Open))
                {
                    config = serializer.Deserialize(stream) as ReplacementConfig;
                }

                if (config != null)
                {
                    foreach (var replacement in config.AllReplacements)
                    {
                        Add(
                            source,
                            dir,
                            replacement,
                            string.IsNullOrEmpty(replacement.TimePeriod) &&
                                !replacement.IsSunrise &&
                                !replacement.IsDay &&
                                !replacement.IsSunset &&
                                !replacement.IsNight);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("DynamicCubemaps: Error while parsing CubemapReplacements.xml of " + source);
                Debug.LogException(e);
            }
        }

        private static void Add(string source, string dir, CubemapReplacement cubemap, bool all)
        {
            var code = cubemap.Code == null ? null : cubemap.Code.Trim();
            var description = cubemap.Description == null ? null : cubemap.Description.Trim();

            if (string.IsNullOrEmpty(code))
            {
                Debug.LogError("DynamicCubemaps: Invalid CubemapReplacements.xml of " + source + ": replacement code is empty!");
                return;
            }

            if (string.IsNullOrEmpty(description))
            {
                Debug.LogError("DynamicCubemaps: Invalid CubemapReplacements.xml of " + source + ": replacement description is empty!");
                return;
            }

            cubemap.Code = code;
            cubemap.Description = description;
            cubemap.Directory = dir;

            if (all)
            {
                Add(source, GetGroup(DynamicCubemaps.DayPeriod.Sunrise), cubemap);
                Add(source, GetGroup(DynamicCubemaps.DayPeriod.Day), cubemap);
                Add(source, GetGroup(DynamicCubemaps.DayPeriod.Sunset), cubemap);
                Add(source, GetGroup(DynamicCubemaps.DayPeriod.Night), cubemap);
                return;
            }

            if (string.Equals(cubemap.TimePeriod, "night", StringComparison.OrdinalIgnoreCase) || cubemap.IsNight)
            {
                Add(source, GetGroup(DynamicCubemaps.DayPeriod.Night), cubemap);
                return;
            }

            if (string.Equals(cubemap.TimePeriod, "sunrise", StringComparison.OrdinalIgnoreCase) || cubemap.IsSunrise)
            {
                Add(source, GetGroup(DynamicCubemaps.DayPeriod.Sunrise), cubemap);
                return;
            }

            if (string.Equals(cubemap.TimePeriod, "sunset", StringComparison.OrdinalIgnoreCase) || cubemap.IsSunset)
            {
                Add(source, GetGroup(DynamicCubemaps.DayPeriod.Sunset), cubemap);
                return;
            }

            Add(source, GetGroup(DynamicCubemaps.DayPeriod.Day), cubemap);
        }

        private static void Add(string source, Dictionary<string, CubemapReplacement> group, CubemapReplacement cubemap)
        {
            if (group.ContainsKey(cubemap.Code))
            {
                Debug.LogError("DynamicCubemaps: Invalid CubemapReplacements.xml of " + source + ": replacement code is already present!");
                return;
            }

            group.Add(cubemap.Code, cubemap);
        }

        private static Dictionary<string, CubemapReplacement> GetGroup(DynamicCubemaps.DayPeriod period)
        {
            switch (period)
            {
                case DynamicCubemaps.DayPeriod.Sunrise:
                    return Sunrise;
                case DynamicCubemaps.DayPeriod.Day:
                    return Day;
                case DynamicCubemaps.DayPeriod.Sunset:
                    return Sunset;
                default:
                    return Night;
            }
        }

        [XmlRoot("CubemapReplacementsConfig")]
        public class ReplacementConfig
        {
            [XmlArray("replacements")]
            [XmlArrayItem("replacement")]
            public List<CubemapReplacement> Replacements { get; set; } = new List<CubemapReplacement>();

            [XmlArray("CubemapReplacements")]
            [XmlArrayItem("CubemapReplacement")]
            public List<CubemapReplacement> LegacyReplacements { get; set; } = new List<CubemapReplacement>();

            [XmlIgnore]
            public IEnumerable<CubemapReplacement> AllReplacements
            {
                get { return Replacements.Concat(LegacyReplacements); }
            }
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
