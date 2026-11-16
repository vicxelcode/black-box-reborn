using System.Collections.Generic;
using UnityEngine;

namespace BlackBoxReborn.Stats;

internal enum CauseKind { None, Enemy, Hazard, PlayerItem, Fall }

/// <summary>Qué destruyó un objeto sin que nadie lo rompiera a golpes.</summary>
internal struct Cause
{
    public CauseKind Kind;
    public string Name;
    public PlayerAvatar Player;
}

/// <summary>
/// Averigua la causa de las destrucciones que no vienen de un golpe: trampas y ataques (HurtCollider,
/// que por defecto destruye cualquier objeto físico que toca), armas de los jugadores y caídas fuera del
/// mapa. Solo el host ve la llamada a DestroyObject; los clientes se quedan con la comprobación de altura.
/// </summary>
internal static class DestroyCauses
{
    /// <summary>Por debajo de esta altura el juego da el objeto por perdido y lo destruye.</summary>
    public const float FallHeight = -50f;

    /// <summary>HurtCollider cuyo bucle de comprobación se está ejecutando ahora mismo.</summary>
    public static HurtCollider ActiveHurt;

    private static readonly Dictionary<int, Cause> Pending = new();

    public static void Clear() => Pending.Clear();

    /// <summary>Llamado en el host justo antes de que el juego destruya el objeto.</summary>
    public static void Record(PhysGrabObjectImpactDetector detector)
    {
        if (!detector || !detector.valuableObject) return;
        Pending[detector.valuableObject.GetInstanceID()] = Resolve(detector.transform.position.y);
    }

    public static Cause Take(int valuableId, float lastY)
    {
        if (Pending.TryGetValue(valuableId, out var cause))
        {
            Pending.Remove(valuableId);
            return cause;
        }
        return lastY < FallHeight ? new Cause { Kind = CauseKind.Fall } : default;
    }

    private static Cause Resolve(float y)
    {
        var hurt = ActiveHurt;
        if (hurt)
        {
            if (hurt.enemyHost) return new Cause { Kind = CauseKind.Enemy, Name = Tracker.EnemyName(hurt.enemyHost) };
            var item = hurt.GetComponentInParent<ItemAttributes>();
            if (item)
            {
                // Granadas, minas y demás: la culpa es de quien lo lanzó o lo puso, si el juego aún lo recuerda.
                var physGrabObject = item.GetComponent<PhysGrabObject>();
                return new Cause
                {
                    Kind = CauseKind.PlayerItem,
                    Name = item.item ? item.item.itemName : item.name,
                    Player = physGrabObject ? physGrabObject.lastPlayerGrabbing : null,
                };
            }
            return new Cause { Kind = CauseKind.Hazard, Name = Loc.T("cause.hazard") };
        }
        return y < FallHeight ? new Cause { Kind = CauseKind.Fall } : default;
    }
}
