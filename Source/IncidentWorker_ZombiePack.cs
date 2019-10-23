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
    public class IncidentWorker_ZombiePack : IncidentWorker
    {
        protected void ResolveRaidPoints(IncidentParms parms)
        {
            float factor = ZombiefiedMod.ZombieRaidAmountMultiplier;
            parms.points = StorytellerUtility.DefaultThreatPointsNow(parms.target) * PointsFactor * factor;
            if (parms.points > 1333f * ZombiefiedMod.ZombieRaidAmountMultiplier)
            {
                parms.points = 1333f * ZombiefiedMod.ZombieRaidAmountMultiplier;
            }
        }

        public bool TryFindAnimalKind(float points, int tile, out PawnKindDef animalKind)
        {
            return (from k in DefDatabase<PawnKindDef>.AllDefs
                    where  k.defName != null && k.defName.Contains("Zombie") && k.defName != "Zombie" && (tile == -1 || Find.World.tileTemperatures.SeasonAndOutdoorTemperatureAcceptableFor(tile, k.race))
                    select k).TryRandomElementByWeight((PawnKindDef k) => 7f, out animalKind);
        }

        // Token: 0x06000D17 RID: 3351 RVA: 0x00061114 File Offset: 0x0005F514
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
            
            //List<Pawn> list = ManhunterPackIncidentUtility.GenerateAnimals(pawnKindDef, map.Tile, parms.points);
            Rot4 rot = Rot4.FromAngleFlat((map.Center - intVec).AngleFlat);

            Pawn reference = null;

            int num = (int)(parms.points / 10f);
            if(num < 2)
            {
                num = 2;
            }
            for (int i = 0; i < num; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(pawnKindDef, zFaction);
                IntVec3 loc = CellFinder.RandomClosewalkCellNear(intVec, map, 10, null);
                pawn.apparel.DestroyAll();
                //pawn.SetFactionDirect(zFaction);
                
                Pawn_Zombiefied zomb = (Pawn_Zombiefied)GenSpawn.Spawn(pawn, loc, map, rot, WipeMode.Vanish, false);
                if (zomb != null)
                {
                    zomb.RemoveApparel();
                }
                //pawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, null, false, false, null);
                //pawn.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Rand.Range(60000, 135000);
                reference = pawn;
            }
            if (ZombiefiedMod.zombieRaidNotifications)
            {
                Find.LetterStack.ReceiveLetter("Zombies", "Some Zombies walked into your territory. You might want to deal with them before they deal with you."
                , LetterDefOf.NeutralEvent, reference , null);
                Find.TickManager.slower.SignalForceNormalSpeedShort();
            }
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.ForbiddingDoors, OpportunityType.Critical);
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.AllowedAreas, OpportunityType.Important);
            return true;
        }

        private const float PointsFactor = 0.05f;
    }
}
