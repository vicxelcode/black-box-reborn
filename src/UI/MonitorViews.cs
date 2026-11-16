using System.Collections.Generic;
using System.Linq;
using BlackBoxReborn.Stats;
using TMPro;
using UnityEngine;

namespace BlackBoxReborn.UI;

/// <summary>
/// Diseño del monitor del camión. El juego pinta a 752×423 y luego escala, así que a unos metros el
/// monitor ocupa unos 300 px de ancho: pocos elementos y letra muy grande (nada por debajo de 30).
/// </summary>
internal static class MonitorViews
{
    private const float Pad = 34f;

    public static void Header(RectTransform p, float w, string left, LevelStats level, bool live)
    {
        Views.Logo(p, Pad, 26f, 64f);
        var title = Ui.Label(p, "BLACK BOX", 76f, Theme.Text, bold: true).Display();
        title.overflowMode = TextOverflowModes.Overflow;
        title.characterSpacing = 4f;
        title.rectTransform.Place(Pad + 84f, 18f, 420f, 80f);

        var right = Ui.Label(p, "", 54f, Theme.Text, TextAlignmentOptions.Right, true).Display();
        right.overflowMode = TextOverflowModes.Overflow;
        if (live && level != null)
        {
            var on = Mathf.FloorToInt(Time.unscaledTime * 1.6f) % 2 == 0;
            right.text = $"<color={Theme.Rich(on ? Theme.Bad : Theme.Hex("5A1E1F"))}>●</color> {Format.Duration(level.Duration)}";
        }
        else
        {
            right.text = left;
            right.color = Theme.Accent;
        }
        right.rectTransform.Place(w / 2f, 24f, w / 2f - Pad, 70f);
    }

    private static void BigStat(RectTransform p, float x, float y, float w, float h, string label, string value, Color color)
    {
        Ui.Box(p, Theme.Surface, "Stat", radiusScale: 1.2f).rectTransform.Place(x, y, w, h);
        Ui.Box(p, color, "StatBar", radiusScale: 0.3f).rectTransform.Place(x + 22f, y + 26f, 30f, 8f);
        var l = Ui.Label(p, label.ToUpperInvariant(), 30f, Theme.Muted, bold: true);
        l.characterSpacing = 3f;
        l.rectTransform.Place(x + 62f, y + 10f, w - 80f, 40f);
        var v = Ui.Label(p, value, 104f, color, bold: true).Display();
        v.enableAutoSizing = true;
        v.fontSizeMin = 40f;
        v.fontSizeMax = 104f;
        v.overflowMode = TextOverflowModes.Overflow;
        v.rectTransform.Place(x + 22f, y + 50f, w - 44f, h - 58f);
    }

    public static void Stats(RectTransform p, float y, float w, float h, LevelStats level, bool shop)
    {
        var items = new List<(string, string, Color)>
        {
            (Loc.T("kpi.extracted"), Format.Dollars(level.ValueExtracted), Theme.Good),
            (Loc.T("kpi.lost"), Format.Loss(level.ValueLost), level.ValueLost > 0.5f ? Theme.Bad : Theme.Muted),
            shop
                ? (Loc.T("kpi.discovered"), $"{level.ItemsDiscovered}/{level.ItemsSpawned}", Theme.Warn)
                : (Loc.T("kpi.remaining"), Format.Dollars(level.ValueRemaining), Theme.Info),
        };
        const float gap = 18f;
        var cw = (w - Pad * 2f - gap * 2f) / 3f;
        for (var i = 0; i < items.Count; i++)
        {
            var (label, value, color) = items[i];
            BigStat(p, Pad + i * (cw + gap), y, cw, h, label, value, color);
        }
    }

