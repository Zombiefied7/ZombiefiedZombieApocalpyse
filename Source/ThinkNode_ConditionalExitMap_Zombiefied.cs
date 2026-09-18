using RimWorld;
using Verse;
using Verse.AI;

namespace Zombiefied
{
    public class ThinkNode_ConditionalExitMap_Zombiefied : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
            {
                return false;
            }

            ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(pawn.Map);
            if (tracker == null || tracker.ZombieCount <= ZombiefiedMod.zombieAmountSoftCap + 7)
            {
                return false;
            }

            if (pawn.Name is NameSingle)
            {
                return false;
            }

            return Rand.RangeSeeded(
                0,
                (int)(333 * pawn.BodySize),
                Find.TickManager.TicksAbs + pawn.thingIDNumber) == 7;
        }
    }
}
