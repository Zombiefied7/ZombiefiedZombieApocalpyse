using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace Zombiefied
{
    [HarmonyPatch(typeof(Caravan), nameof(Caravan.GetInspectString))]
    public static class Caravan_GetInspectString_ZombieInfestationPatch
    {
        public static void Postfix(Caravan __instance, ref string __result)
        {
            if (__instance == null || !__instance.IsPlayerControlled)
            {
                return;
            }

            WorldComponent_ZombieApocalypse component = ZombieWorldUtility.WorldComponent;
            if (component == null)
            {
                return;
            }

            float severity = component.GetInfestation(__instance.Tile);
            if (severity < 0.05f)
            {
                return;
            }

            if (!__result.NullOrEmpty())
            {
                __result += "\n";
            }

            __result += "Zombie activity: " + ZombieWorldUtility.ThreatLabel(severity)
                + " (" + severity.ToStringPercent("F0") + ")";
        }
    }

    [HarmonyPatch(typeof(WorldInspectPane), "get_TileInspectString")]
    public static class WorldInspectPane_TileInspectString_ZombieInfestationPatch
    {
        public static void Postfix(ref string __result)
        {
            if (Find.WorldSelector == null)
            {
                return;
            }

            PlanetTile tile = Find.WorldSelector.SelectedTile;
            if (!tile.Valid)
            {
                return;
            }

            WorldComponent_ZombieApocalypse component = ZombieWorldUtility.WorldComponent;
            if (component == null)
            {
                return;
            }

            float severity = component.GetInfestation(tile);
            if (severity < 0.05f)
            {
                return;
            }

            if (!__result.NullOrEmpty())
            {
                __result += "\n";
            }

            __result += "Zombie pressure: " + ZombieWorldUtility.ThreatLabel(severity)
                + " (" + severity.ToStringPercent("F0") + ")";
        }
    }
}
