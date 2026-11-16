using System.Collections.Generic;
using System.Linq;
using BlackBoxReborn.Stats;
using TMPro;
using UnityEngine;

namespace BlackBoxReborn.UI;

/// <summary>
/// Bloques de la interfaz con posiciones absolutas (x, y desde arriba a la izquierda). Los usan tanto
/// el panel de pantalla como el monitor del camión, que comparten el mismo lienzo de referencia.
/// </summary>
internal static class Views
{
    public const float Pad = 20f;

    // ───────────────────────── Piezas pequeñas ─────────────────────────

    public static void Logo(RectTransform parent, float x, float y, float size)
    {
        Ui.Box(parent, Theme.Accent, "Logo", radiusScale: size / 80f).rectTransform.Place(x, y, size, size);
        var inner = size * 0.56f;
        Ui.Box(parent, Theme.Hex("111215"), "LogoInner", radiusScale: inner / 90f).rectTransform
            .Place(x + (size - inner) / 2f, y + (size - inner) / 2f - size * 0.04f, inner, inner * 0.82f);
        var stripe = size * 0.36f;
        Ui.Box(parent, Theme.Accent, "LogoStripe", round: false).rectTransform
            .Place(x + (size - stripe) / 2f, y + size * 0.47f, stripe, Mathf.Max(2f, size * 0.07f));
    }

    public static void Dot(RectTransform parent, float x, float y, float size, Color color) =>
        Ui.Box(parent, color, "Dot", radiusScale: size / 40f).rectTransform.Place(x, y, size, size);

    public static float Pill(RectTransform parent, float x, float y, string text, Color bg, Color fg, float size = 12f)
    {
        var label = Ui.Label(parent, text, size, fg, TextAlignmentOptions.Center, true, "PillText");
        label.characterSpacing = 3f;
        var w = label.GetPreferredValues(text).x + 16f;
        var h = size + 10f;
        var box = Ui.Box(parent, bg, "Pill", radiusScale: h / 40f);
        box.rectTransform.Place(x, y, w, h);
        box.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
        label.rectTransform.Place(x, y, w, h);
        return w;
    }

    /// <summary>Tarjeta con título. Devuelve el alto ocupado por la cabecera.</summary>
    public static float Card(RectTransform parent, float x, float y, float w, float h, string title)
    {
        Ui.Box(parent, Theme.Surface, "Card", radiusScale: 0.7f).rectTransform.Place(x, y, w, h);
        if (string.IsNullOrEmpty(title)) return 0f;
        var t = Ui.Label(parent, title.ToUpperInvariant(), 14f, Theme.Muted, bold: true, name: "CardTitle");
        t.characterSpacing = 4f;
        t.rectTransform.Place(x + Pad, y + 14f, w - Pad * 2f, 20f);
        return 46f;
    }

