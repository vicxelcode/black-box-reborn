using System.Linq;
using BlackBoxReborn.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace BlackBoxReborn.UI;

/// <summary>
/// Pantalla dentro del camión. Cada jugador la pinta en local con sus propios datos, así que no hay
/// objetos en red. En los niveles muestra el directo; en la tienda, el informe del último nivel.
/// </summary>
internal sealed class TruckMonitor : MonoBehaviour
{
    public const float Width = 1180f;
    public const float Height = 664f;

    // Colocación respecto al camión (padre de TruckScreenText): pared del fondo, entre las dos cámaras
    // «cctv», mirando hacia la cabina. Medida en el juego con las DevTools.
    // La persiana trasera acaba a ~2,1 m y las luces rojas empiezan a ~3,8 m: el monitor va entre ambas.
    public static Vector3 LocalOffset = new(-24.1f, 2.95f, 0f);
    public static Vector3 LocalEuler = new(0f, 270f, 0f);
    public static float WorldWidth = 2.6f;

    private RectTransform content;
    private int builtRevision = -1;
    private float nextRebuild;
    private int page;
    private float nextPage;
    private bool shop;

    public static TruckMonitor Create(Transform anchor, bool shop)
    {
        var go = new GameObject("BlackBoxMonitor", typeof(RectTransform));
        go.layer = anchor.gameObject.layer;
        var t = (RectTransform)go.transform;
        t.SetParent(anchor, false);
        var monitor = go.AddComponent<TruckMonitor>();
        monitor.shop = shop;
        monitor.Build();
        monitor.ApplyPlacement();
        return monitor;
    }

    public void ApplyPlacement()
    {
        var t = (RectTransform)transform;
        t.localPosition = LocalOffset;
        t.localRotation = Quaternion.Euler(LocalEuler);
        // El ancla puede venir escalada: se compensa para que el ancho en el mundo sea el pedido.
        var parentScale = transform.parent ? transform.parent.lossyScale.x : 1f;
        t.localScale = Vector3.one * (WorldWidth / Width / Mathf.Max(0.0001f, parentScale));
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(Width, Height);
        gameObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;

        Ui.Box(rt, Theme.Hex("050506"), "Bezel", radiusScale: 1.2f).rectTransform.Fill(-18f, -18f, -18f, -18f);
        Ui.Box(rt, Theme.Hex("0B0C0F"), "Screen", radiusScale: 0.6f).rectTransform.Fill();
        content = Ui.Rect("Content", rt).Fill();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextPage)
        {
            nextPage = Time.unscaledTime + Settings.MonitorPageSeconds.Value;
            page++;
            builtRevision = -1;
        }
        if (Tracker.Revision == builtRevision && Time.unscaledTime < nextRebuild) return;
        builtRevision = Tracker.Revision;
        nextRebuild = Time.unscaledTime + 1f;
        Ui.Clear(content);

        var level = Tracker.Run.Current;
        if (level == null)
        {
            MonitorViews.Header(content, Width, "", null, false);
            return;
        }
        if (!shop)
        {
            MonitorViews.Header(content, Width, "", level, !level.Finished);
            MonitorViews.Stats(content, 118f, Width, 210f, level, false);
            MonitorViews.Damage(content, 346f, Width, Height - 346f - 30f, level);
            return;
        }

        // Tienda: informe del último nivel, con los jugadores por páginas.
        MonitorViews.Header(content, Width, $"{Loc.T("report.short")} · {Loc.T("level.n", level.Number)}".ToUpperInvariant(), level, false);
        MonitorViews.Stats(content, 118f, Width, 190f, level, true);
        MonitorViews.Players(content, 326f, Width, Height - 326f - 30f, level.RankedPlayers.ToList(), page, out _);
    }
}
