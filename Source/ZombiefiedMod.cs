using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using HugsLib;
using HugsLib.Settings;
using HugsLib.Utils;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    public class ZombiefiedMod : ModBase
    {
        public static JobDef zombieHunt;
        public static JobDef zombieMove;

        public override string ModIdentifier
        {
            get
            {
                return "Zombiefied";
            }
        }
        public override void WorldLoaded()
        {
            base.Logger.Message("loaded", new object[0]);

            noisyLocationsPerMap = new List<Queue<IntVec3>>();
            noisyLocationTicksPerMap = new List<Queue<int>>();
            zombieAmountsPerMap = new List<int>();
            this._ticksUntilNextZombieRaid = new List<int>();

            for (int i = 0; i < 77; i++)
            {
                noisyLocationsPerMap.Add(new Queue<IntVec3>());
                noisyLocationTicksPerMap.Add(new Queue<int>());
                zombieAmountsPerMap.Add(0);

                int temp = GenerateTicksUntilNextRaid();
                if (temp > 17777)
                {
                    temp = 17777;
                }
                this._ticksUntilNextZombieRaid.Add(temp);
            }

            Faction zFaction = Faction.OfInsects;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.def.defName == "Zombie")
                {
                    zFaction = faction;
                }
            }
            //Faction.OfMechanoids.TrySetNotHostileTo(zFaction);
            zFaction.RelationWith(Faction.OfMechanoids).kind = FactionRelationKind.Ally;
            //zFaction.RelationWith(Faction.OfMechanoids).goodwill = 100;
            zFaction.RelationWith(Faction.OfMechanoids).baseGoodwill = 100;
            Faction.OfMechanoids.RelationWith(zFaction).kind = FactionRelationKind.Ally;
            Faction.OfMechanoids.RelationWith(zFaction).baseGoodwill = 100;
            //zFaction.TrySetNotHostileTo(Faction.OfMechanoids);
            //zFaction.TryMakeInitialRelationsWith(Faction.OfMechanoids);

            int zKilled = 0;
            int zCount = 0;
            int zWrongFactionCount = 0;
            foreach (Map map in Find.Maps)
            {
                foreach (Thing thing in map.listerThings.AllThings.ToList<Thing>())
                {
                    if (debugRemoveZombies && (thing is Pawn_Zombiefied || thing is Corpse_Zombiefied))
                    {
                        zKilled++;
                        thing.Destroy(DestroyMode.Vanish);
                    }
                    if (!debugRemoveZombies && thing is Pawn_Zombiefied)
                    {
                        zCount++;
                        if (((Pawn)thing).Faction != zFaction)
                        {
                            zWrongFactionCount++;
                            ((Pawn)thing).SetFaction(zFaction);
                        }
                        ((Pawn_Zombiefied)thing).FixZombie();
                    }
                }
            }

            /*
            foreach (RimWorld.Planet.Site site in Find.World.worldObjects.Sites)
            {
                bool broken = false;
                if (site != null)
                {
                    if (site.core == null || site.core.def == null || site.core.def.Worker == null || site.core.def.workerClass == null)
                    {
                        broken = true;
                    }

                    foreach (RimWorld.Planet.SitePart part in site.parts)
                    {
                        if(part.def == null || part.def.Worker == null || part.def.workerClass == null)
                        {
                            broken = true;
                        }
                    }
                }
                if(broken)
                {
                    base.Logger.Message("found a broken site.");
                    //handle broken site here
                }
            }
            */

            debugRemoveZombies.Value = false;
            //base.Logger.Message("found " + zCount + " Zombies. " + zWrongFactionCount + " of them were repaired.", new object[0]);
        }

        public override void DefsLoaded()
        {
            Predicate<Rect> predicate = delegate (Rect input)
            {
                return false;
            };
            headlineZombieOptions = base.Settings.GetHandle<bool>("headlineZombies", "\nZombie settings:", "Zombie settings", false, null, null);
            headlineZombieOptions.CustomDrawer = new SettingHandle.DrawCustomControl(predicate);
            //headlineZombieOptions.CustomDrawerHeight = 37f;

            disableAnimalZombies = base.Settings.GetHandle<bool>("disableAnimalZombies", "       Disable animal zombies", "Animals will not resurrect and no animal zombies will wander in.", false, null, null);
            disableZombiesAttackingAnimals = base.Settings.GetHandle<bool>("disableZombiesAttackingAnimals", "       Disable zombies attacking animals", "Zombies will ignore animals.", false, null, null);
            zombieSpeedMultiplier = base.Settings.GetHandle<float>("ZombieSpeedMultiplier", "       Zombie speed multiplier [RESTART]", "Zombie speed (in comparison to healthy) will be multiplied by this value.\n(0.03 -> Slowest, 3 -> Fastest)\n(Requires restart to work)", 0.57f, null, null);
            if (zombieSpeedMultiplier < 0.03f)
            {
                zombieSpeedMultiplier.Value = 0.03f;
            }
            else if (zombieSpeedMultiplier > 3f)
            {
                zombieSpeedMultiplier.Value = 3f;
            }

            zombieSoundReactionTimeInHours = base.Settings.GetHandle<int>("ZombieSoundReactionTimeInHours", "       Zombie sound memory time", "The amount of time zombies will remember a sound's location.\n(in hours, ingame time)", 5, null, null);

            headlineZombieAmount = base.Settings.GetHandle<bool>("headlineAmounts", "\nAmount settings:", "Amount settings", false, null, null);
            headlineZombieAmount.CustomDrawer = new SettingHandle.DrawCustomControl(predicate);
            //headlineZombieAmount.CustomDrawerHeight = 37f;

            //easyMode = base.Settings.GetHandle<bool>("EasyMode", "Easy mode", "Keep in mind that you are not meant to kill all zombies at all times and losing is part of the game.", false, null, null);
            zombieAmountSoftCap = base.Settings.GetHandle<int>("ZombieAmountSoftCap", "       Zombie amount soft cap", "The expected amount of zombies per map.", 133, null, null);

            zombieRaidAmountMultiplier = base.Settings.GetHandle<float>("ZombieRaidAmountMultiplier", "       Zombie raid size multiplier", "Zombie raid size will be multiplied by this value.\n(0.1 -> Easiest, 7 -> Hardest)", 1f, null, null);
            zombieRaidFrequencyMultiplier = base.Settings.GetHandle<float>("ZombieRaidFrequencyMultiplier", "       Zombie raid frequency multiplier", "Zombie raid frequency will be multiplied by this value.\n(0.1 -> Easiest, 7 -> Hardest)", 1f, null, null);

            headlineNotifications = base.Settings.GetHandle<bool>("headlineNotifications", "\nNotification settings:", "Notification settings", false, null, null);
            headlineNotifications.CustomDrawer = new SettingHandle.DrawCustomControl(predicate);
            //headlineNotifications.CustomDrawerHeight = 37f;

            zombieRaidNotifications = base.Settings.GetHandle<bool>("ZombieRaidNotifications", "       Zombie raid notifications", "Get a notification when zombies wander in.", true, null, null);
            zombieResurrectNotifications = base.Settings.GetHandle<bool>("ZombieResurrectNotifications", "       Zombie resurrect notifications", "Get a notification when a zombie resurrects.", false, null, null);

            headlineDebug = base.Settings.GetHandle<bool>("headlineDebug", "\nDebug settings:", "Debug settings", false, null, null);
            headlineDebug.CustomDrawer = new SettingHandle.DrawCustomControl(predicate);
            //headlineDebug.CustomDrawerHeight = 37f;

            debugRemoveZombies = base.Settings.GetHandle<bool>("DebugRemoveZombies", "       Debug remove zombies [RELOAD]", "Enable this and reload to remove all zombies on next load.", false, null, null);

            /*
            zombieSoundReactionTimeInHours.CustomDrawerHeight = 77;
            zombieAmountSoftCap.CustomDrawerHeight = 77;
            zombieRaidAmountMultiplier.CustomDrawerHeight = 77;
            zombieRaidFrequencyMultiplier.CustomDrawerHeight = 77;
            zombieRaidNotifications.CustomDrawerHeight = 77;
            zombieResurrectNotifications.CustomDrawerHeight = 77;
            debugRemoveZombies.CustomDrawerHeight = 77;
            */

            InitializeCustom();
        }

        internal static SettingHandle<bool> headlineZombieOptions;
        //
        internal static SettingHandle<bool> disableAnimalZombies;
        internal static SettingHandle<bool> disableZombiesAttackingAnimals;
        internal static SettingHandle<float> zombieSpeedMultiplier;
        internal static SettingHandle<int> zombieSoundReactionTimeInHours;

        internal static SettingHandle<bool> headlineZombieAmount;
        //
        internal static SettingHandle<float> zombieRaidFrequencyMultiplier;
        public static float ZombieRaidFrequencyMultiplier
        {
            get
            {
                if (zombieRaidFrequencyMultiplier < 0.1f)
                {
                    return 0.1f;
                }
                else if (zombieRaidFrequencyMultiplier > 7f)
                {
                    return 7f;
                }
                return zombieRaidFrequencyMultiplier;
            }
        }
        internal static SettingHandle<float> zombieRaidAmountMultiplier;
        public static float ZombieRaidAmountMultiplier
        {
            get
            {
                if (zombieRaidAmountMultiplier < 0.1f)
                {
                    return 0.1f;
                }
                else if (zombieRaidAmountMultiplier > 7f)
                {
                    return 7f;
                }
                return zombieRaidAmountMultiplier;
            }
        }
        internal static SettingHandle<int> zombieAmountSoftCap;

        internal static SettingHandle<bool> headlineNotifications;
        //
        internal static SettingHandle<bool> zombieRaidNotifications;
        internal static SettingHandle<bool> zombieResurrectNotifications;

        internal static SettingHandle<bool> headlineDebug;
        //
        internal static SettingHandle<bool> debugRemoveZombies;

        public override void Tick(int currentTick)
        {
            this.HandleReanimation();
            this.HandleZombieRaid();
            this.HandleSounds();
        }

        private float GetChallengeModifier()
        {
            float num = 2f - Find.Storyteller.difficulty.threatScale;
            if ((double)num < 0.7f)
            {
                num = 0.7f;
            }
            //if (easyMode)
            //{
            num /= ZombieRaidFrequencyMultiplier;
            //}
            if ((double)num < 0.17f)
            {
                num = 0.17f;
            }
            else if ((double)num > 7f)
            {
                num = 7f;
            }
            return num;
        }

        private int GenerateTicksUntilNextRaid()
        {
            float challengeModifier = this.GetChallengeModifier();
            //int num = UnityEngine.Random.Range((int)((float)ZombiesDefOf.ZombiesSettings.MinRaidTicksBase * challengeModifier), (int)((float)ZombiesDefOf.ZombiesSettings.MaxRaidTicksBase * challengeModifier));
            int num = Rand.RangeSeeded((int)((float)7777 * challengeModifier), (int)((float)280000 * challengeModifier), Find.TickManager.TicksAbs + Find.World.ConstantRandSeed);
            //if (first && num < ZombiesDefOf.ZombiesSettings.MinTicksBeforeFirstRaid)
            //base.Logger.Message("next zombieraid in " + num + " ticks.", new object[0]);
            return num;
        }

        private void HandleZombieRaid()
        {
            for (int currentMapIndex = 0; currentMapIndex < Find.Maps.Count; currentMapIndex++)
            {
                this._ticksUntilNextZombieRaid[currentMapIndex] -= 1 + noisyLocationsPerMap[currentMapIndex].Count;
                if (this._ticksUntilNextZombieRaid[currentMapIndex] <= 0)
                {
                    this._ticksUntilNextZombieRaid[currentMapIndex] = this.GenerateTicksUntilNextRaid();

                    //base.Logger.Message("setting up zombieraid for map " + currentMapIndex + ".", new object[0]);

                    if (currentMapIndex < zombieAmountsPerMap.Count && zombieAmountsPerMap[currentMapIndex] < zombieAmountSoftCap)
                    {
                        IncidentParms incidentParms = new IncidentParms
                        {
                            target = Find.Maps[currentMapIndex]
                        };

                        int ran = Rand.RangeSeeded(0, 7, Find.TickManager.TicksAbs);
                        if (ran < 5 || disableAnimalZombies)
                        {
                            IncidentDef.Named("ZombieHorde").Worker.TryExecute(incidentParms);
                        }
                        else
                        {
                            IncidentDef.Named("ZombiePack").Worker.TryExecute(incidentParms);
                        }
                        //base.Logger.Message("Zombieraid started on map " + currentMapIndex + ".", new object[0]);
                    }
                    else
                    {
                        //base.Logger.Message("Zombieraid failed on map " + currentMapIndex + " because map invalid or more zombies are already on the map than cap allows.", new object[0]);
                    }
                }
            }
        }

        public static List<Queue<IntVec3>> noisyLocationsPerMap = new List<Queue<IntVec3>>(0);
        public static List<Queue<int>> noisyLocationTicksPerMap = new List<Queue<int>>(0);
        public static List<int> zombieAmountsPerMap = new List<int>(0);
        public static Dictionary<string, int> shotsFiredPerPawn = new Dictionary<string, int>();

        private void HandleSounds()
        {
            int tickAmount = 333;

            if (Find.TickManager.TicksAbs % tickAmount == 0)
            {
                String log = "";

                for (int m = 0; m < noisyLocationsPerMap.Count; m++)
                {
                    if (noisyLocationsPerMap[m].Count > 0)
                    {
                        if (Find.TickManager.TicksGame - noisyLocationTicksPerMap[m].Peek() > zombieSoundReactionTimeInHours * 2500) //7777)
                        {
                            noisyLocationsPerMap[m].Dequeue();
                            noisyLocationTicksPerMap[m].Dequeue();
                        }
                    }
                }

                for (int m = 0; m < Find.Maps.Count; m++)
                {
                    int zombieAmount = 0;

                    int bestLocationScore = -7;
                    IntVec3 bestLocation = IntVec3.Invalid;
                    int bestLocationTicks = 0;

                    if (Find.Maps[m] != null && Find.Maps[m].mapPawns != null && Find.Maps[m].listerBuildings != null)
                    {
                        List<Pawn> allPawnsSpawned = Find.Maps[m].mapPawns.AllPawnsSpawned;
                        if (allPawnsSpawned != null)
                        {
                            for (int i = 0; i < allPawnsSpawned.Count; i++)
                            {
                                Pawn pawn2 = allPawnsSpawned[i];
                                if (pawn2 != null)
                                {
                                    if (pawn2.def.defName.Length > 5 && pawn2.def.defName.Substring(0, 6) == "Zombie")
                                    {
                                        zombieAmount++;
                                    }

                                    if (pawn2.RaceProps.intelligence > Intelligence.Animal)
                                    {
                                        bool weapon = false;
                                        foreach (ThingWithComps eq in pawn2.equipment.AllEquipmentListForReading)
                                        {
                                            if (eq.def != null && eq.def.IsRangedWeapon && eq.def.weaponTags != null)
                                            {
                                                bool temp = true;
                                                foreach (String tag in eq.def.weaponTags)
                                                {
                                                    if (tag.Equals("Neolithic"))
                                                    {
                                                        temp = false;
                                                    }
                                                }
                                                weapon = temp;
                                            }
                                        }
                                        string key = pawn2.GetHashCode() + "";

                                        if (weapon && shotsFiredPerPawn.ContainsKey(key) &&
                                            pawn2.records.GetAsInt(RecordDefOf.ShotsFired) > shotsFiredPerPawn[key] &&
                                            (pawn2.LastAttackedTarget != null &&
                                            (Find.TickManager.TicksGame - pawn2.LastAttackTargetTick < tickAmount)))
                                        {
                                            int tempScore = 7;
                                            if (pawn2.Faction != null && pawn2.Faction.IsPlayer)
                                            {
                                                tempScore = 13;
                                            }
                                            if (tempScore > bestLocationScore)
                                            {
                                                bestLocationScore = tempScore;
                                                bestLocation = pawn2.Position;
                                                bestLocationTicks = pawn2.LastAttackTargetTick;
                                            }

                                            //noisyLocationsPerMap[m].Enqueue(pawn2.Position);
                                            //noisyLocationTicksPerMap[m].Enqueue(pawn2.LastAttackTargetTick);
                                        }
                                        shotsFiredPerPawn[key] = pawn2.records.GetAsInt(RecordDefOf.ShotsFired);
                                    }
                                }
                            }
                        }

                        if (bestLocationScore < 0)
                        {
                            List<Building> allBuildings = Find.Maps[m].listerBuildings.allBuildingsColonist;
                            if (allBuildings != null)
                            {
                                for (int i = 0; i < allBuildings.Count; i++)
                                {
                                    Building_Turret building2 = allBuildings[i] as Building_Turret;
                                    if (building2 != null)
                                    {
                                        if ((building2.LastAttackedTarget != null && (Find.TickManager.TicksGame - building2.LastAttackTargetTick < tickAmount)))
                                        {
                                            int tempScore = 3;
                                            if (building2.Faction != null && building2.Faction.IsPlayer)
                                            {
                                                tempScore = 13;
                                            }
                                            if (tempScore > bestLocationScore)
                                            {
                                                bestLocationScore = tempScore;
                                                bestLocation = building2.Position;
                                                bestLocationTicks = building2.LastAttackTargetTick;
                                            }
                                            //noisyLocationsPerMap[m].Enqueue(building2.Position);
                                            //noisyLocationTicksPerMap[m].Enqueue(building2.LastAttackTargetTick);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (bestLocationScore > 0)
                    {
                        noisyLocationsPerMap[m].Enqueue(bestLocation);
                        noisyLocationTicksPerMap[m].Enqueue(bestLocationTicks);
                    }

                    zombieAmountsPerMap[m] = zombieAmount;

                    log += "map " + m + " has " + noisyLocationsPerMap[m].Count + " noisy locations and " + zombieAmountsPerMap[m] + " zombies.   ";
                }
                //base.Logger.Message(log, new object[0]);
            }
        }

        public static IntVec3 BestNoisyLocation(Pawn predator)
        {
            List<IntVec3> locations = null;
            int currentMapIndex = -7;

            IntVec3 location = IntVec3.Invalid;

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                if (predator.Map != null && Find.Maps[i] == predator.Map)
                {
                    currentMapIndex = i;
                }
            }

            if (currentMapIndex > -1 && currentMapIndex < noisyLocationsPerMap.Count)
            {
                locations = noisyLocationsPerMap[currentMapIndex].ToList();
            }


            if (locations != null && locations.Count > 0)
            {
                location = locations[locations.Count - 1];
                /*
                for (int i = 0; i < locations.Count; i++)
                {
                    if (location == IntVec3.Invalid || num > (predator.Position - locations[i]).LengthHorizontal)
                    {
                        location = locations[i];
                        num = (predator.Position - locations[i]).LengthHorizontal;
                    }
                }
                */
            }

            return location;
        }
        private void HandleReanimation()
        {
            if (Find.TickManager.TicksAbs % 1777 == 7)
            {
                foreach (Map map in Find.Maps)
                {
                    //List<Thing> list = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
                    List<Thing> list = map.listerThings.AllThings;
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            Corpse corpse = list[i] as Corpse;
                            if (corpse != null)
                            {
                                bool flag = false;
                                foreach (Hediff hediff in corpse.InnerPawn.health.hediffSet.hediffs)
                                {
                                    if (hediff.def.defName.Equals("ZombieWoundInfection"))
                                    {
                                        flag = true;
                                    }
                                }
                                if (flag && corpse.InnerPawn.health.hediffSet.GetBrain() != null && corpse.InnerPawn.RaceProps.IsFlesh && (!disableAnimalZombies || corpse.InnerPawn.RaceProps.intelligence > Intelligence.Animal))
                                {
                                    int ageToReanimate = 2500;
                                    if (corpse.InnerPawn != null && corpse.InnerPawn.Faction != null && corpse.InnerPawn.Faction.IsPlayer)
                                    {
                                        ageToReanimate = 17500;
                                    }
                                    if (corpse.Age > ageToReanimate)
                                    {
                                        ReanimateDeath(corpse);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private List<int> _ticksUntilNextZombieRaid = new List<int>(0);

        public Pawn ReanimateDeath(Corpse corpse)
        {
            //base.Logger.Message(corpse.InnerPawn.story.HeadGraphicPath, new object[0]);
            Pawn_Zombiefied zombiePawn = ZombiefiedMod.GenerateZombieFromSource(corpse.InnerPawn);
            //Pawn zombiePawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
            IntVec3 position = corpse.Position;
            Building building = StoreUtility.StoringThing(corpse) as Building;
            Building_Storage building_Storage = (Building_Storage)building;

            if (building_Storage != null)
            {
                building_Storage.Notify_LostThing(corpse);
            }

            Thing t = GenSpawn.Spawn(zombiePawn, position, corpse.Map);
            corpse.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, 77777f));

            //for(int i = 0; i < Find.BattleLog.Battles.Count; i++)
            //{
            //InteractionCardUtility_Zombiefied.DrawInteractionsLog(corpse.InnerPawn, t, Find.BattleLog.Battles[i].Entries, 50);
            //}
            //InteractionCardUtility_Zombiefied.DrawInteractionsLog(corpse.InnerPawn, t, Find.BattleLog.RawEntries, 50);

            if (zombieResurrectNotifications && t != null)
            {
                Find.LetterStack.ReceiveLetter("Zombie", "A zombie resurrected.", LetterDefOf.NeutralEvent, t, null);
            }

            Faction zFaction = Faction.OfInsects;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.def.defName == "Zombie")
                {
                    zFaction = faction;
                }
            }
            zombiePawn.SetFactionDirect(zFaction);
            zombiePawn.FixZombie();

            return zombiePawn;
        }

        public static Pawn_Zombiefied GenerateZombieFromSource(Pawn sourcePawn)
        {
            PawnKindDef newKindDef = PawnKindDef.Named("Zombie");
            if (DefDatabase<PawnKindDef>.GetNamed("Zombie" + sourcePawn.kindDef.defName, false) != null)
            {
                newKindDef = PawnKindDef.Named("Zombie" + sourcePawn.kindDef.defName);
            }

            Pawn pawn = PawnGenerator.GeneratePawn(newKindDef);
            Pawn_Zombiefied z = pawn as Pawn_Zombiefied;
            if (z != null)
            {
                z.newGraphics(sourcePawn);
                z.copyInjuries(sourcePawn);
            }


            Faction zFaction = Faction.OfInsects;
            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction.def.defName == "Zombie")
                {
                    zFaction = faction;
                }
            }
            pawn.SetFactionDirect(zFaction);

            pawn.records = sourcePawn.records;
            pawn.gender = sourcePawn.gender;
            //pawn.needs.SetInitialLevels();

            if (sourcePawn.Faction != null && sourcePawn.Faction.IsPlayer)
            {
                NameSingle nameSingle = sourcePawn.Name as NameSingle;
                if(nameSingle != null)
                {
                    pawn.Name = new NameSingle("Zombie " + nameSingle.Name);
                }
                NameTriple nameTriple = sourcePawn.Name as NameTriple;
                if (nameTriple != null)
                {
                    pawn.Name = new NameSingle("Zombie " + nameTriple.Nick);
                }
            }

            pawn.ageTracker.AgeBiologicalTicks = sourcePawn.ageTracker.AgeBiologicalTicks;
            pawn.ageTracker.BirthAbsTicks = sourcePawn.ageTracker.BirthAbsTicks;
            pawn.ageTracker.AgeChronologicalTicks = sourcePawn.ageTracker.AgeChronologicalTicks;



            return z;
        }

        public void InitializeCustom()
        {
            int count = 0;

            zombieHunt = new JobDef();
            zombieHunt.driverClass = typeof(JobDriver_ZombieHunt);
            zombieHunt.defName = "ZombieHunt";
            zombieHunt.reportString = "hunting TargetA.";
            zombieHunt.casualInterruptible = false;
            zombieHunt.checkOverrideOnDamage = CheckJobOverrideOnDamageMode.Never;
            zombieHunt.allowOpportunisticPrefix = true;
            zombieHunt.collideWithPawns = false;
            zombieHunt.neverFleeFromEnemies = true;
            InjectedDefHasher.GiveShortHashToDef(zombieHunt, typeof(JobDef));

            zombieMove = new JobDef();
            zombieMove.driverClass = typeof(JobDriver_ZombieMove);
            zombieMove.defName = "ZombieMove";
            zombieMove.reportString = "moving TargetA.";
            zombieMove.casualInterruptible = false;
            zombieMove.checkOverrideOnDamage = CheckJobOverrideOnDamageMode.Never;
            zombieMove.allowOpportunisticPrefix = true;
            zombieMove.collideWithPawns = false;
            zombieMove.neverFleeFromEnemies = true;
            InjectedDefHasher.GiveShortHashToDef(zombieMove, typeof(JobDef));

            ThingDef zombieThingDef = ThingDef.Named("Zombie");
            ToolCapacityDef zBite = new ToolCapacityDef();
            ToolCapacityDef zScratch = new ToolCapacityDef();
            foreach (Tool tool in zombieThingDef.tools)
            {
                foreach (ToolCapacityDef capa in tool.capacities)
                {
                    if (capa.defName == "ZombieScratch")
                    {
                        zScratch = capa;
                    }
                    if (capa.defName == "ZombieBite")
                    {
                        zBite = capa;
                    }
                }
            }

            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            foreach (RecipeDef recipe in recipes)
            {
                recipe.defaultIngredientFilter.SetAllow(zombieThingDef.race.meatDef, false);
            }

            List<ThingDef> things = DefDatabase<ThingDef>.AllDefsListForReading;
            foreach (ThingDef thing in things)
            {
                if (thing.thingClass == typeof(Corpse) && thing.defName.Contains("Zombie"))
                {
                    thing.thingClass = typeof(Corpse_Zombiefied);

                    Predicate<StatModifier> findFlammability = delegate (StatModifier statMod)
                    {
                        if (statMod.stat.defName == "Flammability")
                        {
                            return true;
                        }
                        return false;
                    };

                    thing.statBases.Find(findFlammability).value = 1f;
                }
            }
            ThingRequestGroupUtility.StoreInRegion(ThingRequestGroup.Corpse);
            //Pawn pawntest;
            //pawntest.Map.listerThings.Add

            bool first = true;
            List<PawnKindDef> listPawnKindDef = DefDatabase<PawnKindDef>.AllDefsListForReading;
            for (int i = 0; i < listPawnKindDef.Count; i++)
            {
                PawnKindDef sourcePawnKindDef = listPawnKindDef[i];

                string log = sourcePawnKindDef.defName + " ";
                if (sourcePawnKindDef.weaponTags != null)
                {
                    for(int t = 0; t < sourcePawnKindDef.weaponTags.Count; t++)
                    {
                        if(sourcePawnKindDef.weaponTags[t].Contains("Melee") && sourcePawnKindDef.weaponTags[t].Contains("Neolithic"))
                        {
                            sourcePawnKindDef.weaponTags[t] = "NeolithicRangedChief";
                            //sourcePawnKindDef.
                        }
                        log += sourcePawnKindDef.weaponTags[t] + " ";
                    }
                    //base.Logger.Message(log);
                }


                if (sourcePawnKindDef.defName == null || (sourcePawnKindDef.defName.Length >= 6 && sourcePawnKindDef.defName.Substring(0, 6) == "Zombie") || DefDatabase<PawnKindDef>.GetNamed("Zombie" + sourcePawnKindDef.defName, false) != null)
                {
                    //Nothing
                }
                else
                {
                    try
                    {
                        float rbFactor = 0.5f;
                        float gFactor = 0.7f;
                        if (sourcePawnKindDef.RaceProps.Animal)
                        {
                            count++;

                            ThingDef newThingDef = new ThingDef();
                            if (DefDatabase<ThingDef>.GetNamed("Zombie" + sourcePawnKindDef.race.defName, false) == null)
                            {
                                newThingDef.defName = "Zombie" + sourcePawnKindDef.race.defName;
                                newThingDef.label = "zombie " + sourcePawnKindDef.race.defName;
                                newThingDef.description = sourcePawnKindDef.race.description;

                                newThingDef.thingClass = zombieThingDef.thingClass;
                                newThingDef.category = ThingCategory.Pawn;
                                newThingDef.selectable = true;
                                newThingDef.tickerType = TickerType.Normal;
                                newThingDef.altitudeLayer = AltitudeLayer.Pawn;
                                newThingDef.useHitPoints = false;
                                newThingDef.hasTooltip = true;
                                newThingDef.soundImpactDefault = sourcePawnKindDef.race.soundImpactDefault;

                                newThingDef.inspectorTabs = zombieThingDef.inspectorTabs;
                                newThingDef.comps = zombieThingDef.comps;

                                newThingDef.alwaysFlee = false;

                                newThingDef.drawGUIOverlay = true;

                                //int e = (int)(sourcePawnKindDef.race.shortHash);
                                //ushort f = (ushort)(e + 7);
                                //newThingDef.shortHash = f;
                                InjectedDefHasher.GiveShortHashToDef(newThingDef, typeof(ThingDef));

                                //reached

                                Predicate<StatModifier> findMoveSpeed = delegate (StatModifier statMod)
                                {
                                    if (statMod.stat.defName == "MoveSpeed")
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                Predicate<StatModifier> findLeatherAmount = delegate (StatModifier statMod)
                                {
                                    if (statMod.stat.defName == "LeatherAmount")
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                Predicate<StatModifier> findArmorRating_Sharp = delegate (StatModifier statMod)
                                {
                                    if (statMod.stat.defName == "ArmorRating_Sharp")
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                Predicate<StatModifier> findArmorRating_Blunt = delegate (StatModifier statMod)
                                {
                                    if (statMod.stat.defName == "ArmorRating_Blunt")
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                Predicate<StatModifier> findArmorRating_Heat = delegate (StatModifier statMod)
                                {
                                    if (statMod.stat.defName == "ArmorRating_Heat")
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                Predicate<StatModifier> findMeleeDodgeChance = delegate (StatModifier statMod)
                                {
                                    if (statMod.stat.defName == "MeleeDodgeChance")
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                Predicate<StatModifier> findMeleeCritChance = delegate (StatModifier statMod)
                                {
                                    if (statMod.stat.defName == "MeleeCritChance")
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                Predicate<StatModifier> findMeleeParryChance = delegate (StatModifier statMod)
                                {
                                    if (statMod.stat.defName == "MeleeParryChance")
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                //reached

                                newThingDef.statBases = new List<StatModifier>();

                                StatModifier newStat = new StatModifier();
                                newStat.stat = StatDefOf.Flammability;
                                newStat.value = 0.4f;
                                newThingDef.statBases.Add(newStat);

                                newStat = new StatModifier();
                                newStat.stat = StatDefOf.PainShockThreshold;
                                newStat.value = 70000f;
                                newThingDef.statBases.Add(newStat);

                                newStat = new StatModifier();
                                newStat.stat = StatDefOf.LeatherAmount;
                                newStat.value = sourcePawnKindDef.race.statBases.Find(findLeatherAmount).value;
                                newThingDef.statBases.Add(newStat);

                                StatModifier statSharp = sourcePawnKindDef.race.statBases.Find(findArmorRating_Sharp);
                                if (statSharp != null)
                                {
                                    newStat = new StatModifier();
                                    newStat.stat = StatDefOf.ArmorRating_Sharp;
                                    newStat.value = statSharp.value;
                                    newThingDef.statBases.Add(newStat);
                                }

                                StatModifier statBlunt = sourcePawnKindDef.race.statBases.Find(findArmorRating_Blunt);
                                if (statBlunt != null)
                                {
                                    newStat = new StatModifier();
                                    newStat.stat = StatDefOf.ArmorRating_Blunt;
                                    newStat.value = statBlunt.value;
                                    newThingDef.statBases.Add(newStat);
                                }

                                /*
                                StatModifier statHeat = sourcePawnKindDef.race.statBases.Find(findArmorRating_Heat);
                                if (statHeat != null)
                                {
                                    newStat = new StatModifier();
                                    newStat.stat = StatDefOf.ArmorRating_Heat;
                                    newStat.value = statHeat.value;
                                    newThingDef.statBases.Add(newStat);
                                }
                                */


                                newStat = new StatModifier();
                                newStat.stat = StatDefOf.PsychicSensitivity;
                                newStat.value = 0f;
                                newThingDef.statBases.Add(newStat);

                                newStat = new StatModifier();
                                newStat.stat = StatDefOf.ToxicSensitivity;
                                newStat.value = 0f;
                                newThingDef.statBases.Add(newStat);

                                newStat = new StatModifier();
                                newStat.stat = StatDefOf.MoveSpeed;
                                newStat.value = sourcePawnKindDef.race.statBases.Find(findMoveSpeed).value * zombieSpeedMultiplier;
                                newThingDef.statBases.Add(newStat);

                                //setup standard zombie
                                if(first)
                                {
                                    zombieThingDef.statBases.Find(findMoveSpeed).value = 4.6f * zombieSpeedMultiplier;
                                }
                                //setup standard zombie

                                //CE SUPPORT
                                StatModifier statCEDodge;
                                statCEDodge = sourcePawnKindDef.race.statBases.Find(findMeleeDodgeChance);
                                if(statCEDodge != null)
                                {
                                    //Log.Message("found CE for " + sourcePawnKindDef.defName);
                                    newThingDef.statBases.Add(statCEDodge);
                                    if (first)
                                    {
                                        zombieThingDef.statBases.Add(statCEDodge);
                                    }
                                }
                                StatModifier statCECrit;
                                statCECrit = sourcePawnKindDef.race.statBases.Find(findMeleeCritChance);
                                if (statCECrit != null)
                                {
                                    newThingDef.statBases.Add(statCECrit);
                                    if (first)
                                    {
                                        zombieThingDef.statBases.Add(statCECrit);
                                    }
                                }
                                StatModifier statCEParry;
                                statCEParry = sourcePawnKindDef.race.statBases.Find(findMeleeParryChance);
                                if (statCEParry != null)
                                {
                                    newThingDef.statBases.Add(statCEParry);
                                    if (first)
                                    {
                                        zombieThingDef.statBases.Add(statCEParry);
                                    }
                                }
                                //CE SUPPORT
                                first = false;

                                newThingDef.BaseMarketValue = sourcePawnKindDef.race.BaseMarketValue;

                                //reached

                                newThingDef.tools = new List<Tool>();
                                int iTool = -1;
                                foreach (Tool tool in sourcePawnKindDef.race.tools)
                                {
                                    iTool++;

                                    Tool nTool = new Tool();

                                    nTool.capacities = new List<ToolCapacityDef>();
                                    if (tool.linkedBodyPartsGroup != null && tool.linkedBodyPartsGroup.defName == "Teeth")
                                    {
                                        nTool.capacities.Add(zBite);
                                    }
                                    else
                                    {
                                        nTool.capacities.Add(zScratch);
                                    }

                                    nTool.id = "" + iTool;

                                    nTool.label = tool.label;
                                    nTool.labelUsedInLogging = tool.labelUsedInLogging;
                                    nTool.power = tool.power;
                                    nTool.cooldownTime = tool.cooldownTime;
                                    nTool.linkedBodyPartsGroup = tool.linkedBodyPartsGroup;
                                    nTool.surpriseAttack = tool.surpriseAttack;
                                    nTool.chanceFactor = tool.chanceFactor;

                                    newThingDef.tools.Add(nTool);
                                }

                                newThingDef.race = new RaceProperties();

                                newThingDef.race.corpseDef = zombieThingDef.race.corpseDef;
                                newThingDef.race.deathActionWorkerClass = zombieThingDef.race.deathActionWorkerClass;

                                Color color = new Color(rbFactor, gFactor, rbFactor);
                                newThingDef.race.meatColor = color;

                                newThingDef.race.leatherDef = zombieThingDef.race.leatherDef;
                                //newThingDef.race.leatherColor = color;
                                //newThingDef.race.leatherLabel = "zombie" + sourcePawnKindDef.race.race.leatherLabel;
                                newThingDef.race.useLeatherFrom = zombieThingDef;
                                newThingDef.race.useMeatFrom = zombieThingDef;

                                newThingDef.race.wildness = 1f;

                                newThingDef.race.intelligence = zombieThingDef.race.intelligence;
                                newThingDef.race.thinkTreeMain = zombieThingDef.race.thinkTreeMain;
                                newThingDef.race.thinkTreeConstant = zombieThingDef.race.thinkTreeConstant;
                                newThingDef.race.hasGenders = sourcePawnKindDef.race.race.hasGenders;
                                newThingDef.race.nameCategory = zombieThingDef.race.nameCategory;
                                newThingDef.race.manhunterOnDamageChance = zombieThingDef.race.manhunterOnDamageChance;
                                newThingDef.race.manhunterOnTameFailChance = zombieThingDef.race.manhunterOnTameFailChance;
                                newThingDef.race.hediffGiverSets = zombieThingDef.race.hediffGiverSets;

                                newThingDef.race.body = sourcePawnKindDef.race.race.body;
                                newThingDef.race.needsRest = false;
                                newThingDef.race.baseBodySize = sourcePawnKindDef.race.race.baseBodySize;
                                newThingDef.race.baseHungerRate = sourcePawnKindDef.race.race.baseHungerRate;
                                newThingDef.race.baseHealthScale = sourcePawnKindDef.race.race.baseHealthScale;
                                newThingDef.race.foodType = zombieThingDef.race.foodType;
                                newThingDef.race.predator = zombieThingDef.race.predator;
                                newThingDef.race.makesFootprints = sourcePawnKindDef.race.race.makesFootprints;
                                //newThingDef.race.leatherInsulation = sourcePawnKindDef.race.race.leatherInsulation;

                                newThingDef.race.lifeStageAges = new List<LifeStageAge>();
                                for (int l = 0; l < sourcePawnKindDef.race.race.lifeStageAges.Count; l++)
                                {
                                    if (sourcePawnKindDef.race.race.lifeStageAges[l].def.defName == "AnimalAdult")
                                    {
                                        newThingDef.race.lifeStageAges.Add(zombieThingDef.race.lifeStageAges[2]);
                                    }
                                    else
                                    {
                                        newThingDef.race.lifeStageAges.Add(sourcePawnKindDef.race.race.lifeStageAges[l]);
                                    }
                                }

                                newThingDef.race.soundMeleeHitPawn = sourcePawnKindDef.race.race.soundMeleeHitPawn;
                                newThingDef.race.soundMeleeHitBuilding = sourcePawnKindDef.race.race.soundMeleeHitBuilding;
                                newThingDef.race.soundMeleeMiss = sourcePawnKindDef.race.race.soundMeleeMiss;

                                newThingDef.race.ResolveReferencesSpecial();

                                newThingDef.race.wildBiomes = new List<AnimalBiomeRecord>();

                                newThingDef.tradeTags = zombieThingDef.tradeTags;

                                newThingDef.recipes = zombieThingDef.recipes;

                                newThingDef.ResolveReferences();
                            }

                            //not reached

                            PawnKindDef newKindDef = new PawnKindDef();
                            //newKindDef = PawnKindDef.Named("Zombie");

                            newKindDef.defName = "Zombie" + sourcePawnKindDef.defName;
                            newKindDef.label = "zombie " + sourcePawnKindDef.label;
                            newKindDef.race = newThingDef;
                            //newKindDef.race = ThingDef.Named("Zombie");

                            newKindDef.defaultFactionType = PawnKindDef.Named("Zombie").defaultFactionType;
                            newKindDef.combatPower = 0;// sourcePawnKindDef.combatPower / 2;
                            newKindDef.canArriveManhunter = false;

                            //int s = (int)(sourcePawnKindDef.shortHash);
                            //ushort z = (ushort)(s + 7);
                            //newKindDef.shortHash = z;
                            InjectedDefHasher.GiveShortHashToDef(newKindDef, typeof(PawnKindDef));

                            //base.Logger.Message((newKindDef.RaceProps != null) + "");

                            //newKindDef.lifeStages = PawnKindDef.Named("Zombie").lifeStages;

                            newKindDef.lifeStages = new List<PawnKindLifeStage>();

                            //newKindDef.lifeStages = sourcePawnKindDef.lifeStages;
                            for (int j = 0; j < sourcePawnKindDef.lifeStages.Count; j++)
                            {
                                newKindDef.lifeStages.Add(new PawnKindLifeStage());

                                newKindDef.lifeStages[j].label = sourcePawnKindDef.lifeStages[j].label;
                                newKindDef.lifeStages[j].labelPlural = sourcePawnKindDef.lifeStages[j].labelPlural;
                                newKindDef.lifeStages[j].labelMale = sourcePawnKindDef.lifeStages[j].labelMale;
                                newKindDef.lifeStages[j].labelMalePlural = sourcePawnKindDef.lifeStages[j].labelMalePlural;
                                newKindDef.lifeStages[j].labelFemale = sourcePawnKindDef.lifeStages[j].labelFemale;
                                newKindDef.lifeStages[j].labelFemalePlural = sourcePawnKindDef.lifeStages[j].labelFemalePlural;

                                if (sourcePawnKindDef.lifeStages[j].bodyGraphicData != null)
                                {
                                    newKindDef.lifeStages[j].bodyGraphicData = new GraphicData();
                                    newKindDef.lifeStages[j].bodyGraphicData.CopyFrom(sourcePawnKindDef.lifeStages[j].bodyGraphicData);
                                    newKindDef.lifeStages[j].bodyGraphicData.color = new Color(sourcePawnKindDef.lifeStages[j].bodyGraphicData.color.r * rbFactor, sourcePawnKindDef.lifeStages[j].bodyGraphicData.color.g * gFactor, sourcePawnKindDef.lifeStages[j].bodyGraphicData.color.b * rbFactor);
                                }

                                if (sourcePawnKindDef.lifeStages[j].femaleGraphicData != null)
                                {
                                    newKindDef.lifeStages[j].femaleGraphicData = new GraphicData();
                                    newKindDef.lifeStages[j].femaleGraphicData.CopyFrom(sourcePawnKindDef.lifeStages[j].femaleGraphicData);
                                    newKindDef.lifeStages[j].femaleGraphicData.color = new Color(sourcePawnKindDef.lifeStages[j].femaleGraphicData.color.r * rbFactor, sourcePawnKindDef.lifeStages[j].femaleGraphicData.color.g * gFactor, sourcePawnKindDef.lifeStages[j].femaleGraphicData.color.b * rbFactor);
                                }

                                if (sourcePawnKindDef.lifeStages[j].dessicatedBodyGraphicData != null)
                                {
                                    newKindDef.lifeStages[j].dessicatedBodyGraphicData = new GraphicData();
                                    newKindDef.lifeStages[j].dessicatedBodyGraphicData.CopyFrom(sourcePawnKindDef.lifeStages[j].dessicatedBodyGraphicData);
                                }

                                //newKindDef.lifeStages[j].ResolveReferences();

                                //newKindDef.lifeStages.Add(n);
                            }
                            //base.Logger.Message((newKindDef.RaceProps != null) + "");
                            if (newKindDef != null && newThingDef != null && newKindDef.RaceProps != null)
                            {
                                DefDatabase<PawnKindDef>.Add(newKindDef);
                                DefDatabase<ThingDef>.Add(newThingDef);
                            }
                        }
                    }
                    catch
                    {
                        base.Logger.Warning("Error while setting up zombie for " + sourcePawnKindDef.defName + ".", new object[0]);
                    }
                }
            }
            base.Logger.Message("set up zombies for " + count + " animals.", new object[0]);
        }
    }
}