    /// <summary>Quién ha roto más, en directo. Hasta tres filas.</summary>
    public static void Damage(RectTransform p, float y, float w, float h, LevelStats level)
    {
        var x = Pad;
        var iw = w - Pad * 2f;
        Ui.Box(p, Theme.Surface, "Card", radiusScale: 1.2f).rectTransform.Place(x, y, iw, h);
        var t = Ui.Label(p, Loc.T("sec.damage").ToUpperInvariant(), 30f, Theme.Muted, bold: true);
        t.characterSpacing = 3f;
        t.rectTransform.Place(x + 26f, y + 12f, iw - 52f, 40f);

        // Debajo del ranking, el último suceso: da vida al monitor aunque nadie haya roto nada.
        const float lastH = 58f;
        var last = level.Feed.Count > 0 ? level.Feed[level.Feed.Count - 1] : null;
        if (last != null)
        {
            var (text, value, color) = Views.Describe(last);
            Ui.Box(p, Theme.Border, "Rule", round: false).rectTransform.Place(x + 26f, y + h - lastH - 4f, iw - 52f, 2f);
            Views.Dot(p, x + 26f, y + h - lastH / 2f - 9f, 18f, color);
            Ui.Label(p, text, 34f, Theme.Muted).rectTransform.Place(x + 58f, y + h - lastH, iw * 0.68f, lastH);
            Ui.Label(p, value, 40f, color, TextAlignmentOptions.Right, true).Display()
                .rectTransform.Place(x + iw * 0.70f, y + h - lastH, iw * 0.30f - 26f, lastH);
            h -= lastH;
        }

        var rows = level.Players.Values.Where(r => r.ValueBroken > 0.5f).OrderByDescending(r => r.ValueBroken).Take(3).ToList();
        if (rows.Count == 0)
        {
            var e = Ui.Label(p, Loc.T("sec.damage.empty"), 40f, Theme.Faint, TextAlignmentOptions.Center);
            e.fontStyle = FontStyles.Italic;
            e.rectTransform.Place(x, y + 50f, iw, h - 60f);
            return;
        }
        var rowH = Mathf.Min(78f, (h - 60f) / rows.Count);
        var max = rows[0].ValueBroken;
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var ry = y + 56f + i * rowH;
            Views.Dot(p, x + 26f, ry + rowH / 2f - 12f, 24f, Theme.Player(r.Order, r.IsUnknown));
            Ui.Label(p, r.IsUnknown ? Loc.T("unknown") : r.Name, 46f, Theme.Text, bold: true)
                .rectTransform.Place(x + 66f, ry, iw * 0.45f, rowH);
            var bx = x + iw * 0.50f;
            var bw = iw * 0.20f;
            Ui.Box(p, Theme.Track, "Track", radiusScale: 0.5f).rectTransform.Place(bx, ry + rowH / 2f - 9f, bw, 18f);
            Ui.Box(p, Theme.Bad, "Fill", radiusScale: 0.5f).rectTransform
                .Place(bx, ry + rowH / 2f - 9f, Mathf.Max(18f, bw * r.ValueBroken / max), 18f);
            var v = Ui.Label(p, Format.Loss(r.ValueBroken), 66f, Theme.Bad, TextAlignmentOptions.Right, true).Display();
            v.overflowMode = TextOverflowModes.Overflow;
            v.rectTransform.Place(x + iw * 0.72f, ry, iw * 0.28f - 26f, rowH);
        }
    }

    /// <summary>Informe en la tienda: tres jugadores por página con lo extraído y lo roto.</summary>
    public static void Players(RectTransform p, float y, float w, float h, IList<PlayerStats> players, int page, out int pages)
    {
        const int perPage = 3;
        pages = Mathf.Max(1, Mathf.CeilToInt(players.Count / (float)perPage));
        page %= pages;
        var real = players.Where(r => !r.IsUnknown).ToList();
        var mvp = real.Where(r => r.ValueHauled > 0.5f).OrderByDescending(r => r.ValueHauled).FirstOrDefault();
        var butter = real.Where(r => r.ValueBroken > 0.5f).OrderByDescending(r => r.ValueBroken).FirstOrDefault();

        var x = Pad;
        var iw = w - Pad * 2f;
        Ui.Box(p, Theme.Surface, "Card", radiusScale: 1.2f).rectTransform.Place(x, y, iw, h);
        // Columnas: nombre (+ insignia) | extraído | roto. A esta distancia no caben más.
        const float nameEnd = 0.50f, hauledEnd = 0.75f;
        var head = new[] { (Loc.T("col.hauled"), nameEnd, hauledEnd), (Loc.T("col.broken"), hauledEnd, 1f) };
        foreach (var (text, from, to) in head)
        {
            var l = Ui.Label(p, text.ToUpperInvariant(), 28f, Theme.Muted, TextAlignmentOptions.Right, true);
            l.characterSpacing = 3f;
            l.rectTransform.Place(x + iw * from, y + 12f, iw * (to - from) - 26f, 36f);
        }
        var shown = players.Skip(page * perPage).Take(perPage).ToList();
        var rowH = Mathf.Min(82f, (h - 56f) / perPage);
        for (var i = 0; i < shown.Count; i++)
        {
            var r = shown[i];
            var ry = y + 52f + i * rowH;
            Views.Dot(p, x + 26f, ry + rowH / 2f - 12f, 24f, Theme.Player(r.Order, r.IsUnknown));
            var badge = r == mvp ? Loc.T("badge.mvp") : r == butter ? Loc.T("badge.butter") : null;
            var nameMax = iw * nameEnd - 66f - (badge != null ? 190f : 10f);
            var name = Ui.Label(p, r.IsUnknown ? Loc.T("unknown") : r.Name, 44f, Theme.Text, bold: true);
            name.enableAutoSizing = true;
            name.fontSizeMin = 30f;
            name.fontSizeMax = 44f;
            var nameW = Mathf.Min(name.GetPreferredValues(name.text).x + 4f, nameMax);
            name.rectTransform.Place(x + 66f, ry, nameW, rowH);
            if (badge != null)
                Views.Pill(p, x + 78f + nameW, ry + rowH / 2f - 19f, badge, r == mvp ? Theme.Good : Theme.Bad, Theme.Hex("101114"), 26f);

            var hauled = Ui.Label(p, Format.Dollars(r.ValueHauled), 62f, r.ValueHauled > 0.5f ? Theme.Good : Theme.Faint,
                TextAlignmentOptions.Right, true).Display();
            hauled.overflowMode = TextOverflowModes.Overflow;
            hauled.rectTransform.Place(x + iw * nameEnd, ry, iw * (hauledEnd - nameEnd) - 26f, rowH);
            var broken = Ui.Label(p, Format.Loss(r.ValueBroken), 62f, r.ValueBroken > 0.5f ? Theme.Bad : Theme.Faint,
                TextAlignmentOptions.Right, true).Display();
            broken.overflowMode = TextOverflowModes.Overflow;
            broken.rectTransform.Place(x + iw * hauledEnd, ry, iw * (1f - hauledEnd) - 26f, rowH);
        }
    }
}
