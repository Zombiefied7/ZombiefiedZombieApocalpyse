using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Zombiefied
{
    public class JobGiver_ZombieResponse : ThinkNode_JobGiver
    {
        private const float DefaultHuntRange = 7f;
        private static readonly IntVec3[] SearchOffsets = BuildSearchOffsets(DefaultHuntRange);

        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn_Zombiefied zombie = pawn as Pawn_Zombiefied;
            if (zombie == null || pawn.Map == null)
            {
                return null;
            }

            if (pawn.CurJob != null && pawn.CurJob.def == ZombiefiedMod.zombieHunt)
            {
                zombie.hunting = true;
                return null;
            }

            zombie.hunting = false;
            int now = GenTicks.TicksGame;
            if (now < zombie.nextCombatScanTick)
            {
                return null;
            }

            ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(pawn.Map);
            int interval = tracker != null ? tracker.RecommendedCombatScanInterval : 60;
            zombie.nextCombatScanTick = now + interval + Math.Abs(zombie.thingIDNumber % 17);

            Pawn target = BestPawnToHuntForPredator(pawn, DefaultHuntRange);
            if (target == null)
            {
                return null;
            }

            zombie.hunting = true;
            return new Job(ZombiefiedMod.zombieHunt, target)
            {
                killIncappedTarget = true,
                expiryInterval = (int)(Rand.RangeSeeded(1f, 2f, Find.TickManager.TicksAbs + zombie.thingIDNumber) * 700),
                attackDoorIfTargetLost = true
            };
        }

        public Pawn BestPawnToHuntForPredator(Pawn predator, float range = DefaultHuntRange)
        {
            if (predator == null || predator.Map == null)
            {
                return null;
            }

            float rangeSquared = range * range;
            IntVec3 origin = predator.Position;
            ThingGrid thingGrid = predator.Map.thingGrid;

            // Offsets are ordered by squared distance, so the first reachable acceptable pawn is also the
            // highest-scoring prey under the original distance-only scoring function. Reachability is only
            // evaluated for actual nearby prey instead of for every thing in the current region.
            for (int offsetIndex = 0; offsetIndex < SearchOffsets.Length; offsetIndex++)
            {
                IntVec3 cell = origin + SearchOffsets[offsetIndex];
                if (!cell.InBounds(predator.Map) || origin.DistanceToSquared(cell) > rangeSquared)
                {
                    continue;
                }

                List<Thing> things = thingGrid.ThingsListAtFast(cell);
                for (int thingIndex = 0; thingIndex < things.Count; thingIndex++)
                {
                    Pawn prey = things[thingIndex] as Pawn;
                    if (prey == null || prey == predator || !IsAcceptablePreyFor(predator, prey, range))
                    {
                        continue;
                    }

                    if (prey.IsForbidden(predator))
                    {
                        continue;
                    }

                    if (predator.CanReach(prey, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn))
                    {
                        return prey;
                    }
                }
            }

            return null;
        }

        public bool IsAcceptablePreyFor(Pawn predator, Pawn prey, float distance)
        {
            if (prey == null || prey.Dead || prey is Pawn_Zombiefied)
            {
                return false;
            }

            if (ZombiefiedMod.disableZombiesAttackingAnimals && !prey.RaceProps.Humanlike)
            {
                return false;
            }

            if (!prey.RaceProps.IsFlesh)
            {
                return false;
            }

            return predator.Position.DistanceToSquared(prey.Position) <= distance * distance;
        }

        public float GetPreyScoreFor(Pawn predator, Pawn prey)
        {
            return -(predator.Position - prey.Position).LengthHorizontal;
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
