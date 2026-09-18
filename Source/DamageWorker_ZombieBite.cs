using System;
using RimWorld;
using Verse;

namespace Zombiefied
{
    public class DamageWorker_ZombieBite : DamageWorker_AddInjury
    {
        protected override BodyPartRecord ChooseHitPart(DamageInfo dinfo, Pawn pawn)
        {
            return GetRandomNotMissingNotTorsoPart(dinfo, pawn, 0);
        }

        private BodyPartRecord GetRandomNotMissingNotTorsoPart(DamageInfo dinfo, Pawn pawn, int attempts)
        {
            BodyPartRecord part = pawn.health.hediffSet.GetRandomNotMissingPart(dinfo.Def, dinfo.Height, BodyPartDepth.Outside);
            if (attempts > 7 || pawn.health.Downed || !IsCentralBodyPart(part))
            {
                return part;
            }

            return GetRandomNotMissingNotTorsoPart(dinfo, pawn, attempts + 1);
        }

        protected override void ApplySpecialEffectsToPart(
            Pawn pawn,
            float totalDamage,
            DamageInfo dinfo,
            DamageWorker.DamageResult result)
        {
            bool partSkinnedOrNotSolid = !dinfo.HitPart.def.IsSolid(dinfo.HitPart, pawn.health.hediffSet.hediffs)
                || dinfo.HitPart.def.IsSkinCovered(dinfo.HitPart, pawn.health.hediffSet);

            base.FinalizeAndAddInjury(pawn, totalDamage, dinfo, result);

            HediffDef infection = ZombiefiedDefCache.ZombieWoundInfection;
            if (infection == null || !pawn.def.race.IsFlesh)
            {
                return;
            }

            if (!pawn.health.hediffSet.PartIsMissing(dinfo.HitPart))
            {
                if (partSkinnedOrNotSolid)
                {
                    pawn.health.AddHediff(infection, dinfo.HitPart, null);
                }
                return;
            }

            BodyPartRecord parent = dinfo.HitPart.parent;
            if (parent != null
                && !pawn.health.hediffSet.PartIsMissing(parent)
                && partSkinnedOrNotSolid
                && (!parent.def.IsSolid(parent, pawn.health.hediffSet.hediffs)
                    || parent.def.IsSkinCovered(parent, pawn.health.hediffSet)))
            {
                pawn.health.AddHediff(infection, parent, null);
            }
        }

        private static bool IsCentralBodyPart(BodyPartRecord part)
        {
            if (part == null || part.def == null || part.def.defName == null)
            {
                return false;
            }

            string name = part.def.defName;
            return name.IndexOf("TORSO", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("BODY", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("NECK", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("HEAD", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
