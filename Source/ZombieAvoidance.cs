using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using Verse;
using Verse.AI;

namespace Zombiefied
{
    internal static class ZombieAvoidanceUtility
    {
        // Zombies should behave like an environmental hazard to outsiders. Raiders, visitors, traders,
        // and other foreign humanlikes should preserve their original mission and route around hordes
        // instead of treating zombies as convenient combat objectives.
        private const float AvoidRadius = 14f;
        private const float EmergencyDisengageScanRadius = 9f;
        private const float EmergencyDisengageDistance = 18f;
        private const int EmergencyDisengageJobTicks = 240;
        private const int SnapshotIntervalTicks = 15;
        private const ushort MaximumDangerCost = 4800;

        private sealed class DangerSnapshot
        {
            public int version;
            public ushort[] grid;
            public bool hasDanger;
        }

        private static readonly Dictionary<Map, DangerSnapshot> dangerSnapshots =
            new Dictionary<Map, DangerSnapshot>();

        private static bool IsHumanlikeAvoidanceCandidate(Pawn pawn)
        {
            if (pawn == null || pawn is Pawn_Zombiefied || !pawn.Spawned || pawn.Map == null)
            {
                return false;
            }

            RaceProperties race = pawn.RaceProps;
            return race != null && race.Humanlike && !race.IsMechanoid;
        }

        public static bool ShouldAvoidZombies(Pawn pawn)
        {
            if (!IsHumanlikeAvoidanceCandidate(pawn))
            {
                return false;
            }

            // Combat suppression is intentionally limited to foreign humanlikes. Player pawns can be given
            // explicit combat orders against zombies even when their optional path avoidance is enabled.
            return pawn.Faction != null && pawn.Faction != Faction.OfPlayer;
        }

        public static bool ShouldUseZombieAvoidancePathing(Pawn pawn)
        {
            if (!IsHumanlikeAvoidanceCandidate(pawn) || pawn.Faction == null)
            {
                return false;
            }

            if (pawn.Faction == Faction.OfPlayer)
            {
                return ZombiefiedMod.ColonistsUseZombieAvoidancePathing;
            }

            return true;
        }

        public static bool IsZombie(Thing thing)
        {
            return thing is Pawn_Zombiefied;
        }

        public static bool ShouldRejectZombieCombat(Pawn pawn, Thing target)
        {
            // Foreign humanlikes must never promote a zombie from environmental hazard to combat objective.
            // Allowing close-range or melee-threat exceptions causes vanilla fight AI to cache that zombie as
            // mindState.enemyTarget, after which ranged pawns can receive Goto jobs that actively pursue it.
            return ShouldAvoidZombies(pawn) && IsZombie(target);
        }

        public static bool IsProactiveZombieAttackJob(Pawn pawn, Job job)
        {
            if (!ShouldAvoidZombies(pawn) || job == null)
            {
                return false;
            }

            if (job.def != JobDefOf.AttackMelee && job.def != JobDefOf.AttackStatic)
            {
                return false;
            }

            return IsZombie(job.GetTarget(TargetIndex.A).Thing);
        }

        public static bool IsPassiveWaitJob(Job job)
        {
            if (job == null || job.def == null || job.def == JobDefOf.Wait_Downed)
            {
                return false;
            }

            return job.def == JobDefOf.Wait
                || job.def == JobDefOf.Wait_MaintainPosture
                || job.def == JobDefOf.Wait_Combat
                || job.def == JobDefOf.Wait_Wander
                || job.def.isIdle;
        }

