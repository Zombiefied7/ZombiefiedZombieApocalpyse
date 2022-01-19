using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
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

            this.wornApparelDefs = new List<ThingDef>();
            this.wornApparelColors = new List<Color>();

        }
        public ZombieData(Color color, Color hairColor, string shaderCutoutPath)
        {
            this.color = color;
            this.hairColor = hairColor;
            this.shaderCutoutPath = shaderCutoutPath;
        }
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
        public string headGraphicPath;
        public string hairGraphicPath;
        public CrownType crownType;
        public Color color;
        public Color hairColor;
        public string shaderCutoutPath;
        public List<ThingDef> wornApparelDefs;
        public List<Color> wornApparelColors;
    }
}
