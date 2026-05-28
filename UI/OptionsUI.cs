using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using ColossalFramework.IO;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace DynamicCubemaps
{
    public class Options
    {
        private const string FileName = "Dynamic Cubemaps.xml";
        private const float Gap = 20f;
        private const float GridHeight = 142f;
        private const float RowHeight = 44f;
        private const float ButtonGap = 8f;
        private const float Width = 730f;
        private static Options instance;

        public static Options Current
        {
            get
            {
                if (instance == null)
                {
                    instance = Load();
                }

                return instance;
            }
        }

        [XmlElement("sunriseCubemap")]
        public string SunriseCubemap { get; set; } = CubemapController.Vanilla;

        [XmlElement("dayCubemap")]
        public string DayCubemap { get; set; } = CubemapController.Vanilla;

        [XmlElement("sunsetCubemap")]
        public string SunsetCubemap { get; set; } = CubemapController.Vanilla;

        [XmlElement("nightCubemap")]
        public string NightCubemap { get; set; } = CubemapController.Vanilla;

        [XmlElement("useVanillaNight")]
        public bool UseVanillaNight { get; set; }

        [XmlElement("fixNightHaze")]
        public bool FixNightHaze { get; set; }

        public static void AddSettingsUI(UIHelperBase helper)
        {
            var group = helper.AddGroup("Skybox");
            var uiHelper = group as UIHelper;
            var panel = uiHelper != null ? uiHelper.self as UIPanel : null;
            UIHelperBase leftColumn = group;
            UIHelperBase rightColumn = group;
            if (panel != null)
            {
                var width = Mathf.Max(Width, panel.width);
                var row = panel.AddUIComponent<UIPanel>();
                row.width = width;
                row.height = GridHeight;
                row.autoLayout = true;
                row.autoLayoutDirection = LayoutDirection.Horizontal;
                row.autoLayoutStart = LayoutStart.TopLeft;
                row.autoLayoutPadding = new RectOffset(0, (int)Gap, 0, 0);

                var columnWidth = (width - Gap) / 2f;
                var left = row.AddUIComponent<UIPanel>();
                left.width = columnWidth;
                left.height = GridHeight;
                left.autoLayout = true;
                left.autoLayoutDirection = LayoutDirection.Vertical;
                left.autoLayoutStart = LayoutStart.TopLeft;

                var right = row.AddUIComponent<UIPanel>();
                right.width = columnWidth;
                right.height = GridHeight;
                right.autoLayout = true;
                right.autoLayoutDirection = LayoutDirection.Vertical;
                right.autoLayoutStart = LayoutStart.TopLeft;

                leftColumn = new UIHelper(left);
                rightColumn = new UIHelper(right);
            }

            var sunrise = Current.SunriseCubemap;
            var day = Current.DayCubemap;
            var sunset = Current.SunsetCubemap;
            var night = Current.NightCubemap;
            var useVanillaNight = Current.UseVanillaNight;
            var fixNightHaze = Current.FixNightHaze && !useVanillaNight;

            Dropdown(
                leftColumn,
                "Sunrise Cubemap",
                CubemapManager.GetSunriseCubemaps(),
                Current.SunriseCubemap,
                value =>
                {
                    sunrise = value;
                });

            Dropdown(
                rightColumn,
                "Day Cubemap",
                CubemapManager.GetDayCubemaps(),
                Current.DayCubemap,
                value =>
                {
                    day = value;
                });

            Dropdown(
                leftColumn,
                "Sunset Cubemap",
                CubemapManager.GetSunsetCubemaps(),
                Current.SunsetCubemap,
                value =>
                {
                    sunset = value;
                });

            Dropdown(
                rightColumn,
                "Night Cubemap",
                CubemapManager.GetNightCubemaps(),
                Current.NightCubemap,
                value =>
                {
                    night = value;
                });

            UICheckBox vanillaNightToggle = null;
            UICheckBox nightHazeToggle = null;
            vanillaNightToggle = group.AddCheckbox(
                "Use vanilla mode at night",
                useVanillaNight,
                value =>
                {
                    useVanillaNight = value;
                    if (value)
                    {
                        fixNightHaze = false;
                        if (nightHazeToggle != null)
                        {
                            nightHazeToggle.isChecked = false;
                        }
                    }
                }) as UICheckBox;

            nightHazeToggle = group.AddCheckbox(
                "Disable horizon haze at night",
                fixNightHaze,
                value =>
                {
                    fixNightHaze = value;
                    if (value)
                    {
                        useVanillaNight = false;
                        if (vanillaNightToggle != null)
                        {
                            vanillaNightToggle.isChecked = false;
                        }
                    }
                }) as UICheckBox;

            var actions = helper.AddGroup("Actions");
            var actionsHelper = actions as UIHelper;
            var actionsPanel = actionsHelper != null ? actionsHelper.self as UIPanel : null;
            if (actionsPanel != null)
            {
                var row = actionsPanel.AddUIComponent<UIPanel>();
                row.width = Mathf.Max(Width, actionsPanel.width);
                row.height = RowHeight;
                row.autoLayout = true;
                row.autoLayoutDirection = LayoutDirection.Horizontal;
                row.autoLayoutStart = LayoutStart.TopLeft;
                row.autoLayoutPadding = new RectOffset(0, (int)ButtonGap, 0, 0);
                actions = new UIHelper(row);
            }

            actions.AddButton("Apply Settings", () =>
            {
                Current.SunriseCubemap = sunrise;
                Current.DayCubemap = day;
                Current.SunsetCubemap = sunset;
                Current.NightCubemap = night;
                Current.UseVanillaNight = useVanillaNight;
                Current.FixNightHaze = fixNightHaze && !useVanillaNight;
                Save(Current);
                CubemapController.UpdateCubemaps(true);
                LoadingExtension.RefreshCubemaps();
            });

            actions.AddButton("Reload Cubemaps", () =>
            {
                CubemapController.ReloadCubemaps();
                LoadingExtension.RefreshCubemaps();
            });
        }

        private static void Dropdown(UIHelperBase group, string label, CubemapOption[] options, string selectedCode, Action<string> onChanged)
        {
            var codes = options.Select(option => option.Code).ToArray();
            var descriptions = options.Select(option => option.Description).ToArray();
            var index = Array.IndexOf(codes, selectedCode);

            if (index < 0)
            {
                index = 0;
                onChanged(codes[index]);
            }

            group.AddDropdown(label, descriptions, index, selected =>
            {
                if (selected >= 0 && selected < codes.Length)
                {
                    onChanged(codes[selected]);
                }
            });
        }

        private static Options Load()
        {
            var path = GetOptionsPath();
            if (!File.Exists(path))
            {
                var defaults = new Options();
                Save(defaults);
                return defaults;
            }

            try
            {
                var serializer = new XmlSerializer(typeof(Options));
                using (var streamReader = new StreamReader(path))
                {
                    var options = serializer.Deserialize(streamReader) as Options ?? new Options();
                    options.FixNightHaze = options.FixNightHaze && !options.UseVanillaNight;
                    return options;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("DynamicCubemaps: Error reading options XML file");
                Debug.LogException(e);
                return new Options();
            }
        }

        private static void Save(Options options)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Options));
                using (var streamWriter = new StreamWriter(GetOptionsPath()))
                {
                    serializer.Serialize(streamWriter, options);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static string GetOptionsPath()
        {
            return Path.Combine(DataLocation.localApplicationData, FileName);
        }
    }

}
