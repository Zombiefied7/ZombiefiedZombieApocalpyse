using System;
using Verse;
using Verse.AI;
using RimWorld;

namespace Zombiefied
{
    public class ThinkNode_ConditionalCanDoConstantThinkTreeJobNow_Zombiefied : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            return !pawn.Downed && !pawn.InMentalState && !pawn.Drafted && pawn.Awake();
        }
    }
}
