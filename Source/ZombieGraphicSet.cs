using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class ZombieGraphicSet
    {
        public sealed class VisualGeneNodeRecord
        {
            public PawnRenderNode node;
            public bool attachedToHead;
        }

        private readonly Pawn pawn;
        private PawnRenderTree visualGeneTree;
        private PawnRenderNode furRenderNode;

        public ZombieGraphicSet(Pawn pawn, ZombieData data)
        {
            this.pawn = pawn;
            this.data = data;
        }

        public bool AllResolved
        {
            get { return this.nakedGraphic != null; }
        }

        public List<Material> MatsBodyBaseAt(Rot4 facing, RotDrawMode bodyCondition = RotDrawMode.Fresh)
        {
            int hash = facing.AsInt + 1000 * (int)bodyCondition;
            if (hash != this.cachedMatsBodyBaseHash)
            {
                this.cachedMatsBodyBase.Clear();
                this.cachedMatsBodyBaseHash = hash;

                if (bodyCondition == RotDrawMode.Fresh)
                {
                    this.cachedMatsBodyBase.Add(this.nakedGraphic.MatAt(facing, null));
                }
                else if (bodyCondition == RotDrawMode.Rotting || this.dessicatedGraphic == null)
                {
                    this.cachedMatsBodyBase.Add(this.rottingGraphic.MatAt(facing, null));
                }
                else if (bodyCondition == RotDrawMode.Dessicated)
                {
                    this.cachedMatsBodyBase.Add(this.dessicatedGraphic.MatAt(facing, null));
                }

                Material furMaterial = FurMatAt(facing, bodyCondition);
                if (furMaterial != null && bodyCondition != RotDrawMode.Dessicated)
                {
                    // Fur is a skin overlay in RimWorld's render tree. Keeping it immediately above the
                    // naked body makes ToolUser zombies retain xenotype fur without giving them genes.
                    this.cachedMatsBodyBase.Add(furMaterial);
                }

                for (int i = 0; i < this.apparelGraphics.Count; i++)
                {
                    Apparel apparel = this.apparelGraphics[i].sourceApparel;
                    if (apparel.def.apparel.LastLayer != ApparelLayerDefOf.Shell
                        && apparel.def.apparel.LastLayer != ApparelLayerDefOf.Overhead)
                    {
                        this.cachedMatsBodyBase.Add(this.apparelGraphics[i].graphic.MatAt(facing, null));
                    }
                }
            }

            return this.cachedMatsBodyBase;
        }

        public Material HeadMatAt(Rot4 facing, RotDrawMode bodyCondition = RotDrawMode.Fresh, bool stump = false)
        {
            if (bodyCondition == RotDrawMode.Fresh)
            {
                return stump ? this.headStumpGraphic.MatAt(facing, null) : this.headGraphic.MatAt(facing, null);
            }

            if (bodyCondition == RotDrawMode.Rotting)
            {
                return stump ? this.desiccatedHeadStumpGraphic.MatAt(facing, null) : this.desiccatedHeadGraphic.MatAt(facing, null);
            }

            if (bodyCondition == RotDrawMode.Dessicated && !stump)
            {
                return this.skullGraphic.MatAt(facing, null);
            }

            return null;
        }

        public Material HairMatAt(Rot4 facing)
        {
            return this.hairGraphic != null ? this.hairGraphic.MatAt(facing, null) : null;
        }

        public Material BeardMatAt(Rot4 facing)
        {
            return this.beardGraphic != null ? this.beardGraphic.MatAt(facing, null) : null;
        }

        public void ClearCache()
        {
            this.cachedMatsBodyBaseHash = -1;
        }

        public void ResolveAllGraphics(float scale = 1f)
        {
            Shader shader = ShaderDatabase.LoadShader(this.data.shaderCutoutPath);
            this.ClearCache();

            BodyTypeDef bodyType = this.data.bodyType ?? BodyTypeDefOf.Female;
            this.nakedGraphic = GraphicDatabase.Get<Graphic_Multi>(bodyType.bodyNakedGraphicPath, ShaderDatabase.CutoutSkin, Vector2.one, this.data.color);
            this.rottingGraphic = GraphicDatabase.Get<Graphic_Multi>(bodyType.bodyNakedGraphicPath, ShaderDatabase.CutoutSkin, Vector2.one, RottingColor);
            this.dessicatedGraphic = GraphicDatabase.Get<Graphic_Multi>(bodyType.bodyDessicatedGraphicPath, shader);
            this.headGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetHeadNamed(this.data.headGraphicPath, this.data.color);
            this.desiccatedHeadGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetHeadNamed(this.data.headGraphicPath, RottingColor);
            this.skullGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetSkull();
            this.headStumpGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetStump(this.data.color);
            this.desiccatedHeadStumpGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetStump(RottingColor);
            this.hairGraphic = this.data.hairGraphicPath.NullOrEmpty()
                ? null
                : GraphicDatabase.Get<Graphic_Multi>(this.data.hairGraphicPath, shader, Vector2.one, this.data.hairColor);
            this.beardGraphic = this.data.beardGraphicPath.NullOrEmpty()
                ? null
                : GraphicDatabase.Get<Graphic_Multi>(this.data.beardGraphicPath, shader, Vector2.one, this.data.hairColor);

            ResolveApparelGraphics();
            ResolveVisualGeneNodes();
        }

        public void ResolveApparelGraphics()
        {
            this.ClearCache();
            this.apparelGraphics.Clear();

            for (int i = 0; i < this.data.wornApparelDefs.Count; i++)
            {
                ThingDef def = this.data.wornApparelDefs[i];
                if (def == null || !def.IsApparel)
                {
                    continue;
                }

                Apparel apparel;
                try
                {
                    apparel = MakeApparel(i);
                }
                catch (Exception ex)
                {
                    Log.WarningOnce("Zombiefied could not reconstruct apparel " + def.defName + ": " + ex, Gen.HashCombine(def.shortHash, 186323));
                    continue;
                }

                ApparelGraphicRecord record;
                string savedWornPath = this.data.wornApparelGraphicPaths.Count > i
                    ? this.data.wornApparelGraphicPaths[i]
                    : null;

                bool savedRenderAsPack = this.data.wornApparelRenderAsPack != null
                    && this.data.wornApparelRenderAsPack.Count > i
                    ? this.data.wornApparelRenderAsPack[i]
                    : apparel.RenderAsPack();

                if (TryGetGraphicApparel(apparel, savedWornPath, this.data.bodyType, savedRenderAsPack, out record))
                {
                    this.apparelGraphics.Add(record);
                }
            }
        }

        private Apparel MakeApparel(int index)
        {
            ThingDef def = this.data.wornApparelDefs[index];
            ThingDef stuff = null;

            if (def.MadeFromStuff)
            {
                if (this.data.wornApparelStuffDefs.Count > index)
                {
                    stuff = this.data.wornApparelStuffDefs[index];
                }

                if (stuff == null || !stuff.IsStuff)
                {
                    stuff = GenStuff.DefaultStuffFor(def);
                }
            }

            Apparel apparel = (Apparel)ThingMaker.MakeThing(def, stuff);

            if (this.data.wornApparelStyleDefs.Count > index)
            {
                apparel.StyleDef = this.data.wornApparelStyleDefs[index];
            }

            if (this.data.wornApparelColors.Count > index)
            {
                apparel.SetColor(this.data.wornApparelColors[index], false);
            }

            return apparel;
        }

        private static bool TryGetGraphicApparel(Apparel apparel, string savedWornPath, BodyTypeDef bodyType, bool renderAsPack, out ApparelGraphicRecord record)
        {
            if (bodyType == null)
            {
                bodyType = BodyTypeDefOf.Male;
            }

            string wornPath = savedWornPath.NullOrEmpty() ? apparel.WornGraphicPath : savedWornPath;
            if (wornPath.NullOrEmpty())
            {
                record = new ApparelGraphicRecord(null, null);
                return false;
            }

            string path = wornPath;
            bool usesBodyTypeSuffix = apparel.def.apparel.LastLayer != ApparelLayerDefOf.Overhead
                && apparel.def.apparel.LastLayer != ApparelLayerDefOf.EyeCover
                && !renderAsPack
                && wornPath != BaseContent.PlaceholderImagePath
                && wornPath != BaseContent.PlaceholderGearImagePath;

            if (usesBodyTypeSuffix)
            {
                string bodyTypePath = wornPath + "_" + bodyType.defName;
                if (GraphicMultiPathExists(bodyTypePath) || !GraphicMultiPathExists(wornPath))
                {
                    path = bodyTypePath;
                }
            }
            else if (!GraphicMultiPathExists(path))
            {
                // Old saves do not have the persisted RenderAsPack flag. If a supposedly pack-like item
                // actually has body-type textures, prefer the only path that exists instead of logging a
                // missing-texture error every frame.
                string bodyTypePath = wornPath + "_" + bodyType.defName;
                if (GraphicMultiPathExists(bodyTypePath))
                {
                    path = bodyTypePath;
                }
            }

            Shader shader = ShaderDatabase.Cutout;
            if (apparel.StyleDef != null
                && apparel.StyleDef.graphicData != null
                && apparel.StyleDef.graphicData.shaderType != null)
            {
                shader = apparel.StyleDef.graphicData.shaderType.Shader;
            }
            else if ((apparel.StyleDef == null && apparel.def.apparel.useWornGraphicMask)
                || (apparel.StyleDef != null && apparel.StyleDef.UseWornGraphicMask))
            {
                shader = ShaderDatabase.CutoutComplex;
            }

            Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(path, shader, apparel.def.graphicData.drawSize, apparel.DrawColor);
            record = new ApparelGraphicRecord(graphic, apparel);
            return graphic != null;
        }

        private static bool GraphicMultiPathExists(string path)
        {
            if (path.NullOrEmpty())
            {
                return false;
            }

            return ContentFinder<Texture2D>.Get(path + "_south", false) != null
                || ContentFinder<Texture2D>.Get(path + "_north", false) != null
                || ContentFinder<Texture2D>.Get(path + "_east", false) != null
                || ContentFinder<Texture2D>.Get(path + "_west", false) != null;
        }

        private void ResolveVisualGeneNodes()
        {
            this.visualGeneNodes.Clear();
            this.furRenderNode = null;
            this.visualGeneTree = null;

            if (!ModsConfig.BiotechActive || this.pawn == null || this.data.visualGeneDefs == null || this.data.visualGeneDefs.Count == 0)
            {
                return;
            }

            this.visualGeneTree = new PawnRenderTree(this.pawn);

            for (int i = 0; i < this.data.visualGeneDefs.Count; i++)
            {
                GeneDef geneDef = this.data.visualGeneDefs[i];
                if (geneDef == null || geneDef.RenderNodeProperties.NullOrEmpty())
                {
                    continue;
                }

                Gene visualGene = new Gene();
                visualGene.def = geneDef;
                visualGene.pawn = this.pawn;
                visualGene.loadID = this.data.visualGeneLoadIds.Count > i
                    ? this.data.visualGeneLoadIds[i]
                    : Gen.HashCombine(this.pawn.thingIDNumber, geneDef.shortHash);

                for (int j = 0; j < geneDef.RenderNodeProperties.Count; j++)
                {
                    PawnRenderNodeProperties properties = geneDef.RenderNodeProperties[j];
                    if (properties == null || properties.nodeClass == null)
                    {
                        continue;
                    }

                    try
                    {
                        PawnRenderNode node = (PawnRenderNode)Activator.CreateInstance(properties.nodeClass, this.pawn, properties, this.visualGeneTree);
                        node.gene = visualGene;
                        node.EnsureInitialized((PawnRenderFlags)0);

                        if (node is PawnRenderNode_Fur)
                        {
                            this.furRenderNode = node;
                            continue;
                        }

                        VisualGeneNodeRecord record = new VisualGeneNodeRecord();
                        record.node = node;
                        record.attachedToHead = properties.parentTagDef == PawnRenderNodeTagDefOf.Head
                            || node is PawnRenderNode_AttachmentHead;
                        this.visualGeneNodes.Add(record);
                    }
                    catch (Exception ex)
                    {
                        Log.WarningOnce(
                            "Zombiefied could not reconstruct visual gene node " + geneDef.defName + "/" + properties.nodeClass + ": " + ex,
                            Gen.HashCombine(geneDef.shortHash, properties.nodeClass.GetHashCode()));
                    }
                }
            }
        }

        private Material FurMatAt(Rot4 facing, RotDrawMode bodyCondition)
        {
            if (this.furRenderNode == null || this.furRenderNode.Worker == null || this.pawn == null)
            {
                return null;
            }

            PawnDrawParms parms = PawnDrawParms.DefaultFor(this.pawn);
            parms.facing = facing;
            parms.rotDrawMode = bodyCondition;
            parms.posture = this.pawn.GetPosture();
            parms.tint = Color.white;

            if (!this.furRenderNode.Worker.CanDrawNow(this.furRenderNode, parms))
            {
                return null;
            }

            return this.furRenderNode.Worker.GetFinalizedMaterial(this.furRenderNode, parms);
        }

        public ZombieData data;
        public Graphic nakedGraphic;
        public Graphic rottingGraphic;
        public Graphic dessicatedGraphic;
        public Graphic headGraphic;
        public Graphic desiccatedHeadGraphic;
        public Graphic skullGraphic;
        public Graphic headStumpGraphic;
        public Graphic desiccatedHeadStumpGraphic;
        public Graphic hairGraphic;
        public Graphic beardGraphic;
        public List<ApparelGraphicRecord> apparelGraphics = new List<ApparelGraphicRecord>();
        public List<VisualGeneNodeRecord> visualGeneNodes = new List<VisualGeneNodeRecord>();

        private readonly List<Material> cachedMatsBodyBase = new List<Material>();
        private int cachedMatsBodyBaseHash = -1;

        public static readonly Color RottingColor = new Color(0.34f, 0.32f, 0.3f);
    }
}
