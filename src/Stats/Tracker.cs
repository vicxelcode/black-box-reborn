using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BlackBoxReborn.Stats;

/// <summary>
/// Lleva la cuenta de la partida. Todo se calcula en local con información que el juego ya sincroniza
/// con cada cliente (golpes, agarres, destrucciones, salud), así que no hace falta red ni que el host
/// tenga el mod.
/// </summary>
internal static class Tracker
{
    private sealed class TrackedValuable
    {
        public ValuableObject Obj;
        public int Id;
        public string Name;
        public bool Surplus;
        public bool Destroyed;
        public bool Absorbed;
        public bool Done;
        public float AbsorbedValue;
        public float Original;
        public readonly Dictionary<string, float> Carry = new();
        public Seen Last;
    }

    private sealed class TrackedBox
    {
        public ItemValuableBox Box;
        public readonly List<TrackedValuable> Contents = new();
        public readonly Dictionary<string, float> Carry = new();
        public Seen Last;
    }

    /// <summary>
    /// Lo último que se vio de un objeto vivo. El juego lo borra por tres caminos distintos
    /// (Object.Destroy, PhotonNetwork.Destroy o una RPC) y no todos se pueden enganchar, así que se
    /// decide qué le pasó cuando desaparece, con estos datos.
    /// </summary>
    private struct Seen
    {
        public float Value;
        public bool Dead;
        public float Y;
        /// <summary>Quien lo toca ahora o lo soltó hace menos de 3 s (para culpar de una destrucción).</summary>
        public string Toucher;
        /// <summary>El último que lo tocó alguna vez (para dar el mérito de una extracción sin acarreo).</summary>
        public string Ever;
        public float EverTime;
    }

    private const float DestroyThreshold = 0.15f;
    private const float CartCarryWeight = 0.5f;
    private const int FeedSize = 8;

    /// <summary>Segundos tras generarse el nivel en los que una caída del mapa es un fallo de generación.</summary>
    private const float SpawnGrace = 30f;

    public static RunStats Run { get; private set; } = new();

    /// <summary>Sube con cada cambio para que la interfaz sepa cuándo redibujar.</summary>
    public static int Revision { get; private set; }

    /// <summary>Nivel en curso, o null si no se está jugando uno.</summary>
    public static LevelStats OpenLevel => Run.Current is { Finished: false } level ? level : null;

    private static readonly Dictionary<int, TrackedValuable> Valuables = new();
    private static readonly Dictionary<int, TrackedBox> Boxes = new();
    private static readonly Dictionary<string, int> LastHealth = new();
    private static readonly Dictionary<string, string> Names = new();
    private static readonly Dictionary<string, int> Orders = new();
    private static LevelGenerator levelGenerator;
    private static LevelGenerator staleGenerator;
    private static string saveFile;
    private static bool startTimeFixed;
    private static float nextHealthPoll;
    private static float nextRemainingCalc;
    private static float nextRosterSync;

    // ───────────────────────── Ciclo de vida ─────────────────────────

    public static void Tick()
    {
        if (!SemiFunc.RunIsLevel())
        {
            // Al pasar a un nivel, levelCurrent cambia antes de que cargue la escena: hasta entonces
            // LevelGenerator.Instance sigue siendo el de la escena anterior y no debe abrir un nivel.
            staleGenerator = LevelGenerator.Instance;
            CloseLevel();
            return;
        }

        var level = EnsureLevel();
        if (level == null) return;

        if (!startTimeFixed && levelGenerator && levelGenerator.Generated)
        {
            // El cronómetro arranca cuando el nivel termina de generarse, no durante la pantalla de carga.
            level.StartTime = Time.time;
            startTimeFixed = true;
        }

        Observe(level, Time.deltaTime);

        if (Time.time >= nextRosterSync)
        {
            nextRosterSync = Time.time + 1f;
            SyncRoster(level);
        }
        if (Time.time >= nextHealthPoll)
        {
            nextHealthPoll = Time.time + 0.2f;
            PollHealth(level);
        }
        if (Time.time >= nextRemainingCalc)
        {
            nextRemainingCalc = Time.time + 0.25f;
            var remaining = ComputeRemaining();
            if (!Mathf.Approximately(remaining, level.ValueRemaining))
            {
                level.ValueRemaining = remaining;
                Revision++;
            }
        }
    }

