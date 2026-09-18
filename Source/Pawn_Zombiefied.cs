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
        public int distanceToEdge = 0;

        public float armorRating_Sharp = 0f;
        public float armorRating_Blunt = 0f;
        public float armorRating_Heat = 0f;

        public Pawn_Zombiefied() : base()
        {

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
            SynchronizeStoryWithZombieData(data);
            drawerZ = new Pawn_DrawTracker_Zombiefied(this, data);
        }

        private void SynchronizeStoryWithZombieData(ZombieData data)
        {
            if (data == null || story == null)
            {
                return;
            }

            if (data.bodyType != null)
            {
                story.bodyType = data.bodyType;
            }

            if (!data.headGraphicPath.NullOrEmpty())
            {
                HeadTypeDef matchingHead = DefDatabase<HeadTypeDef>.AllDefsListForReading
                    .FirstOrDefault(def => def != null && def.graphicPath == data.headGraphicPath);

                if (matchingHead != null)
                {
                    story.headType = matchingHead;
                }
            }

            if (!data.hairGraphicPath.NullOrEmpty())
            {
                HairDef matchingHair = DefDatabase<HairDef>.AllDefsListForReading
                    .FirstOrDefault(def => def != null && def.texPath == data.hairGraphicPath);

                if (matchingHair != null)
                {
                    story.hairDef = matchingHair;
                }
            }

            story.HairColor = data.hairColor;
        }

        public void newGraphics(Pawn pawn)
        {
            if (pawn.RaceProps.Humanlike)
            {
                dataZ = new ZombieData(pawn);

                // Preserve the source pawn's exact geometry defs. RimWorld 1.6 derives human mesh sizes from
                // the pawn's story head/body defs, so keeping only texture paths is no longer sufficient.
                if (story != null && pawn.story != null)
                {
                    story.bodyType = pawn.story.bodyType;
                    story.headType = pawn.story.headType;
                    story.hairDef = pawn.story.hairDef;
                    story.HairColor = pawn.story.HairColor;
                }

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
                Pawn human = PawnGenerator.GeneratePawn(kDef, Faction.OfAncients);

                //Thing t = GenSpawn.Spawn(human, this.Position, this.Map);

                copyInjuries(human);

                newGraphics(human);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
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

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            if (def.defName != "Zombie")
            {
                base.DynamicDrawPhaseAt(phase, drawLoc, flip);
                return;
            }

            // The legacy zombie renderer performs immediate drawing and cannot participate in RimWorld's
            // parallel render-tree pass. Initialize its graphics on the main-thread setup phase, skip the
            // parallel preparation phase, and issue the actual draw only during the final main-thread phase.
            if (phase == DrawPhase.EnsureInitialized)
            {
                if (drawerZ == null)
                {
                    newGraphics();
                }

                return;
            }

            if (phase == DrawPhase.Draw)
            {
                if (drawerZ == null)
                {
                    newGraphics();
                }

                if (drawerZ != null)
                {
                    drawerZ.DrawAt(drawLoc);
                }
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
                    if (hediff is Hediff_Injury && injury != null && injury.Part != null)
                    {
                        BodyPartRecord part = injury.Part;
                        bool parentMissing = part.parent != null && health.hediffSet.PartIsMissing(part.parent);
                        bool partMissing = health.hediffSet.PartIsMissing(part);

                        if (!parentMissing && !partMissing)
                        {
                            Hediff_Injury copiedInjury = HediffMaker.MakeHediff(injury.def, this, part) as Hediff_Injury;
                            if (copiedInjury != null)
                            {
                                copiedInjury.Severity = injury.Severity * 0.5f;
                                copiedInjury.ageTicks = 70000000;

                                if (!health.WouldDieAfterAddingHediff(copiedInjury))
                                {
                                    health.AddHediff(copiedInjury, part);
                                }
                            }
                        }
                    }
                    else if (hediff is Hediff_MissingPart && missing != null && missing.Part != null
                        && (missing.Part.parent == null || !health.hediffSet.PartIsMissing(missing.Part.parent))
                        && !health.hediffSet.PartIsMissing(missing.Part))
                    {
                        bool foundMoving = false;
                        for (int i1 = 0; i1 < missing.Part.def.tags.Count; i1++)
                        {
                            if (missing.Part.def.tags[i1].defName.Contains("Moving"))
                            {
                                foundMoving = true;
                            }
                        }

                        // RimWorld 1.6 no longer exposes the old Shredded HediffDef. Preserve the source pawn's
                        // actual missing-part hediff instead of substituting a removed vanilla injury def.
                        if (!foundMoving || !sourcePawn.health.hediffSet.PartIsMissing(missing.Part.parent))
                        {
                            health.AddHediff(missing.def, missing.Part);
                        }
                    }
                    else if (hediff is Hediff_AddedPart && added != null && added.Part != null
                        && (added.Part.parent == null || !health.hediffSet.PartIsMissing(added.Part.parent))
                        && !health.hediffSet.PartIsMissing(added.Part))
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
            HediffDef zombiefiedDef = HediffDef.Named("Zombiefied");
            if (health != null && health.hediffSet != null && !health.hediffSet.HasHediff(zombiefiedDef))
            {
                health.AddHediff(zombiefiedDef);
            }

            if (apparel != null)
            {
                apparel.DestroyAll();
            }
        }

        // RimWorld 1.6 moved substantial pawn update logic into new tracker and render systems.
        // Delegating to Pawn keeps zombies synchronized with those systems instead of duplicating stale internals.
        public override void TickRare()
        {
            base.TickRare();
        }

        protected override void Tick()
        {
            base.Tick();
        }
    }
}
