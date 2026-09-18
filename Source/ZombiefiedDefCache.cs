using RimWorld;
using Verse;

namespace Zombiefied
{
    internal static class ZombiefiedDefCache
    {
        private static HediffDef zombieWoundInfection;
        private static HediffDef zombiefied;

        public static HediffDef ZombieWoundInfection
        {
            get
            {
                if (zombieWoundInfection == null)
                {
                    zombieWoundInfection = DefDatabase<HediffDef>.GetNamedSilentFail("ZombieWoundInfection");
                }
                return zombieWoundInfection;
            }
        }

        public static HediffDef Zombiefied
        {
            get
            {
                if (zombiefied == null)
                {
                    zombiefied = DefDatabase<HediffDef>.GetNamedSilentFail("Zombiefied");
                }
                return zombiefied;
            }
        }
    }
}