    private static LevelStats EnsureLevel()
    {
        var generator = LevelGenerator.Instance;
        if (!generator || !RunManager.instance || generator == staleGenerator) return null;
        if (generator == levelGenerator) return OpenLevel;

        CloseLevel();

        var currentSave = StatsManager.instance ? StatsManager.instance.saveFileCurrent : null;
        var number = RunManager.instance.levelsCompleted + 1;
        if (Run.Current != null && (number <= Run.Current.Number || currentSave != saveFile))
        {
            Run = new RunStats();
            Orders.Clear();
        }
        saveFile = currentSave;

        levelGenerator = generator;
        startTimeFixed = false;
        DestroyCauses.Clear();
        Valuables.Clear();
        Boxes.Clear();
        LastHealth.Clear();

        var level = new LevelStats
        {
            Number = number,
            LevelName = LevelName(RunManager.instance.levelCurrent),
            StartTime = Time.time,
        };
        Run.Levels.Add(level);
        SyncRoster(level);
        Revision++;
        Plugin.Log.LogInfo($"Nivel {level.Number} ({level.LevelName}) empieza");
        return level;
    }

    private static void CloseLevel()
    {
        var level = OpenLevel;
        levelGenerator = null;
        if (level == null) return;

        level.EndTime = Time.time;
        Valuables.Clear();
        Boxes.Clear();
        LastHealth.Clear();
        Revision++;
        Plugin.Log.LogInfo(
            $"Nivel {level.Number} terminado: extraído {level.ValueExtracted:0} de {level.ValueSpawned:0}, " +
            $"perdido {level.ValueLost:0} ({level.ItemsDestroyed} destruidos), restante {level.ValueRemaining:0}; " +
            $"cuadre {level.ValueExtracted + level.ValueLost + level.ValueRemaining - level.ValueSpawned:+0;-0;0}");
    }

    // ───────────────────────── Eventos del juego ─────────────────────────

    public static void OnValuableSpawned(ValuableObject valuable)
    {
        if (!valuable || !SemiFunc.RunIsLevel() || !valuable.gameObject.scene.IsValid()) return;
        var level = EnsureLevel();
        if (level == null) return;
        Track(level, valuable);
    }

    public static void OnDiscovered(ValuableObject valuable)
    {
        if (!valuable || valuable.discovered) return;
        var level = OpenLevel;
        if (level == null || !Valuables.ContainsKey(valuable.GetInstanceID())) return;
        level.ItemsDiscovered++;
        Revision++;
    }

    /// <summary>Un golpe que resta valor. Se llama antes de que el juego aplique la pérdida.</summary>
    public static void OnBreak(PhysGrabObjectImpactDetector detector, float valueLost)
    {
        var valuable = detector ? detector.valuableObject : null;
        if (!valuable || !SemiFunc.RunIsLevel()) return;
        var level = EnsureLevel();
        if (level == null) return;
        var tracked = Track(level, valuable);
        if (tracked.Done || tracked.Destroyed) return;

        var before = valuable.dollarValueCurrent;
        var destroyed = before - valueLost < valuable.dollarValueOriginal * DestroyThreshold && !SemiFunc.RunIsTutorial();
        var lost = destroyed ? before : Mathf.Min(valueLost, before);
        if (lost <= 0f) return;

        ApplyLoss(level, tracked, lost, destroyed, BreakCulprits(level, tracked, detector.physGrabObject));
    }

    /// <summary>Destrucción sin golpe previo que la explique (explosiones, efectos de enemigos).</summary>
    public static void OnDestroyedByImpactDetector(PhysGrabObjectImpactDetector detector)
    {
        var valuable = detector ? detector.valuableObject : null;
        var level = OpenLevel;
        if (!valuable || level == null) return;
        if (!Valuables.TryGetValue(valuable.GetInstanceID(), out var tracked)) return;
        if (tracked.Done || tracked.Destroyed || tracked.Absorbed) return;
        var cause = DestroyCauses.Take(tracked.Id, detector.transform.position.y);
        CausedDestroy(level, tracked, valuable.dollarValueCurrent, cause, Culprits(level, detector.physGrabObject));
    }

