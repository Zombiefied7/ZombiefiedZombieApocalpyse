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

    [HarmonyPatch(typeof(Pawn_MindState), nameof(Pawn_MindState.Notify_DamageTaken))]
    class ManhunterPatch
    {
        static bool Prefix(Pawn_MindState __instance, ref DamageInfo dinfo)
        {
            if (__instance.pawn.AnimalOrWildMan() && dinfo.Instigator is Pawn_Zombiefied)
            {
                __instance.mentalStateHandler.Notify_DamageTaken(dinfo);
                /*
                if (dinfo.Def.ExternalViolenceFor(__instance.pawn))
                {
                    __instance.lastHarmTick = Find.TickManager.TicksGame;
                    if (__instance.pawn.GetPosture() != PawnPosture.Standing)
                    {
                        __instance.lastDisturbanceTick = Find.TickManager.TicksGame;
                    }
                }
                */
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
    class StatPatch
    {
        static bool Prefix(ref Thing thing, ref StatDef stat, ref float __result)
        {
            Pawn_Zombiefied zomb = thing as Pawn_Zombiefied; 
            if (zomb != null)
            {
                if(stat.defName.Equals("ArmorRating_Sharp"))
                {
                    if(zomb.armorRating_Sharp > 0f)
                    {
                        __result = zomb.armorRating_Sharp;
                        return false;
                    }                    
                }

                if (stat.defName.Equals("ArmorRating_Blunt"))
                {
                    if (zomb.armorRating_Blunt > 0f)
                    {
                        __result = zomb.armorRating_Blunt;
                        return false;
                    }
                }

                if (stat.defName.Equals("ArmorRating_Heat"))
                {
                    if (zomb.armorRating_Heat > 0f)
                    {
                        __result = zomb.armorRating_Heat;
                        return false;
                    }
                }
            }
            return true;
        }
    }

    /*
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
    */
}