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
        public static bool inGame = false;

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame && mode != LoadMode.NewGameFromScenario)
            {
                return;
            }

            inGame = true;

            DynamicCubemaps.Initialize();
            LuminaFix.Disable();

            _gameObject = new GameObject("DynamicCubemaps");
            _gameObject.AddComponent<CubemapMonitor>();

            DynamicCubemaps.UpdateCubemaps();
        }

        public override void OnLevelUnloading()
        {
            inGame = false;
            DynamicCubemaps.Revert();
            LuminaFix.Restore();

            if (_gameObject != null)
            {
                GameObject.Destroy(_gameObject);
                _gameObject = null;
            }
        }

        public class CubemapMonitor : MonoBehaviour
        {
            private const float UpdateInterval = 0.1f;

            private float lastTick;
            private DynamicCubemaps.DayPeriod lastPeriod;
            private string sunrise;
            private string day;
            private string sunset;
            private string night;
            private bool fixNightHaze;
            private Coroutine preload;
            private bool wasPaused;

            public void Start()
            {
                Cache();
                lastPeriod = DynamicCubemaps.GetCurrentPeriod();
                wasPaused = SimulationManager.instance.SimulationPaused;

                DynamicCubemaps.UpdateCubemaps();
                QueuePreload();
            }

            public void LateUpdate()
            {
                if (!inGame)
                {
                    return;
                }

                if (Time.time - lastTick < UpdateInterval)
                {
                    return;
                }

                lastTick = Time.time;

                var period = DynamicCubemaps.GetCurrentPeriod();
                var isPaused = SimulationManager.instance.SimulationPaused;
                var resumed = wasPaused && !isPaused;
                wasPaused = isPaused;

                if (Changed())
                {
                    Cache();
                    DynamicCubemaps.UpdateCubemaps(true);
                    QueuePreload();
                    lastPeriod = DynamicCubemaps.GetCurrentPeriod();
                    return;
                }

                if (resumed || period != lastPeriod)
                {
                    DynamicCubemaps.UpdateCubemaps(true);
                    lastPeriod = period;
                }
            }

            private void Cache()
            {
                sunrise = Options.Current.SunriseCubemap;
                day = Options.Current.DayCubemap;
                sunset = Options.Current.SunsetCubemap;
                night = Options.Current.NightCubemap;
                fixNightHaze = Options.Current.FixNightHaze;
            }

            private bool Changed()
            {
                return sunrise != Options.Current.SunriseCubemap ||
                    day != Options.Current.DayCubemap ||
                    sunset != Options.Current.SunsetCubemap ||
                    night != Options.Current.NightCubemap ||
                    fixNightHaze != Options.Current.FixNightHaze;
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
                DynamicCubemaps.GetCubemap(DynamicCubemaps.DayPeriod.Sunrise);
                yield return null;
                DynamicCubemaps.GetCubemap(DynamicCubemaps.DayPeriod.Day);
                yield return null;
                DynamicCubemaps.GetCubemap(DynamicCubemaps.DayPeriod.Sunset);
                yield return null;
                DynamicCubemaps.GetCubemap(DynamicCubemaps.DayPeriod.Night);
                preload = null;
            }

            public void OnDestroy()
            {
                DynamicCubemaps.Revert();
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
