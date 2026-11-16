using HarmonyLib;

namespace BlackBoxReborn.Patches;

/// <summary>
/// Esc cierra el panel. Se hace antes de que GameDirector mire la tecla, y se le activa su propio
/// temporizador para que esa misma pulsación no abra también el menú de pausa.
/// </summary>
[HarmonyPatch(typeof(GameDirector), nameof(GameDirector.Update))]
internal static class EscClosesPanel
{
    private static void Prefix(GameDirector __instance)
    {
        var panel = Controller.Instance ? Controller.Instance.Panel : null;
        if (!panel || !panel.IsOpen) return;
        if (ChatManager.instance && ChatManager.instance.chatActive) return;
        if (!SemiFunc.InputDown(InputKey.Menu)) return;
        panel.Close();
        __instance.disableEscMenuTimer = 0.2f;
    }
}
