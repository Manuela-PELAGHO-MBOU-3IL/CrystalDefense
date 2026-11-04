
namespace CrystalDefenseGame.Crystal;

public class CrystalHealth
{
    public int MaxHealth { get; }
    public int CurrentHealth { get; private set; }

    public float HealthPercentage =>
        (float)CurrentHealth / MaxHealth;

    public bool IsDestroyed => CurrentHealth <= 0;

    public CrystalHealth(int maxHealth = 100)
    {
        if (maxHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHealth));

        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDestroyed)
            return;

        CurrentHealth = Math.Max(
            0,
            CurrentHealth - damage
        );

        Console.WriteLine(
            $"Crystal HP: {CurrentHealth}/{MaxHealth}"
        );

        if (IsDestroyed)
        {
            Console.WriteLine(
                "GAME OVER - Crystal destroyed!"
            );
        }
    }
}
