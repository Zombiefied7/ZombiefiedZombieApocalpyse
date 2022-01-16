using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;

namespace Zombiefied
{
    public class JobGiver_ZombieResponse : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return this.TryGetAttackNearbyEnemyJob(pawn);
        }
        private Job TryGetAttackNearbyEnemyJob(Pawn pawn)
        {
            Pawn thing = BestPawnToHuntForPredator(pawn);
            if (thing != null)
            {
                return new Job(ZombiefiedMod.zombieHunt, thing)
                {
                    killIncappedTarget = true,
                    expiryInterval = (int)(Rand.RangeSeeded(1f, 2f, Find.TickManager.TicksAbs) * 700),
                    attackDoorIfTargetLost = true
                };
            }
            return null;
        }

        public Pawn BestPawnToHuntForPredator(Pawn predator, float range = 7f)
        {
            List<Thing> allThingsRegion = predator.GetRegion().ListerThings.AllThings;

            Pawn pawnToReturn = null;
            float num = 0f;

            for (int i = 0; i < allThingsRegion.Count; i++)
                {
                Pawn pawn2 = allThingsRegion[i] as Pawn;
                if (pawn2 != null && predator != pawn2)
                {
                    if (IsAcceptablePreyFor(predator, pawn2, range))
                    {
                        if (predator.CanReach(pawn2, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn))
                        {
                            if (!pawn2.IsForbidden(predator))
                            {
                                float preyScoreFor = GetPreyScoreFor(predator, pawn2);
                                if (preyScoreFor > num || pawnToReturn == null)
                                {
                                    num = preyScoreFor;
                                    pawnToReturn = pawn2;
                                }
                            }
                        }
                    }
                }
            }
            return pawnToReturn;
        }

        public bool IsAcceptablePreyFor(Pawn predator, Pawn prey, float distance)
        {
            Pawn_Zombiefied preyZ = prey as Pawn_Zombiefied;
            if (preyZ != null)
            {
                return false;
            }         
            if(ZombiefiedMod.disableZombiesAttackingAnimals && !prey.RaceProps.Humanlike)
            {
                return false;
            }
            if (!prey.RaceProps.IsFlesh)
            {
                return false;
            }
            float lengthHorizontal = -GetPreyScoreFor(predator, prey);
            if (lengthHorizontal > distance)
            {
                return false;
            }
            
            return true;
        }

        public float GetPreyScoreFor(Pawn predator, Pawn prey)
        {
            float lengthHorizontal = (predator.Position - prey.Position).LengthHorizontal;
            return -lengthHorizontal;
        }
    }
}
