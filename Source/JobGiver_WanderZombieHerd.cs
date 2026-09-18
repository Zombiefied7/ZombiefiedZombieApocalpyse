using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Zombiefied
{
    public class JobGiver_WanderZombieHerd : JobGiver_Wander
    {
        private const float FireInterestRadius = 3.1f;
        private static readonly IntVec3[] FireSearchOffsets = BuildSearchOffsets(FireInterestRadius);

        public JobGiver_WanderZombieHerd()
        {
            maxDanger = Danger.Deadly;
            priority = 77777777777777f;
            wanderRadius = 6f;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn_Zombiefied zombie = pawn as Pawn_Zombiefied;
            if (zombie == null || pawn.Map == null)
            {
                return null;
            }

            ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(pawn.Map);
            bool nextMoveOrderIsWait = pawn.mindState.nextMoveOrderIsWait;
            pawn.mindState.nextMoveOrderIsWait = !pawn.mindState.nextMoveOrderIsWait;
            if (nextMoveOrderIsWait)
            {
                return new Job(JobDefOf.Wait)
                {
                    expiryInterval = tracker != null ? tracker.RecommendedIdleWaitTicks : 120
                };
            }

            if (!zombie.attracted)
            {
                IntVec3 noisyLocation = tracker != null ? tracker.BestNoisyLocation() : ZombiefiedMod.BestNoisyLocation(pawn);
                if (noisyLocation.IsValid)
                {
                    zombie.attracted = true;
                    Job noiseJob = new Job(ZombiefiedMod.zombieMove, noisyLocation);
                    noiseJob.locomotionUrgency = locomotionUrgency;
                    return noiseJob;
                }
            }
            else
            {
                zombie.attracted = false;
                IntVec3 exactWanderDestAttracted = RCellFinder.RandomWanderDestFor(
                    pawn,
                    pawn.Position,
                    4,
                    wanderDestValidator,
                    Danger.Deadly);

                if (exactWanderDestAttracted.IsValid)
                {
                    Job jobAttracted = new Job(ZombiefiedMod.zombieMove, exactWanderDestAttracted);
                    jobAttracted.locomotionUrgency = locomotionUrgency;
                    return jobAttracted;
                }
            }

            if (!zombie.fired)
            {
                if (!pawn.IsBurning() && pawn.Map.weatherManager.RainRate < 0.1f)
                {
                    IntVec3 fireCell;
                    if (TryFindInterestingFire(pawn, out fireCell))
                    {
                        zombie.fired = true;
                        return new Job(ZombiefiedMod.zombieMove, fireCell);
                    }
                }
            }
            else
            {
                pawn.TryAttachFire(0.37f, pawn);
                zombie.fired = false;
            }

            IntVec3 exactWanderDest = GetExactWanderDest(pawn);
            if (!exactWanderDest.IsValid)
            {
                pawn.mindState.nextMoveOrderIsWait = false;
                return null;
            }

            Job job = new Job(ZombiefiedMod.zombieMove, exactWanderDest);
            job.locomotionUrgency = locomotionUrgency;
            return job;
        }

        protected override IntVec3 GetExactWanderDest(Pawn pawn)
        {
            IntVec3 wanderRoot = GetWanderRoot(pawn);
            return RCellFinder.RandomWanderDestFor(pawn, wanderRoot, wanderRadius, wanderDestValidator, Danger.Deadly);
        }

        protected override IntVec3 GetWanderRoot(Pawn pawn)
        {
            Pawn_Zombiefied zombie = pawn as Pawn_Zombiefied;
            if (zombie == null || pawn.Map == null)
            {
                return pawn.Position;
            }

            ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(pawn.Map);
            IntVec3 anchor;
            if (tracker != null && tracker.TryGetHerdAnchor(zombie, out anchor) && anchor.InBounds(pawn.Map))
            {
                return anchor;
            }

            return pawn.Position;
        }

        private static bool TryFindInterestingFire(Pawn pawn, out IntVec3 fireCell)
        {
            fireCell = IntVec3.Invalid;
            ThingGrid thingGrid = pawn.Map.thingGrid;
            IntVec3 origin = pawn.Position;

            for (int i = 0; i < FireSearchOffsets.Length; i++)
            {
                IntVec3 cell = origin + FireSearchOffsets[i];
                if (!cell.InBounds(pawn.Map))
                {
                    continue;
                }

                List<Thing> things = thingGrid.ThingsListAtFast(cell);
                for (int j = 0; j < things.Count; j++)
                {
                    Fire fire = things[j] as Fire;
                    if (fire != null && fire.fireSize > 1.37f)
                    {
                        fireCell = cell;
                        return true;
                    }
                }
            }

            return false;
        }

        private static IntVec3[] BuildSearchOffsets(float radius)
        {
            int ceiling = Mathf.CeilToInt(radius);
            float radiusSquared = radius * radius;
            List<IntVec3> offsets = new List<IntVec3>();

            for (int z = -ceiling; z <= ceiling; z++)
            {
                for (int x = -ceiling; x <= ceiling; x++)
                {
                    int distanceSquared = x * x + z * z;
                    if (distanceSquared <= radiusSquared)
                    {
                        offsets.Add(new IntVec3(x, 0, z));
                    }
                }
            }

            offsets.Sort(delegate(IntVec3 a, IntVec3 b)
            {
                int aSquared = a.x * a.x + a.z * a.z;
                int bSquared = b.x * b.x + b.z * b.z;
                return aSquared.CompareTo(bSquared);
            });

            return offsets.ToArray();
        }
    }
}
