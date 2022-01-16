using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld;

namespace Zombiefied
{
    public class IncidentWorker_ZombiePack : IncidentWorker
    {
        protected void ResolveRaidPoints(IncidentParms parms)
        {
            float factor = ZombiefiedMod.ZombieRaidAmountMultiplier;
            parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target) * PointsFactor * factor;
            if (parms.points > 3777f * ZombiefiedMod.ZombieRaidAmountMultiplier)
            {
                parms.points = 3777f * ZombiefiedMod.ZombieRaidAmountMultiplier;
            }
        }

        public bool TryFindAnimalKind(float points, int tile, out PawnKindDef animalKind)
        {
            return (from k in DefDatabase<PawnKindDef>.AllDefs
                    where  k.defName != null && k.defName.Contains("Zombie") && k.defName != "Zombie" && (tile == -1 || Find.World.tileTemperatures.SeasonAndOutdoorTemperatureAcceptableFor(tile, k.race))
                    select k).TryRandomElementByWeight((PawnKindDef k) => 7f, out animalKind);
        }
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Faction zFaction = Faction.OfInsects;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.def.defName == "Zombie")
                {
                    zFaction = faction;
                }
            }

            ResolveRaidPoints(parms);

            Map map = (Map)parms.target;
            PawnKindDef pawnKindDef;
            if (!TryFindAnimalKind(parms.points, map.Tile, out pawnKindDef))
            {
                return false;
            }       

            IntVec3 intVec;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out intVec, map, CellFinder.EdgeRoadChance_Animal))
            {
                return false;
            }
            Rot4 rot = Rot4.FromAngleFlat((map.Center - intVec).AngleFlat);

            Pawn reference = null;

            int num = (int)(parms.points / 100f);
            if(num < 2)
            {
                num = 2;
            }
            for (int i = 0; i < num; i++)
            {
                TryFindAnimalKind(parms.points, map.Tile, out pawnKindDef);
                if(pawnKindDef.RaceProps.baseBodySize > 4f && Rand.ChanceSeeded(0.5f, Find.TickManager.TicksAbs + i))
                {
                    TryFindAnimalKind(parms.points, map.Tile, out pawnKindDef);
                }

                Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, zFaction);
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(intVec, map, 10, null);
                pawn.apparel.DestroyAll();
                
                Pawn_Zombiefied zomb = (Pawn_Zombiefied)GenSpawn.Spawn(pawn, loc, map, rot, WipeMode.Vanish, false);
                if (zomb != null)
                {
                    zomb.FixZombie();
                }
                reference = pawn;
            }
            if (ZombiefiedMod.zombieRaidNotifications)
            {
                Find.LetterStack.ReceiveLetter("Zombies", "Some zombies walked into your territory. You might want to deal with them before they deal with you."
                , LetterDefOf.NeutralEvent, reference , null);
                Find.TickManager.slower.SignalForceNormalSpeedShort();
            }
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.ForbiddingDoors, OpportunityType.Critical);
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.AllowedAreas, OpportunityType.Important);
            return true;
        }

        private const float PointsFactor = 1.7f;
    }
}