    /// <summary>
    /// Destrucción que no viene de un golpe. La culpa es de la causa (un enemigo, una trampa, el arma de
    /// un jugador) y no de quien lo sujetaba; si se cayó del mapa, de quien lo tocó el último.
    /// </summary>
    private static void CausedDestroy(LevelStats level, TrackedValuable tracked, float value, Cause cause,
        List<PlayerStats> touchers)
    {
        var nobody = touchers.TrueForAll(p => p.IsUnknown);
        var environment = new List<PlayerStats> { Player(level, PlayerStats.UnknownId) };
        Plugin.Log.LogInfo($"{tracked.Name} (${value:0}) destruido sin golpe: causa={cause.Kind} {cause.Name} " +
                           $"a los {Time.time - level.StartTime:0.0} s");
        switch (cause.Kind)
        {
            case CauseKind.Fall or CauseKind.Hazard when nobody && (!startTimeFixed || Time.time - level.StartTime < SpawnGrace):
                // Se cayó del mapa o lo rompió una trampa al generarse el nivel, sin nadie cerca: es cosa del
                // mapa, no una pérdida del equipo.
                Unspawn(level, tracked);
                return;
            case CauseKind.Fall:
                ApplyLoss(level, tracked, value, true, nobody ? environment : touchers, kind: FeedKind.Fall);
                return;
            case CauseKind.PlayerItem when Ready(cause.Player):
                ApplyLoss(level, tracked, value, true, new List<PlayerStats> { Player(level, cause.Player) },
                    who: $"{cause.Player.playerName} ({cause.Name})");
                return;
            case CauseKind.Enemy:
            case CauseKind.Hazard:
            case CauseKind.PlayerItem:
                ApplyLoss(level, tracked, value, true, environment, who: cause.Name);
                return;
            default:
                ApplyLoss(level, tracked, value, true, touchers);
                return;
        }
    }

    private static void Unspawn(LevelStats level, TrackedValuable tracked)
    {
        tracked.Done = true;
        if (tracked.Surplus) return;
        level.ValueSpawned -= tracked.Original;
        level.ItemsSpawned--;
        if (tracked.Obj && tracked.Obj.discovered) level.ItemsDiscovered--;
        Revision++;
        Plugin.Log.LogInfo($"{tracked.Name} se perdió al generarse el nivel: se descuenta del botín");
    }

    /// <summary>Una caja de botín se traga un objeto: su valor pasa a la caja.</summary>
    public static void OnAbsorbed(ItemValuableBox box, PhysGrabObject target)
    {
        var level = OpenLevel;
        if (level == null || !box || !target) return;
        var valuable = target.GetComponent<ValuableObject>();
        if (!valuable || !Valuables.TryGetValue(valuable.GetInstanceID(), out var tracked)) return;
        if (tracked.Done || tracked.Absorbed) return;

        tracked.Absorbed = true;
        tracked.AbsorbedValue = valuable.dollarValueCurrent;
        var id = box.GetInstanceID();
        if (!Boxes.TryGetValue(id, out var trackedBox))
        {
            trackedBox = new TrackedBox { Box = box };
            Boxes[id] = trackedBox;
        }
        trackedBox.Contents.Add(tracked);
        Revision++;
    }

    /// <summary>
    /// Un objeto ha desaparecido. Si no se rompió ni se lo tragó una caja de botín, es que se ha extraído:
    /// es lo único que borra objetos intactos en mitad de un nivel.
    /// </summary>
    private static void Vanished(LevelStats level, TrackedValuable tracked)
    {
        tracked.Done = true;
        if (tracked.Destroyed || tracked.Absorbed) return;
        if (tracked.Last.Dead)
        {
            CausedDestroy(level, tracked, tracked.Last.Value, DestroyCauses.Take(tracked.Id, tracked.Last.Y),
                Culprits(level, tracked.Last.Toucher));
            return;
        }
        Extract(level, tracked, tracked.Last.Value, tracked.Carry, tracked.Last.Ever);
    }

