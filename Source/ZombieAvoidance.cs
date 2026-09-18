using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Verse;
using Verse.AI;

namespace Zombiefied
{
    internal static class ZombieAvoidanceUtility
    {
        // Zombies acquire prey in their current region out to roughly seven cells. The extra two-cell
        // margin gives visitors and raiders room to route around the attraction radius instead of
        // skimming its edge and immediately pulling a horde.
        private const float AvoidRadius = 9f;
        private const int SnapshotIntervalTicks = 15;
        private const ushort MaximumTraversableCost = 9999;

        private sealed class DangerSnapshot
        {
            public int version;
            public ushort[] grid;
            public bool hasDanger;
        }

        private static readonly Dictionary<Map, DangerSnapshot> dangerSnapshots =
            new Dictionary<Map, DangerSnapshot>();

        public static bool ShouldAvoidZombies(Pawn pawn)
        {
            if (pawn == null || pawn is Pawn_Zombiefied || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            if (pawn.Faction == null || pawn.Faction == Faction.OfPlayer)
            {
                return false;
            }

            RaceProperties race = pawn.RaceProps;
            if (race == null || !race.Humanlike || race.IsMechanoid)
            {
                return false;
            }

            return true;
        }

        public static bool UsesMeleeAsPrimaryAttack(Pawn pawn)
        {
            if (!ShouldAvoidZombies(pawn))
            {
                return false;
            }

            Verb verb = pawn.CurrentEffectiveVerb;
            if (verb == null)
            {
                verb = pawn.TryGetAttackVerb(null, !pawn.IsColonist);
            }

            return verb != null && verb.verbProps != null && verb.verbProps.IsMeleeAttack;
        }

        public static bool IsZombie(Thing thing)
        {
            return thing is Pawn_Zombiefied;
        }

        public static bool IsProactiveZombieMeleeJob(Pawn pawn, Job job)
        {
            if (!ShouldAvoidZombies(pawn) || job == null || job.def != JobDefOf.AttackMelee)
            {
                return false;
            }

            if (job.playerForced || job.reactingToMeleeThreat)
            {
                return false;
            }

            return IsZombie(job.GetTarget(TargetIndex.A).Thing);
        }

        public static void ForgetMap(Map map)
        {
            if (map != null)
            {
                dangerSnapshots.Remove(map);
            }
        }

        public static ZombiePathAvoidanceCustomizer CreatePathCustomizer(Pawn pawn)
        {
            if (!ShouldAvoidZombies(pawn))
            {
                return null;
            }

            DangerSnapshot snapshot = GetDangerSnapshot(pawn.Map);
            if (snapshot == null || !snapshot.hasDanger)
            {
                return null;
            }

            return new ZombiePathAvoidanceCustomizer(pawn.Map, snapshot.version, snapshot.grid);
        }

        private static DangerSnapshot GetDangerSnapshot(Map map)
        {
            int version = GenTicks.TicksGame / SnapshotIntervalTicks;

            DangerSnapshot snapshot;
            if (dangerSnapshots.TryGetValue(map, out snapshot) && snapshot.version == version)
            {
                return snapshot;
            }

            snapshot = BuildDangerSnapshot(map, version);
            dangerSnapshots[map] = snapshot;
            return snapshot;
        }

        private static DangerSnapshot BuildDangerSnapshot(Map map, int version)
        {
            int cellCount = map.cellIndices.NumGridCells;
            ushort[] grid = new ushort[cellCount];
            bool hasDanger = false;

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                Pawn_Zombiefied zombie = pawns[pawnIndex] as Pawn_Zombiefied;
                if (zombie == null || zombie.Dead || !zombie.Spawned)
                {
                    continue;
                }

                hasDanger = true;
                IntVec3 zombiePosition = zombie.Position;

                foreach (IntVec3 cell in GenRadial.RadialCellsAround(zombiePosition, AvoidRadius, true))
                {
                    if (!cell.InBounds(map))
                    {
                        continue;
                    }

                    float distanceSquared = zombiePosition.DistanceToSquared(cell);
                    ushort addedCost = CostForDistanceSquared(distanceSquared);
                    if (addedCost == 0)
                    {
                        continue;
                    }

                    int cellIndex = map.cellIndices.CellToIndex(cell);
                    int combinedCost = grid[cellIndex] + addedCost;
                    grid[cellIndex] = (ushort)Math.Min(MaximumTraversableCost, combinedCost);
                }
            }

            return new DangerSnapshot
            {
                version = version,
                grid = grid,
                hasDanger = hasDanger
            };
        }