        public static bool HasZombieWithin(Pawn pawn, float radius)
        {
            if (!ShouldAvoidZombies(pawn))
            {
                return false;
            }

            float radiusSquared = radius * radius;
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn_Zombiefied zombie = pawns[i] as Pawn_Zombiefied;
                if (zombie == null || zombie.Dead || !zombie.Spawned)
                {
                    continue;
                }

                if (pawn.Position.DistanceToSquared(zombie.Position) <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public static Job TryMakeZombieDisengageJob(Pawn pawn, Thing forcedThreat = null)
        {
            if (!ShouldAvoidZombies(pawn) || pawn.Downed || !pawn.Awake())
            {
                return null;
            }

            List<Thing> threats = new List<Thing>();
            float scanRadiusSquared = EmergencyDisengageScanRadius * EmergencyDisengageScanRadius;
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn_Zombiefied zombie = pawns[i] as Pawn_Zombiefied;
                if (zombie == null || zombie.Dead || !zombie.Spawned)
                {
                    continue;
                }

                if (pawn.Position.DistanceToSquared(zombie.Position) <= scanRadiusSquared)
                {
                    threats.Add(zombie);
                }
            }

            if (IsZombie(forcedThreat) && forcedThreat.Spawned && !threats.Contains(forcedThreat))
            {
                threats.Add(forcedThreat);
            }

            if (threats.Count == 0)
            {
                return null;
            }

            IntVec3 fleeCell = CellFinderLoose.GetFleeDest(pawn, threats, EmergencyDisengageDistance);
            if (!fleeCell.IsValid || !fleeCell.InBounds(pawn.Map) || fleeCell == pawn.Position)
            {
                Thing nearestThreat = null;
                float nearestDistanceSquared = float.MaxValue;
                for (int i = 0; i < threats.Count; i++)
                {
                    Thing threat = threats[i];
                    if (threat == null || !threat.Spawned)
                    {
                        continue;
                    }

                    float distanceSquared = pawn.Position.DistanceToSquared(threat.Position);
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearestDistanceSquared = distanceSquared;
                        nearestThreat = threat;
                    }
                }

                if (nearestThreat == null
                    || !RCellFinder.TryFindDirectFleeDestination(nearestThreat.Position, 10f, pawn, out fleeCell)
                    || !fleeCell.IsValid
                    || fleeCell == pawn.Position)
                {
                    return null;
                }
            }

            Job job = JobMaker.MakeJob(JobDefOf.Goto, fleeCell);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.expiryInterval = EmergencyDisengageJobTicks;
            job.checkOverrideOnExpire = true;
            return job;
        }

        public static void ClearZombieEnemyTarget(Pawn pawn)
        {
            if (!ShouldAvoidZombies(pawn) || pawn.mindState == null)
            {
                return;
            }

            if (IsZombie(pawn.mindState.enemyTarget))
            {
                pawn.mindState.enemyTarget = null;
            }

            // meleeThreat is another persistent route by which vanilla hostility-response nodes can revive
            // zombie combat after the normal target cache has been filtered. Outsiders ignore that signal.
            if (IsZombie(pawn.mindState.meleeThreat))
            {
                pawn.mindState.meleeThreat = null;
            }
        }

        public static void ForgetMap(Map map)
        {
            if (map != null)
            {
                dangerSnapshots.Remove(map);
            }
        }

        public static ZombiePathAvoidanceCustomizer CreatePathCustomizer(Pawn pawn, LocalTargetInfo destination)
        {
            if (!ShouldUseZombieAvoidancePathing(pawn))
            {
                return null;
            }

            // Foreign humanlikes also ignore zombies as combat objectives. Player pawns only receive the path
            // cost layer when the option is enabled, so drafted and ordered attacks remain valid. The destination
            // cell is cleared below to ensure a direct order can still reach a zombie when necessary.
            DangerSnapshot snapshot = GetDangerSnapshot(pawn.Map);
            if (snapshot == null || !snapshot.hasDanger)
            {
                return null;
            }

            IntVec3 destinationCell = destination.IsValid ? destination.Cell : IntVec3.Invalid;
            return new ZombiePathAvoidanceCustomizer(pawn.Map, snapshot.version, snapshot.grid, destinationCell);
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
                    grid[cellIndex] = (ushort)Math.Min(MaximumDangerCost, combinedCost);
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
            // The outer bands start influencing routes well before contact, while the inner bands make entering
            // a zombie cluster far more expensive than taking a sizeable detour. These remain finite costs, so
            // an outsider can still cross the danger field when the map or mission destination leaves no sane
            // alternative. The cumulative cap stays well below the old 9,999 value that caused failed paths.
            if (distanceSquared <= 1f)
            {
                return 1600;
            }

            if (distanceSquared <= 4f)
            {
                return 1200;
            }

            if (distanceSquared <= 16f)
            {
                return 850;
            }

            if (distanceSquared <= 49f)
            {
                return 500;
            }

            if (distanceSquared <= 100f)
            {
                return 280;
            }

            if (distanceSquared <= AvoidRadius * AvoidRadius)
            {
                return 140;
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

        public ZombiePathAvoidanceCustomizer(Map map, int snapshotVersion, ushort[] snapshot, IntVec3 destinationCell)
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

            // Reaching the actual mission destination must always remain possible. The surrounding cells can
            // still be unattractive, but the goal cell itself should never carry artificial zombie cost.
            if (destinationCell.IsValid && destinationCell.InBounds(map))
            {
                offsetGrid[map.cellIndices.CellToIndex(destinationCell)] = 0;
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
        static MethodBase TargetMethod()
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
            [HarmonyArgument(1)] LocalTargetInfo target,
            [HarmonyArgument(3)] TraverseParms traverseParms,
            [HarmonyArgument(6)] Pawn pawn,
            [HarmonyArgument(7)] ref PathRequest.IPathGridCustomizer customizer)
        {
            if (customizer != null)
            {
                return;
            }

            Pawn pathingPawn = pawn ?? traverseParms.pawn;
            customizer = ZombieAvoidanceUtility.CreatePathCustomizer(pathingPawn, target);
        }
    }

    [HarmonyPatch]
    internal static class ZombiePathFindNowPatch
    {
        static MethodBase TargetMethod()
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
            [HarmonyArgument(1)] LocalTargetInfo target,
            [HarmonyArgument(2)] TraverseParms traverseParms,
            [HarmonyArgument(5)] ref PathRequest.IPathGridCustomizer customizer,
            out ZombiePathAvoidanceCustomizer __state)
        {
            __state = null;
            if (customizer != null)
            {
                return;
            }

            __state = ZombieAvoidanceUtility.CreatePathCustomizer(traverseParms.pawn, target);
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

            // A cancelled request can still have a worker reading the NativeArray. PathFinder synchronizes
            // outstanding work at the start of the following tick, so release the array only after that point.
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
            ZombiePathCustomizerLifetime.DisposeAllForMap(___map);
            ZombieAvoidanceUtility.ForgetMap(___map);
        }
    }