    private static void BoxVanished(LevelStats level, TrackedBox box)
    {
        var absorbed = box.Contents.Sum(t => t.AbsorbedValue);
        var factor = absorbed > 0f ? Mathf.Clamp01(box.Last.Value / absorbed) : 1f;
        foreach (var content in box.Contents)
        {
            var value = content.AbsorbedValue * factor;
            if (box.Last.Dead)
            {
                content.Absorbed = false;
                ApplyLoss(level, content, value, true, Culprits(level, box.Last.Toucher));
                continue;
            }
            var carry = new Dictionary<string, float>(content.Carry);
            foreach (var pair in box.Carry)
            {
                carry.TryGetValue(pair.Key, out var t);
                carry[pair.Key] = t + pair.Value;
            }
            Extract(level, content, value, carry, box.Last.Ever);
        }
    }

    public static void OnPlayerDeath(PlayerAvatar avatar, int enemyIndex)
    {
        var level = OpenLevel;
        if (level == null || !avatar) return;
        var stats = Player(level, avatar);
        stats.Deaths++;
        level.Deaths++;

        string killer = null;
        try
        {
            var enemy = SemiFunc.EnemyGetFromIndex(enemyIndex);
            if (enemy) killer = EnemyName(enemy);
        }
        catch (Exception)
        {
            // Índice fuera de rango: muerte sin enemigo (caída, explosión, etc.)
        }
        AddFeed(level, FeedKind.Death, stats.Name, killer, 0f);
    }

    // ───────────────────────── Cálculo ─────────────────────────

    private static TrackedValuable Track(LevelStats level, ValuableObject valuable)
    {
        var id = valuable.GetInstanceID();
        if (Valuables.TryGetValue(id, out var tracked)) return tracked;

        tracked = new TrackedValuable
        {
            Obj = valuable,
            Id = id,
            Name = ValuableName(valuable),
            Surplus = valuable.name.IndexOf("surplus", StringComparison.OrdinalIgnoreCase) >= 0,
            Original = valuable.dollarValueOriginal,
        };
        tracked.Last.Value = valuable.dollarValueCurrent;
        Valuables[id] = tracked;

        // La bolsa de excedente devuelve valor que ya se contó al extraer: no suma al botín del mapa.
        if (!tracked.Surplus)
        {
            level.ValueSpawned += valuable.dollarValueOriginal;
            level.ItemsSpawned++;
            if (valuable.discovered) level.ItemsDiscovered++;
        }
        Revision++;
        return tracked;
    }

    private static void ApplyLoss(LevelStats level, TrackedValuable tracked, float lost, bool destroyed,
        List<PlayerStats> culprits, string who = null, FeedKind? kind = null)
    {
        if (lost <= 0f && !destroyed) return;
        level.ValueLost += lost;
        level.Hits++;
        if (destroyed)
        {
            tracked.Destroyed = true;
            level.ItemsDestroyed++;
        }

        var share = lost / culprits.Count;
        var names = new List<string>();
        foreach (var stats in culprits)
        {
            names.Add(stats.IsUnknown ? Loc.T("unknown") : stats.Name);
            stats.ValueBroken += share;
            stats.Hits++;
            if (destroyed) stats.ItemsDestroyed++;
            if (!stats.Items.TryGetValue(tracked.Id, out var item))
            {
                item = new ItemDamage { ItemName = tracked.Name };
                stats.Items[tracked.Id] = item;
            }
            item.ValueLost += share;
            item.Destroyed |= destroyed;
        }
        AddFeed(level, kind ?? (destroyed ? FeedKind.Destroy : FeedKind.Break), who ?? string.Join(" + ", names),
            tracked.Name, lost);
#if BBR_DEV
        Plugin.Log.LogInfo($"Pérdida: {tracked.Name} -{lost:0} destruido={destroyed} por {string.Join(" + ", names)} " +
                           $"a los {Time.time - level.StartTime:0.0} s");
#endif
    }

    private static void Extract(LevelStats level, TrackedValuable tracked, float value,
        Dictionary<string, float> carry, string lastToucher)
    {
        if (tracked.Surplus || value <= 0f) return;
        level.ValueExtracted += value;
        level.ItemsExtracted++;

        var total = carry.Values.Sum();
        if (total <= 0f)
        {
            // Nadie lo llevó en brazos (empujado, lanzado de lejos...): se atribuye al último que lo tocó.
            var stats = Player(level, lastToucher ?? PlayerStats.UnknownId);
            stats.ValueHauled += value;
            stats.ItemsHauled += 1f;
        }
        else
        {
            foreach (var pair in carry)
            {
                var stats = Player(level, pair.Key);
                var fraction = pair.Value / total;
                stats.ValueHauled += value * fraction;
                stats.ItemsHauled += fraction;
            }
        }
        AddFeed(level, FeedKind.Extract, null, tracked.Name, value);
    }

