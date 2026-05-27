using ICities;

namespace DynamicCubemaps
{
    public class DynamicCubemapsMod : IUserMod
    {
        public string Name => "Dynamic Cubemaps";
        public string Description => "Changes environment cubemaps automatically by time of day for simulated day/night cycles.";

        public void OnSettingsUI(UIHelperBase helper)
        {
            Options.AddSettingsUI(helper);
        }
    }
}
