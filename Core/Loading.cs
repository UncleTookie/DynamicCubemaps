using System;
using System.Collections;
using System.Reflection;
using ICities;
using UnityEngine;

namespace DynamicCubemaps
{
    public class LoadingExtension : LoadingExtensionBase
    {
        private GameObject _gameObject;
        private static CubemapMonitor _monitor;
        public static bool InGame = false;

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame && mode != LoadMode.NewGameFromScenario)
            {
                return;
            }

            InGame = true;

            CubemapController.Initialize();
            LuminaFix.Disable();

            _gameObject = new GameObject("DynamicCubemaps");
            _gameObject.AddComponent<CubemapMonitor>();

            CubemapController.UpdateCubemaps();
        }

        public override void OnLevelUnloading()
        {
            InGame = false;
            CubemapController.Revert();
            LuminaFix.Restore();
            CubemapManager.ResetCache();
            CubemapController.Release();
            _monitor = null;

            if (_gameObject != null)
            {
                GameObject.Destroy(_gameObject);
                _gameObject = null;
            }
        }

        public static void RefreshCubemaps()
        {
            if (_monitor != null)
            {
                _monitor.Refresh();
            }
            else
            {
                CubemapController.UpdateCubemaps(true);
            }
        }

        public class CubemapMonitor : MonoBehaviour
        {
            private const float UpdateInterval = 0.1f;

            private float lastTick;
            private CubemapController.DayPeriod lastPeriod;
            private string sunrise;
            private string day;
            private string sunset;
            private string night;
            private bool useVanillaNight;
            private bool fixNightHaze;
            private Coroutine preload;

            public void Start()
            {
                _monitor = this;
                Cache();
                lastPeriod = CubemapController.GetCurrentPeriod();

                CubemapController.UpdateCubemaps();
                QueuePreload();
            }

            public void LateUpdate()
            {
                if (!InGame)
                {
                    return;
                }

                if (Time.time - lastTick < UpdateInterval)
                {
                    return;
                }

                lastTick = Time.time;

                var period = CubemapController.GetCurrentPeriod();
                if (Changed())
                {
                    Cache();
                    CubemapController.UpdateCubemaps(true);
                    QueuePreload();
                    lastPeriod = CubemapController.GetCurrentPeriod();
                    return;
                }

                if (period != lastPeriod)
                {
                    CubemapController.UpdateCubemaps(true);
                    lastPeriod = period;
                }
            }

            private void Cache()
            {
                sunrise = Options.Current.SunriseCubemap;
                day = Options.Current.DayCubemap;
                sunset = Options.Current.SunsetCubemap;
                night = Options.Current.NightCubemap;
                useVanillaNight = Options.Current.UseVanillaNight;
                fixNightHaze = Options.Current.FixNightHaze;
            }

            private bool Changed()
            {
                return sunrise != Options.Current.SunriseCubemap ||
                    day != Options.Current.DayCubemap ||
                    sunset != Options.Current.SunsetCubemap ||
                    night != Options.Current.NightCubemap ||
                    useVanillaNight != Options.Current.UseVanillaNight ||
                    fixNightHaze != Options.Current.FixNightHaze;
            }

            public void Refresh()
            {
                Cache();
                CubemapController.UpdateCubemaps(true);
                QueuePreload();
            }

            private void QueuePreload()
            {
                if (preload != null)
                {
                    StopCoroutine(preload);
                }

                preload = StartCoroutine(Preload());
            }

            private IEnumerator Preload()
            {
                yield return null;
                CubemapController.GetCubemap(CubemapController.DayPeriod.Sunrise);
                yield return null;
                CubemapController.GetCubemap(CubemapController.DayPeriod.Day);
                yield return null;
                CubemapController.GetCubemap(CubemapController.DayPeriod.Sunset);
                yield return null;
                CubemapController.GetCubemap(CubemapController.DayPeriod.Night);
                preload = null;
            }

            public void OnDestroy()
            {
                if (_monitor == this)
                {
                    _monitor = null;
                }

                CubemapController.Revert();
            }
        }
    }

    public static class LuminaFix
    {
        private const string Updater = "Lumina.CubemapUpdater";

        private static Behaviour updater;

        public static void Disable()
        {
            try
            {
                Type type = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(Updater);
                    if (type != null)
                    {
                        break;
                    }
                }

                if (type == null)
                {
                    return;
                }

                var property = type.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var instance = property == null ? null : property.GetValue(null, null) as Behaviour;
                if (instance == null)
                {
                    instance = UnityEngine.Object.FindObjectOfType(type) as Behaviour;
                }

                if (instance != null && instance.enabled)
                {
                    instance.enabled = false;
                    updater = instance;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("DynamicCubemaps: Failed to disable Lumina cubemap updater: " + e.Message);
            }
        }

        public static void Restore()
        {
            try
            {
                if (updater != null)
                {
                    updater.enabled = true;
                    updater = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("DynamicCubemaps: Failed to restore Lumina cubemap updater: " + e.Message);
            }
        }
    }
}
