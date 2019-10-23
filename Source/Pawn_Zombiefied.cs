using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class Pawn_Zombiefied : Pawn
    {
        public bool hunting = false;
        public bool attracted = false;
        public bool fired = false;

        public Pawn_Zombiefied() : base()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look<ZombieData>(ref this.dataZ, "zombieData", new object[0]);
        }

        public void newGraphics(ZombieData data)
        {
            drawerZ = new Pawn_DrawTracker_Zombiefied(this, data);
        }

        public void newGraphics(Pawn pawn)
        {
            if (pawn.RaceProps.Humanlike)
            {
                GenerateAgeFromSource(pawn, this);
                dataZ = new ZombieData(pawn, "Map/Cutout");

                newGraphics(dataZ);
            }
        }

        public void newGraphics()
        {
            if (dataZ != null)
            {
                newGraphics(dataZ);
            }
            else
            {
                List<PawnKindDef> kindDefs = new List<PawnKindDef>();
                foreach (PawnKindDef def in DefDatabase<PawnKindDef>.AllDefsListForReading)
                {
                    if (def.defName.Contains("Drifter"))
                    {
                        kindDefs.Add(def);
                    }
                }

                PawnKindDef kDef = kindDefs[(int)(Rand.RangeSeeded(0f, 1f, Find.TickManager.TicksAbs) * kindDefs.Count)];
                PawnGenerationRequest req = new PawnGenerationRequest(kDef, Faction.OfAncients, PawnGenerationContext.NonPlayer, -1, true, false, false, true, false, false, 1f, false, true, true, false, false, false, false, null, null, null, null, null, null, null);
                Pawn human = PawnGenerator.GeneratePawn(req);

                //Thing t = GenSpawn.Spawn(human, this.Position, this.Map);

                copyInjuries(human);

                newGraphics(human);
            }
        }

        public void copyInjuries(Pawn sourcePawn)
        {
            if (this.def.race.body == sourcePawn.def.race.body)
            {
                for (int i = sourcePawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    Hediff hediff = sourcePawn.health.hediffSet.hediffs[i];

                    Hediff_Injury injury = hediff as Hediff_Injury;
                    Hediff_AddedPart added = hediff as Hediff_AddedPart;
                    Hediff_MissingPart missing = hediff as Hediff_MissingPart;
                    if (hediff is Hediff_Injury && injury != null && !health.WouldDieAfterAddingHediff(injury) && !health.hediffSet.PartIsMissing(injury.Part.parent) && !health.hediffSet.PartIsMissing(injury.Part))
                    {
                        //float oldChance = injury.def.chanceToCauseNoPain;
                        //injury.def.chanceToCauseNoPain = 70000f;

                        injury.Severity = injury.Severity * 0.5f;
                        injury.PostMake();
                        injury.ageTicks = 70000000;
                        health.AddHediff(injury, injury.Part);

                        //injury.def.chanceToCauseNoPain = oldChance;
                    }
                    else if (hediff is Hediff_MissingPart && missing != null && !health.hediffSet.PartIsMissing(missing.Part.parent) && !health.hediffSet.PartIsMissing(missing.Part))
                    {
                        bool foundMoving = false;
                        for (int i1 = 0; i1 < missing.Part.def.tags.Count; i1++)
                        {
                            if (missing.Part.def.tags[i1].defName.Contains("Moving"))
                            {
                                foundMoving = true;
                            }
                        }

                        if (!foundMoving)
                        {
                            health.AddHediff(missing.def, missing.Part);
                        }
                        else if (!sourcePawn.health.hediffSet.PartIsMissing(missing.Part.parent))
                        {
                            Hediff nHediff = HediffMaker.MakeHediff(HediffDefOf.Shredded, this, null);
                            nHediff.Severity = 3.7f;

                            //float oldChance = nHediff.def.chanceToCauseNoPain;
                            //nHediff.def.chanceToCauseNoPain = 70000f;

                            nHediff.PostMake();
                            nHediff.ageTicks = 70000000;
                            health.AddHediff(nHediff, missing.Part);

                            //nHediff.def.chanceToCauseNoPain = oldChance;
                        }
                    }
                    else if (hediff is Hediff_AddedPart && added != null && !health.hediffSet.PartIsMissing(added.Part.parent) && !health.hediffSet.PartIsMissing(added.Part))
                    {
                        health.AddHediff(added.def, added.Part);
                    }
                }
            }
            RemoveApparel();
        }

        public void RemoveApparel()
        {
            health.AddHediff(HediffDef.Named("Zombiefied"));
            //remove apparel
            if (apparel != null)
            {
                apparel.DestroyAll();
            }
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (def.defName != "Zombie")
            {
                base.DrawAt(drawLoc, flip);
            }
            else if (drawerZ != null)
            {
                this.drawerZ.DrawAt(drawLoc);
                //this.renderer.Render(graphics, drawLoc, Quaternion.identity, true, base.Rotation, base.Rotation, RotDrawMode.Fresh, false, false, 1f);
            }
            else
            {
                newGraphics();
            }
        }

        public ZombieData dataZ;
        public Pawn_DrawTracker_Zombiefied drawerZ;

        private static void GenerateAgeFromSource(Pawn sourcePawn, Pawn pawn)
        {
            pawn.ageTracker.AgeBiologicalTicks = sourcePawn.ageTracker.AgeBiologicalTicks;
            pawn.ageTracker.BirthAbsTicks = sourcePawn.ageTracker.BirthAbsTicks;
            pawn.ageTracker.AgeChronologicalTicks = sourcePawn.ageTracker.AgeChronologicalTicks;
        }


        //Tick + TickRare overrides for better performance
        public override void TickRare()
        {
            //if (Find.TickManager.TicksGame + thingIDNumber % 7777 == 0)
            //{
            //    base.TickRare();
            //}
            //else
            //{
                if (AllComps != null)
                {
                    int i = 0;
                    int count = AllComps.Count;
                    while (i < count)
                    {
                        AllComps[i].CompTickRare();
                        i++;
                    }
                }
                //base.TickRare();

                //if (!base.Suspended)
                //{
                //    if (this.apparel != null)
                //    {
                //        this.apparel.ApparelTrackerTickRare();
                //    }
                //    this.inventory.InventoryTrackerTickRare();
                //}
                //if (this.training != null)
                //{
                //    this.training.TrainingTrackerTickRare();
                //}
                //if (base.Spawned && this.RaceProps.IsFlesh)
                //{
                //    GenTemperature.PushHeat(this, 0.3f * this.BodySize * 4.16666651f * ((!this.def.race.Humanlike) ? 0.6f : 1f));
                //}
            //}
        }

        public override void Tick()
        {
            //if (Find.TickManager.TicksGame + thingIDNumber % 7777 == 0)
            //{
            //    base.Tick();
            //}
            //else
            //{
                if (AllComps != null)
                {
                    int i = 0;
                    int count = AllComps.Count;
                    while (i < count)
                    {
                        AllComps[i].CompTick();
                        i++;
                    }
                }
                //base.Tick();

                if (Find.TickManager.TicksGame % 250 == 0)
                {
                    this.TickRare();
                }
                bool suspended = base.Suspended;
                if (!suspended)
                {
                    if (base.Spawned)
                    {
                        this.pather.PatherTick();
                    }
                    if (base.Spawned)
                    {
                        this.stances.StanceTrackerTick();
                        this.verbTracker.VerbsTick();
                        this.natives.NativeVerbsTick();
                    }
                    if (base.Spawned)
                    {
                        this.jobs.JobTrackerTick();
                    }
                    if (base.Spawned)
                    {
                        this.Drawer.DrawTrackerTick();
                        this.rotationTracker.RotationTrackerTick();
                    }
                    //this.health.HealthTick();
                    if (!this.Dead)
                    {
                        this.mindState.MindStateTick();
                        this.carryTracker.CarryHandsTick();
                    }
                }
                if (!suspended)
                {

                    //if (this.equipment != null)
                    //{
                    //    this.equipment.EquipmentTrackerTick();
                    //}
                    //if (this.apparel != null)
                    //{
                    //    this.apparel.ApparelTrackerTick();
                    //}
                    if (this.interactions != null && base.Spawned)
                    {
                        this.interactions.InteractionsTrackerTick();
                    }
                    if (this.caller != null)
                    {
                        this.caller.CallTrackerTick();
                    }
                    //if (this.skills != null)
                    //{
                    //    this.skills.SkillsTick();
                    //}
                    //if (this.inventory != null)
                    //{
                    //    this.inventory.InventoryTrackerTick();
                    //}
                    //if (this.drafter != null)
                    //{
                    //    this.drafter.DraftControllerTick();
                    //}
                    //if (this.relations != null)
                    //{
                    //    this.relations.RelationsTrackerTick();
                    //}
                    //if (this.RaceProps.Humanlike)
                    //{
                    //    this.guest.GuestTrackerTick();
                    //}
                    this.ageTracker.AgeTick();
                    this.records.RecordsTick();
                }
            //}
        }
    }
}
