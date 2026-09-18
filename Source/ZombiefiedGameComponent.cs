using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace Zombiefied
{
    public class ZombiefiedGameComponent : GameComponent
    {
        public ZombiefiedGameComponent(Game game)
        {
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            RemoveLegacyWorldInfestationMarkers();
            ZombiefiedMod.Instance?.OnWorldLoaded();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            RemoveLegacyWorldInfestationMarkers();
            ZombiefiedMod.Instance?.OnWorldLoaded();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            ZombiefiedMod.Instance?.OnGameTick();
        }

        private static void RemoveLegacyWorldInfestationMarkers()
        {
            WorldObjectsHolder worldObjects = Find.WorldObjects;
            if (worldObjects == null)
            {
                return;
            }

            List<WorldObject> allWorldObjects = worldObjects.AllWorldObjects;
            if (allWorldObjects == null || allWorldObjects.Count == 0)
            {
                return;
            }

            List<WorldObject> legacyMarkers = null;
            for (int i = 0; i < allWorldObjects.Count; i++)
            {
                if (allWorldObjects[i] is WorldObject_ZombieInfestation)
                {
                    if (legacyMarkers == null)
                    {
                        legacyMarkers = new List<WorldObject>();
                    }

                    legacyMarkers.Add(allWorldObjects[i]);
                }
            }

            if (legacyMarkers == null)
            {
                return;
            }

            for (int i = 0; i < legacyMarkers.Count; i++)
            {
                worldObjects.Remove(legacyMarkers[i]);
            }

            Log.Message("[Zombiefied] Removed " + legacyMarkers.Count + " legacy world infestation marker(s) from the discontinued persistent infestation system.");
        }
    }
}
