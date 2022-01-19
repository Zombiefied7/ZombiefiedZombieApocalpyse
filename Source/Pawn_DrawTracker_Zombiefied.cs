﻿using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class Pawn_DrawTracker_Zombiefied
    {
        public Pawn_DrawTracker_Zombiefied(Pawn pawn, ZombieData data)
        {
            this.pawn = pawn;
            this.tweener = new PawnTweener(pawn);
            this.jitterer = new JitterHandler();
            this.leaner = new PawnLeaner(pawn);
            this.renderer = new PawnRenderer_Zombiefied(pawn, data);
            this.ui = new PawnUIOverlay(pawn);
            this.footprintMaker = new PawnFootprintMaker(pawn);
            this.breathMoteMaker = new PawnBreathMoteMaker(pawn);
        }
        public Vector3 DrawPos
        {
            get
            {
                this.tweener.PreDrawPosCalculation();
                Vector3 vector = this.tweener.TweenedPos;
                vector += this.jitterer.CurrentOffset;
                vector += this.leaner.LeanOffset;
                vector.y = this.pawn.def.Altitude;
                return vector;
            }
        }
        public void DrawTrackerTick()
        {
            if (!this.pawn.Spawned)
            {
                return;
            }
            if (Current.ProgramState == ProgramState.Playing && !Find.CameraDriver.CurrentViewRect.ExpandedBy(3).Contains(this.pawn.Position))
            {
                return;
            }
            this.jitterer.JitterHandlerTick();
            this.footprintMaker.FootprintMakerTick();
            this.breathMoteMaker.BreathMoteMakerTick();
            this.leaner.LeanerTick();
            this.renderer.RendererTick();
        }
        public void DrawAt(Vector3 loc)
        {
            this.renderer.RenderPawnAt(loc);
        }
        public void Notify_Spawned()
        {
            this.tweener.ResetTweenedPosToRoot();
        }
        public void Notify_WarmingCastAlongLine(ShootLine newShootLine, IntVec3 ShootPosition)
        {
            this.leaner.Notify_WarmingCastAlongLine(newShootLine, ShootPosition);
        }
        public void Notify_DamageApplied(DamageInfo dinfo)
        {
            if (this.pawn.Destroyed)
            {
                return;
            }
            this.jitterer.Notify_DamageApplied(dinfo);
            this.renderer.Notify_DamageApplied(dinfo);
        }
        public void Notify_MeleeAttackOn(Thing Target)
        {
            if (Target.Position != this.pawn.Position)
            {
                this.jitterer.AddOffset(0.5f, (Target.Position - this.pawn.Position).AngleFlat);
            }
            else if (Target.DrawPos != this.pawn.DrawPos)
            {
                this.jitterer.AddOffset(0.25f, (Target.DrawPos - this.pawn.DrawPos).AngleFlat());
            }
        }
        public void Notify_DebugAffected()
        {
            for (int i = 0; i < 10; i++)
            {
                MoteMaker.ThrowText(this.pawn.DrawPos, this.pawn.Map, "zombiefied");
            }
            this.jitterer.AddOffset(0.05f, (float)Rand.RangeSeeded(0, 360, Find.TickManager.TicksAbs));
        }
        private Pawn pawn;
        public PawnTweener tweener;
        private JitterHandler jitterer;
        public PawnLeaner leaner;
        public PawnRenderer_Zombiefied renderer;
        public PawnUIOverlay ui;
        private PawnFootprintMaker footprintMaker;
        private PawnBreathMoteMaker breathMoteMaker;
        private const float MeleeJitterDistance = 0.5f;
    }
}
