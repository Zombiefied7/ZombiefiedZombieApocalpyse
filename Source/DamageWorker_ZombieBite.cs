using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    // Token: 0x02000C41 RID: 3137
    public class DamageWorker_ZombieBite : DamageWorker_AddInjury
    {
        // Token: 0x06004219 RID: 16921 RVA: 0x001E310A File Offset: 0x001E150A
        protected override BodyPartRecord ChooseHitPart(DamageInfo dinfo, Pawn pawn)
        {
            return GetRandomNotMissingNotTorsoPart(dinfo, pawn, 0);
        }

        private BodyPartRecord GetRandomNotMissingNotTorsoPart(DamageInfo dinfo, Pawn pawn, int i)
        {
            BodyPartRecord temp = pawn.health.hediffSet.GetRandomNotMissingPart(dinfo.Def, dinfo.Height, BodyPartDepth.Outside);
            if(i > 3 || (!temp.def.defName.ToUpper().Contains("TORSO") && !temp.def.defName.ToUpper().Contains("BODY") && !temp.def.defName.ToUpper().Contains("NECK") && !temp.def.defName.ToUpper().Contains("HEAD")))
            {
                return temp;
            }
            return GetRandomNotMissingNotTorsoPart(dinfo, pawn, ++i);
        }

        // Token: 0x0600421A RID: 16922 RVA: 0x001E312B File Offset: 0x001E152B
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            base.FinalizeAndAddInjury(pawn, totalDamage, dinfo, result);

            if(!pawn.health.hediffSet.PartIsMissing(dinfo.HitPart))
            {
                if(pawn.def.race.IsFlesh && (!dinfo.HitPart.def.IsSolid(dinfo.HitPart, pawn.health.hediffSet.hediffs) || dinfo.HitPart.def.IsSkinCovered(dinfo.HitPart, pawn.health.hediffSet)))
                {
                    pawn.health.AddHediff(HediffDef.Named("ZombieWoundInfection"), dinfo.HitPart, null);
                }              
            }
            else if (!pawn.health.hediffSet.PartIsMissing(dinfo.HitPart.parent))
            {
                if (pawn.def.race.IsFlesh && (!dinfo.HitPart.parent.def.IsSolid(dinfo.HitPart.parent, pawn.health.hediffSet.hediffs) || dinfo.HitPart.parent.def.IsSkinCovered(dinfo.HitPart.parent, pawn.health.hediffSet)))
                {
                    pawn.health.AddHediff(HediffDef.Named("ZombieWoundInfection"), dinfo.HitPart.parent, null);
                }
            }
        }
    }
}