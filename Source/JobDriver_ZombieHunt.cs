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
            Log.Message("ExposeData");
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.numMeleeAttacksMade, "numMeleeAttacksMade", 0, false);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Log.Message("TryMakePreToilReservations");
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Log.Message("Making new Toils");
            //yield return Toils_Interact.DestroyThing(TargetIndex.A);
            yield return Toils_Combat.FollowAndMeleeAttack(TargetIndex.A, delegate
            {
                Thing thing = this.job.GetTarget(TargetIndex.A).Thing;
                Log.Message("Attacking " + thing);
                if (this.pawn.meleeVerbs.TryMeleeAttack(thing, this.job.verbToUse, false))
                {
                    Log.Message("Hit?");
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
                } else
                {
                    Log.Message("Fail!?");
                }
            }).FailOnDespawnedOrNull(TargetIndex.A);
        }

        public override void Notify_PatherFailed()
        {
            if (true)
            {
                Log.Message("Pather failed");
                Thing thing;
                using (PawnPath pawnPath = base.Map.pathFinder.FindPath(this.pawn.Position, base.TargetA.Cell, TraverseParms.For(this.pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThingsNotWater, false), PathEndMode.ClosestTouch))
                {
                    if (!pawnPath.Found)
                    {
                        Log.Message("Couldn't get new path");
                        return;
                    }
                    IntVec3 intVec;
                    thing = pawnPath.FirstBlockingBuilding(out intVec, this.pawn);
                }
                Log.Message("Thing " + thing.ToString());
                if (thing != null)
                {
                    this.job.targetA = thing;
                    this.job.maxNumMeleeAttacks = Rand.RangeSeeded(2, 7, Find.TickManager.TicksAbs + pawn.thingIDNumber);
                    this.job.expiryInterval = Rand.RangeSeeded(2000, 4000, Find.TickManager.TicksAbs + pawn.thingIDNumber);
                    return;
                }
            }
            base.Notify_PatherFailed();
        }

        public override bool IsContinuation(Job j)
        {
            return this.job.GetTarget(TargetIndex.A) == j.GetTarget(TargetIndex.A);
        }
    }
}