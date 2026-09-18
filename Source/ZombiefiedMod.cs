using System;
using System.Collections.Generic;
using System.Collections;
using RimWorld;
using UnityEngine;
using Verse;

namespace Zombiefied
{
    // Token: 0x02000008 RID: 8
    public class ZombiefiedMod : Mod
    {
        public static JobDef zombieHunt;
        public static JobDef zombieMove;

        public static ZombiefiedMod Instance { get; private set; }
        public static ZombiefiedSettings Settings { get; private set; }

        private bool customDefsInitialized;
        private string zombieSpeedMultiplierBuffer;
        private string zombieSoundReactionTimeInHoursBuffer;
        private string zombieAmountSoftCapBuffer;
        private string zombieRaidAmountMultiplierBuffer;
        private string zombieRaidFrequencyMultiplierBuffer;
        private string zombieCaravanEncounterMultiplierBuffer;

        public ZombiefiedMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ZombiefiedSettings>();
            zombieSpeedMultiplierBuffer = Settings.zombieSpeedMultiplier.ToString();
            zombieSoundReactionTimeInHoursBuffer = Settings.zombieSoundReactionTimeInHours.ToString();
            zombieAmountSoftCapBuffer = Settings.zombieAmountSoftCap.ToString();
            zombieRaidAmountMultiplierBuffer = Settings.zombieRaidAmountMultiplier.ToString();
            zombieRaidFrequencyMultiplierBuffer = Settings.zombieRaidFrequencyMultiplier.ToString();
            zombieCaravanEncounterMultiplierBuffer = Settings.zombieCaravanEncounterMultiplier.ToString();

            // Runtime patches and dynamic zombie Def generation are initialized after all XML Defs load.
            // This preserves the previous post-Def-load initialization timing.
        }

        public override string SettingsCategory()
        {
            return "Zombiefied (Zombie Apocalypse)";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ZombiefiedSettings settings = Settings;
            if (settings == null)
            {
                return;
            }

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Zombie settings");
            listing.GapLine();
            listing.CheckboxLabeled("Disable animal zombies", ref settings.disableAnimalZombies, "Animals will not resurrect and no animal zombies will wander in.");
            listing.CheckboxLabeled("Disable zombies attacking animals", ref settings.disableZombiesAttackingAnimals, "Zombies will ignore animals.");
            listing.Label((TaggedString)"Zombie speed multiplier [RESTART]", -1f, "Zombie speed compared with a healthy pawn. Range: 0.03 to 3. Requires a restart to affect dynamically generated zombie races.");
            listing.TextFieldNumeric(ref settings.zombieSpeedMultiplier, ref zombieSpeedMultiplierBuffer, 0.03f, 3f);
            listing.Label((TaggedString)"Zombie sound memory time (in-game hours)", -1f, "How long zombies remember a sound location.");
            listing.TextFieldNumeric(ref settings.zombieSoundReactionTimeInHours, ref zombieSoundReactionTimeInHoursBuffer, 0f, 1000000000f);

            listing.Gap();
            listing.Label("Amount settings");
            listing.GapLine();
            listing.Label((TaggedString)"Zombie amount soft cap", -1f, "Expected zombie population per map before new wandering raids are suppressed.");
            listing.TextFieldNumeric(ref settings.zombieAmountSoftCap, ref zombieAmountSoftCapBuffer, 0f, 1000000000f);
            listing.Label((TaggedString)"Zombie raid size multiplier", -1f, "Zombie raid size multiplier. Range: 0.1 to 7.");
            listing.TextFieldNumeric(ref settings.zombieRaidAmountMultiplier, ref zombieRaidAmountMultiplierBuffer, 0.1f, 7f);
            listing.Label((TaggedString)"Zombie raid frequency multiplier", -1f, "Zombie raid frequency multiplier. Range: 0.1 to 7.");
            listing.TextFieldNumeric(ref settings.zombieRaidFrequencyMultiplier, ref zombieRaidFrequencyMultiplierBuffer, 0.1f, 7f);

            listing.Gap();
            listing.Label("Notification settings");
            listing.GapLine();
            listing.CheckboxLabeled("Zombie raid notifications", ref settings.zombieRaidNotifications, "Show a notification when zombies wander in.");
            listing.CheckboxLabeled("Zombie resurrect notifications", ref settings.zombieResurrectNotifications, "Show a notification when a zombie resurrects.");

            listing.Gap();
            listing.Label("World apocalypse");
            listing.GapLine();
            listing.CheckboxLabeled("Zombie threats at world sites", ref settings.enableZombieWorldSiteThreats, "Allow zombies to be selected as a threat at vanilla quest and reward sites, such as item stashes and rescue sites.");
            listing.CheckboxLabeled("Zombie caravan encounters", ref settings.enableCaravanZombieEncounters, "Allow the storyteller to select zombie ambushes as one of the normal threats that can hit travelling caravans.");
            listing.Label((TaggedString)"Caravan zombie encounter frequency multiplier", -1f, "Changes the storyteller selection weight of zombie caravan ambushes. Range: 0.1 to 5.");
            listing.TextFieldNumeric(ref settings.zombieCaravanEncounterMultiplier, ref zombieCaravanEncounterMultiplierBuffer, 0.1f, 5f);
            ApplyWorldEventDefSettings();

            listing.Gap();
            listing.Label("Debug settings");
            listing.GapLine();
            listing.CheckboxLabeled("Debug remove zombies [RELOAD]", ref settings.debugRemoveZombies, "Remove all zombies on the next game load, then automatically turn this option off.");

            listing.End();
        }

