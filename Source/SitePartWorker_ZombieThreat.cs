using RimWorld;

namespace Zombiefied
{
    public class SitePartWorker_ZombieThreat : SitePartWorker
    {
        public override bool IsAvailable()
        {
            return ZombiefiedMod.enableZombieWorldSiteThreats;
        }

        public override bool FactionCanOwn(Faction faction)
        {
            // Zombie infestations are environmental threats rather than faction-owned outposts.
            // Keeping the site factionless prevents an unrelated hostile faction from being shown
            // as the owner when vanilla quest generators select this threat.
            return ZombiefiedMod.enableZombieWorldSiteThreats && faction == null;
        }
    }
}
