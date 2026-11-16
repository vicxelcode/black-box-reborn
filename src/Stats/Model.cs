using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlackBoxReborn.Stats;

/// <summary>Daño acumulado por un jugador sobre un mismo objeto.</summary>
internal sealed class ItemDamage
{
    public string ItemName;
    public float ValueLost;
    public bool Destroyed;
}

internal sealed class PlayerStats
{
    public const string UnknownId = "?";

    public string Id;
    public string Name;
    public int Order;

    public float ValueHauled;
    public float ItemsHauled;
    public float ValueBroken;
    public int Hits;
    public int ItemsDestroyed;
    public int DamageTaken;
    public int Deaths;

    /// <summary>Por id de instancia del objeto, para agrupar golpes al mismo jarrón.</summary>
    public readonly Dictionary<int, ItemDamage> Items = new();

    public bool IsUnknown => Id == UnknownId;

    public bool HasActivity =>
        ValueHauled > 0f || ValueBroken > 0f || Hits > 0 || DamageTaken > 0 || Deaths > 0;

    public IEnumerable<ItemDamage> ItemsByLoss => Items.Values.OrderByDescending(i => i.ValueLost);

    public void AddFrom(PlayerStats other)
    {
        ValueHauled += other.ValueHauled;
        ItemsHauled += other.ItemsHauled;
        ValueBroken += other.ValueBroken;
        Hits += other.Hits;
        ItemsDestroyed += other.ItemsDestroyed;
        DamageTaken += other.DamageTaken;
        Deaths += other.Deaths;
    }
}

internal enum FeedKind { Break, Destroy, Extract, Death, Fall }

/// <summary>Línea del registro de actividad. Se traduce al pintarla, no al crearla.</summary>
internal sealed class FeedEvent
{
    public FeedKind Kind;
    public string Who;
    public string What;
    public float Value;
    public int Count;
    public float Time;
}

internal sealed class LevelStats
{
    public int Number;
    public string LevelName;
    public float StartTime;
    public float EndTime = -1f;

    public float ValueSpawned;
    public int ItemsSpawned;
    public float ValueExtracted;
    public int ItemsExtracted;
    public float ValueLost;
    public int ItemsDestroyed;
    public int ItemsDiscovered;
    public int Hits;
    public int Deaths;

    /// <summary>Valor que sigue en el mapa; lo recalcula el rastreador mientras el nivel está abierto.</summary>
    public float ValueRemaining;

    public readonly Dictionary<string, PlayerStats> Players = new();
    public readonly List<FeedEvent> Feed = new();

    public bool Finished => EndTime >= 0f;

    public float Duration => (Finished ? EndTime : Time.time) - StartTime;

    public float ExtractedPercent => ValueSpawned > 0f ? Mathf.Clamp01(ValueExtracted / ValueSpawned) * 100f : 0f;

    public IEnumerable<PlayerStats> RankedPlayers =>
        Players.Values
            .Where(p => !p.IsUnknown || p.HasActivity)
            .OrderBy(p => p.IsUnknown)
            .ThenByDescending(p => p.ValueHauled)
            .ThenBy(p => p.Order);
}

internal sealed class RunStats
{
    public readonly List<LevelStats> Levels = new();

    public LevelStats Current => Levels.Count > 0 ? Levels[Levels.Count - 1] : null;

    public float ValueExtracted => Levels.Sum(l => l.ValueExtracted);
    public float ValueLost => Levels.Sum(l => l.ValueLost);
    public float Duration => Levels.Sum(l => l.Duration);

    /// <summary>Suma de todos los niveles por jugador.</summary>
    public List<PlayerStats> Totals()
    {
        var byId = new Dictionary<string, PlayerStats>();
        foreach (var level in Levels)
        {
            foreach (var p in level.Players.Values)
            {
                if (!byId.TryGetValue(p.Id, out var total))
                {
                    total = new PlayerStats { Id = p.Id, Name = p.Name, Order = p.Order };
                    byId[p.Id] = total;
                }
                total.Name = p.Name;
                total.AddFrom(p);
            }
        }
        return byId.Values
            .Where(p => !p.IsUnknown || p.HasActivity)
            .OrderBy(p => p.IsUnknown)
            .ThenByDescending(p => p.ValueHauled)
            .ThenBy(p => p.Order)
            .ToList();
    }
}
