using System;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace BlackBoxReborn;

/// <summary>Textos de la interfaz en inglés y español.</summary>
internal static class Loc
{
    private static readonly Dictionary<string, (string en, string es)> Table = new()
    {
        ["tab.level"] = ("Level", "Nivel"),
        ["tab.players"] = ("Players", "Jugadores"),
        ["tab.run"] = ("Run", "Partida"),
        ["hint.panel"] = ("{0} / Esc close  ·  ← → tabs  ·  ↑ ↓ player", "{0} / Esc cerrar  ·  ← → pestañas  ·  ↑ ↓ jugador"),

        ["level.n"] = ("Level {0}", "Nivel {0}"),
        ["level.none"] = ("No level recorded yet", "Aún no hay ningún nivel registrado"),
        ["level.none.sub"] = ("Stats start when a level loads.", "Las estadísticas empiezan al cargar un nivel."),
        ["live"] = ("LIVE", "EN DIRECTO"),
        ["report"] = ("LEVEL REPORT", "INFORME DEL NIVEL"),
        ["report.short"] = ("Report", "Informe"),
        ["finished"] = ("Finished", "Terminado"),

        ["kpi.extracted"] = ("Extracted", "Extraído"),
        ["kpi.lost"] = ("Lost", "Perdido"),
        ["kpi.discovered"] = ("Discovered", "Descubiertos"),
        ["kpi.remaining"] = ("Remaining", "Restante"),
        ["kpi.extracted.sub"] = ("{0}% of the map · {1} items", "{0}% del mapa · {1} objetos"),
        ["kpi.lost.sub"] = ("{0} destroyed · {1} hits", "{0} destruidos · {1} golpes"),
        ["kpi.discovered.sub"] = ("of {0} valuables", "de {0} objetos"),
        ["kpi.remaining.sub"] = ("still on the map", "aún en el mapa"),
        ["kpi.remaining.done"] = ("level closed", "nivel cerrado"),

        ["bar.extracted"] = ("Extracted", "Extraído"),
        ["bar.lost"] = ("Lost", "Perdido"),
        ["bar.remaining"] = ("Left", "Queda"),

        ["sec.damage"] = ("Damage to valuables", "Daños a objetos"),
        ["sec.feed"] = ("Recent activity", "Actividad reciente"),
        ["sec.feed.empty"] = ("Nothing yet. Handle with care.", "Nada todavía. Con cuidado."),
        ["sec.damage.empty"] = ("Nobody has broken anything. Yet.", "Nadie ha roto nada. Todavía."),
        ["sec.items"] = ("Valuables damaged by {0}", "Objetos dañados por {0}"),
        ["sec.items.empty"] = ("Clean hands.", "Manos limpias."),
        ["sec.levels"] = ("Levels", "Niveles"),
        ["sec.runplayers"] = ("Whole run", "Toda la partida"),

        ["col.player"] = ("Player", "Jugador"),
        ["col.hauled"] = ("Hauled", "Extraído"),
        ["col.broken"] = ("Broken", "Roto"),
        ["col.destroyed"] = ("Destroyed", "Destruidos"),
        ["col.hits"] = ("Hits", "Golpes"),
        ["col.hurt"] = ("Damage taken", "Daño recibido"),
        ["col.deaths"] = ("Deaths", "Muertes"),
        ["col.level"] = ("Level", "Nivel"),
        ["col.time"] = ("Time", "Tiempo"),
        ["col.found"] = ("Found", "Halladas"),

        ["run.extracted"] = ("Total extracted", "Total extraído"),
        ["run.lost"] = ("Total lost", "Total perdido"),
        ["run.levels"] = ("Levels", "Niveles"),
        ["run.time"] = ("Time played", "Tiempo jugado"),
        ["run.best"] = ("best: level {0} · {1}", "mejor: nivel {0} · {1}"),
        ["run.avg"] = ("{0} per level", "{0} por nivel"),
        ["last"] = ("Last", "Último"),
        ["run.none"] = ("The run starts with the first level.", "La partida empieza con el primer nivel."),

        ["badge.mvp"] = ("MVP", "MVP"),
        ["badge.butter"] = ("BUTTERFINGERS", "MANAZAS"),
        ["unknown"] = ("Environment", "Entorno"),
        ["unknown.hint"] = ("enemies, falls, explosions", "enemigos, caídas, explosiones"),
        ["destroyed"] = ("DESTROYED", "DESTRUIDO"),

        ["feed.break"] = ("{0} damaged {1}", "{0} dañó {1}"),
        ["feed.destroy"] = ("{0} destroyed {1}", "{0} destruyó {1}"),
        ["feed.extract"] = ("{0} extracted", "{0} extraído"),
        ["feed.extract.n"] = ("{0} valuables extracted", "{0} objetos extraídos"),
        ["feed.fall"] = ("{0} fell off the map", "{0} se cayó del mapa"),
        ["feed.fall.by"] = ("{0} dropped {1} into the void", "{0} tiró {1} al vacío"),
        ["cause.hazard"] = ("A trap", "Una trampa"),
        ["feed.death"] = ("{0} died", "{0} murió"),
        ["feed.death.by"] = ("{0} was killed by {1}", "{1} mató a {0}"),

        ["hud.left"] = ("left", "restante"),
        ["page"] = ("{0}/{1}", "{0}/{1}"),
        ["more"] = ("+{0} more", "+{0} más"),
    };

    public static bool Spanish
    {
        get
        {
            switch (Settings.Language.Value)
            {
                case UiLanguage.English: return false;
                case UiLanguage.Spanish: return true;
            }
            try
            {
                var locale = LocalizationSettings.SelectedLocale;
                return locale != null && locale.Identifier.Code.StartsWith("es", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var pair)) return key;
        return Spanish ? pair.es : pair.en;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);
}
