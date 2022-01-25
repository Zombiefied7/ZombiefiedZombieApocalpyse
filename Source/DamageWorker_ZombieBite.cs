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
        protected override BodyPartRecord ChooseHitPart(DamageInfo damageInfo, Pawn pawn)
        {
            String[] forbiddenBodyParts = { "TORSO", "BODY", "NECK", "HEAD" };
            BodyPartRecord bodyPart = new BodyPartRecord();
            for (int tries = 7; tries > 0; tries--)
            {
                bodyPart = pawn.health.hediffSet.GetRandomNotMissingPart(damageInfo.Def, damageInfo.Height, BodyPartDepth.Outside);
                String partName = bodyPart.def.defName.ToUpper();
                if (pawn.health.Downed || !forbiddenBodyParts.Contains(partName)) break;
            }
            return bodyPart;
        }

        private bool IsInfectable(BodyPartRecord bodyPart, HediffSet hediffSet)
        {
            BodyPartDef bodyPartDef = bodyPart.def;
            if (hediffSet.PartIsMissing(bodyPart)) return false;
            return (!bodyPartDef.IsSolid(bodyPart, hediffSet.hediffs) || bodyPartDef.IsSkinCovered(bodyPart, hediffSet));
        }

        // Token: 0x0600421A RID: 16922 RVA: 0x001E312B File Offset: 0x001E152B
        protected override void ApplySpecialEffectsToPart(Pawn pawn, float totalDamage, DamageInfo damageInfo, DamageWorker.DamageResult result)
        {
            base.FinalizeAndAddInjury(pawn, totalDamage, damageInfo, result);
            // Only infect on bites! Haven't seen an example of a Zombie scratch...
            if (damageInfo.Def.defName != "ZombieBite") return;
            // Can't infect a robot, or other non-fleshy things
            if (!pawn.def.race.IsFlesh) return;
            // Try infecting the hit part first, then the parent of it.
            foreach (BodyPartRecord bodyPart in new BodyPartRecord[] { damageInfo.HitPart, damageInfo.HitPart.parent })
            {
                if (IsInfectable(bodyPart, pawn.health.hediffSet))
                {
                    pawn.health.AddHediff(HediffDef.Named("ZombieWoundInfection"), bodyPart, null);
                    break;
                }
            }
        }
    }
}