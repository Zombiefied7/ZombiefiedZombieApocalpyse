using Verse;

namespace Zombiefied
{
    public class ZombiefiedSettings : ModSettings
    {
        public bool disableAnimalZombies = false;
        public bool disableZombiesAttackingAnimals = false;
        public float zombieSpeedMultiplier = 0.57f;
        public int zombieSoundReactionTimeInHours = 5;
        public int zombieAmountSoftCap = 133;
        public float zombieRaidAmountMultiplier = 1f;
        public float zombieRaidFrequencyMultiplier = 1f;
        public bool zombieRaidNotifications = true;
        public bool zombieResurrectNotifications = false;
        public bool debugRemoveZombies = false;
        public bool enableZombieWorldSiteThreats = true;
        public bool enableCaravanZombieEncounters = true;
        public float zombieCaravanEncounterMultiplier = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref disableAnimalZombies, "disableAnimalZombies", false);
            Scribe_Values.Look(ref disableZombiesAttackingAnimals, "disableZombiesAttackingAnimals", false);
            Scribe_Values.Look(ref zombieSpeedMultiplier, "ZombieSpeedMultiplier", 0.57f);
            Scribe_Values.Look(ref zombieSoundReactionTimeInHours, "ZombieSoundReactionTimeInHours", 5);
            Scribe_Values.Look(ref zombieAmountSoftCap, "ZombieAmountSoftCap", 133);
            Scribe_Values.Look(ref zombieRaidAmountMultiplier, "ZombieRaidAmountMultiplier", 1f);
            Scribe_Values.Look(ref zombieRaidFrequencyMultiplier, "ZombieRaidFrequencyMultiplier", 1f);
            Scribe_Values.Look(ref zombieRaidNotifications, "ZombieRaidNotifications", true);
            Scribe_Values.Look(ref zombieResurrectNotifications, "ZombieResurrectNotifications", false);
            Scribe_Values.Look(ref debugRemoveZombies, "DebugRemoveZombies", false);
            Scribe_Values.Look(ref enableZombieWorldSiteThreats, "EnableZombieWorldSiteThreats", true);
            Scribe_Values.Look(ref enableCaravanZombieEncounters, "EnableCaravanZombieEncounters", true);
            Scribe_Values.Look(ref zombieCaravanEncounterMultiplier, "ZombieCaravanEncounterMultiplier", 1f);
            base.ExposeData();
        }
    }
}
