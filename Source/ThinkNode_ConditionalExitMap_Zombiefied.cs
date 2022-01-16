using System;
using Verse;
using Verse.AI;
using RimWorld;

namespace Zombiefied
{
    public class ThinkNode_ConditionalExitMap_Zombiefied : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if(ZombiefiedMod.zombieAmountsPerMap != null && ZombiefiedMod.zombieAmountsPerMap.Count > pawn.Map.Index)
            {
                if (ZombiefiedMod.zombieAmountsPerMap[pawn.Map.Index] > ZombiefiedMod.zombieAmountSoftCap + 7)
                {
                    if(!(pawn.Name is NameSingle))
                    {
                        if (Rand.RangeSeeded(0, (int)(333 * pawn.BodySize), (Find.TickManager.TicksAbs + pawn.thingIDNumber)) == 7)
                        {
                            return true;
                        }
                    }                  
                }
            }       
            return false;
        }
    }
}
