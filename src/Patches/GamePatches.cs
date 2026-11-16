using System;
using System.Reflection;
using BlackBoxReborn.Stats;
using HarmonyLib;
using Photon.Pun;

namespace BlackBoxReborn.Patches;

/// <summary>
/// Ganchos al juego. Todos son prefijos o sufijos que solo leen: si algo falla se registra y el juego
/// sigue como si el mod no estuviera.
/// </summary>
[HarmonyPatch]
internal static class GamePatches
{
    private static void Safe(Action action, string where)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{where}: {e}");
        }
    }

    // Host y un jugador: el valor se decide aquí.
    [HarmonyPostfix, HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DollarValueSetLogic))]
    private static void ValueSetLogic(ValuableObject __instance) =>
        Safe(() => Tracker.OnValuableSpawned(__instance), "DollarValueSetLogic");

    // Clientes: el host les manda el valor.
    [HarmonyPostfix, HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DollarValueSetRPC))]
    private static void ValueSetRpc(ValuableObject __instance) =>
        Safe(() => Tracker.OnValuableSpawned(__instance), "DollarValueSetRPC");

    [HarmonyPrefix, HarmonyPatch(typeof(ValuableObject), nameof(ValuableObject.DiscoverRPC))]
    private static void Discover(ValuableObject __instance) =>
        Safe(() => Tracker.OnDiscovered(__instance), "DiscoverRPC");

    [HarmonyPrefix, HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.BreakRPC))]
    private static void Break(PhysGrabObjectImpactDetector __instance, float valueLost, bool _loseValue,
        PhotonMessageInfo _info)
    {
        if (!_loseValue) return;
        Safe(() =>
        {
            if (SemiFunc.MasterOnlyRPC(_info)) Tracker.OnBreak(__instance, valueLost);
        }, "BreakRPC");
    }

    [HarmonyPrefix, HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.DestroyObjectRPC))]
    private static void DestroyObject(PhysGrabObjectImpactDetector __instance, PhotonMessageInfo _info) =>
        Safe(() =>
        {
            if (SemiFunc.MasterOnlyRPC(_info)) Tracker.OnDestroyedByImpactDetector(__instance);
        }, "DestroyObjectRPC");

    [HarmonyPrefix, HarmonyPatch(typeof(ItemValuableBox), nameof(ItemValuableBox.StartAbsorbLocal))]
    private static void Absorb(ItemValuableBox __instance, PhysGrabObject target) =>
        Safe(() => Tracker.OnAbsorbed(__instance, target), "StartAbsorbLocal");

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeathRPC))]
    private static void PlayerDeath(PlayerAvatar __instance, int enemyIndex, PhotonMessageInfo _info) =>
        Safe(() =>
        {
            if (SemiFunc.MasterAndOwnerOnlyRPC(_info, __instance.photonView) && !__instance.deadSet)
                Tracker.OnPlayerDeath(__instance, enemyIndex);
        }, "PlayerDeathRPC");
}

/// <summary>
/// HurtCollider comprueba lo que toca en una corrutina (ColliderCheck) y ahí destruye objetos. Se marca
/// cuál está activo mientras corre su MoveNext para saber qué trampa o enemigo destruyó cada objeto.
/// </summary>
[HarmonyPatch]
internal static class HurtColliderPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(HurtCollider), nameof(HurtCollider.ColliderCheck)));

    private static void Prefix(object __instance)
    {
        try
        {
            DestroyCauses.ActiveHurt = Traverse.Create(__instance).Field("<>4__this").GetValue<HurtCollider>();
        }
        catch (Exception)
        {
            DestroyCauses.ActiveHurt = null;
        }
    }

    private static Exception Finalizer(Exception __exception)
    {
        DestroyCauses.ActiveHurt = null;
        return __exception;
    }
}

[HarmonyPatch(typeof(PhysGrabObjectImpactDetector), nameof(PhysGrabObjectImpactDetector.DestroyObject))]
internal static class DestroyObjectCausePatch
{
    private static void Prefix(PhysGrabObjectImpactDetector __instance)
    {
        try
        {
            if (__instance.physGrabObject && !__instance.physGrabObject.dead) DestroyCauses.Record(__instance);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"DestroyObject: {e}");
        }
    }
}
