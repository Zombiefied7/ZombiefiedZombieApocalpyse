using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Zombiefied
{
    public class StatPart_ZombiefiedArmor : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn_Zombiefied zombie = req.Thing as Pawn_Zombiefied;
            if (zombie == null)
            {
                return;
            }

            float storedArmor = 0f;
            if (parentStat == StatDefOf.ArmorRating_Sharp)
            {
                storedArmor = zombie.armorRating_Sharp;
            }
            else if (parentStat == StatDefOf.ArmorRating_Blunt)
            {
                storedArmor = zombie.armorRating_Blunt;
            }
            else if (parentStat == StatDefOf.ArmorRating_Heat)
            {
                storedArmor = zombie.armorRating_Heat;
            }

            // A positive captured clothing-derived value replaces the generated zombie's own armor result.
            // Zero falls through to the normal race and hediff calculation.
            if (storedArmor > 0f)
            {
                val = storedArmor;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            Pawn_Zombiefied zombie = req.Thing as Pawn_Zombiefied;
            if (zombie == null)
            {
                return null;
            }

            float storedArmor = 0f;
            if (parentStat == StatDefOf.ArmorRating_Sharp)
            {
                storedArmor = zombie.armorRating_Sharp;
            }
            else if (parentStat == StatDefOf.ArmorRating_Blunt)
            {
                storedArmor = zombie.armorRating_Blunt;
            }
            else if (parentStat == StatDefOf.ArmorRating_Heat)
            {
                storedArmor = zombie.armorRating_Heat;
            }

            return storedArmor > 0f ? "Armor retained from clothing worn before reanimation" : null;
        }
    }

    internal static class ZombieArmorStatUtility
    {
        public static void Install()
        {
            InstallOn(StatDefOf.ArmorRating_Sharp);
            InstallOn(StatDefOf.ArmorRating_Blunt);
            InstallOn(StatDefOf.ArmorRating_Heat);
        }

        private static void InstallOn(StatDef stat)
        {
            if (stat == null)
            {
                return;
            }

            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }

            for (int i = 0; i < stat.parts.Count; i++)
            {
                if (stat.parts[i] is StatPart_ZombiefiedArmor)
                {
                    return;
                }
            }

            StatPart_ZombiefiedArmor part = new StatPart_ZombiefiedArmor
            {
                parentStat = stat
            };
            stat.parts.Add(part);

            // This part is installed after XML loading. If RimWorld has already classified the stat as immutable,
            // invalidate that classification and its cache so a zombie cannot inherit a pre-install cached value.
            stat.immutable = false;
            StatWorker worker = stat.Worker;
            worker.DeleteStatCache();
            worker.SetCacheability(false);
        }
    }
}
