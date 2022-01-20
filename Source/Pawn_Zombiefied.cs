using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Zombiefied
{
    public class Pawn_Zombiefied : Pawn
    {
        public bool hunting = false;
        public bool attracted = false;
        public bool fired = false;
        public int distanceToEdge = 0;

        public float armorRating_Sharp = 0f;
        public float armorRating_Blunt = 0f;
        public float armorRating_Heat = 0f;

        public Pawn_Zombiefied() : base()
        {
        }
        public bool CanReach(LocalTargetInfo target, bool throughWalls)
        {
            Pawn self = this as Pawn;
            return self.CanReach(target, PathEndMode.ClosestTouch, Danger.Deadly, false, false, (throughWalls)?TraverseMode.PassAllDestroyableThings:TraverseMode.NoPassClosedDoors);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look<ZombieData>(ref this.dataZ, "zombieData", new object[0]);
            Scribe_Values.Look<float>(ref this.armorRating_Sharp, "armorRating_Sharp");
            Scribe_Values.Look<float>(ref this.armorRating_Blunt, "armorRating_Blunt");
            Scribe_Values.Look<float>(ref this.armorRating_Heat, "armorRating_Heat");
        }

        public void newGraphics(ZombieData data)
        {
            drawerZ = new Pawn_DrawTracker_Zombiefied(this, data);
        }

        public void newGraphics(Pawn pawn)
        {
            if (pawn.RaceProps.Humanlike)
            {
                dataZ = new ZombieData(pawn);

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
 // Predicate<Pawn> validatorPreGear = null, Predicate<Pawn> validatorPostGear = null, IEnumerable<TraitDef> forcedTraits = null, IEnumerable<TraitDef> prohibitedTraits = null, float? minChanceToRedressWorldPawn = null, float? fixedBiologicalAge = null, float? fixedChronologicalAge = null, Gender? fixedGender = null, float? fixedMelanin = null, string fixedLastName = null, string fixedBirthName = null, RoyalTitleDef fixedTitle = null, Ideo fixedIdeo = null, bool forceNoIdeo = false, bool forceNoBackstory = false, bool forbidAnyTitle = false)
                PawnGenerationRequest req = new PawnGenerationRequest(
                    kDef, // Kind
                    Faction.OfAncients, // Faction (default: null)
                    PawnGenerationContext.NonPlayer, // context (default)
                    -1, // tile (default)
                    true, // force (false)
                    false, // newborn (default)
                    false, // allowDead (default)
                    true, // allowDowned (false)
                    false, // cangeneratepawnrelations (true)
                    false, // mustBeCapableOfViolence (default)
                    1f, // colonistRelationChanceFactor (default)
                    false, // forceAddFreeWarmLayerIfNeeded (default)
                    true, // allowGay (default)
                    true, // allowFood (default)
                    false, // allowAddictions (true)
                    false, // inhabitant (default)
                    false, // certainlyBeenInCryptosleep (default)
                    false, // forceRedressWorldPawnIfFormerColonist (default)
                    false, //worldPawnFactionDoesntMatter (default)
                    0f, // biocodeWeaponChance (default)
                    0f, // biocodeApparelChance (default)
                    null, // extraPawnForExtraRelationChane (default)
                    0f, // relationWithExtraPawnChanceFactor (1f)
                    null, // Predicate validatorPreGear (default)
                    null, // validatorPostGear(default)
                    null // forcedTraits (default)
                );
                Pawn human = PawnGenerator.GeneratePawn(req);

                //Thing t = GenSpawn.Spawn(human, this.Position, this.Map);

                copyInjuries(human);

                newGraphics(human);
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
            }
            else
            {
                newGraphics();
            }
        }

        public ZombieData dataZ;
        public Pawn_DrawTracker_Zombiefied drawerZ;

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
           
            FixZombie();

            armorRating_Sharp = TryDrawOverallArmor(sourcePawn, StatDefOf.ArmorRating_Sharp);
            armorRating_Blunt = TryDrawOverallArmor(sourcePawn, StatDefOf.ArmorRating_Blunt);
            //armorRating_Heat = TryDrawOverallArmor(sourcePawn, StatDefOf.ArmorRating_Heat);
        }

        private float TryDrawOverallArmor(Pawn sourcePawn, StatDef stat)
        {
            float num = 0f;
            float num2 = Mathf.Clamp01(sourcePawn.GetStatValue(stat, true) / 2f);
            List<BodyPartRecord> allParts = sourcePawn.RaceProps.body.AllParts;
            List<Apparel> list = (sourcePawn.apparel == null) ? null : sourcePawn.apparel.WornApparel;
            for (int i = 0; i < allParts.Count; i++)
            {
                float num3 = 1f - num2;
                if (list != null)
                {
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (list[j].def.apparel.CoversBodyPart(allParts[i]))
                        {
                            float num4 = Mathf.Clamp01(list[j].GetStatValue(stat, true) / 2f);
                            num3 *= 1f - num4;
                        }
                    }
                }
                num += allParts[i].coverageAbs * (1f - num3);
            }
            num = Mathf.Clamp(num * 2f, 0f, 2f);
            return num;
        }

        public void FixZombie()
        {
            health.AddHediff(HediffDef.Named("Zombiefied"));
            if (apparel != null)
            {
                apparel.DestroyAll();
            }
        }

        //Tick + TickRare overrides for better performance
        public override void TickRare()
        {
            //if (Find.TickManager.TicksGame + thingIDNumber % 77777 == 0)
            //{
            //    base.TickRare();
            //}
            //else
            {
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
            }
        }

        public override void Tick()
        {
            //if (Find.TickManager.TicksGame + thingIDNumber % 77777 == 0)
            //{
            //    base.Tick();
            //}
            //else
            {
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

                if (Find.TickManager.TicksGame + thingIDNumber % 250 == 0)
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
            }
        }
    }
}