    private static void Empty(RectTransform parent, float x, float y, float w, float h, string text)
    {
        var t = Ui.Label(parent, text, 16f, Theme.Faint, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Italic;
        t.rectTransform.Place(x, y, w, h);
    }

    private static string PlayerName(PlayerStats p) => p.IsUnknown ? Loc.T("unknown") : p.Name;

    private static void NameCell(RectTransform parent, float x, float y, float w, float h, PlayerStats p,
        float size, params (string text, Color bg)[] badges)
    {
        var chip = size * 0.55f;
        Dot(parent, x, y + (h - chip) / 2f, chip, Theme.Player(p.Order, p.IsUnknown));
        var name = Ui.Label(parent, PlayerName(p), size, p.IsUnknown ? Theme.Muted : Theme.Text, bold: !p.IsUnknown);
        var nameW = Mathf.Min(name.GetPreferredValues(name.text).x + 2f, w - chip - 12f);
        name.rectTransform.Place(x + chip + 10f, y, nameW, h);
        var bx = x + chip + 10f + nameW + 8f;
        foreach (var (text, bg) in badges)
        {
            if (bx > x + w - 40f) break;
            bx += Pill(parent, bx, y + (h - 22f) / 2f, text, bg, Theme.Hex("101114"), 11f) + 6f;
        }
    }

    // ───────────────────────── Cabecera ─────────────────────────

    public static void Header(RectTransform parent, float w, LevelStats level, string kicker)
    {
        Logo(parent, Pad, 18f, 40f);
        var title = Ui.Label(parent, $"BLACK BOX <size=60%><color={Theme.Rich(Theme.Accent)}>REBORN</color></size>",
            36f, Theme.Text, bold: true, name: "Title").Display();
        title.characterSpacing = 3f;
        title.overflowMode = TextOverflowModes.Overflow;
        title.rectTransform.Place(Pad + 54f, 10f, 420f, 38f);

        var sub = new List<string>();
        if (!string.IsNullOrEmpty(kicker)) sub.Add(kicker);
        if (level != null)
        {
            sub.Add(Loc.T("level.n", level.Number));
            sub.Add(level.LevelName);
            sub.Add(Format.Duration(level.Duration));
        }
        var subtitle = Ui.Label(parent, string.Join("  ·  ", sub), 15f, Theme.Muted, name: "Subtitle");
        var subX = Pad + 54f;
        if (level != null && !level.Finished)
        {
            // Punto rojo que parpadea: el nivel se está grabando.
            var on = Mathf.FloorToInt(Time.unscaledTime * 1.6f) % 2 == 0;
            Dot(parent, subX, 50f, 9f, on ? Theme.Bad : Theme.Hex("5A1E1F"));
            var live = Ui.Label(parent, Loc.T("live"), 13f, Theme.Bad, bold: true, name: "Live");
            live.characterSpacing = 3f;
            live.rectTransform.Place(subX + 15f, 44f, 120f, 20f);
            subX += 22f + live.GetPreferredValues(live.text).x;
        }
        subtitle.rectTransform.Place(subX, 44f, w - subX - 300f, 20f);
    }

    // ───────────────────────── Nivel ─────────────────────────

    public static void Kpis(RectTransform parent, float x, float y, float w, float h,
        IList<(string label, string value, Color color, string sub)> items)
    {
        const float gap = 14f;
        var cw = (w - gap * (items.Count - 1)) / items.Count;
        for (var i = 0; i < items.Count; i++)
        {
            var (label, value, color, sub) = items[i];
            var cx = x + i * (cw + gap);
            Ui.Box(parent, Theme.Surface, "Kpi", radiusScale: 0.7f).rectTransform.Place(cx, y, cw, h);
            Ui.Box(parent, color, "KpiAccent", radiusScale: 0.1f).rectTransform.Place(cx + Pad, y + 18f, 18f, 4f);
            var l = Ui.Label(parent, label.ToUpperInvariant(), 13f, Theme.Muted, bold: true);
            l.characterSpacing = 4f;
            l.rectTransform.Place(cx + Pad + 26f, y + 10f, cw - Pad * 2f - 26f, 20f);
            var v = Ui.Label(parent, value, 50f, color, bold: true, name: "KpiValue").Display();
            v.enableAutoSizing = true;
            v.fontSizeMin = 24f;
            v.fontSizeMax = 50f;
            v.characterSpacing = 1f;
            v.rectTransform.Place(cx + Pad, y + 32f, cw - Pad * 2f, 48f);
            Ui.Label(parent, sub, 14f, Theme.Muted).rectTransform.Place(cx + Pad, y + h - 34f, cw - Pad * 2f, 20f);
        }
    }

    public static void LevelKpis(RectTransform parent, float x, float y, float w, float h, LevelStats level)
    {
        Kpis(parent, x, y, w, h, new[]
        {
            (Loc.T("kpi.extracted"), Format.Dollars(level.ValueExtracted), Theme.Good,
                Loc.T("kpi.extracted.sub", Format.Percent(level.ExtractedPercent), level.ItemsExtracted)),
            (Loc.T("kpi.lost"), Format.Loss(level.ValueLost), level.ValueLost > 0.5f ? Theme.Bad : Theme.Text,
                Loc.T("kpi.lost.sub", level.ItemsDestroyed, level.Hits)),
            (Loc.T("kpi.discovered"), $"{level.ItemsDiscovered}<size=60%><color={Theme.Rich(Theme.Muted)}> / {level.ItemsSpawned}</color></size>",
                Theme.Warn, Loc.T("kpi.discovered.sub", level.ItemsSpawned)),
            (Loc.T("kpi.remaining"), Format.Dollars(level.ValueRemaining), level.Finished ? Theme.Muted : Theme.Info,
                level.Finished ? Loc.T("kpi.remaining.done") : Loc.T("kpi.remaining.sub")),
        });
    }

    /// <summary>Barra apilada: extraído | perdido | queda en el mapa.</summary>
    public static void ValueBar(RectTransform parent, float x, float y, float w, LevelStats level)
    {
        var remaining = level.Finished ? 0f : level.ValueRemaining;
        var total = Mathf.Max(1f, level.ValueExtracted + level.ValueLost + remaining);
        var segments = new[]
        {
            (Loc.T("bar.extracted"), level.ValueExtracted, Theme.Good),
            (Loc.T("bar.lost"), level.ValueLost, Theme.Bad),
            (Loc.T("bar.remaining"), remaining, Theme.Remaining),
        };

        // Leyenda
        var lx = x;
        foreach (var (label, value, color) in segments)
        {
            Dot(parent, lx, y + 5f, 10f, color);
            var t = Ui.Label(parent,
                $"{label} <color={Theme.Rich(Theme.Text)}>{Format.Percent(value / total * 100f)}%</color>", 14f, Theme.Muted);
            var tw = t.GetPreferredValues(t.text).x + 4f;
            t.rectTransform.Place(lx + 16f, y, tw, 20f);
            lx += 16f + tw + 22f;
        }

        const float barH = 12f;
        var by = y + 28f;
        Ui.Box(parent, Theme.Track, "BarTrack", radiusScale: barH / 40f).rectTransform.Place(x, by, w, barH);
        var sx = x;
        foreach (var (_, value, color) in segments)
        {
            var sw = w * value / total;
            if (sw < 1f) continue;
            Ui.Box(parent, color, "BarSegment", radiusScale: barH / 40f).rectTransform.Place(sx, by, sw, barH);
            sx += sw;
        }
    }

    public static void DamageRanking(RectTransform parent, float x, float y, float w, float h, LevelStats level)
    {
        var top = Card(parent, x, y, w, h, Loc.T("sec.damage"));
        var rows = level.Players.Values.Where(p => p.ValueBroken > 0.5f).OrderByDescending(p => p.ValueBroken).ToList();
        if (rows.Count == 0)
        {
            Empty(parent, x, y + top, w, h - top - 10f, Loc.T("sec.damage.empty"));
            return;
        }
        var max = rows[0].ValueBroken;
        const float rowH = 40f;
        var fit = Mathf.Max(1, Mathf.FloorToInt((h - top - 10f) / rowH));
        var ix = x + Pad;
        var iw = w - Pad * 2f;
        for (var i = 0; i < Mathf.Min(fit, rows.Count); i++)
        {
            var p = rows[i];
            var ry = y + top + i * rowH;
            NameCell(parent, ix, ry, iw * 0.36f, rowH, p, 17f);
            var bx = ix + iw * 0.38f;
            var bw = iw * 0.30f;
            Ui.Box(parent, Theme.Track, "Track", radiusScale: 0.2f).rectTransform.Place(bx, ry + rowH / 2f - 4f, bw, 8f);
            Ui.Box(parent, Theme.Bad, "Fill", radiusScale: 0.2f).rectTransform
                .Place(bx, ry + rowH / 2f - 4f, Mathf.Max(8f, bw * p.ValueBroken / max), 8f);
            Ui.Label(parent,
                    $"{Format.Loss(p.ValueBroken)} <size=75%><color={Theme.Rich(Theme.Muted)}>· {p.Hits}</color></size>",
                    17f, Theme.Bad, TextAlignmentOptions.Right, true)
                .rectTransform.Place(ix + iw * 0.70f, ry, iw * 0.30f, rowH);
        }
    }

    public static void Feed(RectTransform parent, float x, float y, float w, float h, LevelStats level)
    {
        var top = Card(parent, x, y, w, h, Loc.T("sec.feed"));
        if (level.Feed.Count == 0)
        {
            Empty(parent, x, y + top, w, h - top - 10f, Loc.T("sec.feed.empty"));
            return;
        }
        const float rowH = 36f;
        var fit = Mathf.Max(1, Mathf.FloorToInt((h - top - 10f) / rowH));
        var ix = x + Pad;
        var iw = w - Pad * 2f;
        var events = Enumerable.Reverse(level.Feed).Take(fit).ToList();
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            var ry = y + top + i * rowH;
            var (text, value, color) = Describe(e);
            Dot(parent, ix, ry + rowH / 2f - 4f, 8f, color);
            Ui.Label(parent, text, 16f, Theme.Text).rectTransform.Place(ix + 18f, ry, iw * 0.62f, rowH);
            Ui.Label(parent, value, 16f, color, TextAlignmentOptions.Right, true)
                .rectTransform.Place(ix + iw * 0.60f, ry, iw * 0.24f, rowH);
            Ui.Label(parent, Format.Ago(Time.time - e.Time), 13f, Theme.Faint, TextAlignmentOptions.Right)
                .rectTransform.Place(ix + iw * 0.84f, ry, iw * 0.16f, rowH);
        }
    }

