using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Zombiefied
{
    [StaticConstructorOnStartup]
    class ZombiefiedPatches
    {
        static ZombiefiedPatches()
        {
            var harmony = HarmonyInstance.Create("com.github.harmony.rimworld.mod.zombiefied");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    /*
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.HasNaturallyHealingInjury))]
    class NaturallyHealingPatch
    {
        static bool Prefix(ref bool __result, HediffSet __instance)
        {
            if (__instance.pawn is Pawn_Zombiefied)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
    */

    [HarmonyPatch(typeof(Pawn_MindState), nameof(Pawn_MindState.Notify_DamageTaken))]
    class ManhunterPatch
    {
        static bool Prefix(Pawn_MindState __instance, ref DamageInfo dinfo)
        {
            if (__instance.pawn.AnimalOrWildMan() && dinfo.Instigator is Pawn_Zombiefied)
            {
                __instance.mentalStateHandler.Notify_DamageTaken(dinfo);
                if (dinfo.Def.ExternalViolenceFor(__instance.pawn))
                {
                    __instance.lastHarmTick = Find.TickManager.TicksGame;
                    if (__instance.pawn.GetPosture() != PawnPosture.Standing)
                    {
                        __instance.lastDisturbanceTick = Find.TickManager.TicksGame;
                    }
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Fire), "DoFireDamage")]
    class FirePatch
    {
        static bool Prefix(Fire __instance, ref Thing targ)
        {
            if (targ is Pawn_Zombiefied)
            {
                return Rand.ChanceSeeded(0.5f, (Find.TickManager.TicksAbs + targ.thingIDNumber));
            }
            return true;
        }
    }
}