        private static ushort CostForDistanceSquared(float distanceSquared)
        {
            if (distanceSquared <= 4f)
            {
                return 6000;
            }

            if (distanceSquared <= 16f)
            {
                return 3000;
            }

            if (distanceSquared <= 49f)
            {
                return 1400;
            }

            if (distanceSquared <= AvoidRadius * AvoidRadius)
            {
                return 400;
            }

            return 0;
        }
    }

    internal static class ZombiePathCustomizerLifetime
    {
        private sealed class DeferredDispose
        {
            public Map map;
            public int queuedTick;
            public ZombiePathAvoidanceCustomizer customizer;
        }

        private static readonly List<DeferredDispose> deferred = new List<DeferredDispose>();

        public static void Defer(Map map, ZombiePathAvoidanceCustomizer customizer)
        {
            if (customizer == null)
            {
                return;
            }

            deferred.Add(new DeferredDispose
            {
                map = map,
                queuedTick = GenTicks.TicksGame,
                customizer = customizer
            });
        }

        public static void DisposeCompletedForMap(Map map)
        {
            int currentTick = GenTicks.TicksGame;
            for (int i = deferred.Count - 1; i >= 0; i--)
            {
                DeferredDispose item = deferred[i];
                if (ReferenceEquals(item.map, map) && item.queuedTick < currentTick)
                {
                    item.customizer.Dispose();
                    deferred.RemoveAt(i);
                }
            }
        }

        public static void DisposeAllForMap(Map map)
        {
            for (int i = deferred.Count - 1; i >= 0; i--)
            {
                DeferredDispose item = deferred[i];
                if (ReferenceEquals(item.map, map))
                {
                    item.customizer.Dispose();
                    deferred.RemoveAt(i);
                }
            }
        }
    }

    internal sealed class ZombiePathAvoidanceCustomizer : PathRequest.IPathGridCustomizer, IDisposable
    {
        private readonly Map map;
        private readonly int snapshotVersion;
        private NativeArray<ushort> offsetGrid;
        private bool disposed;

        public ZombiePathAvoidanceCustomizer(Map map, int snapshotVersion, ushort[] snapshot)
        {
            this.map = map;
            this.snapshotVersion = snapshotVersion;
            offsetGrid = new NativeArray<ushort>(
                snapshot.Length,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < snapshot.Length; i++)
            {
                offsetGrid[i] = snapshot[i];
            }
        }

        public NativeArray<ushort> GetOffsetGrid()
        {
            return offsetGrid;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (offsetGrid.IsCreated)
            {
                offsetGrid.Dispose();
            }
        }

