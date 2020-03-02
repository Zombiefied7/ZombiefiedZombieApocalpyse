using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Zombiefied
{
    // Token: 0x02000A34 RID: 2612
    public class JobGiver_WanderZombieHerd : JobGiver_Wander
    {
        //bool attracted = false;
        //bool fired = false;

        // Token: 0x0600377A RID: 14202 RVA: 0x0019834C File Offset: 0x0019674C
        public JobGiver_WanderZombieHerd()
        {
            this.maxDanger = Danger.Deadly;
            this.priority = 77777777777777f;
            this.wanderRadius = 6f;
            //this.ticksBetweenWandersRange = new IntRange(125, 200);
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is Pawn_Zombiefied))
            {
                return null;
            }
            bool nextMoveOrderIsWait = pawn.mindState.nextMoveOrderIsWait;
            pawn.mindState.nextMoveOrderIsWait = !pawn.mindState.nextMoveOrderIsWait;
            if (nextMoveOrderIsWait)
            {
                //pawn.mindState.canFleeIndividual = false;
                return new Job(JobDefOf.Wait)
                {
                    expiryInterval = 33
                };
            }

            if (!((Pawn_Zombiefied)pawn).attracted)
            {
                IntVec3 thing2 = ZombiefiedMod.BestNoisyLocation(pawn);
                if (thing2 != IntVec3.Invalid)
                {
                    ((Pawn_Zombiefied)pawn).attracted = true;

                    bool found = false;
                    using (PawnPath pawnPath = pawn.Map.pathFinder.FindPath(pawn.Position, thing2, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors, false), PathEndMode.OnCell))
                    {
                        if (pawnPath.Found)
                        {
                            found = true;
                            IntVec3 loc;
                            IntVec3 randomCell;

                            int iNodesReversed = 0;

                            if (pawnPath.TryFindLastCellBeforeBlockingDoor(pawn, out loc))
                            {
                                for (int i = 0; i < pawnPath.NodesReversed.Count; i++)
                                {
                                    if (pawnPath.NodesReversed[i].Equals(loc))
                                    {
                                        iNodesReversed = i;
                                    }
                                }
                            }

                            int wanderLength = Rand.RangeSeeded(10, 17, Find.TickManager.TicksAbs + pawn.thingIDNumber);
                            if (pawnPath.NodesReversed.Count - iNodesReversed > wanderLength)
                            {
                                randomCell = pawnPath.NodesReversed[pawnPath.NodesReversed.Count - wanderLength];

                                
                                return new Job(ZombiefiedMod.zombieMove, randomCell)
                                {
                                    //expiryInterval = 777
                                };
                            }
                        }
                    }
                    if (!found)
                    {
                        using (PawnPath pawnPath = pawn.Map.pathFinder.FindPath(pawn.Position, thing2, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings, false), PathEndMode.OnCell))
                        {
                            if (pawnPath.Found)
                            {
                                found = true;
                                IntVec3 loc;
                                IntVec3 randomCell;

                                int iNodesReversed = 0;

                                if (pawnPath.TryFindLastCellBeforeBlockingDoor(pawn, out loc))
                                {
                                    for (int i = 0; i < pawnPath.NodesReversed.Count; i++)
                                    {
                                        if (pawnPath.NodesReversed[i].Equals(loc))
                                        {
                                            iNodesReversed = i;
                                        }
                                    }
                                }

                                int wanderLength = Rand.RangeSeeded(10, 17, Find.TickManager.TicksAbs + pawn.thingIDNumber);
                                if (pawnPath.NodesReversed.Count - iNodesReversed > wanderLength)
                                {
                                    randomCell = pawnPath.NodesReversed[pawnPath.NodesReversed.Count - wanderLength];

                                    ((Pawn_Zombiefied)pawn).attracted = true;
                                    return new Job(ZombiefiedMod.zombieMove, randomCell)
                                    {
                                        //expiryInterval = 777
                                    };
                                }
                            }
                        }
                    }
                }
            }
            else
            {

                ((Pawn_Zombiefied)pawn).attracted = false;
                IntVec3 exactWanderDestAttracted = RCellFinder.RandomWanderDestFor(pawn, pawn.Position, 4, this.wanderDestValidator, Danger.Deadly);
                if (exactWanderDestAttracted.IsValid)
                {
                    Job jobAttracted = new Job(ZombiefiedMod.zombieMove, exactWanderDestAttracted);
                    //pawn.Map.pawnDestinationReservationManager.Reserve(pawn, jobAttracted, exactWanderDestAttracted);
                    jobAttracted.locomotionUrgency = this.locomotionUrgency;
                    return jobAttracted;
                }
            }

            if (!((Pawn_Zombiefied)pawn).fired)
            {
                if (!pawn.IsBurning() && pawn.Map.weatherManager.RainRate < 0.1f)
                {
                    foreach (Thing thing in pawn.Position.GetRegion(pawn.Map, RegionType.Set_Passable).ListerThings.AllThings)
                    {
                        //if (vec.ContainsStaticFire(pawn.Map))
                        if (thing is Fire && ((Fire)thing).fireSize > 1.37f && (pawn.Position - thing.Position).LengthHorizontal < 3.1f)
                        {
                            ((Pawn_Zombiefied)pawn).fired = true;
                            return new Job(ZombiefiedMod.zombieMove, thing.Position);
                        }
                    }
                }
            }
            else
            {
                pawn.TryAttachFire(0.37f);
                ((Pawn_Zombiefied)pawn).fired = false;
            }

            IntVec3 exactWanderDest = this.GetExactWanderDest(pawn);
            if (!exactWanderDest.IsValid)
            {
                pawn.mindState.nextMoveOrderIsWait = false;
                return null;
            }
            Job job = new Job(ZombiefiedMod.zombieMove, exactWanderDest);
            //pawn.Map.pawnDestinationReservationManager.Reserve(pawn, job, exactWanderDest);
            job.locomotionUrgency = this.locomotionUrgency;
            return job;
        }

        protected override IntVec3 GetExactWanderDest(Pawn pawn)
        {
            IntVec3 wanderRoot = this.GetWanderRoot(pawn);
            //pawn.Map.
            //wanderDestValidator = Validator;
            return RCellFinder.RandomWanderDestFor(pawn, wanderRoot, this.wanderRadius, this.wanderDestValidator, Danger.Deadly);
            //return RCellFinder.RandomWanderDestFor(pawn, wanderRoot, this.wanderRadius, Validator, Danger.Deadly);
            //return CellFinder.RandomClosewalkCellNear(wanderRoot, pawn.Map, (int)this.wanderRadius);
        }

        private bool Validator(Pawn pawn, IntVec3 vec0, IntVec3 vec1)
        {
            return vec0.ContainsStaticFire(pawn.Map) && !pawn.IsBurning();
        }

        // Token: 0x0600377B RID: 14203 RVA: 0x00198374 File Offset: 0x00196774
        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            Pawn pawn2 = BestPawnToWander(pawn as Pawn_Zombiefied);
            if (pawn2 != null)
            {
                return pawn2.Position;
            }
            return pawn.Position;
        }

        private Pawn BestPawnToWander(Pawn_Zombiefied pawn)
        {
            if (pawn.Map == null)
            {
                return null;
            }

            List<Pawn> allPawnsSpawned = pawn.Map.mapPawns.AllPawnsSpawned;
            //List<Thing> allThingsRegion = pawn.GetRegion().ListerThings.AllThings;

            Pawn pawnToReturn = null;
            float num = 0f;

            pawn.distanceToEdge = pawn.Position.DistanceToEdge(pawn.Map);
            bool closeToEdge = pawn.distanceToEdge < Rand.RangeSeeded(17, 57, Find.TickManager.TicksAbs + pawn.thingIDNumber);

            for (int i = 0; i < allPawnsSpawned.Count; i++)
            //for (int i = 0; i < allThingsRegion.Count; i++)
            {
                Pawn_Zombiefied pawn2 = allPawnsSpawned[i] as Pawn_Zombiefied;
                if (pawn2 != null && pawn != pawn2)
                {
                    float score = GetPreyScoreFor(pawn, pawn2);
                    if (score > -37f)
                    {
                        if (pawn2.CurJob != null && pawn2.CurJob.def.Equals(ZombiefiedMod.zombieHunt))
                        {
                            score += 27f;
                            pawn.hunting = true;
                        }
                        if (pawn2.hunting)
                        {
                            score += 23f;
                        }
                        if (closeToEdge && pawn.distanceToEdge < pawn2.distanceToEdge)
                        {
                            score += 3f;
                        }
                        if (Rand.RangeSeeded(0f, 1f, Find.TickManager.TicksAbs + pawn2.thingIDNumber) > 0.5f)
                        {
                            score += 7f;
                        }
                        float lengthHorizontal = (pawn.Position - pawn2.Position).LengthHorizontal;
                        score -= lengthHorizontal;

                        if (pawnToReturn == null || score > num)
                        {
                            pawnToReturn = pawn2;
                            num = score;
                        }
                    }
                }
            }

            return pawnToReturn;
        }

        public float GetPreyScoreFor(Pawn predator, Pawn prey)
        {
            float lengthHorizontal = (predator.Position - prey.Position).LengthHorizontal;
            return -lengthHorizontal;
        }
    }
}