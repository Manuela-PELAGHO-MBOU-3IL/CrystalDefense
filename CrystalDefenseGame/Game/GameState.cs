
using CrystalEntity = CrystalDefense.Crystal.Crystal;

namespace CrystalDefense.Game;

public enum GameStatus
{
    Playing,
    Victory,
    Defeat
}

public sealed class GameState
{
    public GameStatus Status { get; private set; } = GameStatus.Playing;

    public bool IsGameOver => Status != GameStatus.Playing;

    public void Update(
        CrystalEntity crystal,
        bool allWavesCompleted
    )
    {
        // Defeat: the crystal has been destroyed.
        if (crystal.Health <= 0)
        {
            Status = GameStatus.Defeat;
            Console.WriteLine("GAME OVER - CRYSTAL DESTROYED!");
            return;
        }

        // Victory: all waves are completed.
        if (allWavesCompleted)
        {
            Status = GameStatus.Victory;
            Console.WriteLine("VICTORY - ALL WAVES COMPLETED!");
        }
    }
}
