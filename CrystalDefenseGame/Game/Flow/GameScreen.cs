namespace CrystalDefense.Game.Flow;

// The screens of the game. Only one is active at a time (a simple state machine).
public enum GameScreen
{
    MainMenu,          // title, controls, "press Enter to play"
    Playing,           // the game itself, with the HUD
    Paused,            // game frozen, pause menu
    ChoosingUpgrade,   // a wave is finished: choose 1 upgrade out of 3
    Victory,           // all waves defeated
    Defeat             // the crystal was destroyed
}