    [HarmonyPatch(typeof(AttackTargetsCache), nameof(AttackTargetsCache.GetPotentialTargetsFor))]
    internal static class ZombieAttackTargetsCachePatch
    {
        static void Postfix(IAttackTargetSearcher th, ref List<IAttackTarget> __result)
        {
            Pawn pawn = th as Pawn;
            if (!ZombieAvoidanceUtility.ShouldAvoidZombies(pawn) || __result == null)
            {
                return;
            }

            // AttackTargetsCache returns a shared static scratch list. Never remove entries from it in place,
            // because another AI query in the same frame can observe the mutation. Only allocate a private copy
            // when a zombie is actually present.
            bool containsZombie = false;
            for (int i = 0; i < __result.Count; i++)
            {
                IAttackTarget candidate = __result[i];
                if (candidate != null && ZombieAvoidanceUtility.IsZombie(candidate.Thing))
                {
                    containsZombie = true;
                    break;
                }
            }

            if (!containsZombie)
            {
                return;
            }

            List<IAttackTarget> filtered = new List<IAttackTarget>(__result.Count);
            for (int i = 0; i < __result.Count; i++)
            {
                IAttackTarget candidate = __result[i];
                if (candidate == null || !ZombieAvoidanceUtility.IsZombie(candidate.Thing))
                {
                    filtered.Add(candidate);
                }
            }

            __result = filtered;
        }
    }

    [HarmonyPatch(typeof(JobGiver_AIFightEnemy), "TryGiveJob")]
    internal static class ZombieFightEnemyStickyTargetPatch
    {
        static void Prefix(Pawn pawn)
        {
            // Vanilla JobGiver_AIFightEnemy keeps mindState.enemyTarget between think-tree evaluations. A pawn
            // that selected a zombie before filtering was applied can otherwise keep generating melee, combat
            // wait, or shooting-position Goto jobs toward that stale target indefinitely.
            ZombieAvoidanceUtility.ClearZombieEnemyTarget(pawn);
        }

