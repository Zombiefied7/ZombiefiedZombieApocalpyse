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
                // Added parts are applied first so later injuries and missing parts see the same structural
                // body state as the corpse. Missing parts that would kill the zombie are converted into a
                // severe but survivable wound instead of abandoning this pawn and generating a replacement.
                CopyAddedPartsFrom(sourcePawn);
                CopyMissingPartsFrom(sourcePawn);
                CopyInjuriesFrom(sourcePawn);
            }

            if (Dead || Destroyed)
            {
                return false;
            }

            // Keep generated zombies free of random generator apparel before they enter the map, but do not
            // add the Zombiefied hediff yet. Adding a hediff can trigger MakeUndowned/CheckForJobOverride;
            // JobDriver_Wait immediately reserves pawn.Position on pawn.Map, which is null for an unspawned pawn.
            // FixZombie() is therefore called only after GenSpawn has completed.
            if (apparel != null)
            {
                apparel.DestroyAll();
            }

            armorRating_Sharp = TryDrawOverallArmor(sourcePawn, StatDefOf.ArmorRating_Sharp);
            armorRating_Blunt = TryDrawOverallArmor(sourcePawn, StatDefOf.ArmorRating_Blunt);
            return true;
        }

        private void CopyAddedPartsFrom(Pawn sourcePawn)
        {
            List<Hediff> sourceHediffs = sourcePawn.health.hediffSet.hediffs;
            for (int i = 0; i < sourceHediffs.Count; i++)
            {
                Hediff_AddedPart sourceAddedPart = sourceHediffs[i] as Hediff_AddedPart;
                if (sourceAddedPart == null || sourceAddedPart.Part == null)
                {
                    continue;
                }

                BodyPartRecord part = sourceAddedPart.Part;
                if ((part.parent != null && health.hediffSet.PartIsMissing(part.parent)) || health.hediffSet.PartIsMissing(part))
                {
                    continue;
                }

                Hediff copied = HediffMaker.MakeHediff(sourceAddedPart.def, this, part);
                if (copied == null)
                {
                    continue;
                }

                copied.Severity = sourceAddedPart.Severity;
                copied.ageTicks = sourceAddedPart.ageTicks;
                if (!health.WouldDieAfterAddingHediff(copied))
                {
                    health.AddHediff(copied, part);
                }
            }
        }

        private void CopyMissingPartsFrom(Pawn sourcePawn)
        {
            List<Hediff> sourceHediffs = sourcePawn.health.hediffSet.hediffs;
            for (int i = 0; i < sourceHediffs.Count; i++)
            {
                Hediff_MissingPart sourceMissingPart = sourceHediffs[i] as Hediff_MissingPart;
                if (sourceMissingPart == null || sourceMissingPart.Part == null)
                {
                    continue;
                }

                BodyPartRecord part = sourceMissingPart.Part;

                // Child missing-part hediffs are implied by their missing parent and must not be copied again.
                if (part.parent != null && sourcePawn.health.hediffSet.PartIsMissing(part.parent))
                {
                    continue;
                }

                if ((part.parent != null && health.hediffSet.PartIsMissing(part.parent)) || health.hediffSet.PartIsMissing(part))
                {
                    continue;
                }

                bool movementPart = HasMovementTag(part);
                if (!movementPart && TryAddMissingPartWithoutKilling(sourceMissingPart))
                {
                    continue;
                }

                // The original mod represented destroyed movement parts with a shredded wound so zombies
                // remained mobile. RimWorld 1.6 removed HediffDefOf.Shredded, so use a capped cut wound.
                // The same conversion handles vital missing parts that RimWorld would otherwise treat as death.
                float maxHealth = Mathf.Max(1f, part.def.GetMaxHealth(this));
                float desiredSeverity = Mathf.Max(1f, maxHealth * 0.65f);
                TryAddSurvivableInjury(HediffDefOf.Cut, part, desiredSeverity, 70000000);
            }
        }

        private void CopyInjuriesFrom(Pawn sourcePawn)
        {
            List<Hediff> sourceHediffs = sourcePawn.health.hediffSet.hediffs;
            for (int i = 0; i < sourceHediffs.Count; i++)
            {
                Hediff_Injury sourceInjury = sourceHediffs[i] as Hediff_Injury;
                if (sourceInjury == null || sourceInjury.Part == null)
                {
                    continue;
                }

                BodyPartRecord part = sourceInjury.Part;
                if ((part.parent != null && health.hediffSet.PartIsMissing(part.parent)) || health.hediffSet.PartIsMissing(part))
                {
                    continue;
                }

                float desiredSeverity = Mathf.Max(0.01f, sourceInjury.Severity * 0.5f);
                int ageTicks = Math.Max(sourceInjury.ageTicks, 70000000);
                TryAddSurvivableInjury(sourceInjury.def, part, desiredSeverity, ageTicks);
            }
        }

        private bool TryAddMissingPartWithoutKilling(Hediff_MissingPart sourceMissingPart)
        {
            BodyPartRecord part = sourceMissingPart.Part;
            if (part == null || part == this.def.race.body.corePart)
            {
                return false;
            }

            Hediff copiedMissing = HediffMaker.MakeHediff(sourceMissingPart.def, this, part);
            if (copiedMissing == null || health.WouldDieAfterAddingHediff(copiedMissing))
            {
                return false;
            }

            health.AddHediff(copiedMissing, part);
            return !Dead && !Destroyed;
        }

        private bool TryAddSurvivableInjury(HediffDef injuryDef, BodyPartRecord part, float desiredSeverity, int ageTicks)
        {
            if (injuryDef == null || part == null)
            {
                return false;
            }

            if ((part.parent != null && health.hediffSet.PartIsMissing(part.parent)) || health.hediffSet.PartIsMissing(part))
            {
                return false;
            }

            // Never let a copied wound destroy its body part. This is important for brains, hearts, necks,
            // and small-animal body parts where a seemingly modest copied severity can cross the part HP limit.
            float currentPartHealth = health.hediffSet.GetPartHealth(part);
            float severity = Mathf.Min(desiredSeverity, Mathf.Max(0f, currentPartHealth - 1f));
            if (severity <= 0.001f)
            {
                return false;
            }

            for (int attempt = 0; attempt < 10 && severity > 0.001f; attempt++)
            {
                Hediff_Injury copiedInjury = HediffMaker.MakeHediff(injuryDef, this, part) as Hediff_Injury;
                if (copiedInjury == null)
                {
                    // Some modded injury defs may no longer instantiate as Hediff_Injury in 1.6.
                    if (injuryDef != HediffDefOf.Cut)
                    {
                        injuryDef = HediffDefOf.Cut;
                        continue;
                    }
                    return false;
                }

                copiedInjury.Severity = severity;
                copiedInjury.ageTicks = ageTicks;

                if (!health.WouldDieAfterAddingHediff(copiedInjury))
                {
                    health.AddHediff(copiedInjury, part);
                    return !Dead && !Destroyed;
                }

                severity *= 0.5f;
            }

            return false;
        }

        private static bool HasMovementTag(BodyPartRecord part)
        {
            if (part == null || part.def == null || part.def.tags == null)
            {
                return false;
            }

            for (int i = 0; i < part.def.tags.Count; i++)
            {
                BodyPartTagDef tag = part.def.tags[i];
                if (tag != null && tag.defName != null && tag.defName.IndexOf("Moving", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
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
