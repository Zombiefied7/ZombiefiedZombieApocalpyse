using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;

namespace Zombiefied
{
    public class IncidentWorker_ZombieHorde : IncidentWorker
    {
        private const float PointsFactor = 3f;
        private const int MaxFallbackSourcePawns = 60;

        protected void ResolveRaidPoints(IncidentParms parms)
        {
            float factor = ZombiefiedMod.ZombieRaidAmountMultiplier;
            parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target) * PointsFactor * factor;
            if (parms.points > 3333f * ZombiefiedMod.ZombieRaidAmountMultiplier)
            {
                parms.points = 3333f * ZombiefiedMod.ZombieRaidAmountMultiplier;
            }
        }

        protected virtual bool FactionCanBeGroupSource(Faction faction, Map map, bool desperate = true)
        {
            if (faction == null || faction.IsPlayer)
            {
                return false;
            }

            if (!faction.def.humanlikeFaction)
            {
                return false;
            }

            return true;
        }

        protected bool TryResolveRaidFaction(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (parms.faction != null)
            {
                return true;
            }

            float points = parms.points;
            if (points <= 0f)
            {
                points = 999999f;
            }

            return PawnGroupMakerUtility.TryGetRandomFactionForCombatPawnGroup(
                points,
                out parms.faction,
                faction => FactionCanBeGroupSource(faction, map),
                true,
                true,
                true,
                true);
        }

        private static List<Pawn> GenerateFallbackSourcePawns(IncidentParms parms)
        {
            List<Pawn> pawns = new List<Pawn>();
            PawnKindDef sourceKind = PawnKindDefOf.Colonist;
            if (sourceKind == null)
            {
                Log.Error("Zombiefied could not generate a fallback zombie horde because PawnKindDefOf.Colonist is unavailable.");
                return pawns;
            }

            float combatPower = sourceKind.combatPower;
            if (combatPower < 35f)
            {
                combatPower = 35f;
            }

            int count = (int)Math.Ceiling(Math.Max(1f, parms.points) / combatPower);
            count = Math.Max(1, Math.Min(MaxFallbackSourcePawns, count));

            bool warnedFactionFallback = false;
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = null;

                try
                {
                    pawn = PawnGenerator.GeneratePawn(sourceKind, parms.faction);
                }
                catch (Exception ex)
                {
                    if (!warnedFactionFallback)
                    {
                        Log.Warning("Zombiefied could not generate fallback horde source pawns for faction "
                            + (parms.faction != null ? parms.faction.ToString() : "null")
                            + ". Retrying without a source faction. " + ex);
                        warnedFactionFallback = true;
                    }
                }

                if (pawn == null)
                {
                    try
                    {
                        pawn = PawnGenerator.GeneratePawn(sourceKind);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Zombiefied failed to generate a fallback horde source pawn. " + ex);
                        break;
                    }
                }

                if (pawn != null)
                {
                    pawns.Add(pawn);
                }
            }

            return pawns;
        }

        private static List<Pawn> ConvertSourcePawnsToZombies(List<Pawn> sourcePawns)
        {
            List<Pawn> zombies = new List<Pawn>(sourcePawns.Count);

            for (int i = 0; i < sourcePawns.Count; i++)
            {
                Pawn sourcePawn = sourcePawns[i];
                if (sourcePawn == null)
                {
                    continue;
                }

                Pawn_Zombiefied zombie = null;
                try
                {
                    zombie = ZombiefiedMod.GenerateZombieFromSource(sourcePawn);
                }
                catch (Exception ex)
                {
                    Log.Error("Zombiefied failed to convert raid source pawn " + sourcePawn + " into a zombie. " + ex);
                }

                if (zombie != null)
                {
                    zombies.Add(zombie);
                }

                if (!sourcePawn.Destroyed)
                {
                    sourcePawn.Destroy(DestroyMode.Vanish);
                }
            }

            return zombies;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            ResolveRaidPoints(parms);

            if (!TryResolveRaidFaction(parms))
            {
                return false;
            }

            IntVec3 entryCell;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out entryCell, map, CellFinder.EdgeRoadChance_Animal))
            {
                return false;
            }

            PawnGroupMakerParms groupParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(
                PawnGroupKindDefOf.Combat,
                parms,
                false);

            List<Pawn> sourcePawns;
            try
            {
                sourcePawns = PawnGroupMakerUtility.GeneratePawns(groupParms, true).ToList();
            }
            catch (Exception ex)
            {
                Log.Warning("Zombiefied raid pawn-group generation failed for " + parms
                    + ". Using generic human source pawns for the zombie horde instead. " + ex);
                sourcePawns = new List<Pawn>();
            }

            if (sourcePawns.Count == 0)
            {
                Log.Warning("Zombiefied raid pawn-group generation returned no pawns for " + parms
                    + ". Using generic human source pawns for the zombie horde instead.");
                sourcePawns = GenerateFallbackSourcePawns(parms);
            }

            if (sourcePawns.Count == 0)
            {
                Log.Error("Zombiefied could not create any source pawns for zombie horde parms " + parms + ".");
                return false;
            }

            List<Pawn> zombies = ConvertSourcePawnsToZombies(sourcePawns);
            if (zombies.Count == 0)
            {
                Log.Error("Zombiefied created source pawns but could not convert any of them into zombies for parms " + parms + ".");
                return false;
            }

            Rot4 rotation = Rot4.FromAngleFlat((map.Center - entryCell).AngleFlat);

            Faction zombieFaction = Faction.OfInsects;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.def.defName == "Zombie")
                {
                    zombieFaction = faction;
                    break;
                }
            }

            Pawn firstSpawnedZombie = null;
            for (int i = 0; i < zombies.Count; i++)
            {
                Pawn pawn = zombies[i];
                IntVec3 location = CellFinder.RandomClosewalkCellNear(entryCell, map, 10, null);

                pawn.SetFactionDirect(zombieFaction);
                if (pawn.apparel != null)
                {
                    pawn.apparel.DestroyAll();
                }

                Pawn_Zombiefied zombie = GenSpawn.Spawn(pawn, location, map, rotation) as Pawn_Zombiefied;
                if (zombie != null)
                {
                    zombie.FixZombie();
                    if (firstSpawnedZombie == null)
                    {
                        firstSpawnedZombie = zombie;
                    }
                }
            }

            if (firstSpawnedZombie == null)
            {
                Log.Error("Zombiefied generated a zombie horde but failed to spawn any zombie pawns on " + map + ".");
                return false;
            }

            if (ZombiefiedMod.zombieRaidNotifications)
            {
                Find.LetterStack.ReceiveLetter(
                    "Zombies",
                    "Some zombies walked into your territory. You might want to deal with them before they deal with you.",
                    LetterDefOf.NeutralEvent,
                    firstSpawnedZombie,
                    null);
                Find.TickManager.slower.SignalForceNormalSpeedShort();
            }

            LessonAutoActivator.TeachOpportunity(ConceptDefOf.ForbiddingDoors, OpportunityType.Critical);
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.AllowedAreas, OpportunityType.Important);
            return true;
        }
    }
}
