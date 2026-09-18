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
            yield return Toils_Combat.FollowAndMeleeAttack(TargetIndex.A, delegate
            {
                Thing thing = this.job.GetTarget(TargetIndex.A).Thing;
                if (this.pawn.meleeVerbs.TryMeleeAttack(thing, this.job.verbToUse, false))
                {
                    if (this.pawn.CurJob == null || this.pawn.jobs.curDriver != this)
                    {
                        return;
                    }
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
            // RimWorld 1.6 pathfinding is request-driven and no longer exposes synchronous PathFinder.FindPath.
            // Let JobDriver perform the standard failure handling rather than probing the pathfinder directly.
            base.Notify_PatherFailed();
        }

        public override bool IsContinuation(Job j)
        {
            return this.job.GetTarget(TargetIndex.A) == j.GetTarget(TargetIndex.A);
        }
    }
}