    internal static (string text, string value, Color color) Describe(FeedEvent e)
    {
        var who = $"<b>{e.Who}</b>";
        switch (e.Kind)
        {
            case FeedKind.Break:
                return (Loc.T("feed.break", who, e.What), Format.Loss(e.Value), Theme.Warn);
            case FeedKind.Destroy:
                return (Loc.T("feed.destroy", who, e.What), Format.Loss(e.Value), Theme.Bad);
            case FeedKind.Fall:
                return (e.Who == null || e.Who == Loc.T("unknown")
                    ? Loc.T("feed.fall", e.What)
                    : Loc.T("feed.fall.by", who, e.What), Format.Loss(e.Value), Theme.Bad);
            case FeedKind.Extract:
                var text = e.Count > 1 ? Loc.T("feed.extract.n", e.Count) : Loc.T("feed.extract", e.What);
                return (text, "+" + Format.Dollars(e.Value), Theme.Good);
            default:
                return (string.IsNullOrEmpty(e.What) ? Loc.T("feed.death", who) : Loc.T("feed.death.by", who, e.What),
                    "", Theme.Info);
        }
    }

    // ───────────────────────── Jugadores ─────────────────────────

    private enum Col { Player, Hauled, Broken, Destroyed, Hits, Hurt, Deaths }

