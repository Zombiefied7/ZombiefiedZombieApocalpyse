using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public enum CrownType : byte
    {
        Undefined,
        Average,
        Narrow
    }

    public class ZombieData : IExposable
    {
        public ZombieData()
        {
            this.hairColor = Color.green;
            this.color = Color.green;
            this.shaderCutoutPath = "Map/Cutout";
            this.bodyType = BodyTypeDefOf.Female;
            this.headGraphicPath = "Things/Pawn/Humanlike/Heads/None_Average_Skull";
            this.hairGraphicPath = "Things/Pawn/Humanlike/Hairs/Bob";
            this.crownType = CrownType.Average;
            this.bodySizeFactor = 1f;
            this.bodyDrawOffset = Vector3.zero;
            this.bodyMeshWidth = 1.5f;
            this.headMeshWidth = 1.5f;
            this.hairMeshWidth = 1.5f;
            this.hairMeshHeight = 1.5f;
            EnsureCollections();
        }

        public ZombieData(Color color, Color hairColor, string shaderCutoutPath) : this()
        {
            this.color = color;
            this.hairColor = hairColor;
            this.shaderCutoutPath = shaderCutoutPath;
        }

        public ZombieData(Pawn pawn) : this()
        {
            Pawn_StoryTracker story = pawn.story;
            this.bodyType = story != null && story.bodyType != null ? story.bodyType : BodyTypeDefOf.Female;

            HeadTypeDef headType = story != null ? story.headType : null;
            this.headTypeDef = headType;
            this.hairDef = story != null ? story.hairDef : null;
            this.beardDef = pawn.style != null ? pawn.style.beardDef : null;
            this.headGraphicPath = headType != null && !headType.graphicPath.NullOrEmpty()
                ? headType.graphicPath
                : "Things/Pawn/Humanlike/Heads/None_Average_Skull";

            this.hairGraphicPath = this.hairDef != null
                ? this.hairDef.texPath
                : "Things/Pawn/Humanlike/Hairs/Bob";

            this.beardGraphicPath = this.beardDef != null && !this.beardDef.texPath.NullOrEmpty()
                ? this.beardDef.texPath
                : null;

            this.crownType = this.headGraphicPath.IndexOf("Narrow", StringComparison.OrdinalIgnoreCase) >= 0
                ? CrownType.Narrow
                : CrownType.Average;

            Color skinColor = story != null ? story.SkinColor : Color.green;
            this.color = new Color(skinColor.r * 0.5f, skinColor.g * 0.7f, skinColor.b * 0.5f);
            this.hairColor = story != null ? story.HairColor : Color.green;
            this.shaderCutoutPath = "Map/Cutout";
            this.furDef = story != null ? story.furDef : null;

            LifeStageDef lifeStage = pawn.ageTracker != null ? pawn.ageTracker.CurLifeStage : null;
            this.bodySizeFactor = lifeStage != null ? lifeStage.bodySizeFactor : 1f;
            this.bodyDrawOffset = lifeStage != null ? lifeStage.bodyDrawOffset : Vector3.zero;

            float headSizeFactor = 1f;
            this.bodyMeshWidth = 1.5f;
            if (ModsConfig.BiotechActive && lifeStage != null && lifeStage.bodyWidth.HasValue)
            {
                this.bodyMeshWidth = lifeStage.bodyWidth.Value;
            }
            if (ModsConfig.BiotechActive && lifeStage != null && lifeStage.headSizeFactor.HasValue)
            {
                headSizeFactor = lifeStage.headSizeFactor.Value;
            }

            this.headMeshWidth = 1.5f * headSizeFactor;
            Vector2 hairMeshSize = headType != null ? headType.hairMeshSize : new Vector2(1.5f, 1.5f);
            hairMeshSize *= headSizeFactor;
            this.hairMeshWidth = hairMeshSize.x;
            this.hairMeshHeight = hairMeshSize.y;

            if (pawn.apparel != null)
            {
                foreach (Apparel worn in pawn.apparel.WornApparel)
                {
                    this.wornApparelDefs.Add(worn.def);
                    this.wornApparelStuffDefs.Add(worn.Stuff);
                    this.wornApparelStyleDefs.Add(worn.StyleDef);
                    this.wornApparelGraphicPaths.Add(worn.WornGraphicPath);
                    this.wornApparelColors.Add(worn.GetColorIgnoringTainted());
                    this.wornApparelRenderAsPack.Add(worn.RenderAsPack());
                }
            }

            // Zombie pawns intentionally remain ToolUser. Keep only the source pawn's visual gene data so
            // Biotech xenotype graphics can be reconstructed without granting the zombie functional genes.
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                IReadOnlyList<Gene> genes = pawn.genes.GenesListForReading;
                for (int i = 0; i < genes.Count; i++)
                {
                    Gene gene = genes[i];
                    if (gene == null || gene.def == null || !gene.Active)
                    {
                        continue;
                    }

                    if (gene.def.HasDefinedGraphicProperties || gene.def.fur != null)
                    {
                        this.visualGeneDefs.Add(gene.def);
                        this.visualGeneLoadIds.Add(gene.loadID);
                    }

                    if (this.furDef == null && gene.def.fur != null)
                    {
                        this.furDef = gene.def.fur;
                    }
                }
            }
        }

        public ZombieData(ZombieData source, Color color, Color hairColor, string shaderCutoutPath) : this()
        {
            this.bodyType = source.bodyType;
            this.headTypeDef = source.headTypeDef;
            this.hairDef = source.hairDef;
            this.beardDef = source.beardDef;
            this.headGraphicPath = source.headGraphicPath;
            this.hairGraphicPath = source.hairGraphicPath;
            this.beardGraphicPath = source.beardGraphicPath;
            this.crownType = source.crownType;
            this.color = color;
            this.hairColor = hairColor;
            this.shaderCutoutPath = shaderCutoutPath;
            this.bodySizeFactor = source.bodySizeFactor;
            this.bodyDrawOffset = source.bodyDrawOffset;
            this.bodyMeshWidth = source.bodyMeshWidth;
            this.headMeshWidth = source.headMeshWidth;
            this.hairMeshWidth = source.hairMeshWidth;
            this.hairMeshHeight = source.hairMeshHeight;
            this.furDef = source.furDef;

            this.wornApparelDefs.AddRange(source.wornApparelDefs ?? new List<ThingDef>());
            this.wornApparelStuffDefs.AddRange(source.wornApparelStuffDefs ?? new List<ThingDef>());
            this.wornApparelStyleDefs.AddRange(source.wornApparelStyleDefs ?? new List<ThingStyleDef>());
            this.wornApparelGraphicPaths.AddRange(source.wornApparelGraphicPaths ?? new List<string>());
            this.wornApparelColors.AddRange(source.wornApparelColors ?? new List<Color>());
            this.wornApparelRenderAsPack.AddRange(source.wornApparelRenderAsPack ?? new List<bool>());
            this.visualGeneDefs.AddRange(source.visualGeneDefs ?? new List<GeneDef>());
            this.visualGeneLoadIds.AddRange(source.visualGeneLoadIds ?? new List<int>());
        }

        public void ExposeData()
        {
            Scribe_Defs.Look<BodyTypeDef>(ref this.bodyType, "bodyTypeDef");
            Scribe_Defs.Look<HeadTypeDef>(ref this.headTypeDef, "headTypeDef");
            Scribe_Defs.Look<HairDef>(ref this.hairDef, "hairDef");
            Scribe_Defs.Look<BeardDef>(ref this.beardDef, "beardDef");
            Scribe_Values.Look<string>(ref this.headGraphicPath, "headGraphicPath", null, false);
            Scribe_Values.Look<string>(ref this.hairGraphicPath, "hairGraphicPath", null, false);
            Scribe_Values.Look<string>(ref this.beardGraphicPath, "beardGraphicPath", null, false);
            Scribe_Values.Look<CrownType>(ref this.crownType, "crownType", CrownType.Undefined, false);
            Scribe_Values.Look<Color>(ref this.color, "color", default(Color), false);
            Scribe_Values.Look<Color>(ref this.hairColor, "hairColor", default(Color), false);
            Scribe_Values.Look<string>(ref this.shaderCutoutPath, "shaderCutoutPath", null, false);
            Scribe_Values.Look<float>(ref this.bodySizeFactor, "bodySizeFactor", 1f, false);
            Scribe_Values.Look<Vector3>(ref this.bodyDrawOffset, "bodyDrawOffset", Vector3.zero, false);
            Scribe_Values.Look<float>(ref this.bodyMeshWidth, "bodyMeshWidth", 1.5f, false);
            Scribe_Values.Look<float>(ref this.headMeshWidth, "headMeshWidth", 1.5f, false);
            Scribe_Values.Look<float>(ref this.hairMeshWidth, "hairMeshWidth", 1.5f, false);
            Scribe_Values.Look<float>(ref this.hairMeshHeight, "hairMeshHeight", 1.5f, false);
            Scribe_Defs.Look<FurDef>(ref this.furDef, "furDef");
            Scribe_Collections.Look<ThingDef>(ref this.wornApparelDefs, "wornApparelDefs", LookMode.Def, new object[0]);
            Scribe_Collections.Look<ThingDef>(ref this.wornApparelStuffDefs, "wornApparelStuffDefs", LookMode.Def, new object[0]);
            Scribe_Collections.Look<ThingStyleDef>(ref this.wornApparelStyleDefs, "wornApparelStyleDefs", LookMode.Def, new object[0]);
            Scribe_Collections.Look<string>(ref this.wornApparelGraphicPaths, "wornApparelGraphicPaths", LookMode.Value, new object[0]);
            Scribe_Collections.Look<Color>(ref this.wornApparelColors, "wornApparelColors", LookMode.Value, new object[0]);
            Scribe_Collections.Look<bool>(ref this.wornApparelRenderAsPack, "wornApparelRenderAsPack", LookMode.Value, new object[0]);
            Scribe_Collections.Look<GeneDef>(ref this.visualGeneDefs, "visualGeneDefs", LookMode.Def, new object[0]);
            Scribe_Collections.Look<int>(ref this.visualGeneLoadIds, "visualGeneLoadIds", LookMode.Value, new object[0]);
            EnsureCollections();
        }

        private void EnsureCollections()
        {
            if (this.wornApparelDefs == null)
            {
                this.wornApparelDefs = new List<ThingDef>();
            }
            if (this.wornApparelStuffDefs == null)
            {
                this.wornApparelStuffDefs = new List<ThingDef>();
            }
            if (this.wornApparelStyleDefs == null)
            {
                this.wornApparelStyleDefs = new List<ThingStyleDef>();
            }
            if (this.wornApparelGraphicPaths == null)
            {
                this.wornApparelGraphicPaths = new List<string>();
            }
            if (this.wornApparelColors == null)
            {
                this.wornApparelColors = new List<Color>();
            }
            if (this.wornApparelRenderAsPack == null)
            {
                this.wornApparelRenderAsPack = new List<bool>();
            }
            if (this.visualGeneDefs == null)
            {
                this.visualGeneDefs = new List<GeneDef>();
            }
            if (this.visualGeneLoadIds == null)
            {
                this.visualGeneLoadIds = new List<int>();
            }
        }

        public bool CanWearWithoutDroppingAnything(ThingDef apDef)
        {
            for (int i = 0; i < this.wornApparelDefs.Count; i++)
            {
                if (!ApparelUtility.CanWearTogether(apDef, this.wornApparelDefs[i], ThingDefOf.Human.race.body))
                {
                    return false;
                }
            }
            return true;
        }

        public BodyTypeDef bodyType;
        public HeadTypeDef headTypeDef;
        public HairDef hairDef;
        public BeardDef beardDef;
        public string headGraphicPath;
        public string hairGraphicPath;
        public string beardGraphicPath;
        public CrownType crownType;
        public Color color;
        public Color hairColor;
        public float bodySizeFactor = 1f;
        public Vector3 bodyDrawOffset = Vector3.zero;
        public float bodyMeshWidth = 1.5f;
        public float headMeshWidth = 1.5f;
        public float hairMeshWidth = 1.5f;
        public float hairMeshHeight = 1.5f;
        public string shaderCutoutPath;
        public FurDef furDef;
        public List<ThingDef> wornApparelDefs;
        public List<ThingDef> wornApparelStuffDefs;
        public List<ThingStyleDef> wornApparelStyleDefs;
        public List<string> wornApparelGraphicPaths;
        public List<Color> wornApparelColors;
        public List<bool> wornApparelRenderAsPack;
        public List<GeneDef> visualGeneDefs;
        public List<int> visualGeneLoadIds;
    }
}
