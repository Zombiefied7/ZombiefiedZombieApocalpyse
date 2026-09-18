using HarmonyLib;
using Verse;

namespace Zombiefied
{
    // Verb.TryCastNextBurstShot is the common successful-shot gate for vanilla projectile verbs, including
    // pawn weapons and manned or autonomous turrets. Tracking a changed LastShotTick avoids scanning every
    // pawn, weapon, record tracker, and turret every few seconds just to discover that a shot happened.
    [HarmonyPatch(typeof(Verb), "TryCastNextBurstShot")]
    internal static class ZombieWeaponNoisePatch
    {
        static void Prefix(Verb __instance, out int __state)
        {
            __state = __instance != null ? __instance.LastShotTick : -1;
        }

        static void Postfix(Verb __instance, int __state)
        {
            if (__instance == null || __instance.LastShotTick == __state || __instance.verbProps == null || !__instance.verbProps.LaunchesProjectile)
            {
                return;
            }

            Thing caster = __instance.Caster;
            if (caster == null || !caster.Spawned || caster.Map == null)
            {
                return;
            }

            ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(caster.Map);
            tracker?.RegisterWeaponNoise(caster);
        }
    }

    [HarmonyPatch(typeof(Corpse), nameof(Corpse.SpawnSetup))]
    internal static class ZombieCorpseSchedulePatch
    {
        static void Postfix(Corpse __instance)
        {
            if (__instance == null || !__instance.Spawned || __instance.Map == null)
            {
                return;
            }

            ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(__instance.Map);
            tracker?.RegisterCorpseForReanimation(__instance);
        }
    }
}
