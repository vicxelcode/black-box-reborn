using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BlackBoxReborn;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "vicxelcode.BlackBoxReborn";
    public const string Name = "Black Box Reborn";
    public const string Version = "1.0.0";

    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;
        Settings.Bind(Config);
        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

        // R.E.P.O. destruye el objeto del cargador de BepInEx al cambiar de escena: el controlador vive
        // en un objeto propio que sobrevive a las cargas.
        var host = new GameObject("BlackBoxReborn");
        host.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(host);
        host.AddComponent<Controller>();
#if BBR_DEV
        host.AddComponent<Dev.DevTools>();
#endif
        Log.LogInfo($"{Name} {Version} cargado");
    }
}
