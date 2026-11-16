using System.Linq;
using BlackBoxReborn.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackBoxReborn.UI;

/// <summary>Panel a pantalla completa que se abre con una tecla. Tres pestañas: nivel, jugadores, partida.</summary>
internal sealed class StatsPanel : MonoBehaviour
{
    public const float Width = 1180f;
    public const float Height = 700f;
    private const float HeaderH = 82f;
    private const float FooterH = 40f;

    private static readonly string[] Tabs = { "tab.level", "tab.players", "tab.run" };

    private RectTransform root;
    private RectTransform content;
    private CanvasGroup group;
    private bool open;
    private float visibility;
    private int tab;
    private int selected;
    private int builtRevision = -1;
    private float nextRebuild;
    private bool dirty;

    public bool IsOpen => open;

    private void Awake()
    {
        var canvasGo = new GameObject("BlackBoxPanel", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.layer = 5;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = Ui.Rect("Panel", canvasGo.transform);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(Width, Height);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // Marco: borde fino + fondo casi opaco.
        Ui.Box(root, Theme.Border, "Border", radiusScale: 0.9f).rectTransform.Fill(-1f, -1f, -1f, -1f);
        Ui.Box(root, Theme.Backdrop, "Backdrop", radiusScale: 0.9f).rectTransform.Fill();
        Ui.Box(root, Theme.Accent, "TopLine", round: false).rectTransform.Place(28f, 0f, 120f, 3f);
        content = Ui.Rect("Content", root).Fill();
        root.gameObject.SetActive(false);
    }

    public void Toggle()
    {
        open = !open;
        if (open)
        {
            root.gameObject.SetActive(true);
            dirty = true;
        }
    }

    public void Close() => open = false;

    public void ChangeTab(int delta)
    {
        tab = (tab + delta + Tabs.Length) % Tabs.Length;
        dirty = true;
    }

    public void SetTab(int index)
    {
        tab = Mathf.Clamp(index, 0, Tabs.Length - 1);
        dirty = true;
    }

    public void ChangeSelection(int delta)
    {
        if (tab != 1) return;
        selected += delta;
        dirty = true;
    }

    private void Update()
    {
        var target = open ? 1f : 0f;
        visibility = Mathf.MoveTowards(visibility, target, Time.unscaledDeltaTime * 7f);
        var eased = 1f - Mathf.Pow(1f - visibility, 3f);
        group.alpha = eased;
        root.localScale = Vector3.one * (Settings.PanelScale.Value * (0.96f + 0.04f * eased));
        if (!open && visibility <= 0f)
        {
            if (root.gameObject.activeSelf) root.gameObject.SetActive(false);
            return;
        }

        if (dirty || Tracker.Revision != builtRevision || Time.unscaledTime >= nextRebuild)
            Rebuild();
    }

    private void Rebuild()
    {
        dirty = false;
        builtRevision = Tracker.Revision;
        nextRebuild = Time.unscaledTime + 1f;
        Ui.Clear(content);

        var run = Tracker.Run;
        var level = run.Current;
        Views.Header(content, Width, level, null);
        DrawTabs();

        var body = Ui.Rect("Body", content).Place(Views.Pad + 8f, HeaderH + 8f, Width - (Views.Pad + 8f) * 2f,
            Height - HeaderH - FooterH - 16f);
        var w = body.rect.width;
        var h = body.rect.height;

        switch (tab)
        {
            case 0: DrawLevel(body, w, h, level); break;
            case 1: DrawPlayers(body, w, h, level); break;
            default: DrawRun(body, w, h, run); break;
        }

        var key = Settings.PanelKey.Value.ToString();
        Ui.Box(content, Theme.Border, "FooterRule", round: false).rectTransform
            .Place(Views.Pad + 8f, Height - FooterH, Width - (Views.Pad + 8f) * 2f, 1f);
        Ui.Label(content, Loc.T("hint.panel", key), 13f, Theme.Faint)
            .rectTransform.Place(Views.Pad + 8f, Height - FooterH + 2f, 700f, FooterH - 4f);
        Ui.Label(content, $"v{Plugin.Version}", 13f, Theme.Faint, TextAlignmentOptions.Right)
            .rectTransform.Place(Width - 220f, Height - FooterH + 2f, 220f - Views.Pad - 8f, FooterH - 4f);
    }

    private void DrawTabs()
    {
        var x = Width - Views.Pad - 8f;
        for (var i = Tabs.Length - 1; i >= 0; i--)
        {
            var active = i == tab;
            var label = Ui.Label(content, $"<size=75%><color={Theme.Rich(active ? Theme.Hex("5A2A08") : Theme.Faint)}>{i + 1}</color></size>  {Loc.T(Tabs[i])}",
                15f, active ? Theme.Hex("141518") : Theme.Muted, TextAlignmentOptions.Center, true, "Tab");
            var w = label.GetPreferredValues(label.text).x + 32f;
            x -= w;
            var box = Ui.Box(content, active ? Theme.Accent : Theme.SurfaceHi, "TabBg", radiusScale: 0.45f);
            box.rectTransform.Place(x, 24f, w, 36f);
            box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            label.rectTransform.Place(x, 24f, w, 36f);
            x -= 8f;
        }
    }

    private static void DrawEmpty(RectTransform body, float w, float h)
    {
        Views.Logo(body, w / 2f - 32f, h / 2f - 90f, 64f);
        Ui.Label(body, Loc.T("level.none"), 22f, Theme.Text, TextAlignmentOptions.Center, true)
            .rectTransform.Place(0f, h / 2f - 10f, w, 30f);
        Ui.Label(body, Loc.T("level.none.sub"), 15f, Theme.Muted, TextAlignmentOptions.Center)
            .rectTransform.Place(0f, h / 2f + 24f, w, 24f);
    }

    private static void DrawLevel(RectTransform body, float w, float h, LevelStats level)
    {
        if (level == null)
        {
            DrawEmpty(body, w, h);
            return;
        }
        Views.LevelKpis(body, 0f, 0f, w, 116f, level);
        Views.ValueBar(body, 0f, 134f, w, level);
        const float top = 196f;
        var half = (w - 14f) / 2f;
        Views.DamageRanking(body, 0f, top, half, h - top, level);
        Views.Feed(body, half + 14f, top, half, h - top, level);
    }

    private void DrawPlayers(RectTransform body, float w, float h, LevelStats level)
    {
        if (level == null)
        {
            DrawEmpty(body, w, h);
            return;
        }
        var players = level.RankedPlayers.ToList();
        if (players.Count == 0) return;
        selected = Mathf.Clamp(selected, 0, players.Count - 1);
        var picked = players[selected];
        var used = Views.PlayerTable(body, 0f, 0f, w, h * 0.62f, players, picked.Id, false);
        Views.ItemDetail(body, 0f, used + 14f, w, h - used - 14f, picked);
    }

    private static void DrawRun(RectTransform body, float w, float h, RunStats run)
    {
        if (run.Levels.Count == 0)
        {
            DrawEmpty(body, w, h);
            return;
        }
        Views.RunKpis(body, 0f, 0f, w, 116f, run);
        const float top = 132f;
        var left = w * 0.6f;
        Views.LevelsTable(body, 0f, top, left - 7f, h - top, run);
        Views.RunPlayers(body, left + 7f, top, w - left - 7f, h - top, run.Totals());
    }
}
