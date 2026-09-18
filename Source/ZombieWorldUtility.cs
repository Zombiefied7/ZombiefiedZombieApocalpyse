using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public static class ZombieWorldUtility
    {
        private const int MaxEncounterZombies = 48;
        private const float FallbackZombieCombatPower = 55f;

        public static Faction GetZombieFaction()
        {
            if (Find.FactionManager == null)
            {
                return null;
            }

            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int i = 0; i < factions.Count; i++)
            {
                Faction faction = factions[i];
                if (faction != null && faction.def != null && faction.def.defName == "Zombie")
                {
                    return faction;
                }
            }

            return null;
        }

        public static List<Pawn> GenerateZombieEncounterPawns(float points)
        {
            List<Pawn> zombies = new List<Pawn>();
            PawnKindDef sourceKind = PawnKindDefOf.Colonist;
            if (sourceKind == null)
            {
                Log.Error("Zombiefied could not generate a world encounter because PawnKindDefOf.Colonist is unavailable.");
                return zombies;
            }

            float sourceCombatPower = sourceKind.combatPower;
            if (sourceCombatPower < 35f)
            {
                sourceCombatPower = FallbackZombieCombatPower;
            }

            float scaledPoints = Math.Max(35f, points) * ZombiefiedMod.ZombieRaidAmountMultiplier;
            int count = Mathf.Clamp((int)Math.Ceiling(scaledPoints / sourceCombatPower), 2, MaxEncounterZombies);
            Faction zombieFaction = GetZombieFaction();

            for (int i = 0; i < count; i++)
            {
                Pawn sourcePawn = null;
                try
                {
                    sourcePawn = PawnGenerator.GeneratePawn(sourceKind);
                    Pawn_Zombiefied zombie = ZombiefiedMod.GenerateZombieFromSource(sourcePawn);
                    if (zombie != null)
                    {
                        if (zombieFaction != null)
                        {
                            zombie.SetFactionDirect(zombieFaction);
                        }

                        if (zombie.apparel != null)
                        {
                            zombie.apparel.DestroyAll();
                        }

                        zombies.Add(zombie);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Zombiefied could not generate one of the zombies for a world encounter. " + ex);
                }
                finally
                {
                    if (sourcePawn != null && !sourcePawn.Destroyed)
                    {
                        sourcePawn.Destroy(DestroyMode.Vanish);
                    }
                }
            }

            return zombies;
        }
    }
}