        internal void InitializeCustomOnce()
        {
            if (customDefsInitialized)
            {
                return;
            }

            customDefsInitialized = true;
            InitializeCustom();
            ZombieArmorStatUtility.Install();
            ApplyWorldEventDefSettings();
        }

        internal void OnWorldLoaded()
        {
            Log.Message("[Zombiefied] Loaded game runtime.");

            ZombieWorldUtility.RefreshZombieFaction();
            Faction zombieFaction = ZombieWorldUtility.GetZombieFaction() ?? Faction.OfInsects;
            Faction mechanoidFaction = Faction.OfMechanoids;
            if (zombieFaction != null && mechanoidFaction != null && zombieFaction != mechanoidFaction)
            {
                zombieFaction.RelationWith(mechanoidFaction).kind = FactionRelationKind.Ally;
                zombieFaction.RelationWith(mechanoidFaction).baseGoodwill = 100;
                mechanoidFaction.RelationWith(zombieFaction).kind = FactionRelationKind.Ally;
                mechanoidFaction.RelationWith(zombieFaction).baseGoodwill = 100;
            }

            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                Map map = Find.Maps[mapIndex];
                if (map == null)
                {
                    continue;
                }

                if (debugRemoveZombies)
                {
                    IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                    for (int pawnIndex = pawns.Count - 1; pawnIndex >= 0; pawnIndex--)
                    {
                        Pawn_Zombiefied zombie = pawns[pawnIndex] as Pawn_Zombiefied;
                        if (zombie != null && !zombie.Destroyed)
                        {
                            zombie.Destroy(DestroyMode.Vanish);
                        }
                    }

                    List<Thing> corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
                    if (corpses != null)
                    {
                        for (int corpseIndex = corpses.Count - 1; corpseIndex >= 0; corpseIndex--)
                        {
                            Corpse_Zombiefied zombieCorpse = corpses[corpseIndex] as Corpse_Zombiefied;
                            if (zombieCorpse != null && !zombieCorpse.Destroyed)
                            {
                                zombieCorpse.Destroy(DestroyMode.Vanish);
                            }
                        }
                    }
                }
                else
                {
                    IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                    for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
                    {
                        Pawn_Zombiefied zombie = pawns[pawnIndex] as Pawn_Zombiefied;
                        if (zombie == null)
                        {
                            continue;
                        }

                        if (zombieFaction != null && zombie.Faction != zombieFaction)
                        {
                            zombie.SetFaction(zombieFaction);
                        }
                        zombie.FixZombie();
                    }
                }

                ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(map);
                if (tracker != null)
                {
                    tracker.RebuildFromMap();
                    if (tracker.TicksUntilNextZombieRaid < 0)
                    {
                        tracker.TicksUntilNextZombieRaid = Math.Min(17777, GenerateTicksUntilNextRaid());
                    }
                }
            }

