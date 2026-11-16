using BlackBoxReborn.Stats;
using BlackBoxReborn.UI;
using HarmonyLib;

namespace BlackBoxReborn.Patches;

/// <summary>Añade «· $X restante» al contador de botín del HUD.</summary>
[HarmonyPatch(typeof(HaulUI), nameof(HaulUI.Update))]
internal static class HaulUiPatch
{
    private static void Postfix(HaulUI __instance)
    {
        var mode = Settings.RemainingValue.Value;
        if (mode == RemainingValueMode.Never || !__instance.Text || !SemiFunc.RunIsLevel()) return;
        if (!RoundDirector.instance || !RoundDirector.instance.extractionPointActive) return;
        if (mode == RemainingValueMode.InTruck &&
            (!PlayerAvatar.instance || !PlayerAvatar.instance.RoomVolumeCheck || !PlayerAvatar.instance.RoomVolumeCheck.inTruck))
            return;

        var level = Tracker.OpenLevel;
        if (level == null) return;

        // HaulUI deja abiertos <u> y <size>: se cierran antes de añadir lo nuestro.
        __instance.Text.text += "</u></size> <color=#616161><size=45>·</size></color> <size=30><color=#558B2F>$</color>"
                                + Format.Money(level.ValueRemaining) + " <size=20><color=#9E9E9E>"
                                + Loc.T("hud.left") + "</color></size></size>";
    }
}
