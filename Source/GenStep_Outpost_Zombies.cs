using System;
using Verse;
using System.Collections.Generic;
using RimWorld;

namespace Zombiefied
{
    public class GenStep_Outpost_Zombies : GenStep
    {
        public override int SeedPart => 7;
        public override void Generate(Map map, GenStepParams parms)
        {
           List<Pawn> list = new List<Pawn>();
           foreach(Pawn pawn in map.mapPawns.AllPawns)
            {
                if(!pawn.NonHumanlikeOrWildMan())
                {
                    list.Add(pawn);
                }
            }
           foreach(Pawn pawn in list)
            {
                Thing zombie = GenSpawn.Spawn(ZombiefiedMod.GenerateZombieFromSource(pawn), pawn.Position, map, pawn.Rotation);
                pawn.Destroy();
            }
        }
    }
}
