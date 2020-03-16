using System;
using System.Collections.Generic;
using System.Linq;
using HugsLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class Corpse_Zombiefied : Corpse
    {
        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Building building = this.StoringThing() as Building;
            if (building != null && building.def == ThingDefOf.Grave)
            {
                return;
            }

            Pawn_Zombiefied pawnZ = InnerPawn as Pawn_Zombiefied;

            if (pawnZ != null)
            {
                if (pawnZ.def.defName != "Zombie")
                {
                    base.DrawAt(drawLoc, flip);
                }
                else if (pawnZ.drawerZ != null)
                {
                    pawnZ.drawerZ.renderer.RenderPawnAt(drawLoc);
                }
                else
                {
                    pawnZ.newGraphics();
                }
            }
            else
            {
                base.DrawAt(drawLoc, flip);
            }
        }
    }
}
