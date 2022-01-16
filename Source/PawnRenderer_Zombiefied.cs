using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    // Token: 0x02000C3D RID: 3133
    public class PawnRenderer_Zombiefied
    {
        // Token: 0x060041F0 RID: 16880 RVA: 0x001E11B8 File Offset: 0x001DF5B8
        public PawnRenderer_Zombiefied(Pawn pawn, ZombieData data)
        {
            //this.oldPawn = oldPawn;
            this.pawn = pawn;
            this.wiggler = new PawnDownedWiggler(pawn);
            this.statusOverlays = new PawnHeadOverlays(pawn);
            this.woundOverlays = new PawnWoundDrawer(pawn);
            this.graphics = new ZombieGraphicSet(data);
            //this.graphics = gra;
            //this.effecters = new PawnStatusEffecters(pawn);
        }

        // Token: 0x17000A40 RID: 2624
        // (get) Token: 0x060041F1 RID: 16881 RVA: 0x001E120E File Offset: 0x001DF60E
        private RotDrawMode CurRotDrawMode
        {
            get
            {
                if (this.pawn.Dead && this.pawn.Corpse != null)
                {
                    return this.pawn.Corpse.CurRotDrawMode;
                }
                return RotDrawMode.Fresh;
            }
        }

        // Token: 0x060041F2 RID: 16882 RVA: 0x001E1242 File Offset: 0x001DF642
        public void RenderPawnAt(Vector3 drawLoc)
        {
            this.RenderPawnAt(drawLoc, this.CurRotDrawMode, !this.pawn.health.hediffSet.HasHead);
        }

        // Token: 0x060041F3 RID: 16883 RVA: 0x001E126C File Offset: 0x001DF66C
        public void RenderPawnAt(Vector3 drawLoc, RotDrawMode bodyDrawType, bool headStump)
        {
            if (!this.graphics.AllResolved)
            {
                this.graphics.ResolveAllGraphics();
            }
            if (this.pawn.GetPosture() == PawnPosture.Standing)
            {
                this.RenderPawnInternal(drawLoc, Quaternion.identity, true, bodyDrawType, headStump);
                if (this.pawn.carryTracker != null)
                {
                    Thing carriedThing = this.pawn.carryTracker.CarriedThing;
                    if (carriedThing != null)
                    {
                        Vector3 vector = drawLoc;
                        bool flag = false;
                        bool flip = false;
                        if (this.pawn.CurJob == null || !this.pawn.jobs.curDriver.ModifyCarriedThingDrawPos(ref vector, ref flag, ref flip))
                        {
                            if (carriedThing is Pawn || carriedThing is Corpse)
                            {
                                vector += new Vector3(0.44f, 0f, 0f);
                            }
                            else
                            {
                                vector += new Vector3(0.18f, 0f, 0.05f);
                            }
                        }
                        if (flag)
                        {
                            vector.y -= 0.0390625f;
                        }
                        else
                        {
                            vector.y += 0.0390625f;
                        }
                        carriedThing.DrawAt(vector, flip);
                    }
                }
                if (this.pawn.def.race.specialShadowData != null)
                {
                    if (this.shadowGraphic == null)
                    {
                        this.shadowGraphic = new Graphic_Shadow(pawn.def.race.specialShadowData);
                    }
                    this.shadowGraphic.Draw(drawLoc, Rot4.North, pawn, 0f);
                }
                if (this.graphics.nakedGraphic != null && this.graphics.nakedGraphic.ShadowGraphic != null)
                {
                    this.graphics.nakedGraphic.ShadowGraphic.Draw(drawLoc, Rot4.North, pawn, 0f);
                }
            }
            else
            {
                Rot4 rot = this.LayingFacing();
                Building_Bed building_Bed = this.pawn.CurrentBed();
                bool renderBody;
                Quaternion quat;
                Vector3 rootLoc;
                if (building_Bed != null && true)
                {
                    renderBody = building_Bed.def.building.bed_showSleeperBody;
                    Rot4 rotation = building_Bed.Rotation;
                    rotation.AsInt += 2;
                    quat = rotation.AsQuat;
                    AltitudeLayer altLayer = (AltitudeLayer)Mathf.Max((int)building_Bed.def.altitudeLayer, 15);
                    Vector3 vector2 = this.pawn.Position.ToVector3ShiftedWithAltitude(altLayer);
                    Vector3 vector3 = vector2;
                    vector3.y += 0.02734375f;
                    float d = -this.BaseHeadOffsetAt(Rot4.South).z;
                    Vector3 a = rotation.FacingCell.ToVector3();
                    rootLoc = vector2 + a * d;
                    rootLoc.y += 0.0078125f;
                }
                else
                {
                    renderBody = true;
                    rootLoc = drawLoc;
                    if (!this.pawn.Dead && this.pawn.CarriedBy == null)
                    {
                        rootLoc.y = Altitudes.AltitudeFor(AltitudeLayer.LayingPawn) + 0.0078125f;
                    }
                    if (this.pawn.Downed || this.pawn.Dead)
                    {
                        quat = Quaternion.AngleAxis(this.wiggler.downedAngle, Vector3.up);
                    }
                    else
                    {
                        quat = rot.AsQuat;
                    }
                }
                this.RenderPawnInternal(rootLoc, quat, renderBody, rot, rot, bodyDrawType, false, headStump);
            }
            if (this.pawn.Spawned && !this.pawn.Dead)
            {
                this.pawn.stances.StanceTrackerDraw();
                this.pawn.pather.PatherDraw();
            }
            this.DrawDebug();
        }

        // Token: 0x060041F4 RID: 16884 RVA: 0x001E1690 File Offset: 0x001DFA90
        public void RenderPortait()
        {
            Vector3 zero = Vector3.zero;
            Quaternion quat;
            if (this.pawn.Dead || this.pawn.Downed)
            {
                quat = Quaternion.Euler(0f, 85f, 0f);
                zero.x -= 0.18f;
                zero.z -= 0.18f;
            }
            else
            {
                quat = Quaternion.identity;
            }
            this.RenderPawnInternal(zero, quat, true, Rot4.South, Rot4.South, this.CurRotDrawMode, true, !this.pawn.health.hediffSet.HasHead);
        }

        // Token: 0x060041F5 RID: 16885 RVA: 0x001E173C File Offset: 0x001DFB3C
        private void RenderPawnInternal(Vector3 rootLoc, Quaternion quat, bool renderBody, RotDrawMode draw, bool headStump)
        {
            this.RenderPawnInternal(rootLoc, quat, renderBody, this.pawn.Rotation, this.pawn.Rotation, draw, false, headStump);
        }

        // Token: 0x060041F6 RID: 16886 RVA: 0x001E1770 File Offset: 0x001DFB70
        private void RenderPawnInternal(Vector3 rootLoc, Quaternion quat, bool renderBody, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, bool portrait, bool headStump)
        {
            if (!this.graphics.AllResolved)
            {
                this.graphics.ResolveAllGraphics();
            }
            Mesh mesh = null;
            if (renderBody)
            {
                Vector3 loc = rootLoc;
                loc.y += 0.0078125f;
                if (bodyDrawType == RotDrawMode.Dessicated && !true && this.graphics.dessicatedGraphic != null && !portrait)
                {
                    this.graphics.dessicatedGraphic.Draw(loc, bodyFacing, this.pawn, 0f);
                }
                else
                {
                    mesh = MeshPool.humanlikeBodySet.MeshAt(bodyFacing);
                    List<Material> list = this.graphics.MatsBodyBaseAt(bodyFacing, bodyDrawType);
                    for (int i = 0; i < list.Count; i++)
                    {
                        //Material damagedMat = this.graphics.flasher.GetDamagedMat(list[i]);
                        GenDraw.DrawMeshNowOrLater(mesh, loc, quat, list[i], portrait);
                        loc.y += 0.00390625f;
                    }
                    if (bodyDrawType == RotDrawMode.Fresh)
                    {
                        Vector3 drawLoc = rootLoc;
                        drawLoc.y += 0.01953125f;
                        this.woundOverlays.RenderOverBody(drawLoc, mesh, quat, portrait, BodyTypeDef.WoundLayer.Body, bodyFacing);
                    }
                }
            }
            Vector3 vector = rootLoc;
            Vector3 a = rootLoc;
            if (bodyFacing != Rot4.North)
            {
                a.y += 0.02734375f;
                vector.y += 0.0234375f;
            }
            else
            {
                a.y += 0.0234375f;
                vector.y += 0.02734375f;
            }
            if (this.graphics.headGraphic != null)
            {
                Vector3 b = quat * this.BaseHeadOffsetAt(headFacing);
                //b = new Vector3(b.x, 0f, b.y);
                Material material = this.graphics.HeadMatAt(headFacing, bodyDrawType, headStump);
                if (material != null)
                {
                    Mesh mesh2 = MeshPool.humanlikeHeadSet.MeshAt(headFacing);
                    GenDraw.DrawMeshNowOrLater(mesh2, a + b, quat, material, portrait);
                }
                Vector3 loc2 = rootLoc + b;
                loc2.y += 0.03125f;
                bool flag = false;
                if (!portrait || !Prefs.HatsOnlyOnMap)
                {
                    Mesh mesh3 = this.graphics.HairMeshSet.MeshAt(headFacing);
                    List<ApparelGraphicRecord> apparelGraphics = this.graphics.apparelGraphics;
                    for (int j = 0; j < apparelGraphics.Count; j++)
                    {
                        if (apparelGraphics[j].sourceApparel.def.apparel.LastLayer == ApparelLayerDefOf.Overhead)
                        {
                            if (!apparelGraphics[j].sourceApparel.def.apparel.hatRenderedFrontOfFace)
                            {
                                flag = true;
                                Material material2 = apparelGraphics[j].graphic.MatAt(bodyFacing, null);
                                //material2 = this.graphics.flasher.GetDamagedMat(material2);
                                GenDraw.DrawMeshNowOrLater(mesh3, loc2, quat, material2, portrait);
                            }
                            else
                            {
                                Material material3 = apparelGraphics[j].graphic.MatAt(bodyFacing, null);
                                //material3 = this.graphics.flasher.GetDamagedMat(material3);
                                Vector3 loc3 = rootLoc + b;
                                loc3.y += ((!(bodyFacing == Rot4.North)) ? 0.03515625f : 0.00390625f);
                                GenDraw.DrawMeshNowOrLater(mesh3, loc3, quat, material3, portrait);
                            }
                        }
                    }
                }
                if (!flag && bodyDrawType != RotDrawMode.Dessicated && !headStump)
                {
                    Mesh mesh4 = this.graphics.HairMeshSet.MeshAt(headFacing);
                    Material mat = this.graphics.HairMatAt(headFacing);
                    GenDraw.DrawMeshNowOrLater(mesh4, loc2, quat, mat, portrait);
                }
            }
            if (renderBody)
            {
                for (int k = 0; k < this.graphics.apparelGraphics.Count; k++)
                {
                    ApparelGraphicRecord apparelGraphicRecord = this.graphics.apparelGraphics[k];
                    if (apparelGraphicRecord.sourceApparel.def.apparel.LastLayer == ApparelLayerDefOf.Shell)
                    {
                        Material material4 = apparelGraphicRecord.graphic.MatAt(bodyFacing, null);
                        //material4 = this.graphics.flasher.GetDamagedMat(material4);
                        GenDraw.DrawMeshNowOrLater(mesh, vector, quat, material4, portrait);
                    }
                }
            }
            /*
            if (!portrait && this.oldPawn.RaceProps.Animal && this.oldPawn.inventory != null && this.oldPawn.inventory.innerContainer.Count > 0) && this.graphics.packGraphic != null)
            {
                Graphics.DrawMesh(mesh, vector, quat, this.graphics.packGraphic.MatAt(bodyFacing, null), 0);
            }
            */
            if (!portrait)
            {
                this.DrawEquipment(rootLoc);
                /*
                if (this.oldPawn.apparel != null)
                {
                    List<Apparel> wornApparel = this.oldPawn.apparel.WornApparel;
                    for (int l = 0; l < wornApparel.Count; l++)
                    {
                        wornApparel[l].DrawWornExtras();
                    }
                }
                */
                Vector3 bodyLoc = rootLoc;
                bodyLoc.y += 0.04296875f;
                this.statusOverlays.RenderStatusOverlays(bodyLoc, quat, MeshPool.humanlikeHeadSet.MeshAt(headFacing));
            }
        }

        // Token: 0x060041F7 RID: 16887 RVA: 0x001E1CE8 File Offset: 0x001E00E8
        private void DrawEquipment(Vector3 rootLoc)
        {
            if (this.pawn.Dead || !this.pawn.Spawned)
            {
                return;
            }
            if (this.pawn.equipment == null || this.pawn.equipment.Primary == null)
            {
                return;
            }
            if (this.pawn.CurJob != null && this.pawn.CurJob.def.neverShowWeapon)
            {
                return;
            }
            Stance_Busy stance_Busy = this.pawn.stances.curStance as Stance_Busy;
            if (stance_Busy != null && !stance_Busy.neverAimWeapon && stance_Busy.focusTarg.IsValid)
            {
                Vector3 a;
                if (stance_Busy.focusTarg.HasThing)
                {
                    a = stance_Busy.focusTarg.Thing.DrawPos;
                }
                else
                {
                    a = stance_Busy.focusTarg.Cell.ToVector3Shifted();
                }
                float num = 0f;
                if ((a - this.pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f)
                {
                    num = (a - this.pawn.DrawPos).AngleFlat();
                }
                Vector3 drawLoc = rootLoc + new Vector3(0f, 0f, 0.4f).RotatedBy(num);
                drawLoc.y += 0.0390625f;
                this.DrawEquipmentAiming(this.pawn.equipment.Primary, drawLoc, num);
            }
            else if (this.CarryWeaponOpenly())
            {
                if (this.pawn.Rotation == Rot4.South)
                {
                    Vector3 drawLoc2 = rootLoc + new Vector3(0f, 0f, -0.22f);
                    drawLoc2.y += 0.0390625f;
                    this.DrawEquipmentAiming(this.pawn.equipment.Primary, drawLoc2, 143f);
                }
                else if (this.pawn.Rotation == Rot4.North)
                {
                    Vector3 drawLoc3 = rootLoc + new Vector3(0f, 0f, -0.11f);
                    //drawLoc3.y = drawLoc3.y;
                    this.DrawEquipmentAiming(this.pawn.equipment.Primary, drawLoc3, 143f);
                }
                else if (this.pawn.Rotation == Rot4.East)
                {
                    Vector3 drawLoc4 = rootLoc + new Vector3(0.2f, 0f, -0.22f);
                    drawLoc4.y += 0.0390625f;
                    this.DrawEquipmentAiming(this.pawn.equipment.Primary, drawLoc4, 143f);
                }
                else if (this.pawn.Rotation == Rot4.West)
                {
                    Vector3 drawLoc5 = rootLoc + new Vector3(-0.2f, 0f, -0.22f);
                    drawLoc5.y += 0.0390625f;
                    this.DrawEquipmentAiming(this.pawn.equipment.Primary, drawLoc5, 217f);
                }
            }
        }

        // Token: 0x060041F8 RID: 16888 RVA: 0x001E2014 File Offset: 0x001E0414
        public void DrawEquipmentAiming(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            float num = aimAngle - 90f;
            Mesh mesh;
            if (aimAngle > 20f && aimAngle < 160f)
            {
                mesh = MeshPool.plane10;
                num += eq.def.equippedAngleOffset;
            }
            else if (aimAngle > 200f && aimAngle < 340f)
            {
                mesh = MeshPool.plane10Flip;
                num -= 180f;
                num -= eq.def.equippedAngleOffset;
            }
            else
            {
                mesh = MeshPool.plane10;
                num += eq.def.equippedAngleOffset;
            }
            num %= 360f;
            Graphic_StackCount graphic_StackCount = eq.Graphic as Graphic_StackCount;
            Material matSingle;
            if (graphic_StackCount != null)
            {
                matSingle = graphic_StackCount.SubGraphicForStackCount(1, eq.def).MatSingle;
            }
            else
            {
                matSingle = eq.Graphic.MatSingle;
            }
            Graphics.DrawMesh(mesh, drawLoc, Quaternion.AngleAxis(num, Vector3.up), matSingle, 0);
        }

        // Token: 0x060041F9 RID: 16889 RVA: 0x001E20FC File Offset: 0x001E04FC
        private bool CarryWeaponOpenly()
        {
            return (this.pawn.carryTracker == null || this.pawn.carryTracker.CarriedThing == null) && (this.pawn.Drafted || (this.pawn.CurJob != null && this.pawn.CurJob.def.alwaysShowWeapon) || (this.pawn.mindState.duty != null && this.pawn.mindState.duty.def.alwaysShowWeapon));
        }

        // Token: 0x060041FA RID: 16890 RVA: 0x001E21A8 File Offset: 0x001E05A8
        private Rot4 LayingFacing()
        {
            if (this.pawn.GetPosture() == PawnPosture.LayingOnGroundFaceUp)
            {
                return Rot4.South;
            }
            switch (this.pawn.thingIDNumber % 4)
            {
                case 0:
                    return Rot4.South;
                case 1:
                    return Rot4.South;
                case 2:
                    return Rot4.East;
                case 3:
                    return Rot4.West;
            }
            return Rot4.East;
        }

        // Token: 0x060041FB RID: 16891 RVA: 0x001E2270 File Offset: 0x001E0670
        public Vector3 BaseHeadOffsetAt(Rot4 rotation)
        {
            float num = PawnRenderer_Zombiefied.HorHeadOffsets[(int)graphics.data.bodyType.index];
            switch (rotation.AsInt)
            {
                case 0:
                    return new Vector3(0f, 0f, 0.34f);
                case 1:
                    return new Vector3(num, 0f, 0.34f);
                case 2:
                    return new Vector3(0f, 0f, 0.34f);
                case 3:
                    return new Vector3(-num, 0f, 0.34f);
                default:
                    Log.Error("BaseHeadOffsetAt error in " + this.pawn);
                    return Vector3.zero;
            }
        }

        // Token: 0x060041FC RID: 16892 RVA: 0x001E231E File Offset: 0x001E071E
        public void Notify_DamageApplied(DamageInfo dam)
        {
            //this.graphics.flasher.Notify_DamageApplied(dam);
            this.wiggler.Notify_DamageApplied(dam);
        }

        // Token: 0x060041FD RID: 16893 RVA: 0x001E233D File Offset: 0x001E073D
        public void RendererTick()
        {
            this.wiggler.WigglerTick();
            //this.effecters.EffectersTick();
        }

        // Token: 0x060041FE RID: 16894 RVA: 0x001E2358 File Offset: 0x001E0758
        private void DrawDebug()
        {
            if (DebugViewSettings.drawDuties && Find.Selector.IsSelected(this.pawn) && this.pawn.mindState != null && this.pawn.mindState.duty != null)
            {
                //this.pawn.mindState.duty.DrawDebug(this.pawn);
            }
        }

        //private Pawn oldPawn;

        // Token: 0x04002DCF RID: 11727
        private Pawn pawn;

        // Token: 0x04002DD0 RID: 11728
        public ZombieGraphicSet graphics;

        // Token: 0x04002DD1 RID: 11729
        public PawnDownedWiggler wiggler;

        // Token: 0x04002DD2 RID: 11730
        private PawnHeadOverlays statusOverlays;

        // Token: 0x04002DD3 RID: 11731
        //private PawnStatusEffecters effecters;

        // Token: 0x04002DD4 RID: 11732
        private PawnWoundDrawer woundOverlays;

        // Token: 0x04002DD5 RID: 11733
        private Graphic_Shadow shadowGraphic;

        // Token: 0x04002DD6 RID: 11734
        private const float CarriedThingDrawAngle = 16f;

        // Token: 0x04002DD7 RID: 11735
        private const float SubInterval = 0.00390625f;

        // Token: 0x04002DD8 RID: 11736
        private const float YOffset_PrimaryEquipmentUnder = 0f;

        // Token: 0x04002DD9 RID: 11737
        private const float YOffset_Behind = 0.00390625f;

        // Token: 0x04002DDA RID: 11738
        private const float YOffset_Body = 0.0078125f;

        // Token: 0x04002DDB RID: 11739
        private const float YOffsetInterval_Clothes = 0.00390625f;

        // Token: 0x04002DDC RID: 11740
        private const float YOffset_Wounds = 0.01953125f;

        // Token: 0x04002DDD RID: 11741
        private const float YOffset_Shell = 0.0234375f;

        // Token: 0x04002DDE RID: 11742
        private const float YOffset_Head = 0.02734375f;

        // Token: 0x04002DDF RID: 11743
        private const float YOffset_OnHead = 0.03125f;

        // Token: 0x04002DE0 RID: 11744
        private const float YOffset_PostHead = 0.03515625f;

        // Token: 0x04002DE1 RID: 11745
        private const float YOffset_CarriedThing = 0.0390625f;

        // Token: 0x04002DE2 RID: 11746
        private const float YOffset_PrimaryEquipmentOver = 0.0390625f;

        // Token: 0x04002DE3 RID: 11747
        private const float YOffset_Status = 0.04296875f;

        // Token: 0x04002DE4 RID: 11748
        private const float UpHeadOffset = 0.34f;

        // Token: 0x04002DE5 RID: 11749
        private static readonly float[] HorHeadOffsets = new float[]
        {
            0f,
            0.04f,
            0.1f,
            0.09f,
            0.1f,
            0.09f
        };
    }
}

