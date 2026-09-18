using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Zombiefied
{
    public class IncidentWorker_ZombieCaravanAmbush : IncidentWorker_Ambush
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return ZombiefiedMod.enableCaravanZombieEncounters && base.CanFireNowSub(parms);
        }

        protected override List<Pawn> GeneratePawns(IncidentParms parms)
        {
            return ZombieWorldUtility.GenerateZombieEncounterPawns(parms.points);
        }

        protected override void PostProcessGeneratedPawnsAfterSpawning(List<Pawn> generatedPawns)
        {
            base.PostProcessGeneratedPawnsAfterSpawning(generatedPawns);

            Faction zombieFaction = ZombieWorldUtility.GetZombieFaction();
            for (int i = 0; i < generatedPawns.Count; i++)
            {
                Pawn_Zombiefied zombie = generatedPawns[i] as Pawn_Zombiefied;
                if (zombie == null)
                {
                    continue;
                }

                if (zombieFaction != null && zombie.Faction != zombieFaction)
                {
                    zombie.SetFaction(zombieFaction);
                }

                zombie.FixZombie();
            }
        }

        protected override string GetLetterLabel(Pawn anyPawn, IncidentParms parms)
        {
            return "Zombie ambush";
        }

        protected override string GetLetterText(Pawn anyPawn, IncidentParms parms)
        {
            Caravan caravan = parms.target as Caravan;
            if (caravan == null)
            {
                return "A mass of zombies has closed in on your people.";
            }

            return "While travelling, " + caravan.LabelCap + " has run into a mass of wandering dead. The zombies are closing in.";
        }

        protected override LetterDef GetLetterDef(Pawn anyPawn, IncidentParms parms)
        {
            return LetterDefOf.ThreatBig;
        }
    }
}