    private static readonly (Col col, float width)[] FullColumns =
    {
        (Col.Player, 0.30f), (Col.Hauled, 0.14f), (Col.Broken, 0.13f), (Col.Destroyed, 0.11f),
        (Col.Hits, 0.08f), (Col.Hurt, 0.14f), (Col.Deaths, 0.10f),
    };

    private static readonly (Col col, float width)[] CompactColumns =
    {
        (Col.Player, 0.36f), (Col.Hauled, 0.19f), (Col.Broken, 0.17f), (Col.Destroyed, 0.15f), (Col.Deaths, 0.13f),
    };

    private static string Header(Col c) => c switch
    {
        Col.Player => Loc.T("col.player"),
        Col.Hauled => Loc.T("col.hauled"),
        Col.Broken => Loc.T("col.broken"),
        Col.Destroyed => Loc.T("col.destroyed"),
        Col.Hits => Loc.T("col.hits"),
        Col.Hurt => Loc.T("col.hurt"),
        _ => Loc.T("col.deaths"),
    };

    /// <summary>Tabla de jugadores. Devuelve el alto que ha ocupado.</summary>
    public static float PlayerTable(RectTransform parent, float x, float y, float w, float maxH,
        IList<PlayerStats> players, string selectedId, bool compact, int first = 0, int count = int.MaxValue)
    {
        var columns = compact ? CompactColumns : FullColumns;
        var real = players.Where(p => !p.IsUnknown).ToList();
        var mvp = real.Where(p => p.ValueHauled > 0.5f).OrderByDescending(p => p.ValueHauled).FirstOrDefault();
        var butter = real.Where(p => p.ValueBroken > 0.5f).OrderByDescending(p => p.ValueBroken).FirstOrDefault();

        const float headH = 44f;
        var visible = players.Skip(first).Take(count).ToList();
        var rowH = Mathf.Clamp((maxH - headH - 10f) / Mathf.Max(1, visible.Count), 30f, 46f);
        var h = headH + rowH * visible.Count + 10f;
        Card(parent, x, y, w, h, null);

        var ix = x + Pad;
        var iw = w - Pad * 2f;
        var cx = ix;
        foreach (var (col, width) in columns)
        {
            var head = Ui.Label(parent, Header(col).ToUpperInvariant(), 13f, Theme.Muted,
                col == Col.Player ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, true);
            head.characterSpacing = 3f;
            head.rectTransform.Place(cx, y + 12f, iw * width, 24f);
            cx += iw * width;
        }
        Ui.Box(parent, Theme.Border, "Rule", round: false).rectTransform.Place(ix, y + headH - 2f, iw, 1f);

        for (var i = 0; i < visible.Count; i++)
        {
            var p = visible[i];
            var ry = y + headH + i * rowH;
            if (p.Id == selectedId)
            {
                Ui.Box(parent, Theme.SurfaceHi, "Selected", radiusScale: 0.3f).rectTransform.Place(x + 6f, ry + 2f, w - 12f, rowH - 4f);
                Ui.Box(parent, Theme.Accent, "SelectedBar", radiusScale: 0.1f).rectTransform.Place(x + 6f, ry + 8f, 4f, rowH - 16f);
            }
            var badges = new List<(string, Color)>();
            if (p == mvp) badges.Add((Loc.T("badge.mvp"), Theme.Good));
            if (p == butter) badges.Add((Loc.T("badge.butter"), Theme.Bad));

            cx = ix;
            foreach (var (col, width) in columns)
            {
                var cw = iw * width;
                if (col == Col.Player)
                {
                    NameCell(parent, cx, ry, cw - 8f, rowH, p, 17f, badges.ToArray());
                }
                else
                {
                    var (text, color) = Cell(col, p);
                    Ui.Label(parent, text, 17f, color, TextAlignmentOptions.Right, col == Col.Hauled || col == Col.Broken)
                        .rectTransform.Place(cx, ry, cw, rowH);
                }
                cx += cw;
            }
        }
        return h;
    }

