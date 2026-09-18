using RimWorld.Planet;

namespace Zombiefied
{
    /// <summary>
    /// Compatibility shell for infestation markers created by the discontinued persistent world-infestation system.
    /// The event-driven world integration never creates these objects; existing markers are removed after a game loads.
    /// Keeping the type available prevents old XML files or saves from failing during deserialization.
    /// </summary>
    public class WorldObject_ZombieInfestation : WorldObject
    {
        public override bool AppendFactionToInspectString => false;
    }
}
