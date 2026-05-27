using System.Collections;
using ICities;
using UnityEngine;

namespace DynamicCubemaps
{
    public class LoadingExtension : LoadingExtensionBase
    {
        private GameObject _gameObject;
        private static CubemapMonitor _monitor;
        public static bool InGame { get; private set; }

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame && mode != LoadMode.NewGameFromScenario)
            {
                return;
            }

            InGame = true;

            CubemapController.Init();

            _gameObject = new GameObject("DynamicCubemaps");
            _gameObject.AddComponent<CubemapMonitor>();
        }

        public override void OnLevelUnloading()
        {
            InGame = false;
            CubemapController.Revert();
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
            private const int SettleFrames = 20;

            private int _hour;
            private CubemapController.DayPeriod _period;
            private int _settleFrames;
            private uint _dayTimeOffset;
            private bool _paused;

            public void Start()
            {
                _monitor = this;
                var simulation = SimulationManager.instance;
                _hour = CubemapController.GetCurrentHour();
                _period = CubemapController.GetCurrentPeriod();
                _dayTimeOffset = simulation.m_dayTimeOffsetFrames;
                _paused = simulation.SimulationPaused;
                _settleFrames = SettleFrames;

                StartCoroutine(Startup());
            }

            public void OnDestroy()
            {
                if (_monitor == this)
                {
                    _monitor = null;
                }
            }

            public void Refresh()
            {
                _settleFrames = SettleFrames;
                CubemapController.UpdateCubemaps(true);
            }

            public void LateUpdate()
            {
                if (!InGame)
                {
                    return;
                }

                var simulation = SimulationManager.instance;
                var currentHour = CubemapController.GetCurrentHour();
                var currentPeriod = CubemapController.GetCurrentPeriod();
                var currentOffset = simulation.m_dayTimeOffsetFrames;
                var paused = simulation.SimulationPaused;
                if (currentHour != _hour ||
                    currentPeriod != _period ||
                    currentOffset != _dayTimeOffset ||
                    paused != _paused)
                {
                    _hour = currentHour;
                    _period = currentPeriod;
                    _dayTimeOffset = currentOffset;
                    _paused = paused;
                    _settleFrames = SettleFrames;
                }

                if (_settleFrames > 0)
                {
                    CubemapController.UpdateCubemaps(true);
                    _settleFrames--;
                    return;
                }

                CubemapController.UpdateCubemaps(false);
            }

            private IEnumerator Startup()
            {
                CubemapController.UpdateCubemaps(true);

                var codes = new[]
                {
                    Options.Current.SunriseCubemap,
                    Options.Current.DayCubemap,
                    Options.Current.SunsetCubemap,
                    Options.Current.NightCubemap,
                };

                var periods = new[]
                {
                    CubemapController.DayPeriod.Sunrise,
                    CubemapController.DayPeriod.Day,
                    CubemapController.DayPeriod.Sunset,
                    CubemapController.DayPeriod.Night,
                };

                for (var i = 0; i < codes.Length; i++)
                {
                    yield return null;

                    if (codes[i] != CubemapController.Vanilla)
                    {
                        CubemapController.GetCubemap(periods[i]);
                    }
                }

                for (var i = 0; i < 60; i++)
                {
                    yield return null;

                    if (i == 5 || i == 20 || i == 59)
                    {
                        CubemapController.UpdateCubemaps(true);
                    }
                }
            }
        }
    }
}