    private static void AddFeed(LevelStats level, FeedKind kind, string who, string what, float value)
    {
        var last = level.Feed.Count > 0 ? level.Feed[level.Feed.Count - 1] : null;
        // Una extracción saca muchos objetos a la vez: se agrupan en una sola línea.
        if (kind == FeedKind.Extract && last is { Kind: FeedKind.Extract } && Time.time - last.Time < 3f)
        {
            last.Count++;
            last.Value += value;
            last.Time = Time.time;
        }
        else
        {
            level.Feed.Add(new FeedEvent { Kind = kind, Who = who, What = what, Value = value, Count = 1, Time = Time.time });
            if (level.Feed.Count > FeedSize) level.Feed.RemoveAt(0);
        }
        Revision++;
    }

    private static List<PlayerStats> Culprits(LevelStats level, PhysGrabObject physGrabObject)
    {
        var culprits = new List<PlayerStats>();
        if (physGrabObject)
        {
            foreach (var grabber in physGrabObject.playerGrabbing)
            {
                if (!grabber || !Ready(grabber.playerAvatar)) continue;
                var stats = Player(level, grabber.playerAvatar);
                if (!culprits.Contains(stats)) culprits.Add(stats);
            }
            // El juego recuerda al último que lo soltó durante 3 s: cubre los lanzamientos.
            if (culprits.Count == 0 && Ready(physGrabObject.lastPlayerGrabbing))
                culprits.Add(Player(level, physGrabObject.lastPlayerGrabbing));
        }
        if (culprits.Count == 0) culprits.Add(Player(level, PlayerStats.UnknownId));
        return culprits;
    }

    private const float RecentTouchSeconds = 10f;
    private const float ContactDistance = 1.5f;

    /// <summary>
    /// Quién tiene la culpa de un golpe, de más a menos seguro: quien lo sujeta (o lo soltó hace menos de
    /// 3 s, lo que recuerda el juego), quien lo tocó hace menos de 10 s (lanzamientos largos, objetos que
    /// ruedan) y, si no, el jugador pegado a él (chocarse andando o darle con otro objeto en la mano).
    /// </summary>
    private static List<PlayerStats> BreakCulprits(LevelStats level, TrackedValuable tracked, PhysGrabObject physGrabObject)
    {
        var culprits = Culprits(level, physGrabObject);
        if (!culprits.TrueForAll(p => p.IsUnknown)) return culprits;

        if (tracked.Last.Ever != null && Time.time - tracked.Last.EverTime < RecentTouchSeconds)
            return Culprits(level, tracked.Last.Ever);

        if (physGrabObject && GameDirector.instance)
        {
            PlayerAvatar closest = null;
            var best = ContactDistance;
            foreach (var avatar in GameDirector.instance.PlayerList)
            {
                if (!Ready(avatar) || avatar.deadSet) continue;
                var d = Vector3.Distance(physGrabObject.centerPoint, avatar.transform.position + Vector3.up);
                if (d < best)
                {
                    best = d;
                    closest = avatar;
                }
            }
            if (closest) return new List<PlayerStats> { Player(level, closest) };
        }
        Plugin.Log.LogInfo($"Golpe sin culpable: {tracked.Name} agarrado={(physGrabObject ? physGrabObject.playerGrabbing.Count : -1)} " +
                           $"último={tracked.Last.Ever ?? "-"} hace {(tracked.Last.Ever != null ? Time.time - tracked.Last.EverTime : -1f):0.0} s " +
                           $"carrito={(physGrabObject && physGrabObject.impactDetector && physGrabObject.impactDetector.inCart)}");
        return culprits;
    }

    private static List<PlayerStats> Culprits(LevelStats level, string id) =>
        new() { Player(level, id ?? PlayerStats.UnknownId) };

