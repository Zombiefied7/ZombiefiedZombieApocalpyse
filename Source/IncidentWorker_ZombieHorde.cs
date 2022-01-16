using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;
namespace Zombiefied
{
    // Token: 0x02000313 RID: 787
    public class IncidentWorker_ZombieHorde : IncidentWorker
    {
        protected void ResolveRaidPoints(IncidentParms parms)
        {
            float factor = ZombiefiedMod.ZombieRaidAmountMultiplier;
            parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target) * PointsFactor * factor;
            if (parms.points > 3333f * ZombiefiedMod.ZombieRaidAmountMultiplier)
            {
                parms.points = 3333f * ZombiefiedMod.ZombieRaidAmountMultiplier;
            }
        }

        protected virtual bool FactionCanBeGroupSource(Faction f, Map map, bool desperate = true)
        {
            //bool result;
            if (f.IsPlayer)
            {
                return false;
            }
            if (!f.def.humanlikeFaction)
            {
                return false;
            }
            /*
            if (f.defeated)
            {
                return false;
            }
            if (!desperate)
            {
                if (!f.def.allowedArrivalTemperatureRange.Includes(map.mapTemperature.OutdoorTemp) || !f.def.allowedArrivalTemperatureRange.Includes(map.mapTemperature.SeasonalTemp))
                {
                    return false;
                }
            }
            */
            return true;
        }

        protected bool TryResolveRaidFaction(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            bool result;
            if (parms.faction != null)
            {
                result = true;
            }
            else
            {
                float num = parms.points;
                if (num <= 0f)
                {
                    num = 999999f;
                }
                result = (PawnGroupMakerUtility.TryGetRandomFactionForCombatPawnGroup(num, out parms.faction, (Faction f) => FactionCanBeGroupSource(f, map), true, true, true, true));
            }
            return result;
        }


        // Token: 0x06000D17 RID: 3351 RVA: 0x00061114 File Offset: 0x0005F514
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            ResolveRaidPoints(parms);

            if (!TryResolveRaidFaction(parms))
            {
                return false;
            }

            IntVec3 intVec;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out intVec, map, CellFinder.EdgeRoadChance_Animal))
            {
                return false;
            }

            PawnGroupKindDef combat = PawnGroupKindDefOf.Combat;
            PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(combat, parms, false);

            List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms, true).ToList<Pawn>();

            if (list.Count < 1)
            {
                Log.Error("Got no pawns spawning raid from parms " + parms);
                return false;
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Pawn pawn = list[i];
                    list[i] = ZombiefiedMod.GenerateZombieFromSource(list[i]);
                    pawn.Destroy(DestroyMode.Vanish);
                }
            }

            Rot4 rot = Rot4.FromAngleFlat((map.Center - intVec).AngleFlat);

            Faction zFaction = Faction.OfInsects;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.def.defName == "Zombie")
                {
                    zFaction = faction;
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                Pawn pawn = list[i];
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(intVec, map, 10, null);
                pawn.SetFactionDirect(zFaction);
                pawn.apparel.DestroyAll();
                Pawn_Zombiefied zomb = (Pawn_Zombiefied)GenSpawn.Spawn(pawn, loc, map, rot);
                if (zomb != null)
                {
                    zomb.FixZombie();
                }
            }

            if (ZombiefiedMod.zombieRaidNotifications)
            {
                Find.LetterStack.ReceiveLetter("Zombies", "Some zombies walked into your territory. You might want to deal with them before they deal with you."
                , LetterDefOf.NeutralEvent, list[0], null);
                Find.TickManager.slower.SignalForceNormalSpeedShort();
            }
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.ForbiddingDoors, OpportunityType.Critical);
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.AllowedAreas, OpportunityType.Important);
            return true;
        }

        // Token: 0x04000880 RID: 2176
        private const float PointsFactor = 3f;
    }
}
