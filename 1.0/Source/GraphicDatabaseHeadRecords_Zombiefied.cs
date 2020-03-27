using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace Zombiefied
{
    // Token: 0x02000E28 RID: 3624
    public static class GraphicDatabaseHeadRecords_Zombiefied
    {
        // Token: 0x060051CF RID: 20943 RVA: 0x0025EBF4 File Offset: 0x0025CFF4
        public static void Reset()
        {
            GraphicDatabaseHeadRecords_Zombiefied.heads.Clear();
            GraphicDatabaseHeadRecords_Zombiefied.skull = null;
            GraphicDatabaseHeadRecords_Zombiefied.stump = null;
        }

        // Token: 0x060051D0 RID: 20944 RVA: 0x0025EC0C File Offset: 0x0025D00C
        private static void BuildDatabaseIfNecessary()
        {
            if (GraphicDatabaseHeadRecords_Zombiefied.heads.Count > 0 && GraphicDatabaseHeadRecords_Zombiefied.skull != null && GraphicDatabaseHeadRecords_Zombiefied.stump != null)
            {
                return;
            }
            GraphicDatabaseHeadRecords_Zombiefied.heads.Clear();
            foreach (string text in GraphicDatabaseHeadRecords_Zombiefied.HeadsFolderPaths)
            {
                foreach (string str in GraphicDatabaseUtility.GraphicNamesInFolder(text))
                {
                    GraphicDatabaseHeadRecords_Zombiefied.heads.Add(new GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord(text + "/" + str));
                }
            }
            GraphicDatabaseHeadRecords_Zombiefied.skull = new GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord(GraphicDatabaseHeadRecords_Zombiefied.SkullPath);
            GraphicDatabaseHeadRecords_Zombiefied.stump = new GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord(GraphicDatabaseHeadRecords_Zombiefied.StumpPath);
        }

        // Token: 0x060051D1 RID: 20945 RVA: 0x0025ECEC File Offset: 0x0025D0EC
        public static Graphic_Multi GetHeadNamed(string graphicPath, Color skinColor)
        {
            GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord HGR = new GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord(graphicPath);
            return HGR.GetGraphic(skinColor, false);
        }

        // Token: 0x060051D2 RID: 20946 RVA: 0x0025ED66 File Offset: 0x0025D166
        public static Graphic_Multi GetSkull()
        {
            GraphicDatabaseHeadRecords_Zombiefied.BuildDatabaseIfNecessary();
            return GraphicDatabaseHeadRecords_Zombiefied.skull.GetGraphic(Color.white, true);
        }

        // Token: 0x060051D3 RID: 20947 RVA: 0x0025ED7D File Offset: 0x0025D17D
        public static Graphic_Multi GetStump(Color skinColor)
        {
            GraphicDatabaseHeadRecords_Zombiefied.BuildDatabaseIfNecessary();
            return GraphicDatabaseHeadRecords_Zombiefied.stump.GetGraphic(skinColor, false);
        }

        // Token: 0x060051D4 RID: 20948 RVA: 0x0025ED90 File Offset: 0x0025D190
        public static Graphic_Multi GetHeadRandom(Gender gender, Color skinColor, CrownType crownType)
        {
            GraphicDatabaseHeadRecords_Zombiefied.BuildDatabaseIfNecessary();
            Predicate<GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord> predicate = (GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord head) => head.crownType == crownType && head.gender == gender;
            int num = 0;
            GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord headGraphicRecord;
            for (; ; )
            {
                headGraphicRecord = GraphicDatabaseHeadRecords_Zombiefied.heads.RandomElement<GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord>();
                if (predicate(headGraphicRecord))
                {
                    break;
                }
                num++;
                if (num > 40)
                {
                    goto Block_2;
                }
            }
            return headGraphicRecord.GetGraphic(skinColor, false);
            Block_2:
            foreach (GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord headGraphicRecord2 in GraphicDatabaseHeadRecords_Zombiefied.heads.InRandomOrder(null))
            {
                if (predicate(headGraphicRecord2))
                {
                    return headGraphicRecord2.GetGraphic(skinColor, false);
                }
            }
            Log.Error("Failed to find head for gender=" + gender + ". Defaulting...", false);
            return GraphicDatabaseHeadRecords_Zombiefied.heads.First<GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord>().GetGraphic(skinColor, false);
        }

        // Token: 0x04003687 RID: 13959
        private static List<GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord> heads = new List<GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord>();

        // Token: 0x04003688 RID: 13960
        private static GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord skull;

        // Token: 0x04003689 RID: 13961
        private static GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord stump;

        // Token: 0x0400368A RID: 13962
        private static readonly string[] HeadsFolderPaths = new string[]
        {
            "Things/Pawn/Humanlike/Heads/Male",
            "Things/Pawn/Humanlike/Heads/Female",
        };

        // Token: 0x0400368B RID: 13963
        private static readonly string SkullPath = "Things/Pawn/Humanlike/Heads/None_Average_Skull";

        // Token: 0x0400368C RID: 13964
        private static readonly string StumpPath = "Things/Pawn/Humanlike/Heads/None_Average_Stump";

        // Token: 0x02000E29 RID: 3625
        private class HeadGraphicRecord
        {
            // Token: 0x060051D6 RID: 20950 RVA: 0x0025EED8 File Offset: 0x0025D2D8
            public HeadGraphicRecord(string graphicPath)
            {
                this.graphicPath = graphicPath;
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(graphicPath);
                string[] array = fileNameWithoutExtension.Split(new char[]
                {
                    '_'
                });
                try
                {
                    this.crownType = (CrownType)ParseHelper.FromString(array[array.Length - 2], typeof(CrownType));
                    this.gender = (Gender)ParseHelper.FromString(array[array.Length - 3], typeof(Gender));
                }
                catch (Exception ex)
                {
                    //Log.Error("Parse error with head graphic at " + graphicPath + ": " + ex.Message, false);
                    this.crownType = CrownType.Undefined;
                    this.gender = Gender.None;
                }
            }

            // Token: 0x060051D7 RID: 20951 RVA: 0x0025EFA0 File Offset: 0x0025D3A0
            public Graphic_Multi GetGraphic(Color color, bool dessicated = false)
            {
                for (int i = 0; i < this.graphics.Count; i++)
                {
                    if (color.IndistinguishableFrom(this.graphics[i].Key))
                    {
                        return this.graphics[i].Value;
                    }
                }
                Shader shader = dessicated ? ShaderDatabase.Cutout : ShaderDatabase.CutoutSkin;
                Graphic_Multi graphic_Multi = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(this.graphicPath, shader, Vector2.one, color);
                this.graphics.Add(new KeyValuePair<Color, Graphic_Multi>(color, graphic_Multi));
                return graphic_Multi;
            }

            // Token: 0x0400368D RID: 13965
            public Gender gender;

            // Token: 0x0400368E RID: 13966
            public CrownType crownType;

            // Token: 0x0400368F RID: 13967
            public string graphicPath;

            // Token: 0x04003690 RID: 13968
            private List<KeyValuePair<Color, Graphic_Multi>> graphics = new List<KeyValuePair<Color, Graphic_Multi>>();
        }
    }
}
