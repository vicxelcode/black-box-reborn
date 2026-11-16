using System;
using BlackBoxReborn.Stats;
using BlackBoxReborn.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackBoxReborn;

/// <summary>Bucle principal: alimenta el rastreador, atiende el teclado y coloca el monitor.</summary>
internal sealed class Controller : MonoBehaviour
{
    internal static Controller Instance;

    private StatsPanel panel;
    private TruckMonitor monitor;
    private int errors;

    public StatsPanel Panel => panel;
    public TruckMonitor Monitor => monitor;

    private void Awake()
    {
        Instance = this;
        panel = gameObject.AddComponent<StatsPanel>();
    }

    private void Update()
    {
        Guard(Tracker.Tick, "Tick");
        Guard(HandleInput, "Input");
        Guard(UpdateMonitor, "Monitor");
    }

    private void Guard(Action action, string where)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            // Un fallo por fotograma inundaría el registro: se cuentan los primeros.
            if (errors++ < 20) Plugin.Log.LogError($"{where}: {e}");
        }
    }

    private void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (ChatManager.instance && ChatManager.instance.chatActive) return;

        var key = Settings.PanelKey.Value;
        if (key != Key.None && keyboard[key].wasPressedThisFrame) panel.Toggle();
        if (!panel.IsOpen) return;

        if (keyboard.leftArrowKey.wasPressedThisFrame) panel.ChangeTab(-1);
        if (keyboard.rightArrowKey.wasPressedThisFrame) panel.ChangeTab(1);
        if (keyboard.upArrowKey.wasPressedThisFrame) panel.ChangeSelection(-1);
        if (keyboard.downArrowKey.wasPressedThisFrame) panel.ChangeSelection(1);
    }

    private void UpdateMonitor()
    {
        // En el nivel, el directo; en la tienda y en la sala de espera del camión, el informe del último nivel.
        var level = SemiFunc.RunIsLevel();
        var shop = SemiFunc.RunIsShop() || SemiFunc.RunIsLobby();
        var screen = TruckScreenText.instance;
        var want = Settings.MonitorEnabled.Value && (level || shop) && screen &&
                   LevelGenerator.Instance && LevelGenerator.Instance.Generated;

        if (!want)
        {
            if (monitor) Destroy(monitor.gameObject);
            monitor = null;
            return;
        }
        // Cuelga del camión: se destruye solo al descargar la escena.
        if (!monitor) monitor = TruckMonitor.Create(screen.transform.parent ? screen.transform.parent : screen.transform, shop);
    }
}
