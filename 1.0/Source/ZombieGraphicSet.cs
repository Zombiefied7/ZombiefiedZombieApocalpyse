using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    // Token: 0x02000017 RID: 23
    public class ZombieGraphicSet
    {
        // Token: 0x17000010 RID: 16
        // (get) Token: 0x06000071 RID: 113 RVA: 0x000048A1 File Offset: 0x00002AA1
        public bool AllResolved
        {
            get
            {
                return this.nakedGraphic != null;
            }
        }

        // Token: 0x17000011 RID: 17
        // (get) Token: 0x06000072 RID: 114 RVA: 0x000048AC File Offset: 0x00002AAC
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

        // Token: 0x06000073 RID: 115 RVA: 0x00004905 File Offset: 0x00002B05
        public ZombieGraphicSet(ZombieData data)
        {
            this.data = data;
        }

        // Token: 0x06000074 RID: 116 RVA: 0x00004934 File Offset: 0x00002B34
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

        // Token: 0x06000075 RID: 117 RVA: 0x00004A54 File Offset: 0x00002C54
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

        // Token: 0x06000076 RID: 118 RVA: 0x00004AC6 File Offset: 0x00002CC6
        public Material HairMatAt(Rot4 facing)
        {
            return this.hairGraphic.MatAt(facing, null);
        }

        // Token: 0x06000077 RID: 119 RVA: 0x00004AD5 File Offset: 0x00002CD5
        public void ClearCache()
        {
            this.cachedMatsBodyBaseHash = -1;
        }

        // Token: 0x06000078 RID: 120 RVA: 0x00004AE0 File Offset: 0x00002CE0
        public void ResolveAllGraphics(float scale = 1f)
        {
            Shader shader = ShaderDatabase.LoadShader(this.data.shaderCutoutPath);
            this.ClearCache();
            this.nakedGraphic = GraphicDatabase.Get<Graphic_Multi>(this.data.bodyType.bodyNakedGraphicPath, ShaderDatabase.CutoutSkin, Vector2.one, this.data.color);
            this.rottingGraphic = GraphicDatabase.Get<Graphic_Multi>(this.data.bodyType.bodyNakedGraphicPath, ShaderDatabase.CutoutSkin, Vector2.one, PawnGraphicSet.RottingColor);
            this.dessicatedGraphic = GraphicDatabase.Get<Graphic_Multi>(this.data.bodyType.bodyDessicatedGraphicPath, shader);
            this.headGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetHeadNamed(this.data.headGraphicPath, this.data.color);
            this.desiccatedHeadGraphic = GraphicDatabaseHeadRecords_Zombiefied.GetHeadNamed(this.data.headGraphicPath, PawnGraphicSet.RottingColor);
            this.skullGraphic = GraphicDatabaseHeadRecords.GetSkull();
            this.headStumpGraphic = GraphicDatabaseHeadRecords.GetStump(this.data.color);
            this.desiccatedHeadStumpGraphic = GraphicDatabaseHeadRecords.GetStump(PawnGraphicSet.RottingColor);
            this.hairGraphic = GraphicDatabase.Get<Graphic_Multi>(this.data.hairGraphicPath, shader, Vector2.one, this.data.hairColor);
            this.ResolveApparelGraphics();
        }

        // Token: 0x06000079 RID: 121 RVA: 0x00004BF8 File Offset: 0x00002DF8
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

        // Token: 0x0600007A RID: 122 RVA: 0x00004CA8 File Offset: 0x00002EA8
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

        // Token: 0x0600007B RID: 123 RVA: 0x00004CC4 File Offset: 0x00002EC4
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

        // Token: 0x04000063 RID: 99
        public ZombieData data;

        // Token: 0x04000064 RID: 100
        public Graphic nakedGraphic;

        // Token: 0x04000065 RID: 101
        public Graphic rottingGraphic;

        // Token: 0x04000066 RID: 102
        public Graphic dessicatedGraphic;

        // Token: 0x04000067 RID: 103
        public Graphic headGraphic;

        // Token: 0x04000068 RID: 104
        public Graphic desiccatedHeadGraphic;

        // Token: 0x04000069 RID: 105
        public Graphic skullGraphic;

        // Token: 0x0400006A RID: 106
        public Graphic headStumpGraphic;

        // Token: 0x0400006B RID: 107
        public Graphic desiccatedHeadStumpGraphic;

        // Token: 0x0400006C RID: 108
        public Graphic hairGraphic;

        // Token: 0x0400006D RID: 109
        public List<ApparelGraphicRecord> apparelGraphics = new List<ApparelGraphicRecord>();

        // Token: 0x0400006E RID: 110
        private List<Material> cachedMatsBodyBase = new List<Material>();

        // Token: 0x0400006F RID: 111
        private int cachedMatsBodyBaseHash = -1;

        // Token: 0x04000070 RID: 112
        public static readonly Color RottingColor = new Color(0.34f, 0.32f, 0.3f);
    }
}