    /// <summary>Quién está tocando el objeto ahora mismo (o lo soltó hace menos de 3 s).</summary>
    private static string Toucher(PhysGrabObject physGrabObject)
    {
        foreach (var grabber in physGrabObject.playerGrabbing)
        {
            if (grabber && Ready(grabber.playerAvatar)) return Remember(grabber.playerAvatar);
        }
        return Ready(physGrabObject.lastPlayerGrabbing) ? Remember(physGrabObject.lastPlayerGrabbing) : null;
    }

    private static string Remember(PlayerAvatar avatar)
    {
        var id = IdOf(avatar);
        if (!string.IsNullOrEmpty(avatar.playerName)) Names[id] = avatar.playerName;
        return id;
    }

    /// <summary>Repaso de cada fotograma: quién lleva qué y qué ha desaparecido.</summary>
    private static void Observe(LevelStats level, float dt)
    {
        // Con un generador distinto la escena se está descargando: lo que desaparece ahora no se ha extraído.
        var sceneAlive = levelGenerator && levelGenerator == LevelGenerator.Instance;
        foreach (var tracked in Valuables.Values)
        {
            if (tracked.Done || tracked.Absorbed) continue;
            if (!tracked.Obj)
            {
                if (sceneAlive) Vanished(level, tracked);
                else tracked.Done = true;
                continue;
            }
            var physGrabObject = tracked.Obj.physGrabObject;
            if (!physGrabObject) continue;
            TrackValueChanges(level, tracked);
            tracked.Last.Value = tracked.Obj.dollarValueCurrent;
            tracked.Last.Dead = physGrabObject.dead;
            tracked.Last.Y = physGrabObject.transform.position.y;
            tracked.Last.Toucher = Toucher(physGrabObject);
            if (tracked.Last.Toucher != null)
            {
                tracked.Last.Ever = tracked.Last.Toucher;
                tracked.Last.EverTime = Time.time;
            }
            AddCarry(tracked.Carry, physGrabObject, dt);
        }

        List<int> gone = null;
        foreach (var pair in Boxes)
        {
            var box = pair.Value;
            if (!box.Box)
            {
                if (sceneAlive) BoxVanished(level, box);
                (gone ??= new List<int>()).Add(pair.Key);
                continue;
            }
            var physGrabObject = box.Box.GetComponent<PhysGrabObject>();
            if (!physGrabObject) continue;
            box.Last.Value = box.Box.CurrentValue;
            box.Last.Dead = physGrabObject.dead;
            box.Last.Toucher = Toucher(physGrabObject);
            box.Last.Ever = box.Last.Toucher ?? box.Last.Ever;
            AddCarry(box.Carry, physGrabObject, dt);
        }
        if (gone != null) foreach (var id in gone) Boxes.Remove(id);
    }

    /// <summary>
    /// Objetos que suben de valor después de aparecer: el Cubo del Conocimiento fija un valor nuevo al
    /// terminar (y el juego sube el objetivo de botín en esa diferencia) y las curaciones devuelven valor
    /// perdido. Sin esto, lo extraído más lo perdido superaría lo generado.
    /// </summary>
    private static void TrackValueChanges(LevelStats level, TrackedValuable tracked)
    {
        var original = tracked.Obj.dollarValueOriginal;
        var current = tracked.Obj.dollarValueCurrent;
        if (original > tracked.Original + 0.5f)
        {
            if (!tracked.Surplus) level.ValueSpawned += original - tracked.Original;
            tracked.Original = original;
            Revision++;
        }
        else if (current > tracked.Last.Value + 0.5f && !tracked.Surplus)
        {
            level.ValueSpawned += current - tracked.Last.Value;
            Revision++;
        }
    }

    private static void AddCarry(Dictionary<string, float> carry, PhysGrabObject physGrabObject, float dt)
    {
        if (dt <= 0f) return;
        var grabbers = physGrabObject.playerGrabbing;
        var weight = 1f;
        if (grabbers.Count == 0)
        {
            // En un carrito, el mérito es de quien empuja el carrito.
            var detector = physGrabObject.impactDetector;
            if (!detector || !detector.inCart || !detector.currentCart) return;
            var cart = detector.currentCart.physGrabObject;
            if (!cart) return;
            grabbers = cart.playerGrabbing;
            weight = CartCarryWeight;
        }
        if (grabbers.Count == 0) return;

        var share = dt * weight / grabbers.Count;
        foreach (var grabber in grabbers)
        {
            if (!grabber || !Ready(grabber.playerAvatar)) continue;
            var id = IdOf(grabber.playerAvatar);
            carry.TryGetValue(id, out var t);
            carry[id] = t + share;
        }
    }

