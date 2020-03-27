using System;
using Verse;
using System.Collections.Generic;
using RimWorld;

namespace Zombiefied
{
    // Token: 0x020003D1 RID: 977
    public class GenStep_Outpost_Zombies : GenStep
    {
        public override int SeedPart => 7;

        // Token: 0x06001042 RID: 4162 RVA: 0x0007BEDC File Offset: 0x0007A2DC
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
                //pawn.Kill(new DamageInfo(DamageDefOf.Bite, 7000));
                Thing zombie = GenSpawn.Spawn(ZombiefiedMod.GenerateZombieFromSource(pawn), pawn.Position, map, pawn.Rotation);
                pawn.Destroy();
                //ZombiefiedMod.ReanimateDeath(pawn.Corpse);
            }
        }
    }
}
