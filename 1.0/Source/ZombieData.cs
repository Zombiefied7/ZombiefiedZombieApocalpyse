using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    // Token: 0x02000016 RID: 22
    public class ZombieData : IExposable
    {
        // Token: 0x0600006A RID: 106 RVA: 0x0000303E File Offset: 0x0000123E
        public ZombieData()
        {
            this.hairColor = Color.green;
            this.color = Color.green;
            this.shaderCutoutPath = "Map/Cutout";

            this.bodyType = BodyTypeDefOf.Female;
            this.headGraphicPath = "Things/Pawn/Humanlike/Heads/None_Average_Skull";
            
            this.hairGraphicPath = "Things/Pawn/Humanlike/Hairs/Bob";
            this.crownType = CrownType.Average;

            this.wornApparelDefs = new List<ThingDef>();
            this.wornApparelColors = new List<Color>();

        }

        // Token: 0x0600006B RID: 107 RVA: 0x00004630 File Offset: 0x00002830
        public ZombieData(Color color, Color hairColor, string shaderCutoutPath)
        {
            this.color = color;
            this.hairColor = hairColor;
            this.shaderCutoutPath = shaderCutoutPath;
        }

        // Token: 0x0600006C RID: 108 RVA: 0x00004648 File Offset: 0x00002848
        public ZombieData(Pawn pawn)
        {
            this.bodyType = pawn.story.bodyType;

            if(pawn.Corpse == null)
            {
                this.headGraphicPath = pawn.story.HeadGraphicPath;
            }
            else
            {
                string reflectedPath = pawn.story.GetFieldValue<string>("headGraphicPath");
                //Log.Message("++" + reflectedPath + "++");
                if (reflectedPath != null && reflectedPath.Length > 7)
                {
                    this.headGraphicPath = reflectedPath;
                }
                else
                {
                    this.headGraphicPath = "Things/Pawn/Humanlike/Heads/None_Average_Skull";
                }
            }        

            this.hairGraphicPath = pawn.story.hairDef.texPath;
            this.crownType = pawn.story.crownType;

            this.color = new Color(pawn.story.SkinColor.r * 0.5f, pawn.story.SkinColor.g * 0.7f, pawn.story.SkinColor.b * 0.5f);
            this.hairColor = pawn.story.hairColor;
            this.shaderCutoutPath = "Map/Cutout";
            this.wornApparelDefs = pawn.apparel.WornApparel.ConvertAll<ThingDef>((Apparel ap) => ap.def);
            this.wornApparelColors = new List<Color>();
            foreach(Apparel worn in pawn.apparel.WornApparel)
            {
                this.wornApparelColors.Add(worn.DrawColor);
            }
        }

        // Token: 0x0600006D RID: 109 RVA: 0x000046E8 File Offset: 0x000028E8
        public ZombieData(ZombieData source, Color color, Color hairColor, string shaderCutoutPath)
        {
            this.bodyType = source.bodyType;
            this.headGraphicPath = source.headGraphicPath;
            this.hairGraphicPath = source.hairGraphicPath;
            this.crownType = source.crownType;
            this.color = color;
            this.hairColor = hairColor;
            this.shaderCutoutPath = shaderCutoutPath;
            //this.wornApparelDefs = new List<ThingDef>(source.wornApparelDefs);
        }

        /*
        // Token: 0x0600006E RID: 110 RVA: 0x0000474C File Offset: 0x0000294C
        public void CopyFromPreset(ZombiePreset preset)
        {
            this.bodyType = preset.bodyType;
            this.headGraphicPath = preset.headGraphicPath;
            this.hairGraphicPath = preset.hairDef.texPath;
            this.crownType = preset.crownType;
            this.wornApparelDefs = preset.apparels.ConvertAll<ThingDef>((ZombieApparelData data) => data.apparelDef);
        }
        */

        // Token: 0x0600006F RID: 111 RVA: 0x000047C0 File Offset: 0x000029C0
        public void ExposeData()
        {
            Scribe_Defs.Look<BodyTypeDef>(ref this.bodyType, "bodyTypeDef");
            Scribe_Values.Look<string>(ref this.headGraphicPath, "headGraphicPath", null, false);
            Scribe_Values.Look<string>(ref this.hairGraphicPath, "hairGraphicPath", null, false);
            Scribe_Values.Look<CrownType>(ref this.crownType, "crownType", CrownType.Undefined, false);
            Scribe_Values.Look<Color>(ref this.color, "color", default(Color), false);
            Scribe_Values.Look<Color>(ref this.hairColor, "hairColor", default(Color), false);
            Scribe_Values.Look<string>(ref this.shaderCutoutPath, "shaderCutoutPath", null, false);
            Scribe_Collections.Look<ThingDef>(ref this.wornApparelDefs, "wornApparelDefs", LookMode.Def, new object[0]);
            Scribe_Collections.Look<Color>(ref this.wornApparelColors, "wornApparelColors", LookMode.Value, new object[0]);
        }

        // Token: 0x06000070 RID: 112 RVA: 0x00004858 File Offset: 0x00002A58
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

        // Token: 0x0400005C RID: 92
        public BodyTypeDef bodyType;

        // Token: 0x0400005D RID: 93
        public string headGraphicPath;

        // Token: 0x0400005E RID: 94
        public string hairGraphicPath;

        // Token: 0x0400005F RID: 95
        public CrownType crownType;

        // Token: 0x04000060 RID: 96
        public Color color;

        public Color hairColor;

        // Token: 0x04000061 RID: 97
        public string shaderCutoutPath;

        // Token: 0x04000062 RID: 98
        public List<ThingDef> wornApparelDefs;

        public List<Color> wornApparelColors;
    }
}