    private static void PollHealth(LevelStats level)
    {
        if (!GameDirector.instance) return;
        foreach (var avatar in GameDirector.instance.PlayerList)
        {
            if (!Ready(avatar) || !avatar.playerHealth) continue;
            var id = IdOf(avatar);
            var health = avatar.playerHealth.health;
            if (LastHealth.TryGetValue(id, out var previous) && health < previous)
            {
                Player(level, avatar).DamageTaken += previous - health;
                Revision++;
            }
            LastHealth[id] = health;
        }
    }

    private static void SyncRoster(LevelStats level)
    {
        if (!GameDirector.instance) return;
        foreach (var avatar in GameDirector.instance.PlayerList)
        {
            if (Ready(avatar)) Player(level, avatar);
        }
    }

    private static float ComputeRemaining()
    {
        var sum = 0f;
        foreach (var tracked in Valuables.Values)
        {
            // La bolsa de excedente devuelve valor ya contado como extraído: no es botín pendiente.
            if (!tracked.Done && !tracked.Absorbed && !tracked.Surplus && tracked.Obj) sum += tracked.Obj.dollarValueCurrent;
        }
        foreach (var box in Boxes.Values)
        {
            if (box.Box) sum += box.Box.CurrentValue;
        }
        return sum;
    }

    // ───────────────────────── Identidades y nombres ─────────────────────────

    /// <summary>Recién creado, el avatar aún no tiene nombre ni id de Steam: se ignora hasta entonces.</summary>
    private static bool Ready(PlayerAvatar avatar) =>
        avatar && (!string.IsNullOrEmpty(avatar.steamID) || !string.IsNullOrEmpty(avatar.playerName));

    private static string IdOf(PlayerAvatar avatar) =>
        !string.IsNullOrEmpty(avatar.steamID) ? avatar.steamID : "name:" + avatar.playerName;

    private static PlayerStats Player(LevelStats level, PlayerAvatar avatar)
    {
        if (!Ready(avatar)) return Player(level, PlayerStats.UnknownId);
        var id = IdOf(avatar);
        var name = string.IsNullOrEmpty(avatar.playerName) ? "?" : avatar.playerName;
        Names[id] = name;
        var stats = Player(level, id);
        stats.Name = name;
        return stats;
    }

    private static PlayerStats Player(LevelStats level, string id)
    {
        if (level.Players.TryGetValue(id, out var stats)) return stats;
        if (!Orders.TryGetValue(id, out var order))
        {
            order = id == PlayerStats.UnknownId ? 99 : Orders.Count(o => o.Key != PlayerStats.UnknownId);
            Orders[id] = order;
        }
        stats = new PlayerStats
        {
            Id = id,
            Name = id == PlayerStats.UnknownId ? null : Names.TryGetValue(id, out var n) ? n : "?",
            Order = order,
        };
        level.Players[id] = stats;
        return stats;
    }

    private static string LevelName(Level level)
    {
        if (!level) return "?";
        if (!string.IsNullOrEmpty(level.NarrativeName)) return level.NarrativeName;
        return level.name.Replace("Level - ", "").Trim();
    }

    internal static string EnemyName(Enemy enemy)
    {
        if (enemy.HasRigidbody && enemy.Rigidbody && enemy.Rigidbody.enemyParent)
            return enemy.Rigidbody.enemyParent.enemyName;
        if (enemy.EnemyParent) return enemy.EnemyParent.enemyName;
        return enemy.name;
    }

    internal static string ValuableName(ValuableObject valuable)
    {
        var name = valuable.name.Replace("(Clone)", "").Replace("Valuable", "").Trim();
        if (name.StartsWith("Enemy", StringComparison.OrdinalIgnoreCase))
        {
            // «Enemy Valuable - Small» → «Orbe Small»
            var dash = name.LastIndexOf('-');
            var size = dash >= 0 ? name.Substring(dash + 1).Trim() : "";
            return (Loc.Spanish ? "Orbe " : "Orb ") + size;
        }
        return name;
    }
}