    private static (string, Color) Cell(Col col, PlayerStats p)
    {
        switch (col)
        {
            case Col.Hauled:
                return (Format.Dollars(p.ValueHauled), p.ValueHauled > 0.5f ? Theme.Good : Theme.Faint);
            case Col.Broken:
                return (Format.Loss(p.ValueBroken), p.ValueBroken > 0.5f ? Theme.Bad : Theme.Faint);
            case Col.Destroyed:
                return (p.ItemsDestroyed.ToString(), p.ItemsDestroyed > 0 ? Theme.Bad : Theme.Faint);
            case Col.Hits:
                return (p.Hits.ToString(), p.Hits > 0 ? Theme.Text : Theme.Faint);
            case Col.Hurt:
                return (p.DamageTaken > 0 ? $"{p.DamageTaken} HP" : "0", p.DamageTaken > 0 ? Theme.Warn : Theme.Faint);
            default:
                return (p.Deaths.ToString(), p.Deaths > 0 ? Theme.Text : Theme.Faint);
        }
    }

    public static void ItemDetail(RectTransform parent, float x, float y, float w, float h, PlayerStats p)
    {
        var name = p == null ? "" : PlayerName(p);
        var top = Card(parent, x, y, w, h, Loc.T("sec.items", name));
        var items = p?.ItemsByLoss.ToList() ?? new List<ItemDamage>();
        if (items.Count == 0)
        {
            Empty(parent, x, y + top, w, h - top - 10f, Loc.T("sec.items.empty"));
            return;
        }
        const float rowH = 32f;
        var fit = Mathf.Max(1, Mathf.FloorToInt((h - top - 8f) / rowH));
        var columns = w > 700f ? 2 : 1;
        var perColumn = fit;
        var shown = Mathf.Min(items.Count, perColumn * columns);
        if (shown < items.Count) shown = Mathf.Max(0, shown - 1);
        var colW = (w - Pad * 2f - (columns - 1) * 30f) / columns;
        for (var i = 0; i < shown; i++)
        {
            var item = items[i];
            var col = i / perColumn;
            var cx = x + Pad + col * (colW + 30f);
            var ry = y + top + (i % perColumn) * rowH;
            Ui.Label(parent, item.ItemName, 16f, Theme.Text).rectTransform.Place(cx, ry, colW * 0.5f, rowH);
            if (item.Destroyed)
                Pill(parent, cx + colW * 0.5f, ry + 6f, Loc.T("destroyed"), Theme.Hex("3A1517"), Theme.Bad, 10f);
            Ui.Label(parent, Format.Loss(item.ValueLost), 16f, Theme.Bad, TextAlignmentOptions.Right, true)
                .rectTransform.Place(cx + colW * 0.7f, ry, colW * 0.3f, rowH);
        }
        if (shown < items.Count)
        {
            var col = shown / perColumn;
            Ui.Label(parent, Loc.T("more", items.Count - shown), 14f, Theme.Muted)
                .rectTransform.Place(x + Pad + col * (colW + 30f), y + top + (shown % perColumn) * rowH, colW, rowH);
        }
    }

