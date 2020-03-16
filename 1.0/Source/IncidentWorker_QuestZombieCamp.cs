using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;
using RimWorld;

namespace Zombiefied
{
    // Token: 0x02000330 RID: 816
    public class IncidentWorker_QuestZombieCamp : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Faction faction;
            int num;
            return base.CanFireNowSub(parms) && this.TryFindFactions(out faction) && TileFinder.TryFindNewSiteTile(out num, 8, 30, false, true, -1);
        }

        // Token: 0x06000DA9 RID: 3497 RVA: 0x000648C0 File Offset: 0x00062CC0
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Faction faction;
            if (!this.TryFindFactions(out faction))
            {
                return false;
            }
            int tile;
            if (!TileFinder.TryFindNewSiteTile(out tile, 8, 30, false, true, -1))
            {
                return false;
            }
            Faction zFaction = Faction.OfInsects;
            foreach (Faction factionZ in Find.FactionManager.AllFactionsListForReading)
            {
                if (factionZ.def.defName == "Zombie")
                {
                    zFaction = factionZ;
                }
            }
            SitePartDef def = DefDatabase<SitePartDef>.GetNamed("ZombieOutpost", false);
            Site site = SiteMaker.MakeSite(SiteCoreDefOf.Nothing, def, tile, faction);
            //site.Tile = tile;
            List<Thing> list = this.GenerateRewards(faction);
            site.GetComponent<DefeatAllEnemiesQuestComp>().StartQuest(faction, 7, list);
            Find.WorldObjects.Add(site);
            /*
            base.SendStandardLetter(site, new string[]
            {
                faction.leader.LabelShort,
                faction.def.leaderTitle,
                faction.Name,
                list[0].LabelCap
            });
            */
            return true;
        }

        // Token: 0x06000DAA RID: 3498 RVA: 0x0006497C File Offset: 0x00062D7C
        private List<Thing> GenerateRewards(Faction alliedFaction)
        {
            return ThingSetMakerDefOf.Reward_StandardByDropPod.root.Generate();
        }

        // Token: 0x06000DAB RID: 3499 RVA: 0x000649B8 File Offset: 0x00062DB8
        private bool TryFindFactions(out Faction alliedFaction)
        {
            if ((from x in Find.FactionManager.AllFactions
                 where !x.def.hidden && !x.defeated && !x.IsPlayer && !x.HostileTo(Faction.OfPlayer) && !this.AnyQuestExistsFrom(x)
                 select x).TryRandomElement(out alliedFaction))
            {
                return true;
            }
            alliedFaction = null;
            return false;
        }

        // Token: 0x06000DAC RID: 3500 RVA: 0x000649F8 File Offset: 0x00062DF8
        private bool AnyQuestExistsFrom(Faction faction)
        {
            List<Site> sites = Find.WorldObjects.Sites;
            for (int i = 0; i < sites.Count; i++)
            {
                DefeatAllEnemiesQuestComp component = sites[i].GetComponent<DefeatAllEnemiesQuestComp>();
                if (component != null && component.Active && component.requestingFaction == faction)
                {
                    return true;
                }
            }
            return false;
        }

        // Token: 0x040008A5 RID: 2213
        private const float RelationsImprovement = 8f;
    }
}
