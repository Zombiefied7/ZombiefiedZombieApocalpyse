using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class WorldComponent_ZombieApocalypse : WorldComponent
    {
        private const int TicksPerDay = 60000;
        private const int CaravanCheckIntervalTicks = 2500;
        private const int MapSyncIntervalTicks = 5000;
        private const int EncounterCooldownTicks = 45000;
        private const int MarkerCap = 24;
        private const int MaxTrackedInfestedTiles = 768;
        private const float MarkerCreateThreshold = 0.55f;
        private const float MarkerRemoveThreshold = 0.45f;
        private const float MinimumEncounterSeverity = 0.10f;

        private Dictionary<PlanetTile, float> infestationByTile = new Dictionary<PlanetTile, float>();
        private Dictionary<string, int> caravanNextEncounterTick = new Dictionary<string, int>();
        private int nextSpreadTick;
        private int nextCaravanCheckTick;
        private int nextMapSyncTick;
        private bool initialized;

        public WorldComponent_ZombieApocalypse(World world) : base(world)
        {
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);

            EnsureCollections();
            if (!initialized)
            {
                SeedInitialInfestations();
                initialized = true;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (nextSpreadTick <= now)
            {
                nextSpreadTick = now + TicksPerDay;
            }
            if (nextCaravanCheckTick <= now)
            {
                nextCaravanCheckTick = now + CaravanCheckIntervalTicks;
            }
            if (nextMapSyncTick <= now)
            {
                nextMapSyncTick = now + MapSyncIntervalTicks;
            }

            SyncInfestationMarkers();
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (Find.TickManager == null || Find.World == null || Find.WorldGrid == null)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;

            if (!ZombiefiedMod.enablePlanetaryZombieInfestations)
            {
                if (now >= nextMapSyncTick)
                {
                    nextMapSyncTick = now + MapSyncIntervalTicks;
                    RemoveAllInfestationMarkers();
                }
                return;
            }

            if (now >= nextMapSyncTick)
            {
                nextMapSyncTick = now + MapSyncIntervalTicks;
                SyncZombieActivityFromMaps();
            }

            if (now >= nextSpreadTick)
            {
                nextSpreadTick = now + TicksPerDay;
                SpreadInfestations();
                SyncInfestationMarkers();
            }

            if (ZombiefiedMod.enableCaravanZombieEncounters && now >= nextCaravanCheckTick)
            {
                nextCaravanCheckTick = now + CaravanCheckIntervalTicks;
                CheckPlayerCaravansForEncounters(now);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref infestationByTile, "zombiefiedInfestationByTile", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref caravanNextEncounterTick, "zombiefiedCaravanNextEncounterTick", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref nextSpreadTick, "zombiefiedNextSpreadTick", 0);
            Scribe_Values.Look(ref nextCaravanCheckTick, "zombiefiedNextCaravanCheckTick", 0);
            Scribe_Values.Look(ref nextMapSyncTick, "zombiefiedNextMapSyncTick", 0);
            Scribe_Values.Look(ref initialized, "zombiefiedWorldInitialized", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureCollections();
                PruneInvalidTiles();
            }
        }

        public float GetInfestation(PlanetTile tile)
        {
            float severity;
            if (!IsSurfaceTile(tile) || infestationByTile == null || !infestationByTile.TryGetValue(tile, out severity))
            {
                return 0f;
            }

            return Mathf.Clamp01(severity);
        }

        public void AddInfestation(PlanetTile tile, float amount)
        {
            if (!IsUsableSurfaceTile(tile) || amount <= 0f)
            {
                return;
            }

            EnsureCollections();

            float current;
            infestationByTile.TryGetValue(tile, out current);
            infestationByTile[tile] = Mathf.Clamp01(current + amount);
        }

        public void SetInfestationAtLeast(PlanetTile tile, float severity)
        {
            if (!IsUsableSurfaceTile(tile))
            {
                return;
            }

            EnsureCollections();

            float current;
            infestationByTile.TryGetValue(tile, out current);
            if (severity > current)
            {
                infestationByTile[tile] = Mathf.Clamp01(severity);
            }
        }

        public static void NotifyZombieActivity(PlanetTile tile, float amount)
        {
            WorldComponent_ZombieApocalypse component = ZombieWorldUtility.WorldComponent;
            if (component != null)
            {
                component.AddInfestation(tile, amount);
            }
        }

        private void EnsureCollections()
        {
            if (infestationByTile == null)
            {
                infestationByTile = new Dictionary<PlanetTile, float>();
            }
            if (caravanNextEncounterTick == null)
            {
                caravanNextEncounterTick = new Dictionary<string, int>();
            }
        }

        private bool IsSurfaceTile(PlanetTile tile)
        {
            return tile.Valid && Find.WorldGrid != null && tile.Layer != null && tile.Layer.IsRootSurface;
        }

        private bool IsUsableSurfaceTile(PlanetTile tile)
        {
            return IsSurfaceTile(tile) && Find.World != null && !Find.World.Impassable(tile);
        }

        private void SeedInitialInfestations()
        {
            if (!ZombiefiedMod.enablePlanetaryZombieInfestations || Find.World == null || Find.WorldGrid == null)
            {
                return;
            }

            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map == null || !map.IsPlayerHome || !IsSurfaceTile(map.Tile))
                {
                    continue;
                }

                for (int j = 0; j < 3; j++)
                {
                    PlanetTile nearbyTile = RandomWalkToNearbyPassableTile(map.Tile, Rand.RangeInclusive(4, 10));
                    if (nearbyTile.Valid)
                    {
                        SetInfestationAtLeast(nearbyTile, Rand.Range(0.34f, 0.52f));
                    }
                }
            }

            int remoteSeeds = Mathf.Clamp(6 + maps.Count * 2, 6, 14);
            for (int i = 0; i < remoteSeeds; i++)
            {
                PlanetTile tile = FindRandomPassableSurfaceTile();
                if (tile.Valid)
                {
                    SetInfestationAtLeast(tile, Rand.Range(0.58f, 0.82f));
                }
            }
        }

        private PlanetTile FindRandomPassableSurfaceTile()
        {
            int tilesCount = Find.WorldGrid.Surface.TilesCount;
            if (tilesCount <= 0)
            {
                return PlanetTile.Invalid;
            }

            for (int attempt = 0; attempt < 200; attempt++)
            {
                PlanetTile tile = new PlanetTile(Rand.Range(0, tilesCount), Find.WorldGrid.Surface);
                if (!Find.World.Impassable(tile))
                {
                    return tile;
                }
            }

            return PlanetTile.Invalid;
        }

        private PlanetTile RandomWalkToNearbyPassableTile(PlanetTile startTile, int steps)
        {
            if (!IsSurfaceTile(startTile))
            {
                return PlanetTile.Invalid;
            }

            PlanetTile current = startTile;
            List<PlanetTile> neighbors = new List<PlanetTile>();
            for (int step = 0; step < steps; step++)
            {
                neighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(current, neighbors);
                neighbors.RemoveAll(tile => !IsUsableSurfaceTile(tile));
                if (neighbors.Count == 0)
                {
                    break;
                }

                current = neighbors.RandomElement();
            }

            return current;
        }

        private void SyncZombieActivityFromMaps()
        {
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map == null || !IsSurfaceTile(map.Tile))
                {
                    continue;
                }

                int zombieCount = 0;
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int j = 0; j < pawns.Count; j++)
                {
                    if (pawns[j] is Pawn_Zombiefied)
                    {
                        zombieCount++;
                    }
                }

                if (zombieCount > 0)
                {
                    float pressure = Mathf.Clamp(0.004f + zombieCount * 0.00035f, 0.004f, 0.035f);
                    AddInfestation(map.Tile, pressure);
                }
                else if (map.IsPlayerHome)
                {
                    ReduceInfestation(map.Tile, 0.0025f);
                }
            }
        }

        private void SpreadInfestations()
        {
            EnsureCollections();
            if (infestationByTile.Count == 0)
            {
                SeedInitialInfestations();
                return;
            }

            float spreadMultiplier = ZombiefiedMod.zombieWorldSpreadMultiplier;
            List<PlanetTile> tiles = infestationByTile.Keys.ToList();
            List<KeyValuePair<PlanetTile, float>> additions = new List<KeyValuePair<PlanetTile, float>>();
            List<PlanetTile> neighbors = new List<PlanetTile>();

            for (int i = 0; i < tiles.Count; i++)
            {
                PlanetTile tile = tiles[i];
                if (!IsUsableSurfaceTile(tile))
                {
                    continue;
                }

                float severity = GetInfestation(tile);
                infestationByTile[tile] = Mathf.Max(0f, severity - 0.0035f);

                if (severity < 0.12f || infestationByTile.Count + additions.Count >= MaxTrackedInfestedTiles)
                {
                    continue;
                }

                float spreadChance = Mathf.Clamp01((0.035f + severity * 0.16f) * spreadMultiplier);
                if (!Rand.Chance(spreadChance))
                {
                    continue;
                }

                neighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(tile, neighbors);
                neighbors.RemoveAll(neighbor => !IsUsableSurfaceTile(neighbor));
                if (neighbors.Count == 0)
                {
                    continue;
                }

                PlanetTile target = neighbors.RandomElement();
                float amount = Rand.Range(0.045f, 0.085f) + severity * 0.055f;
                additions.Add(new KeyValuePair<PlanetTile, float>(target, amount));
            }

            for (int i = 0; i < additions.Count; i++)
            {
                AddInfestation(additions[i].Key, additions[i].Value);
            }

            List<PlanetTile> remove = infestationByTile
                .Where(pair => pair.Value < 0.01f || !IsUsableSurfaceTile(pair.Key))
                .Select(pair => pair.Key)
                .ToList();
            for (int i = 0; i < remove.Count; i++)
            {
                infestationByTile.Remove(remove[i]);
            }

            if (infestationByTile.Count > MaxTrackedInfestedTiles)
            {
                List<PlanetTile> weakest = infestationByTile
                    .OrderBy(pair => pair.Value)
                    .Take(infestationByTile.Count - MaxTrackedInfestedTiles)
                    .Select(pair => pair.Key)
                    .ToList();
                for (int i = 0; i < weakest.Count; i++)
                {
                    infestationByTile.Remove(weakest[i]);
                }
            }
        }

        private void CheckPlayerCaravansForEncounters(int now)
        {
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (caravan == null || !caravan.IsPlayerControlled || caravan.pather == null || !caravan.pather.MovingNow || !IsSurfaceTile(caravan.Tile))
                {
                    continue;
                }

                float severity = GetInfestation(caravan.Tile);
                if (severity < MinimumEncounterSeverity)
                {
                    continue;
                }

                string caravanId = caravan.GetUniqueLoadID();
                int nextAllowedTick;
                if (caravanNextEncounterTick.TryGetValue(caravanId, out nextAllowedTick) && now < nextAllowedTick)
                {
                    continue;
                }

                float encounterChance = (0.0035f + severity * 0.025f) * ZombiefiedMod.zombieCaravanEncounterMultiplier;
                if (!Rand.Chance(Mathf.Clamp01(encounterChance)))
                {
                    continue;
                }

                if (TryStartCaravanAmbush(caravan, severity))
                {
                    caravanNextEncounterTick[caravanId] = now + EncounterCooldownTicks;
                }
                else
                {
                    caravanNextEncounterTick[caravanId] = now + CaravanCheckIntervalTicks * 2;
                }
            }

            RemoveStaleCaravanCooldowns(caravans);
        }

        private bool TryStartCaravanAmbush(Caravan caravan, float severity)
        {
            IncidentDef incident = DefDatabase<IncidentDef>.GetNamed("ZombieCaravanAmbush", false);
            if (incident == null || incident.Worker == null)
            {
                Log.ErrorOnce("Zombiefied could not find the ZombieCaravanAmbush incident def.", 178300921);
                return false;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatSmall, caravan);
            parms.target = caravan;
            parms.forced = true;
            parms.faction = ZombieWorldUtility.GetZombieFaction();

            float basePoints = StorytellerUtility.DefaultThreatPointsNow(caravan);
            float severityFactor = Mathf.Lerp(0.45f, 1.45f, severity);
            parms.points = Mathf.Clamp(basePoints * severityFactor * ZombiefiedMod.ZombieRaidAmountMultiplier, 90f, 3200f);

            if (!incident.Worker.CanFireNow(parms))
            {
                return false;
            }

            return incident.Worker.TryExecute(parms);
        }

        private void RemoveStaleCaravanCooldowns(List<Caravan> caravans)
        {
            if (caravanNextEncounterTick.Count == 0)
            {
                return;
            }

            HashSet<string> activeIds = new HashSet<string>();
            for (int i = 0; i < caravans.Count; i++)
            {
                if (caravans[i] != null)
                {
                    activeIds.Add(caravans[i].GetUniqueLoadID());
                }
            }

            List<string> stale = caravanNextEncounterTick.Keys.Where(id => !activeIds.Contains(id)).ToList();
            for (int i = 0; i < stale.Count; i++)
            {
                caravanNextEncounterTick.Remove(stale[i]);
            }
        }

        private void SyncInfestationMarkers()
        {
            if (Find.WorldObjects == null)
            {
                return;
            }

            WorldObjectDef markerDef = DefDatabase<WorldObjectDef>.GetNamed("ZombieInfestation", false);
            if (markerDef == null)
            {
                return;
            }

            List<WorldObject_ZombieInfestation> existing = Find.WorldObjects.AllWorldObjects
                .OfType<WorldObject_ZombieInfestation>()
                .ToList();

            HashSet<PlanetTile> desired = new HashSet<PlanetTile>(infestationByTile
                .Where(pair => pair.Value >= MarkerCreateThreshold && IsLocalInfestationMaximum(pair.Key, pair.Value))
                .OrderByDescending(pair => pair.Value)
                .Take(MarkerCap)
                .Select(pair => pair.Key));

            for (int i = 0; i < existing.Count; i++)
            {
                WorldObject_ZombieInfestation marker = existing[i];
                if (marker == null || marker.Destroyed)
                {
                    continue;
                }

                if (!desired.Contains(marker.Tile) || GetInfestation(marker.Tile) < MarkerRemoveThreshold)
                {
                    marker.Destroy();
                }
            }

            HashSet<PlanetTile> existingTiles = new HashSet<PlanetTile>(Find.WorldObjects.AllWorldObjects
                .OfType<WorldObject_ZombieInfestation>()
                .Where(marker => marker != null && !marker.Destroyed)
                .Select(marker => marker.Tile));

            foreach (PlanetTile tile in desired)
            {
                if (existingTiles.Contains(tile))
                {
                    continue;
                }

                WorldObject_ZombieInfestation marker = WorldObjectMaker.MakeWorldObject(markerDef) as WorldObject_ZombieInfestation;
                if (marker == null)
                {
                    continue;
                }

                marker.Tile = tile;
                Find.WorldObjects.Add(marker);
            }
        }

        private bool IsLocalInfestationMaximum(PlanetTile tile, float severity)
        {
            List<PlanetTile> neighbors = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(tile, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (GetInfestation(neighbors[i]) > severity)
                {
                    return false;
                }
            }

            return true;
        }

        private void RemoveAllInfestationMarkers()
        {
            if (Find.WorldObjects == null)
            {
                return;
            }

            List<WorldObject_ZombieInfestation> markers = Find.WorldObjects.AllWorldObjects
                .OfType<WorldObject_ZombieInfestation>()
                .ToList();
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null && !markers[i].Destroyed)
                {
                    markers[i].Destroy();
                }
            }
        }

        private void ReduceInfestation(PlanetTile tile, float amount)
        {
            float current;
            if (!infestationByTile.TryGetValue(tile, out current))
            {
                return;
            }

            current = Mathf.Max(0f, current - amount);
            if (current < 0.01f)
            {
                infestationByTile.Remove(tile);
            }
            else
            {
                infestationByTile[tile] = current;
            }
        }

        private void PruneInvalidTiles()
        {
            EnsureCollections();
            if (Find.World == null || Find.WorldGrid == null)
            {
                return;
            }

            List<PlanetTile> invalid = infestationByTile.Keys
                .Where(tile => !IsUsableSurfaceTile(tile))
                .ToList();
            for (int i = 0; i < invalid.Count; i++)
            {
                infestationByTile.Remove(invalid[i]);
            }
        }
    }
}
