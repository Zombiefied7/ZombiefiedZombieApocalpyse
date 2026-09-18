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
            ZombiefiedMod.Instance?.OnWorldLoaded();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            ZombiefiedMod.Instance?.OnWorldLoaded();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            ZombiefiedMod.Instance?.OnGameTick();
        }
    }
}
