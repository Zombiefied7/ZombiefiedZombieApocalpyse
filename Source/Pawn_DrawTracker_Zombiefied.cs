using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    // Token: 0x02000C35 RID: 3125
    public class Pawn_DrawTracker_Zombiefied
    {
        // Token: 0x060041CE RID: 16846 RVA: 0x001E04F8 File Offset: 0x001DE8F8
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

        // Token: 0x17000A39 RID: 2617
        // (get) Token: 0x060041CF RID: 16847 RVA: 0x001E0568 File Offset: 0x001DE968
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

        // Token: 0x060041D0 RID: 16848 RVA: 0x001E05C8 File Offset: 0x001DE9C8
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

        // Token: 0x060041D1 RID: 16849 RVA: 0x001E0654 File Offset: 0x001DEA54
        public void DrawAt(Vector3 loc)
        {
            this.renderer.RenderPawnAt(loc);
        }

        // Token: 0x060041D2 RID: 16850 RVA: 0x001E0662 File Offset: 0x001DEA62
        public void Notify_Spawned()
        {
            this.tweener.ResetTweenedPosToRoot();
        }

        // Token: 0x060041D3 RID: 16851 RVA: 0x001E066F File Offset: 0x001DEA6F
        public void Notify_WarmingCastAlongLine(ShootLine newShootLine, IntVec3 ShootPosition)
        {
            this.leaner.Notify_WarmingCastAlongLine(newShootLine, ShootPosition);
        }

        // Token: 0x060041D4 RID: 16852 RVA: 0x001E067E File Offset: 0x001DEA7E
        public void Notify_DamageApplied(DamageInfo dinfo)
        {
            if (this.pawn.Destroyed)
            {
                return;
            }
            this.jitterer.Notify_DamageApplied(dinfo);
            this.renderer.Notify_DamageApplied(dinfo);
        }

        // Token: 0x060041D5 RID: 16853 RVA: 0x001E06AC File Offset: 0x001DEAAC
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

        // Token: 0x060041D6 RID: 16854 RVA: 0x001E0750 File Offset: 0x001DEB50
        public void Notify_DebugAffected()
        {
            for (int i = 0; i < 10; i++)
            {
                MoteMaker.ThrowText(this.pawn.DrawPos, this.pawn.Map, "zombiefied");
            }
            this.jitterer.AddOffset(0.05f, (float)Rand.RangeSeeded(0, 360, Find.TickManager.TicksAbs));
        }

        // Token: 0x04002DA2 RID: 11682
        private Pawn pawn;

        // Token: 0x04002DA3 RID: 11683
        public PawnTweener tweener;

        // Token: 0x04002DA4 RID: 11684
        private JitterHandler jitterer;

        // Token: 0x04002DA5 RID: 11685
        public PawnLeaner leaner;

        // Token: 0x04002DA6 RID: 11686
        public PawnRenderer_Zombiefied renderer;

        // Token: 0x04002DA7 RID: 11687
        public PawnUIOverlay ui;

        // Token: 0x04002DA8 RID: 11688
        private PawnFootprintMaker footprintMaker;

        // Token: 0x04002DA9 RID: 11689
        private PawnBreathMoteMaker breathMoteMaker;

        // Token: 0x04002DAA RID: 11690
        private const float MeleeJitterDistance = 0.5f;
    }
}