            if (Settings != null && Settings.debugRemoveZombies)
            {
                Settings.debugRemoveZombies = false;
                WriteSettings();
            }
        }

        internal static bool disableAnimalZombies => Settings != null && Settings.disableAnimalZombies;
        internal static bool disableZombiesAttackingAnimals => Settings != null && Settings.disableZombiesAttackingAnimals;
        internal static float zombieSpeedMultiplier => Settings != null ? Mathf.Clamp(Settings.zombieSpeedMultiplier, 0.03f, 3f) : 0.57f;
        internal static int zombieSoundReactionTimeInHours => Settings != null ? Settings.zombieSoundReactionTimeInHours : 5;

        internal static float zombieRaidFrequencyMultiplier => Settings != null ? Mathf.Clamp(Settings.zombieRaidFrequencyMultiplier, 0.1f, 7f) : 1f;
        public static float ZombieRaidFrequencyMultiplier => zombieRaidFrequencyMultiplier;

        internal static float zombieRaidAmountMultiplier => Settings != null ? Mathf.Clamp(Settings.zombieRaidAmountMultiplier, 0.1f, 7f) : 1f;
        public static float ZombieRaidAmountMultiplier => zombieRaidAmountMultiplier;

        internal static int zombieAmountSoftCap => Settings != null ? Settings.zombieAmountSoftCap : 133;
        internal static bool zombieRaidNotifications => Settings == null || Settings.zombieRaidNotifications;
        internal static bool enableZombieWorldSiteThreats => Settings == null || Settings.enableZombieWorldSiteThreats;
        internal static bool enableCaravanZombieEncounters => Settings == null || Settings.enableCaravanZombieEncounters;
        internal static float zombieCaravanEncounterMultiplier => Settings != null ? Mathf.Clamp(Settings.zombieCaravanEncounterMultiplier, 0.1f, 5f) : 1f;

        internal static void ApplyWorldEventDefSettings()
        {
            IncidentDef caravanAmbush = DefDatabase<IncidentDef>.GetNamed("ZombieCaravanAmbush", false);
            if (caravanAmbush != null)
            {
                caravanAmbush.baseChance = enableCaravanZombieEncounters ? 1f * zombieCaravanEncounterMultiplier : 0f;
            }
        }
        internal static bool zombieResurrectNotifications => Settings != null && Settings.zombieResurrectNotifications;
        internal static bool debugRemoveZombies => Settings != null && Settings.debugRemoveZombies;

        internal void OnGameTick()
        {
            HandleZombieRaid();
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
            return num;
        }

        private void HandleZombieRaid()
        {
            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                Map map = Find.Maps[mapIndex];
                ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(map);
                if (tracker == null)
                {
                    continue;
                }

                if (tracker.TicksUntilNextZombieRaid < 0)
                {
                    tracker.TicksUntilNextZombieRaid = Math.Min(17777, GenerateTicksUntilNextRaid());
                }

                tracker.TicksUntilNextZombieRaid -= 1 + tracker.RecentNoiseCount;
                if (tracker.TicksUntilNextZombieRaid > 0)
                {
                    continue;
                }

                tracker.TicksUntilNextZombieRaid = GenerateTicksUntilNextRaid();
                if (tracker.ZombieCount >= zombieAmountSoftCap)
                {
                    continue;
                }

                IncidentParms incidentParms = new IncidentParms
                {
                    target = map
                };

                int randomChoice = Rand.RangeSeeded(0, 7, Find.TickManager.TicksAbs + map.uniqueID);
                if (randomChoice < 5 || disableAnimalZombies)
                {
                    IncidentDef.Named("ZombieHorde").Worker.TryExecute(incidentParms);
                }
                else
                {
                    IncidentDef.Named("ZombiePack").Worker.TryExecute(incidentParms);
                }
            }
        }

        public static IntVec3 BestNoisyLocation(Pawn predator)
        {
            if (predator == null || predator.Map == null)
            {
                return IntVec3.Invalid;
            }

            ZombieMapTracker tracker = ZombieMapTrackerUtility.GetTracker(predator.Map);
            return tracker != null ? tracker.BestNoisyLocation() : IntVec3.Invalid;
        }

        public Pawn ReanimateDeath(Corpse corpse)
        {
            if (corpse == null || corpse.Destroyed || !corpse.Spawned || corpse.Map == null || corpse.InnerPawn == null)
            {
                return null;
            }

            Pawn sourcePawn = corpse.InnerPawn;
            Pawn_Zombiefied zombiePawn = ZombiefiedMod.GenerateZombieFromSource(sourcePawn);
            if (zombiePawn == null)
            {
                Log.Error("Zombiefied could not reanimate " + sourcePawn + " because zombie generation failed.");
                return null;
            }

            Faction zombieFaction = ZombieWorldUtility.GetZombieFaction() ?? Faction.OfInsects;
            zombiePawn.SetFactionDirect(zombieFaction);

            // A pawn that has entered RimWorld's death/destruction pipeline is not reusable. Health.Reset() can
            // clear hediffs, but it does not reconstruct trackers or reverse Thing.Destroy(). Reject such pawns
            // before GenSpawn can register a structurally invalid attack target or dynamic draw entry.
            if (zombiePawn.Dead || zombiePawn.Destroyed || zombiePawn.health == null || zombiePawn.mindState == null)
            {
                Log.Error("Zombiefied refused to spawn invalid reanimated pawn " + zombiePawn + " for " + sourcePawn + ".");
                return null;
            }

            IntVec3 position = corpse.Position;
            Map map = corpse.Map;
            Thing spawnedThing;

            try
            {
                spawnedThing = GenSpawn.Spawn(zombiePawn, position, map);

                // Health changes can trigger job-state transitions. Run FixZombie only after SpawnSetup so any
                // vanilla recovery/wait job has a real map, pather, and reservation manager available.
                Pawn_Zombiefied spawnedZombie = spawnedThing as Pawn_Zombiefied;
                if (spawnedZombie != null)
                {
                    spawnedZombie.FixZombie();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Zombiefied failed to spawn reanimated pawn for " + sourcePawn + ". " + ex);

                // A failed SpawnSetup can leave a pawn partially registered. Cleanup must never escape into
                // the game-component tick loop, because partially initialized pawns can also throw during DeSpawn.
                try
                {
                    if (zombiePawn.Spawned && !zombiePawn.Destroyed)
                    {
                        zombiePawn.DeSpawn(DestroyMode.Vanish);
                    }
                    else if (!zombiePawn.Destroyed)
                    {
                        zombiePawn.Destroy(DestroyMode.Vanish);
                    }
                }
                catch (Exception cleanupEx)
                {
                    Log.Error("Zombiefied could not fully clean up failed reanimation pawn " + zombiePawn + ". " + cleanupEx);
                }

                return null;
            }

            // Notify storage only after the replacement pawn has actually spawned. A failed resurrection leaves
            // the original corpse untouched and still valid in its storage owner.
            Building_Storage storage = StoreUtility.StoringThing(corpse) as Building_Storage;
            if (storage != null)
            {
                storage.Notify_LostThing(corpse);
            }

            if (!corpse.Destroyed)
            {
                corpse.Destroy(DestroyMode.Vanish);
            }

            if (zombieResurrectNotifications && spawnedThing != null)
            {
                Find.LetterStack.ReceiveLetter("Zombie", "A zombie resurrected.", LetterDefOf.NeutralEvent, spawnedThing, null);
            }
            return zombiePawn;
        }

        public static Pawn_Zombiefied GenerateZombieFromSource(Pawn sourcePawn)
        {
            if (sourcePawn == null || sourcePawn.kindDef == null)
            {
                return null;
            }

            PawnKindDef newKindDef = PawnKindDef.Named("Zombie");
            PawnKindDef specificKindDef = DefDatabase<PawnKindDef>.GetNamed("Zombie" + sourcePawn.kindDef.defName, false);
            if (specificKindDef != null)
            {
                newKindDef = specificKindDef;
            }

            // Preserve corpse damage when possible, but never let one unusual health state make the entire
            // reanimation fail. A fresh candidate without copied health is still preferable to a dead candidate.
            Pawn_Zombiefied zombie = GenerateZombieCandidate(newKindDef, sourcePawn, true);
            if (zombie == null || zombie.Dead || zombie.Destroyed)
            {
                Log.Warning("Zombiefied could not build a healthy zombie with copied corpse conditions for "
                    + sourcePawn + ". Retrying without copied health conditions.");
                zombie = GenerateZombieCandidate(newKindDef, sourcePawn, false);
            }

            if (zombie == null || zombie.Dead || zombie.Destroyed)
            {
                Log.Error("Zombiefied could not generate a living replacement pawn for " + sourcePawn
                    + " using pawn kind " + newKindDef + ".");
                return null;
            }

            Faction zombieFaction = ZombieWorldUtility.GetZombieFaction() ?? Faction.OfInsects;
            zombie.SetFactionDirect(zombieFaction);
            zombie.gender = sourcePawn.gender;

            if (sourcePawn.Faction != null && sourcePawn.Faction.IsPlayer)
            {
                NameSingle nameSingle = sourcePawn.Name as NameSingle;
                if (nameSingle != null)
                {
                    zombie.Name = new NameSingle("Zombie " + nameSingle.Name);
                }

                NameTriple nameTriple = sourcePawn.Name as NameTriple;
                if (nameTriple != null)
                {
                    zombie.Name = new NameSingle("Zombie " + nameTriple.Nick);
                }
            }

            if (zombie.ageTracker != null && sourcePawn.ageTracker != null)
            {
                zombie.ageTracker.AgeBiologicalTicks = sourcePawn.ageTracker.AgeBiologicalTicks;
                zombie.ageTracker.BirthAbsTicks = sourcePawn.ageTracker.BirthAbsTicks;
                zombie.ageTracker.AgeChronologicalTicks = sourcePawn.ageTracker.AgeChronologicalTicks;
            }

            return zombie;
        }

        private static Pawn_Zombiefied GenerateZombieCandidate(PawnKindDef kindDef, Pawn sourcePawn, bool copyHealthConditions)
        {
            Pawn generatedPawn;
            try
            {
                generatedPawn = PawnGenerator.GeneratePawn(kindDef);
            }
            catch (Exception ex)
            {
                Log.Error("Zombiefied failed to generate pawn kind " + kindDef + " from source " + sourcePawn + ". " + ex);
                return null;
            }

            if (generatedPawn == null)
            {
                Log.Error("Zombiefied pawn generator returned null for pawn kind " + kindDef + " from source " + sourcePawn + ".");
                return null;
            }

            Pawn_Zombiefied zombie = generatedPawn as Pawn_Zombiefied;
            if (zombie == null)
            {
                Log.Error("Zombiefied pawn kind " + kindDef + " generated " + generatedPawn.GetType()
                    + " instead of Pawn_Zombiefied.");
                return null;
            }

            if (zombie.Dead || zombie.Destroyed || zombie.health == null || zombie.mindState == null)
            {
                Log.Warning("Zombiefied pawn generator produced an invalid candidate for kind " + kindDef
                    + " before source data was copied from " + sourcePawn + ". Dead=" + zombie.Dead
                    + ", Destroyed=" + zombie.Destroyed + ", Health=" + (zombie.health != null)
                    + ", MindState=" + (zombie.mindState != null) + ".");
                return null;
            }

            try
            {
                zombie.newGraphics(sourcePawn);
                if (!zombie.copyInjuries(sourcePawn, copyHealthConditions))
                {
                    Log.Warning("Zombiefied rejected candidate " + zombie + " while copying source data from "
                        + sourcePawn + ". CopyHealthConditions=" + copyHealthConditions + ".");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Zombiefied failed while copying source data from " + sourcePawn + " to generated kind "
                    + kindDef + ". CopyHealthConditions=" + copyHealthConditions + ". " + ex);
                return null;
            }

            if (zombie.Dead || zombie.Destroyed || zombie.health == null || zombie.mindState == null)
            {
                Log.Warning("Zombiefied candidate for " + sourcePawn + " became invalid after source data transfer."
                    + " Dead=" + zombie.Dead + ", Destroyed=" + zombie.Destroyed
                    + ", Health=" + (zombie.health != null) + ", MindState=" + (zombie.mindState != null) + ".");
                return null;
            }

            return zombie;
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

                            string zombieRaceDefName = "Zombie" + sourcePawnKindDef.race.defName;
                            ThingDef newThingDef = DefDatabase<ThingDef>.GetNamed(zombieRaceDefName, false);
                            bool createdNewThingDef = newThingDef == null;
                            if (createdNewThingDef)
                            {
                                newThingDef = new ThingDef();
                                newThingDef.defName = zombieRaceDefName;
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

                                StatDef toxicEnvironmentResistance = DefDatabase<StatDef>.GetNamedSilentFail("ToxicEnvironmentResistance");
                                if (toxicEnvironmentResistance != null)
                                {
                                    newStat = new StatModifier();
                                    newStat.stat = toxicEnvironmentResistance;
                                    newStat.value = 1f;
                                    newThingDef.statBases.Add(newStat);
                                }

                                StatDef wildness = DefDatabase<StatDef>.GetNamedSilentFail("Wildness");
                                if (wildness != null)
                                {
                                    newStat = new StatModifier();
                                    newStat.stat = wildness;
                                    newStat.value = 1f;
                                    newThingDef.statBases.Add(newStat);
                                }

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

                                Color color = new Color(rbFactor, gFactor, rbFactor);
                                newThingDef.race.meatColor = color;

                                newThingDef.race.leatherDef = zombieThingDef.race.leatherDef;
                                //newThingDef.race.leatherColor = color;
                                //newThingDef.race.leatherLabel = "zombie" + sourcePawnKindDef.race.race.leatherLabel;
                                newThingDef.race.useLeatherFrom = zombieThingDef;
                                newThingDef.race.useMeatFrom = zombieThingDef;


                                newThingDef.race.intelligence = zombieThingDef.race.intelligence;
                                newThingDef.race.thinkTreeMain = zombieThingDef.race.thinkTreeMain;
                                newThingDef.race.thinkTreeConstant = zombieThingDef.race.thinkTreeConstant;
                                newThingDef.race.hasGenders = sourcePawnKindDef.race.race.hasGenders;
                                newThingDef.race.nameCategory = zombieThingDef.race.nameCategory;
                                newThingDef.race.manhunterOnDamageChance = zombieThingDef.race.manhunterOnDamageChance;
                                newThingDef.race.manhunterOnTameFailChance = zombieThingDef.race.manhunterOnTameFailChance;
                                newThingDef.race.hediffGiverSets = zombieThingDef.race.hediffGiverSets;

                                newThingDef.race.body = sourcePawnKindDef.race.race.body;

                                // RimWorld 1.5+ renders pawns through a PawnRenderTreeDef stored on RaceProperties.
                                // Dynamically created zombie animal races must inherit the source animal's tree or the
                                // vanilla renderer reaches PawnRenderTree.Draw without a resolved graph.
                                newThingDef.race.renderTree = sourcePawnKindDef.race.race.renderTree
                                    ?? DefDatabase<PawnRenderTreeDef>.GetNamedSilentFail("Animal");

                                newThingDef.race.needsRest = false;
                                newThingDef.race.baseBodySize = sourcePawnKindDef.race.race.baseBodySize;
                                newThingDef.race.baseHungerRate = sourcePawnKindDef.race.race.baseHungerRate;
                                newThingDef.race.baseHealthScale = sourcePawnKindDef.race.race.baseHealthScale;

                                // PawnGenerator uses life expectancy while choosing and validating generated ages.
                                // A freshly constructed RaceProperties otherwise keeps its zero/default value, which
                                // can make dynamically generated animal zombies invalid or immediately dead.
                                newThingDef.race.lifeExpectancy = sourcePawnKindDef.race.race.lifeExpectancy;
                                if (newThingDef.race.lifeExpectancy <= 0f)
                                {
                                    newThingDef.race.lifeExpectancy = zombieThingDef.race.lifeExpectancy;
                                }

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

                            newKindDef.defaultFactionDef = PawnKindDef.Named("Zombie").defaultFactionDef;
                            newKindDef.combatPower = 0;// sourcePawnKindDef.combatPower / 2;
                            newKindDef.canArriveManhunter = false;

                            //int s = (int)(sourcePawnKindDef.shortHash);
                            //ushort z = (ushort)(s + 7);
                            //newKindDef.shortHash = z;
                            InjectedDefHasher.GiveShortHashToDef(newKindDef, typeof(PawnKindDef));


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
                            if (newKindDef != null && newThingDef != null && newKindDef.RaceProps != null)
                            {
                                DefDatabase<PawnKindDef>.Add(newKindDef);
                                if (createdNewThingDef)
                                {
                                    DefDatabase<ThingDef>.Add(newThingDef);
                                }
                            }
                        }
                    }
                    catch
                    {
                        Log.Warning("[Zombiefied] Error while setting up zombie for " + sourcePawnKindDef.defName + ".");
                    }
                }
            }
            Log.Message("[Zombiefied] Set up zombies for " + count + " animals.");
        }
    }
}
