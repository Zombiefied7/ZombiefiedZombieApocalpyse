using System;
using RimWorld;
using Verse;

namespace Zombiefied
{
    public class HediffGiver_Zombiefied : HediffGiver
    {
        public override bool OnHediffAdded(Pawn pawn, Hediff hediff)
        {
            if (hediff is Hediff_Injury && !pawn.health.hediffSet.PartIsMissing(hediff.Part.parent) && !pawn.health.hediffSet.PartIsMissing(hediff.Part))
            {
                Hediff_Injury injury = hediff as Hediff_Injury;

                //float oldChance = injury.def.chanceToCauseNoPain;
                //injury.def.chanceToCauseNoPain = 70000f;

                injury.PostMake();
                injury.ageTicks = 70000000;

                //injury.def.chanceToCauseNoPain = oldChance;
            }
            else if (hediff is Hediff_MissingPart && !pawn.health.hediffSet.PartIsMissing(hediff.Part.parent))
            {
                Hediff_MissingPart missing = hediff as Hediff_MissingPart;
                //float oldChance = missing.def.chanceToCauseNoPain;
                //missing.def.chanceToCauseNoPain = 70000f;

                missing.PostMake();
                missing.ageTicks = 70000000;

                //missing.def.chanceToCauseNoPain = oldChance;
            }
            if (pawn.health.Downed && !pawn.health.InPainShock)
            {
                DamageInfo info = new DamageInfo(DamageDefOf.Rotting, 17f, 0f, 0f, null, pawn.health.hediffSet.GetBrain());
                pawn.Kill(info);
            }

            return false;           
        }
    }
}