        public override bool Equals(object obj)
        {
            ZombiePathAvoidanceCustomizer other = obj as ZombiePathAvoidanceCustomizer;
            return other != null
                && ReferenceEquals(map, other.map)
                && snapshotVersion == other.snapshotVersion;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((map != null ? map.GetHashCode() : 0) * 397) ^ snapshotVersion;
            }
        }
    }

    [HarmonyPatch]
    internal static class ZombiePathCreateRequestPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(PathFinder),
                nameof(PathFinder.CreateRequest),
                new Type[]
                {
                    typeof(IntVec3),
                    typeof(LocalTargetInfo),
                    typeof(IntVec3?),
                    typeof(TraverseParms),
                    typeof(PathFinderCostTuning?),
                    typeof(PathEndMode),
                    typeof(Pawn),
                    typeof(PathRequest.IPathGridCustomizer)
                });
        }

        static void Prefix(
            TraverseParms traverseParms,
            Pawn pawn,
            ref PathRequest.IPathGridCustomizer customizer)
        {
            if (customizer != null)
            {
                return;
            }

            Pawn pathingPawn = pawn ?? traverseParms.pawn;
            customizer = ZombieAvoidanceUtility.CreatePathCustomizer(pathingPawn);
        }
    }

    [HarmonyPatch]
    internal static class ZombiePathFindNowPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(PathFinder),
                nameof(PathFinder.FindPathNow),
                new Type[]
                {
                    typeof(IntVec3),
                    typeof(LocalTargetInfo),
                    typeof(TraverseParms),
                    typeof(PathFinderCostTuning?),
                    typeof(PathEndMode),
                    typeof(PathRequest.IPathGridCustomizer)
                });
        }

        static void Prefix(
            TraverseParms traverseParms,
            ref PathRequest.IPathGridCustomizer customizer,
            out ZombiePathAvoidanceCustomizer __state)
        {
            __state = null;
            if (customizer != null)
            {
                return;
            }

            __state = ZombieAvoidanceUtility.CreatePathCustomizer(traverseParms.pawn);
            if (__state != null)
            {
                customizer = __state;
            }
        }

        static Exception Finalizer(Exception __exception, ZombiePathAvoidanceCustomizer __state)
        {
            if (__state != null)
            {
                __state.Dispose();
            }

            return __exception;
        }
    }

    [HarmonyPatch(typeof(PathRequest), nameof(PathRequest.Resolve))]
    internal static class ZombiePathRequestResolvePatch
    {
        static void Postfix(PathRequest __instance)
        {
            DisposeZombieCustomizer(__instance);
        }

        internal static void DisposeZombieCustomizer(PathRequest request)
        {
            ZombiePathAvoidanceCustomizer customizer = request.customizer as ZombiePathAvoidanceCustomizer;
            if (customizer == null)
            {
                return;
            }

            customizer.Dispose();
            request.customizer = null;
        }
    }

    [HarmonyPatch(typeof(PathRequest), nameof(PathRequest.Dispose))]
    internal static class ZombiePathRequestDisposePatch
    {
        static void Postfix(PathRequest __instance)
        {
            ZombiePathAvoidanceCustomizer customizer = __instance.customizer as ZombiePathAvoidanceCustomizer;
            if (customizer == null)
            {
                return;
            }

            // A cancelled request can still have a Burst grid/path job reading this NativeArray. The
            // pathfinder force-completes previous work at the start of its next tick, so defer disposal
            // until after that synchronization point instead of invalidating memory under a worker job.
            ZombiePathCustomizerLifetime.Defer(__instance.map, customizer);
            __instance.customizer = null;
        }
    }

    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.PathFinderTick))]
    internal static class ZombiePathFinderTickPatch
    {
        static void Postfix(Map ___map)
        {
            ZombiePathCustomizerLifetime.DisposeCompletedForMap(___map);
        }
    }

    [HarmonyPatch(typeof(PathFinder), nameof(PathFinder.Dispose))]
    internal static class ZombiePathFinderDisposePatch
    {
        static void Postfix(Map ___map)
        {
            // PathFinder.Dispose completes all outstanding jobs before returning, so every deferred
            // custom grid belonging to this map is safe to release here.
            ZombiePathCustomizerLifetime.DisposeAllForMap(___map);
            ZombieAvoidanceUtility.ForgetMap(___map);
        }
    }

    [HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget))]
    internal static class ZombieMeleeTargetSelectionPatch
    {
        static void Prefix(
            IAttackTargetSearcher searcher,
            ref Predicate<Thing> validator,
            bool onlyRanged)
        {
            Pawn pawn = searcher as Pawn;
            if (onlyRanged || !ZombieAvoidanceUtility.UsesMeleeAsPrimaryAttack(pawn))
            {
                return;
            }

            Predicate<Thing> originalValidator = validator;
            validator = delegate(Thing target)
            {
                if (ZombieAvoidanceUtility.IsZombie(target))
                {
                    return false;
                }

                return originalValidator == null || originalValidator(target);
            };
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class ZombieProactiveMeleeJobPatch
    {
        static void Prefix(Pawn ___pawn, Job newJob)
        {
            if (!ZombieAvoidanceUtility.IsProactiveZombieMeleeJob(___pawn, newJob))
            {
                return;
            }

            // Path-following AI can manufacture a one-swing AttackMelee job when another pawn blocks
            // its next cell for long enough. Turning that proactive zombie attack into a short wait
            // lets the normal thinker and zombie-aware path costs choose a safer route on the next pass.
            newJob.def = JobDefOf.Wait;
            newJob.expiryInterval = 45;
            newJob.checkOverrideOnExpire = true;
            newJob.maxNumMeleeAttacks = 0;
            newJob.verbToUse = null;
        }
    }
}
