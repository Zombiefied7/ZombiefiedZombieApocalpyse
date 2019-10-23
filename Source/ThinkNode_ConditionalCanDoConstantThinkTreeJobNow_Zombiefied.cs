using System;
using Verse;
using Verse.AI;
using RimWorld;

namespace Zombiefied
{
    // Token: 0x020001E0 RID: 480
    public class ThinkNode_ConditionalCanDoConstantThinkTreeJobNow_Zombiefied : ThinkNode_Conditional
    {
        // Token: 0x0600098D RID: 2445 RVA: 0x000579F4 File Offset: 0x00055DF4
        protected override bool Satisfied(Pawn pawn)
        {
            return !pawn.Downed && !pawn.InMentalState && !pawn.Drafted && pawn.Awake();
        }
    }
}
