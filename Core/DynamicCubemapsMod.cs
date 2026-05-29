using ICities;

namespace DynamicCubemaps
{
    public class DynamicCubemapsMod : IUserMod
    {
        public string Name => "Dynamic Cubemaps";
        public string Description => "Enhances skybox configuration with time-based day-night cycle. Based on the original Cubemap Replacer mod.";

        public void OnSettingsUI(UIHelperBase helper)
        {
            Options.AddSettingsUI(helper);
        }
    }
}
