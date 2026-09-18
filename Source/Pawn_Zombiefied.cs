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
            if (data == null)
            {
                return;
            }

            if (story == null)
            {
                story = new Pawn_StoryTracker(this);
            }

            if (data.bodyType != null)
            {
                story.bodyType = data.bodyType;
            }

            if (data.headTypeDef != null)
            {
                story.headType = data.headTypeDef;
            }
            else if (!data.headGraphicPath.NullOrEmpty())
            {
                HeadTypeDef matchingHead = DefDatabase<HeadTypeDef>.AllDefsListForReading
                    .FirstOrDefault(def => def != null && def.graphicPath == data.headGraphicPath);

                if (matchingHead != null)
                {
                    story.headType = matchingHead;
                }
            }

            if (data.hairDef != null)
            {
                story.hairDef = data.hairDef;
            }
            else if (!data.hairGraphicPath.NullOrEmpty())
            {
                HairDef matchingHair = DefDatabase<HairDef>.AllDefsListForReading
                    .FirstOrDefault(def => def != null && def.texPath == data.hairGraphicPath);

                if (matchingHair != null)
                {
                    story.hairDef = matchingHair;
                }
            }

            // Gene render nodes ask the pawn story tracker for their colors and fur definition.
            // These are visual snapshots only; the zombie itself remains a ToolUser with no copied genes.
            story.HairColor = data.hairColor;
            story.SkinColorBase = data.color;
            story.skinColorOverride = data.color;
            story.furDef = data.furDef;

            if (style == null)
            {
                style = new Pawn_StyleTracker(this);
            }

            if (data.beardDef != null)
            {
                style.beardDef = data.beardDef;
            }
            else if (!data.beardGraphicPath.NullOrEmpty())
            {
                BeardDef matchingBeard = DefDatabase<BeardDef>.AllDefsListForReading
                    .FirstOrDefault(def => def != null && def.texPath == data.beardGraphicPath);

                if (matchingBeard != null)
                {
                    style.beardDef = matchingBeard;
                }
            }
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

        public bool copyInjuries(Pawn sourcePawn, bool copyHealthConditions = true)
        {
            if (sourcePawn == null || sourcePawn.health == null || health == null)
            {
                return false;
            }

            if (copyHealthConditions && this.def.race.body == sourcePawn.def.race.body)
            {
                for (int i = sourcePawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    Hediff hediff = sourcePawn.health.hediffSet.hediffs[i];
                    Hediff_Injury injury = hediff as Hediff_Injury;
                    Hediff_AddedPart added = hediff as Hediff_AddedPart;
                    Hediff_MissingPart missing = hediff as Hediff_MissingPart;

                    if (injury != null && injury.Part != null)
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
                                    if (Dead || Destroyed)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                    else if (missing != null && missing.Part != null
                        && (missing.Part.parent == null || !health.hediffSet.PartIsMissing(missing.Part.parent))
                        && !health.hediffSet.PartIsMissing(missing.Part))
                    {
                        bool foundMoving = false;
                        if (missing.Part.def.tags != null)
                        {
                            for (int i1 = 0; i1 < missing.Part.def.tags.Count; i1++)
                            {
                                if (missing.Part.def.tags[i1].defName.Contains("Moving"))
                                {
                                    foundMoving = true;
                                }
                            }
                        }

                        if (!foundMoving || missing.Part.parent == null || !sourcePawn.health.hediffSet.PartIsMissing(missing.Part.parent))
                        {
                            Hediff copiedMissing = HediffMaker.MakeHediff(missing.def, this, missing.Part);
                            if (copiedMissing != null && !health.WouldDieAfterAddingHediff(copiedMissing))
                            {
                                health.AddHediff(copiedMissing, missing.Part);
                                if (Dead || Destroyed)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                    else if (added != null && added.Part != null
                        && (added.Part.parent == null || !health.hediffSet.PartIsMissing(added.Part.parent))
                        && !health.hediffSet.PartIsMissing(added.Part))
                    {
                        Hediff copiedAddedPart = HediffMaker.MakeHediff(added.def, this, added.Part);
                        if (copiedAddedPart != null)
                        {
                            copiedAddedPart.Severity = added.Severity;
                            if (!health.WouldDieAfterAddingHediff(copiedAddedPart))
                            {
                                health.AddHediff(copiedAddedPart, added.Part);
                                if (Dead || Destroyed)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            if (Dead || Destroyed)
            {
                return false;
            }

            FixZombie();
            if (Dead || Destroyed)
            {
                return false;
            }

            armorRating_Sharp = TryDrawOverallArmor(sourcePawn, StatDefOf.ArmorRating_Sharp);
            armorRating_Blunt = TryDrawOverallArmor(sourcePawn, StatDefOf.ArmorRating_Blunt);
            return true;
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
