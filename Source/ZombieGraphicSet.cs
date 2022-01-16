using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class ZombieGraphicSet
    {
        public bool AllResolved
        {
            get
            {
                return this.nakedGraphic != null;
            }
        }

        public GraphicMeshSet HairMeshSet
        {
            get
            {
                if (this.data.crownType == CrownType.Average)
                {
                    return MeshPool.humanlikeHairSetAverage;
                }
                if (this.data.crownType == CrownType.Narrow)
                {
                    return MeshPool.humanlikeHairSetNarrow;
                }
                Log.Error("Unknown crown type: " + this.data.crownType);
                return MeshPool.humanlikeHairSetAverage;
            }
        }
        public ZombieGraphicSet(ZombieData data)
        {
            this.data = data;
        }
        public List<Material> MatsBodyBaseAt(Rot4 facing, RotDrawMode bodyCondition = RotDrawMode.Fresh)
        {
            int num = facing.AsInt + 1000 * (int)bodyCondition;
            if (num != this.cachedMatsBodyBaseHash)
            {
                this.cachedMatsBodyBase.Clear();
                this.cachedMatsBodyBaseHash = num;
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
                for (int i = 0; i < this.apparelGraphics.Count; i++)
                {
                    if (this.apparelGraphics[i].sourceApparel.def.apparel.LastLayer != ApparelLayerDefOf.Shell && this.apparelGraphics[i].sourceApparel.def.apparel.LastLayer != ApparelLayerDefOf.Overhead)
                    {
                        this.cachedMatsBodyBase.Add(this.apparelGraphics[i].graphic.MatAt(facing, null));
                    }
                }
            }
            return this.cachedMatsBodyBase;
        }
        public Material HeadMatAt(Rot4 facing, RotDrawMode bodyCondition = RotDrawMode.Fresh, bool stump = false)
        {
            Material result = null;
            if (bodyCondition == RotDrawMode.Fresh)
            {
                if (stump)
                {
                    result = this.headStumpGraphic.MatAt(facing, null);
                }
                else
                {
                    result = this.headGraphic.MatAt(facing, null);
                }
            }
            else if (bodyCondition == RotDrawMode.Rotting)
            {
                if (stump)
                {
                    result = this.desiccatedHeadStumpGraphic.MatAt(facing, null);
                }
                else
                {
                    result = this.desiccatedHeadGraphic.MatAt(facing, null);
                }
            }
            else if (bodyCondition == RotDrawMode.Dessicated && !stump)
            {
                result = this.skullGraphic.MatAt(facing, null);
            }
            return result;
        }
        public Material HairMatAt(Rot4 facing)
        {
            return this.hairGraphic.MatAt(facing, null);
        }
        public void ClearCache()
        {
            this.cachedMatsBodyBaseHash = -1;
        }
        public void ResolveAllGraphics(float scale = 1f)
        {
            Shader shader = ShaderDatabase.LoadShader(this.data.shaderCutoutPath);
            this.ClearCache();
            this.nakedGraphic = GraphicDatabase.Get<Graphic_Multi>(this.data.bodyType.bodyNakedGraphicPath, ShaderDatabase.CutoutSkin, Vector2.one, this.data.color);
            this.rottingGraphic = GraphicDatabase.Get<Graphic_Multi>(this.data.bodyType.bodyNakedGraphicPath, ShaderDatabase.CutoutSkin, Vector2.one, PawnGraphicSet.RottingColorDefault);
            this.dessicatedGraphic = GraphicDatabase.Get<Graphic_Multi>(this.data.bodyType.bodyDessicatedGraphicPath, shader);
            this.headGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetHeadNamed(this.data.headGraphicPath, this.data.color);
            this.desiccatedHeadGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetHeadNamed(this.data.headGraphicPath, PawnGraphicSet.RottingColorDefault);
            this.skullGraphic = GraphicDatabaseHeadRecords.GetSkull();
            this.headStumpGraphic = GraphicDatabaseHeadRecords.GetStump(this.data.color);
            this.desiccatedHeadStumpGraphic = GraphicDatabaseHeadRecords.GetStump(PawnGraphicSet.RottingColorDefault);
            this.hairGraphic = GraphicDatabase.Get<Graphic_Multi>(this.data.hairGraphicPath, shader, Vector2.one, this.data.hairColor);
            this.ResolveApparelGraphics();
        }
        public void ResolveApparelGraphics()
        {
            /*
            Shader shader = ShaderDatabase.LoadShader(this.data.shaderCutoutPath);
            this.ClearCache();
            this.apparelGraphics.Clear();
            for(int i = 0; i < data.wornApparel.Count; i++)
            {
                ApparelGraphicRecord item;
                if (ZombieGraphicSet.TryGetGraphicApparel(data.wornApparel[i], data.wornApparel[i].DrawColor, this.data.bodyType, shader, out item))
                {
                    this.apparelGraphics.Add(item);
                }
            }
            */
            Shader shader = ShaderDatabase.LoadShader(this.data.shaderCutoutPath);
            this.ClearCache();
            this.apparelGraphics.Clear();
            for(int i = 0; i < this.data.wornApparelDefs.Count; i++)
            {
                ApparelGraphicRecord item;
                Apparel newApparel = MakeApparel(i);
                if (ZombieGraphicSet.TryGetGraphicApparel(newApparel, newApparel.DrawColor, this.data.bodyType, shader, out item))
                {
                    this.apparelGraphics.Add(item);
                }
            }
            /*
            using (List<ThingDef>.Enumerator enumerator = this.data.wornApparelDefs.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    ApparelGraphicRecord item;
                    Apparel newApparel = ZombieGraphicSet.MakeApparel(enumerator.Current, this.data.color);
                    if (ZombieGraphicSet.TryGetGraphicApparel(newApparel, newApparel.DrawColor, this.data.bodyType, shader, out item))
                    {
                        this.apparelGraphics.Add(item);
                    }
                }
            }
            */
        }
        private Apparel MakeApparel(int index)
        {
            ThingDef def = this.data.wornApparelDefs[index];
            Apparel apparel = (Apparel)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
            if(this.data.wornApparelColors.Count > index && this.data.wornApparelDefs[index] != null)
            {
                apparel.SetColor(this.data.wornApparelColors[index], false);
            }                      
            return apparel;
        }
        private static bool TryGetGraphicApparel(Apparel apparel, Color color, BodyTypeDef bodyType, Shader shader, out ApparelGraphicRecord rec)
        {
            /*
            if (bodyType == BodyTypeDefOf.)
            {
                Log.Error("Getting apparel graphic with undefined body type.");
                bodyType = BodyType.Male;
            }
            */
            if (apparel.def.apparel.wornGraphicPath.NullOrEmpty())
            {
                rec = new ApparelGraphicRecord(null, null);
                return false;
            }
            string path;
            if (apparel.def.apparel.LastLayer == ApparelLayerDefOf.Overhead)
            {
                path = apparel.def.apparel.wornGraphicPath;
            }
            else
            {
                path = apparel.def.apparel.wornGraphicPath + "_" + bodyType.ToString();
            }

            Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(path, shader, apparel.def.graphicData.drawSize, color);

            if(graphic != null && graphic.MatEast != null && graphic.MatEast.mainTexture != null)
            {
                rec = new ApparelGraphicRecord(graphic, apparel);
                return true;
            }

            rec = new ApparelGraphicRecord();
            return false;
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
        public List<ApparelGraphicRecord> apparelGraphics = new List<ApparelGraphicRecord>();
        private List<Material> cachedMatsBodyBase = new List<Material>();
        private int cachedMatsBodyBaseHash = -1;
        public static readonly Color RottingColor = new Color(0.34f, 0.32f, 0.3f);
    }
}