        static void Postfix(Pawn pawn, ref Job __result)
        {
            if (!ZombieAvoidanceUtility.ShouldAvoidZombies(pawn) || pawn.mindState == null)
            {
                return;
            }

            Thing zombieTarget = ZombieAvoidanceUtility.IsZombie(pawn.mindState.enemyTarget)
                ? pawn.mindState.enemyTarget
                : null;

            if (zombieTarget != null)
            {
                pawn.mindState.enemyTarget = null;
                __result = ZombieAvoidanceUtility.TryMakeZombieDisengageJob(pawn, zombieTarget);
                return;
            }

            // If target filtering leaves the fight node with no job while zombies are already on top of the
            // pawn, give it one short movement job. On arrival it returns to the normal raid/visit think tree.
            if (__result == null && ZombieAvoidanceUtility.HasZombieWithin(pawn, 4.5f))
            {
                __result = ZombieAvoidanceUtility.TryMakeZombieDisengageJob(pawn);
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_AIGotoNearestHostile), "TryGiveJob")]
    internal static class ZombieGotoNearestHostilePatch
    {
        static void Postfix(Pawn pawn, ref Job __result)
        {
            if (!ZombieAvoidanceUtility.ShouldAvoidZombies(pawn) || __result == null)
            {
                return;
            }

            Thing target = __result.GetTarget(TargetIndex.A).Thing;
            if (ZombieAvoidanceUtility.IsZombie(target))
            {
                // This job giver scans AttackTargetsCache directly and historically bypassed the
                // AttackTargetFinder validator. If another mod reintroduces a zombie target, convert the
                // pursuit into a short disengagement instead of leaving the pawn without a job.
                ZombieAvoidanceUtility.ClearZombieEnemyTarget(pawn);
                __result = ZombieAvoidanceUtility.TryMakeZombieDisengageJob(pawn, target);
            }
        }
    }

    [HarmonyPatch(typeof(AttackTargetFinder), nameof(AttackTargetFinder.BestAttackTarget))]
    internal static class ZombieMissionTargetSelectionPatch
    {
        static void Prefix(
            IAttackTargetSearcher searcher,
            ref Predicate<Thing> validator)
        {
            Pawn pawn = searcher as Pawn;
            if (!ZombieAvoidanceUtility.ShouldAvoidZombies(pawn))
            {
                return;
            }

            Predicate<Thing> originalValidator = validator;
            validator = delegate(Thing target)
            {
                if (ZombieAvoidanceUtility.ShouldRejectZombieCombat(pawn, target))
                {
                    return false;
                }

                return originalValidator == null || originalValidator(target);
            };
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class ZombieProactiveAttackJobPatch
    {
        static void Prefix(Pawn ___pawn, ref Job newJob)
        {
            if (!ZombieAvoidanceUtility.ShouldAvoidZombies(___pawn) || newJob == null)
            {
                return;
            }

            Thing zombieTarget = ZombieAvoidanceUtility.IsProactiveZombieAttackJob(___pawn, newJob)
                ? newJob.GetTarget(TargetIndex.A).Thing
                : null;

            if (zombieTarget != null)
            {
                // Replacing the attack with Wait caused a stable failure loop: the think tree selected the
                // zombie again after every wait, so a pawn already in melee range could stand still forever.
                // A short sprint away breaks contact, then normal mission AI takes control again.
                Job disengageJob = ZombieAvoidanceUtility.TryMakeZombieDisengageJob(___pawn, zombieTarget);
                if (disengageJob != null)
                {
                    newJob = disengageJob;
                }
                return;
            }

            // RimWorld also creates recovery/idle waits after failed paths or an empty think-tree branch. If a
            // zombie is already within striking distance, turn that passive wait into movement instead of
            // allowing the pawn to be locked in place by repeated path or target reevaluations.
            if (ZombieAvoidanceUtility.IsPassiveWaitJob(newJob)
                && ZombieAvoidanceUtility.HasZombieWithin(___pawn, 3.5f))
            {
                Job disengageJob = ZombieAvoidanceUtility.TryMakeZombieDisengageJob(___pawn);
                if (disengageJob != null)
                {
                    newJob = disengageJob;
                }
            }
        }
    }

    [HarmonyPatch]
    internal static class ZombieTryStartAttackPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Pawn))
                .Where(method => method.Name == "TryStartAttack" && method.ReturnType == typeof(bool));
        }

        static bool Prefix(Pawn __instance, object[] __args, ref bool __result)
        {
            if (__args == null || __args.Length == 0 || !(__args[0] is LocalTargetInfo))
            {
                return true;
            }

            LocalTargetInfo target = (LocalTargetInfo)__args[0];
            if (!ZombieAvoidanceUtility.ShouldRejectZombieCombat(__instance, target.Thing))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class ZombieTryMeleeAttackPatch
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_MeleeVerbs), "pawn");

        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Pawn_MeleeVerbs))
                .Where(method => method.Name == "TryMeleeAttack" && method.ReturnType == typeof(bool));
        }

        static bool Prefix(Pawn_MeleeVerbs __instance, object[] __args, ref bool __result)
        {
            Pawn pawn = PawnField == null ? null : PawnField.GetValue(__instance) as Pawn;
            Thing target = (__args != null && __args.Length > 0) ? __args[0] as Thing : null;
            if (!ZombieAvoidanceUtility.ShouldRejectZombieCombat(pawn, target))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
