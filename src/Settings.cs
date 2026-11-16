using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace BlackBoxReborn;

public enum UiLanguage { Auto, English, Spanish }

public enum RemainingValueMode { InTruck, Always, Never }

internal static class Settings
{
    public static ConfigEntry<UiLanguage> Language;
    public static ConfigEntry<Key> PanelKey;
    public static ConfigEntry<float> PanelScale;
    public static ConfigEntry<bool> MonitorEnabled;
    public static ConfigEntry<float> MonitorPageSeconds;
    public static ConfigEntry<RemainingValueMode> RemainingValue;

    public static void Bind(ConfigFile cfg)
    {
        Language = cfg.Bind("General", "Language", UiLanguage.Auto,
            "Language of the panel and the monitor. Auto follows the game language.");
        PanelKey = cfg.Bind("Panel", "ToggleKey", Key.F6,
            "Key that opens and closes the stats panel.");
        PanelScale = cfg.Bind("Panel", "Scale", 1f,
            new ConfigDescription("Size of the stats panel.", new AcceptableValueRange<float>(0.6f, 1.6f)));
        MonitorEnabled = cfg.Bind("Monitor", "Enabled", true,
            "Show the stats monitor inside the truck and the level report in the shop.");
        MonitorPageSeconds = cfg.Bind("Monitor", "PageSeconds", 7f,
            new ConfigDescription("Seconds each page stays on the monitor when there are too many players to fit.",
                new AcceptableValueRange<float>(3f, 30f)));
        RemainingValue = cfg.Bind("HUD", "RemainingValue", RemainingValueMode.InTruck,
            "Show the value still left on the map next to the haul goal.");
    }
}