    // ───────────────────────── Partida ─────────────────────────

    public static void RunKpis(RectTransform parent, float x, float y, float w, float h, RunStats run)
    {
        var spawned = run.Levels.Sum(l => l.ValueSpawned);
        var pct = spawned > 0f ? run.ValueExtracted / spawned * 100f : 0f;
        var best = run.Levels.OrderByDescending(l => l.ValueExtracted).First();
        Kpis(parent, x, y, w, h, new[]
        {
            (Loc.T("run.extracted"), Format.Dollars(run.ValueExtracted), Theme.Good,
                Loc.T("kpi.extracted.sub", Format.Percent(pct), run.Levels.Sum(l => l.ItemsExtracted))),
            (Loc.T("run.lost"), Format.Loss(run.ValueLost), run.ValueLost > 0.5f ? Theme.Bad : Theme.Text,
                Loc.T("kpi.lost.sub", run.Levels.Sum(l => l.ItemsDestroyed), run.Levels.Sum(l => l.Hits))),
            (Loc.T("run.levels"), run.Levels.Count.ToString(), Theme.Accent,
                Loc.T("run.best", best.Number, Format.Dollars(best.ValueExtracted))),
            (Loc.T("run.time"), Format.Duration(run.Duration), Theme.Info,
                Loc.T("run.avg", Format.Duration(run.Duration / Mathf.Max(1, run.Levels.Count)))),
        });
    }

