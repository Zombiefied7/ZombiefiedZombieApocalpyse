using HarmonyLib;
using Verse;

namespace Zombiefied
{
    [StaticConstructorOnStartup]
    public static class ZombiefiedStartup
    {
        static ZombiefiedStartup()
        {
            new Harmony("zombiefied.zombiefied").PatchAll();
            ZombiefiedMod.Instance?.InitializeCustomOnce();
        }
    }
}
