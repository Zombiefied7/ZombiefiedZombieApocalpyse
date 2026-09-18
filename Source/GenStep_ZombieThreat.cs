using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Zombiefied
{
    public class GenStep_ZombieThreat : GenStep
    {
        private const float DefaultThreatPoints = 350f;
        private const float MinimumThreatPoints = 120f;

        public override int SeedPart => 178325941;

        public override void Generate(Map map, GenStepParams parms)
        {
            if (map == null)
            {
                return;
            }

            float threatPoints = parms.sitePart != null
                ? Math.Max(MinimumThreatPoints, parms.sitePart.parms.threatPoints)
                : DefaultThreatPoints;

            List<Pawn> zombies = ZombieWorldUtility.GenerateZombieEncounterPawns(threatPoints);
            if (zombies.Count == 0)
            {
                Log.Error("Zombiefied could not generate zombies for a zombie-infested world site.");
                return;
            }

            CellRect rectToDefend;
            IntVec3 singleCellToSpawnNear;
            if (!SiteGenStepUtility.TryFindRootToSpawnAroundRectOfInterest(out rectToDefend, out singleCellToSpawnNear, map))
            {
                DestroyUnspawnedPawns(zombies, 0);
                Log.Warning("Zombiefied could not find a valid area for the zombies at a world site.");
                return;
            }

            for (int i = 0; i < zombies.Count; i++)
            {
                Pawn pawn = zombies[i];
                if (pawn == null || pawn.Destroyed)
                {
                    continue;
                }

                IntVec3 spawnCell;
                if (!SiteGenStepUtility.TryFindSpawnCellAroundOrNear(rectToDefend, singleCellToSpawnNear, map, out spawnCell))
                {
                    DestroyUnspawnedPawns(zombies, i);
                    Log.Warning("Zombiefied ran out of valid spawn cells while generating a zombie-infested world site.");
                    break;
                }

                Pawn_Zombiefied zombie = GenSpawn.Spawn(pawn, spawnCell, map, Rot4.Random, WipeMode.Vanish, false) as Pawn_Zombiefied;
                if (zombie != null)
                {
                    zombie.FixZombie();
                }
            }
        }

        private static void DestroyUnspawnedPawns(List<Pawn> pawns, int startIndex)
        {
            for (int i = startIndex; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && !pawn.Destroyed && !pawn.Spawned)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }
            }
        }
    }
}
