using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace Zombiefied
{
    // Token: 0x020000A4 RID: 164
    public class JobGiver_ZombieResponse : ThinkNode_JobGiver
    {
        // Token: 0x060003E6 RID: 998 RVA: 0x0002923C File Offset: 0x0002763C
        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn_Zombiefied predator = pawn as Pawn_Zombiefied;
            Pawn prey = BestPawnToHuntForPredator(predator);
            if (prey == null) return null;
            return new Job(ZombiefiedMod.zombieHunt, prey)
            {
                killIncappedTarget = true,
                expiryInterval = Rand.RangeSeeded(1, 2, Find.TickManager.TicksAbs) * 700,
                maxNumMeleeAttacks = Rand.Range(1, 4)
            };
        }

        public Pawn BestPawnToHuntForPredator(Pawn_Zombiefied predator, float range = 7f)
        {
            List<Thing> allThingsRegion = predator.Map.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.Pawn));

            Pawn pawnToReturn = null;
            float closest = 0f;
            foreach (Thing item in allThingsRegion)
            {
                Pawn prey = item as Pawn;

                // if prey is a not pawn or is the predator, skip.
                if (prey == null || predator == prey) continue;
                // if prey isn't acceptable, skip.
                if (!IsAcceptablePreyFor(predator, prey)) continue;
                // if prey is too far, skip.
                float distance = predator.Position.DistanceTo(prey.Position);
                if (distance > range) continue;
                if (!predator.CanReach(prey, false))
                {
                    // Chance to breanch, divided by distance
                    if(Rand.Chance((float)ZombiefiedMod.breachChance / 100f / distance))
                        distance *= 5;
                    else
                        continue;
                }
                if (distance < closest || pawnToReturn == null)
                {
                    closest = distance;
                    pawnToReturn = prey;
                }
            }
            return pawnToReturn;
        }

        public bool IsAcceptablePreyFor(Pawn predator, Pawn prey)
        {
            // If prey is zombie
            if (prey as Pawn_Zombiefied != null) return false;
            // If prey isn't human, and zombies can't attack animals
            if(ZombiefiedMod.disableZombiesAttackingAnimals && !prey.RaceProps.Humanlike) return false;
            // If prey isn't made of flesh
            if (!prey.RaceProps.IsFlesh) return false;
            // if prey is forbidden to predator, skip.
            if (prey.IsForbidden(predator)) return false;
            return true;
        }
    }
}
