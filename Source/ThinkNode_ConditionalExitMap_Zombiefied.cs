using System;
using Verse;
using Verse.AI;
using RimWorld;

namespace Zombiefied
{
    // Token: 0x020001E0 RID: 480
    public class ThinkNode_ConditionalExitMap_Zombiefied : ThinkNode_Conditional
    {
        // Token: 0x0600098D RID: 2445 RVA: 0x000579F4 File Offset: 0x00055DF4
        protected override bool Satisfied(Pawn pawn)
        {
            if(ZombiefiedMod.zombieAmountsPerMap != null && ZombiefiedMod.zombieAmountsPerMap.Count > pawn.Map.Index)
            {
                if (ZombiefiedMod.zombieAmountsPerMap[pawn.Map.Index] > ZombiefiedMod.zombieAmountSoftCap + 7)
                {
                    if (Rand.RangeSeeded(0, 333, (Find.TickManager.TicksAbs + pawn.thingIDNumber)) == 7)
                    {
                        return true;
                    }
                }
            }       
            return false;
        }
    }
}
