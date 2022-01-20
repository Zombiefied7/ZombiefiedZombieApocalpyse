using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace Zombiefied
{
    // Token: 0x02000030 RID: 48
    public class JobDriver_ZombieHunt : JobDriver
    {
        private int numMeleeAttacksMade;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.numMeleeAttacksMade, "numMeleeAttacksMade", 0, false);
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Pawn_Zombiefied pawn = this.pawn as Pawn_Zombiefied;
            if (!pawn.CanReach(this.TargetA, false))
            {
                Toil blockerToil = GotoBlockers();
                if (blockerToil != null)
                    yield return blockerToil;
            }
            // Hit the target.
            yield return HitThings();
        }
        public Toil GotoBlockers()
        {
            Thing thing;
            using (PawnPath pawnPath = base.Map.pathFinder.FindPath(this.pawn.Position, base.TargetA.Cell, TraverseParms.For(this.pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings, false, false, false), PathEndMode.Touch, null))
            {
                if (!pawnPath.Found)
                {
                    this.EndJobWith(JobCondition.Incompletable);
                    return null;
                }
                thing = pawnPath.FirstBlockingBuilding(out _, this.pawn);
            }
            if (thing != null)
            {
                this.job.targetA = thing;
                return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            }
            return null;
        }
        public Toil HitThings()
        {
            return Toils_Combat.FollowAndMeleeAttack(TargetIndex.A, delegate
            {
                Thing thing = this.job.GetTarget(TargetIndex.A).Thing;
                if (this.pawn.meleeVerbs.TryMeleeAttack(thing, this.job.verbToUse, false))
                {
                    if (this.pawn.CurJob == null || this.pawn.jobs.curDriver != this) return;
                    this.numMeleeAttacksMade++;
                    if (this.numMeleeAttacksMade >= this.job.maxNumMeleeAttacks)
                    {
                        this.EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                }
            }).FailOnDespawnedOrNull(TargetIndex.A);
        }
        public override void Notify_PatherFailed()
        {
            this.EndJobWith(JobCondition.ErroredPather);
            return;
        }

        public override bool IsContinuation(Job j)
        {
            return this.job.GetTarget(TargetIndex.A) == j.GetTarget(TargetIndex.A);
        }
    }
}
