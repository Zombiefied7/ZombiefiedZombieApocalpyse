using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class ZombieMapTracker : MapComponent
    {
        private const int SpatialBucketSize = 12;
        private const int SpatialRefreshTicks = 30;
        private const int HerdBucketSize = 24;
        private const int HerdRefreshTicks = 250;
        private const int NoiseMaintenanceTicks = 333;
        private const int MaximumRememberedNoises = 24;

        private sealed class HerdAccumulator
        {
            public int count;
            public int huntingCount;
            public IntVec3 representativePosition = IntVec3.Invalid;
            public IntVec3 huntingRepresentativePosition = IntVec3.Invalid;
        }

        private struct HerdAnchor
        {
            public IntVec3 position;
            public int count;
            public bool hunting;
        }

        private sealed class ScheduledReanimation
        {
            public Corpse corpse;
            public int dueTick;
        }

        private readonly HashSet<Pawn_Zombiefied> zombies = new HashSet<Pawn_Zombiefied>();
        private readonly Dictionary<int, List<Pawn_Zombiefied>> zombieBuckets = new Dictionary<int, List<Pawn_Zombiefied>>();
        private readonly Dictionary<int, HerdAccumulator> herdAccumulators = new Dictionary<int, HerdAccumulator>();
        private readonly List<HerdAnchor> herdAnchors = new List<HerdAnchor>();
        private readonly Dictionary<int, IntVec3> indexedZombiePositions = new Dictionary<int, IntVec3>();
        private readonly Queue<IntVec3> noisyLocations = new Queue<IntVec3>();
        private readonly Queue<int> noisyLocationTicks = new Queue<int>();
        private readonly List<ScheduledReanimation> scheduledReanimations = new List<ScheduledReanimation>();
        private readonly HashSet<int> scheduledCorpseIds = new HashSet<int>();

        private int spatialBucketColumns;
        private int herdBucketColumns;
        private int nextSpatialRefreshTick;
        private int nextHerdRefreshTick;
        private int nextNoiseMaintenanceTick;
        private int ticksUntilNextZombieRaid = -1;
        private int dangerRevision = 1;

        private IntVec3 pendingNoisePosition = IntVec3.Invalid;
        private int pendingNoiseTick = -1;
        private int pendingNoiseScore = int.MinValue;
        private IntVec3 latestNoisePosition = IntVec3.Invalid;

        public ZombieMapTracker(Map map) : base(map)
        {
            spatialBucketColumns = Math.Max(1, (map.Size.x + SpatialBucketSize - 1) / SpatialBucketSize);
            herdBucketColumns = Math.Max(1, (map.Size.x + HerdBucketSize - 1) / HerdBucketSize);
        }

        public int ZombieCount => zombies.Count;
        public int DangerRevision => dangerRevision;
        public int RecentNoiseCount => noisyLocations.Count;

        public int TicksUntilNextZombieRaid
        {
            get => ticksUntilNextZombieRaid;
            set => ticksUntilNextZombieRaid = value;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RebuildFromMap();

            int now = GenTicks.TicksGame;
            nextSpatialRefreshTick = now + SpatialRefreshTicks;
            nextHerdRefreshTick = now + HerdRefreshTicks;
            nextNoiseMaintenanceTick = now + NoiseMaintenanceTicks;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            int now = GenTicks.TicksGame;
            if (now >= nextSpatialRefreshTick)
            {
                RebuildSpatialIndex();
                nextSpatialRefreshTick = now + SpatialRefreshTicks;
            }

            if (now >= nextHerdRefreshTick)
            {
                RebuildHerdAnchors();
                nextHerdRefreshTick = now + HerdRefreshTicks;
            }

            if (now >= nextNoiseMaintenanceTick)
            {
                FlushPendingNoise();
                PruneExpiredNoises(now);
                nextNoiseMaintenanceTick = now + NoiseMaintenanceTicks;
            }

            ProcessDueReanimations(now);
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            ZombieAvoidanceUtility.ForgetMap(map);
        }

        public void RebuildFromMap()
        {
            zombies.Clear();
            indexedZombiePositions.Clear();

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn_Zombiefied zombie = pawns[i] as Pawn_Zombiefied;
                if (zombie != null && !zombie.Dead && zombie.Spawned)
                {
                    zombies.Add(zombie);
                    indexedZombiePositions[zombie.thingIDNumber] = zombie.Position;
                }
            }

            RebuildSpatialIndex(forceDangerRevision: true);
            RebuildHerdAnchors();
            RegisterExistingCorpses();
        }

        public void RegisterZombie(Pawn_Zombiefied zombie)
        {
            if (zombie == null || zombie.Map != map || !zombie.Spawned || zombie.Dead)
            {
                return;
            }

            if (zombies.Add(zombie))
            {
                indexedZombiePositions[zombie.thingIDNumber] = zombie.Position;
                dangerRevision++;
                AddZombieToSpatialBucket(zombie);
            }
        }

        public void UnregisterZombie(Pawn_Zombiefied zombie)
        {
            if (zombie == null)
            {
                return;
            }

            if (zombies.Remove(zombie))
            {
                indexedZombiePositions.Remove(zombie.thingIDNumber);
                dangerRevision++;
            }
        }

        public void GetZombiesNear(IntVec3 center, float radius, List<Pawn_Zombiefied> results)
        {
            if (results == null)
            {
                return;
            }

            float radiusSquared = radius * radius;
            int minX = Mathf.Max(0, center.x - Mathf.CeilToInt(radius));
            int maxX = Mathf.Min(map.Size.x - 1, center.x + Mathf.CeilToInt(radius));
            int minZ = Mathf.Max(0, center.z - Mathf.CeilToInt(radius));
            int maxZ = Mathf.Min(map.Size.z - 1, center.z + Mathf.CeilToInt(radius));

            int minBucketX = minX / SpatialBucketSize;
            int maxBucketX = maxX / SpatialBucketSize;
            int minBucketZ = minZ / SpatialBucketSize;
            int maxBucketZ = maxZ / SpatialBucketSize;

            for (int bucketZ = minBucketZ; bucketZ <= maxBucketZ; bucketZ++)
            {
                for (int bucketX = minBucketX; bucketX <= maxBucketX; bucketX++)
                {
                    List<Pawn_Zombiefied> bucket;
                    if (!zombieBuckets.TryGetValue(BucketKey(bucketX, bucketZ), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        Pawn_Zombiefied zombie = bucket[i];
                        if (zombie == null || zombie.Dead || !zombie.Spawned || zombie.Map != map)
                        {
                            continue;
                        }

                        if (center.DistanceToSquared(zombie.Position) <= radiusSquared)
                        {
                            results.Add(zombie);
                        }
                    }
                }
            }
        }

        public void CopyZombiesTo(List<Pawn_Zombiefied> results)
        {
            if (results == null)
            {
                return;
            }

            foreach (Pawn_Zombiefied zombie in zombies)
            {
                if (zombie != null && !zombie.Dead && zombie.Spawned && zombie.Map == map)
                {
                    results.Add(zombie);
                }
            }
        }

        public bool AnyZombieWithin(IntVec3 center, float radius)
        {
            float radiusSquared = radius * radius;
            int minX = Mathf.Max(0, center.x - Mathf.CeilToInt(radius));
            int maxX = Mathf.Min(map.Size.x - 1, center.x + Mathf.CeilToInt(radius));
            int minZ = Mathf.Max(0, center.z - Mathf.CeilToInt(radius));
            int maxZ = Mathf.Min(map.Size.z - 1, center.z + Mathf.CeilToInt(radius));

            int minBucketX = minX / SpatialBucketSize;
            int maxBucketX = maxX / SpatialBucketSize;
            int minBucketZ = minZ / SpatialBucketSize;
            int maxBucketZ = maxZ / SpatialBucketSize;

            for (int bucketZ = minBucketZ; bucketZ <= maxBucketZ; bucketZ++)
            {
                for (int bucketX = minBucketX; bucketX <= maxBucketX; bucketX++)
                {
                    List<Pawn_Zombiefied> bucket;
                    if (!zombieBuckets.TryGetValue(BucketKey(bucketX, bucketZ), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        Pawn_Zombiefied zombie = bucket[i];
                        if (zombie != null && !zombie.Dead && zombie.Spawned && zombie.Map == map
                            && center.DistanceToSquared(zombie.Position) <= radiusSquared)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool TryGetHerdAnchor(Pawn_Zombiefied zombie, out IntVec3 anchor)
        {
            anchor = IntVec3.Invalid;
            if (zombie == null || herdAnchors.Count == 0)
            {
                return false;
            }

            float bestScore = float.MinValue;
            int currentTick = GenTicks.TicksGame;
            for (int i = 0; i < herdAnchors.Count; i++)
            {
                HerdAnchor candidate = herdAnchors[i];
                float distance = (zombie.Position - candidate.position).LengthHorizontal;
                if (distance > 84f)
                {
                    continue;
                }

                float score = -distance;
                score += Mathf.Min(candidate.count, 20) * 0.45f;
                if (candidate.hunting)
                {
                    score += 32f;
                }

                int noiseSeed = currentTick / HerdRefreshTicks + zombie.thingIDNumber * 397 + i * 7919;
                score += Rand.RangeSeeded(0f, 7f, noiseSeed);

                if (score > bestScore)
                {
                    bestScore = score;
                    anchor = candidate.position;
                }
            }

            return anchor.IsValid;
        }

        public int RecommendedCombatScanInterval
        {
            get
            {
                int count = zombies.Count;
                if (count >= 400)
                {
                    return 120;
                }
                if (count >= 200)
                {
                    return 90;
                }
                return 60;
            }
        }

        public int RecommendedIdleWaitTicks
        {
            get
            {
                int count = zombies.Count;
                if (count >= 400)
                {
                    return 180;
                }
                if (count >= 200)
                {
                    return 150;
                }
                return 120;
            }
        }

        public void RegisterWeaponNoise(Thing caster)
        {
            if (caster == null || !caster.Spawned || caster.Map != map)
            {
                return;
            }

            int score;
            Pawn pawn = caster as Pawn;
            if (pawn != null)
            {
                ThingWithComps equipment = pawn.equipment != null ? pawn.equipment.Primary : null;
                if (equipment == null || equipment.def == null || !equipment.def.IsRangedWeapon || equipment.def.weaponTags == null)
                {
                    return;
                }

                for (int i = 0; i < equipment.def.weaponTags.Count; i++)
                {
                    if (equipment.def.weaponTags[i] == "Neolithic")
                    {
                        return;
                    }
                }

                score = pawn.Faction != null && pawn.Faction.IsPlayer ? 13 : 7;
            }
            else if (caster is Building_Turret)
            {
                score = caster.Faction != null && caster.Faction.IsPlayer ? 13 : 3;
            }
            else
            {
                return;
            }

            int now = GenTicks.TicksGame;
            if (score > pendingNoiseScore || (score == pendingNoiseScore && now >= pendingNoiseTick))
            {
                pendingNoiseScore = score;
                pendingNoisePosition = caster.Position;
                pendingNoiseTick = now;
            }
        }

        public IntVec3 BestNoisyLocation()
        {
            return noisyLocations.Count > 0 ? latestNoisePosition : IntVec3.Invalid;
        }

        public void RegisterCorpseForReanimation(Corpse corpse)
        {
            if (!IsReanimationCandidate(corpse) || scheduledCorpseIds.Contains(corpse.thingIDNumber))
            {
                return;
            }

            int ageToReanimate = corpse.InnerPawn.Faction != null && corpse.InnerPawn.Faction.IsPlayer ? 17500 : 2500;
            int remainingTicks = Math.Max(0, ageToReanimate - corpse.Age);
            ScheduleCorpse(corpse, GenTicks.TicksGame + remainingTicks);
        }

        private void ScheduleCorpse(Corpse corpse, int dueTick)
        {
            ScheduledReanimation entry = new ScheduledReanimation
            {
                corpse = corpse,
                dueTick = dueTick
            };

            int insertAt = scheduledReanimations.Count;
            while (insertAt > 0 && scheduledReanimations[insertAt - 1].dueTick > dueTick)
            {
                insertAt--;
            }

            scheduledReanimations.Insert(insertAt, entry);
            scheduledCorpseIds.Add(corpse.thingIDNumber);
        }

        private void ProcessDueReanimations(int now)
        {
            while (scheduledReanimations.Count > 0 && scheduledReanimations[0].dueTick <= now)
            {
                ScheduledReanimation entry = scheduledReanimations[0];
                scheduledReanimations.RemoveAt(0);

                Corpse corpse = entry.corpse;
                if (corpse != null)
                {
                    scheduledCorpseIds.Remove(corpse.thingIDNumber);
                }

                if (!IsReanimationCandidate(corpse))
                {
                    continue;
                }

                Pawn reanimated = ZombiefiedMod.Instance != null ? ZombiefiedMod.Instance.ReanimateDeath(corpse) : null;
                if (reanimated == null && corpse != null && !corpse.Destroyed && corpse.Spawned && corpse.Map == map)
                {
                    // Unsupported modded races should not hammer pawn generation and the log every tick.
                    ScheduleCorpse(corpse, now + 60000);
                }
            }
        }

        private bool IsReanimationCandidate(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed || !corpse.Spawned || corpse.Map != map || corpse.InnerPawn == null)
            {
                return false;
            }

            Pawn pawn = corpse.InnerPawn;
            if (pawn.health == null || pawn.health.hediffSet == null || pawn.health.hediffSet.GetBrain() == null || !pawn.RaceProps.IsFlesh)
            {
                return false;
            }

            if (ZombiefiedMod.disableAnimalZombies && pawn.RaceProps.intelligence <= Intelligence.Animal)
            {
                return false;
            }

            HediffDef infection = ZombiefiedDefCache.ZombieWoundInfection;
            return infection != null && pawn.health.hediffSet.HasHediff(infection);
        }

        private void RegisterExistingCorpses()
        {
            List<Thing> corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            if (corpses == null)
            {
                return;
            }

            for (int i = 0; i < corpses.Count; i++)
            {
                RegisterCorpseForReanimation(corpses[i] as Corpse);
            }
        }

        private void FlushPendingNoise()
        {
            if (!pendingNoisePosition.IsValid || pendingNoiseTick < 0)
            {
                return;
            }

            noisyLocations.Enqueue(pendingNoisePosition);
            noisyLocationTicks.Enqueue(pendingNoiseTick);
            latestNoisePosition = pendingNoisePosition;
            while (noisyLocations.Count > MaximumRememberedNoises)
            {
                noisyLocations.Dequeue();
                noisyLocationTicks.Dequeue();
            }

            pendingNoisePosition = IntVec3.Invalid;
            pendingNoiseTick = -1;
            pendingNoiseScore = int.MinValue;
        }

        private void PruneExpiredNoises(int now)
        {
            int lifetime = Math.Max(0, ZombiefiedMod.zombieSoundReactionTimeInHours) * 2500;
            while (noisyLocationTicks.Count > 0 && now - noisyLocationTicks.Peek() > lifetime)
            {
                noisyLocationTicks.Dequeue();
                noisyLocations.Dequeue();
            }

            if (noisyLocations.Count == 0)
            {
                latestNoisePosition = IntVec3.Invalid;
            }
        }

        private void RebuildSpatialIndex(bool forceDangerRevision = false)
        {
            foreach (List<Pawn_Zombiefied> bucket in zombieBuckets.Values)
            {
                bucket.Clear();
            }

            bool positionsChanged = forceDangerRevision;
            List<Pawn_Zombiefied> stale = null;
            foreach (Pawn_Zombiefied zombie in zombies)
            {
                if (zombie == null || zombie.Destroyed || zombie.Dead || !zombie.Spawned || zombie.Map != map)
                {
                    if (stale == null)
                    {
                        stale = SimplePool<List<Pawn_Zombiefied>>.Get();
                        stale.Clear();
                    }
                    stale.Add(zombie);
                    positionsChanged = true;
                    continue;
                }

                IntVec3 previous;
                if (!indexedZombiePositions.TryGetValue(zombie.thingIDNumber, out previous) || previous != zombie.Position)
                {
                    indexedZombiePositions[zombie.thingIDNumber] = zombie.Position;
                    positionsChanged = true;
                }

                AddZombieToSpatialBucket(zombie);
            }

            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++)
                {
                    Pawn_Zombiefied zombie = stale[i];
                    if (zombie != null)
                    {
                        zombies.Remove(zombie);
                        indexedZombiePositions.Remove(zombie.thingIDNumber);
                    }
                }
                stale.Clear();
                SimplePool<List<Pawn_Zombiefied>>.Return(stale);
            }

            if (positionsChanged)
            {
                dangerRevision++;
            }
        }

        private void AddZombieToSpatialBucket(Pawn_Zombiefied zombie)
        {
            int bucketX = zombie.Position.x / SpatialBucketSize;
            int bucketZ = zombie.Position.z / SpatialBucketSize;
            int key = BucketKey(bucketX, bucketZ);

            List<Pawn_Zombiefied> bucket;
            if (!zombieBuckets.TryGetValue(key, out bucket))
            {
                bucket = new List<Pawn_Zombiefied>(8);
                zombieBuckets.Add(key, bucket);
            }
            bucket.Add(zombie);
        }

        private int BucketKey(int bucketX, int bucketZ)
        {
            return bucketX + bucketZ * spatialBucketColumns;
        }

        private void RebuildHerdAnchors()
        {
            herdAnchors.Clear();
            foreach (HerdAccumulator accumulator in herdAccumulators.Values)
            {
                accumulator.count = 0;
                accumulator.huntingCount = 0;
                accumulator.representativePosition = IntVec3.Invalid;
                accumulator.huntingRepresentativePosition = IntVec3.Invalid;
            }

            foreach (Pawn_Zombiefied zombie in zombies)
            {
                if (zombie == null || zombie.Dead || !zombie.Spawned || zombie.Map != map)
                {
                    continue;
                }

                int bucketX = zombie.Position.x / HerdBucketSize;
                int bucketZ = zombie.Position.z / HerdBucketSize;
                int key = bucketX + bucketZ * herdBucketColumns;

                HerdAccumulator accumulator;
                if (!herdAccumulators.TryGetValue(key, out accumulator))
                {
                    accumulator = new HerdAccumulator();
                    herdAccumulators.Add(key, accumulator);
                }

                accumulator.count++;
                if (!accumulator.representativePosition.IsValid)
                {
                    accumulator.representativePosition = zombie.Position;
                }

                if (zombie.hunting || (zombie.CurJob != null && zombie.CurJob.def == ZombiefiedMod.zombieHunt))
                {
                    accumulator.huntingCount++;
                    if (!accumulator.huntingRepresentativePosition.IsValid)
                    {
                        accumulator.huntingRepresentativePosition = zombie.Position;
                    }
                }
            }

            foreach (HerdAccumulator accumulator in herdAccumulators.Values)
            {
                if (accumulator.count <= 0)
                {
                    continue;
                }

                bool hunting = accumulator.huntingCount > 0;
                IntVec3 representative = hunting && accumulator.huntingRepresentativePosition.IsValid
                    ? accumulator.huntingRepresentativePosition
                    : accumulator.representativePosition;

                if (!representative.IsValid)
                {
                    continue;
                }

                herdAnchors.Add(new HerdAnchor
                {
                    // Use an actual zombie cell rather than a geometric center that could land inside a wall.
                    position = representative,
                    count = accumulator.count,
                    hunting = hunting
                });
            }
        }
    }

    internal static class ZombieMapTrackerUtility
    {
        public static ZombieMapTracker GetTracker(Map map)
        {
            return map != null ? map.GetComponent<ZombieMapTracker>() : null;
        }
    }
}