    public static void LevelsTable(RectTransform parent, float x, float y, float w, float h, RunStats run)
    {
        var top = Card(parent, x, y, w, h, Loc.T("sec.levels"));
        var columns = new (string head, float width, TextAlignmentOptions align)[]
        {
            ("#", 0.06f, TextAlignmentOptions.Left),
            (Loc.T("col.level"), 0.33f, TextAlignmentOptions.Left),
            (Loc.T("col.hauled"), 0.17f, TextAlignmentOptions.Right),
            (Loc.T("col.broken"), 0.15f, TextAlignmentOptions.Right),
            (Loc.T("col.found"), 0.15f, TextAlignmentOptions.Right),
            (Loc.T("col.time"), 0.14f, TextAlignmentOptions.Right),
        };
        var ix = x + Pad;
        var iw = w - Pad * 2f;
        var cx = ix;
        foreach (var (head, width, align) in columns)
        {
            var t = Ui.Label(parent, head.ToUpperInvariant(), 12f, Theme.Faint, align, true);
            t.characterSpacing = 3f;
            t.rectTransform.Place(cx, y + top - 4f, iw * width, 20f);
            cx += iw * width;
        }
        const float rowH = 34f;
        var fit = Mathf.Max(1, Mathf.FloorToInt((h - top - 24f) / rowH));
        var levels = run.Levels.Skip(Mathf.Max(0, run.Levels.Count - fit)).ToList();
        for (var i = 0; i < levels.Count; i++)
        {
            var l = levels[i];
            var ry = y + top + 20f + i * rowH;
            var cells = new (string text, Color color, bool bold)[]
            {
                (l.Number.ToString(), Theme.Muted, true),
                (l.LevelName + (l.Finished ? "" : $"  <color={Theme.Rich(Theme.Bad)}>●</color>"), Theme.Text, false),
                (Format.Dollars(l.ValueExtracted), Theme.Good, true),
                (Format.Loss(l.ValueLost), l.ValueLost > 0.5f ? Theme.Bad : Theme.Faint, false),
                ($"{l.ItemsDiscovered}/{l.ItemsSpawned}", Theme.Warn, false),
                (Format.Duration(l.Duration), Theme.Muted, false),
            };
            cx = ix;
            for (var c = 0; c < columns.Length; c++)
            {
                Ui.Label(parent, cells[c].text, 16f, cells[c].color, columns[c].align, cells[c].bold)
                    .rectTransform.Place(cx, ry, iw * columns[c].width, rowH);
                cx += iw * columns[c].width;
            }
        }
    }

    public static void RunPlayers(RectTransform parent, float x, float y, float w, float h, List<PlayerStats> players)
    {
        var top = Card(parent, x, y, w, h, Loc.T("sec.runplayers"));
        if (players.Count == 0)
        {
            Empty(parent, x, y + top, w, h - top - 10f, Loc.T("run.none"));
            return;
        }
        const float rowH = 52f;
        var fit = Mathf.Max(1, Mathf.FloorToInt((h - top - 8f) / rowH));
        var ix = x + Pad;
        var iw = w - Pad * 2f;
        var maxHauled = Mathf.Max(1f, players.Max(p => p.ValueHauled));
        for (var i = 0; i < Mathf.Min(fit, players.Count); i++)
        {
            var p = players[i];
            var ry = y + top + i * rowH;
            NameCell(parent, ix, ry, iw * 0.55f, 26f, p, 16f);
            Ui.Label(parent, Format.Dollars(p.ValueHauled), 16f, Theme.Good, TextAlignmentOptions.Right, true)
                .rectTransform.Place(ix + iw * 0.5f, ry, iw * 0.5f, 26f);
            Ui.Box(parent, Theme.Track, "Track", radiusScale: 0.15f).rectTransform.Place(ix, ry + 30f, iw, 5f);
            Ui.Box(parent, Theme.Good, "Fill", radiusScale: 0.15f).rectTransform
                .Place(ix, ry + 30f, Mathf.Max(5f, iw * p.ValueHauled / maxHauled), 5f);
            var deaths = p.Deaths > 0 ? $"  ·  {p.Deaths} {Loc.T("col.deaths").ToLowerInvariant()}" : "";
            Ui.Label(parent, $"{Format.Loss(p.ValueBroken)}{deaths}", 12f, Theme.Muted, TextAlignmentOptions.Right)
                .rectTransform.Place(ix + iw * 0.4f, ry + 34f, iw * 0.6f, 16f);
        }
    }
}
