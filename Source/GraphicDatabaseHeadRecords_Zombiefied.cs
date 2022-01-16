using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace Zombiefied
{
    public static class GraphicDatabaseHeadRecords_Zombiefied
    {
        public static void Reset()
        {
            GraphicDatabaseHeadRecords_Zombiefied.heads.Clear();
            GraphicDatabaseHeadRecords_Zombiefied.skull = null;
            GraphicDatabaseHeadRecords_Zombiefied.stump = null;
        }
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
        public static Graphic_Multi GetHeadNamed(string graphicPath, Color skinColor)
        {
            GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord HGR = new GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord(graphicPath);
            return HGR.GetGraphic(skinColor, false);
        }
        public static Graphic_Multi GetSkull()
        {
            GraphicDatabaseHeadRecords_Zombiefied.BuildDatabaseIfNecessary();
            return GraphicDatabaseHeadRecords_Zombiefied.skull.GetGraphic(Color.white, true);
        }
        public static Graphic_Multi GetStump(Color skinColor)
        {
            GraphicDatabaseHeadRecords_Zombiefied.BuildDatabaseIfNecessary();
            return GraphicDatabaseHeadRecords_Zombiefied.stump.GetGraphic(skinColor, false);
        }
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
            Log.Error("Failed to find head for gender=" + gender + ". Defaulting...");
            return GraphicDatabaseHeadRecords_Zombiefied.heads.First<GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord>().GetGraphic(skinColor, false);
        }
        private static List<GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord> heads = new List<GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord>();
        private static GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord skull;
        private static GraphicDatabaseHeadRecords_Zombiefied.HeadGraphicRecord stump;
        private static readonly string[] HeadsFolderPaths = new string[]
        {
            "Things/Pawn/Humanlike/Heads/Male",
            "Things/Pawn/Humanlike/Heads/Female",
        };
        private static readonly string SkullPath = "Things/Pawn/Humanlike/Heads/None_Average_Skull";
        private static readonly string StumpPath = "Things/Pawn/Humanlike/Heads/None_Average_Stump";
        private class HeadGraphicRecord
        {
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
                catch (Exception)
                {
                    this.crownType = CrownType.Undefined;
                    this.gender = Gender.None;
                }
            }
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
            public Gender gender;
            public CrownType crownType;
            public string graphicPath;
            private List<KeyValuePair<Color, Graphic_Multi>> graphics = new List<KeyValuePair<Color, Graphic_Multi>>();
        }
    }
}
