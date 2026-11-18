using CrystalDefense.PlayerAndProjectiles;

using CrystalEntity = CrystalDefense.Crystal.Crystal;

namespace CrystalDefense.Game.Flow;

// What an upgrade can change.
public sealed class UpgradeTargets
{
    public UpgradeTargets(Player player, ProjectileManager projectiles, CrystalEntity crystal)
    {
        Player = player;
        Projectiles = projectiles;
        Crystal = crystal;
    }

    public Player Player { get; }
    public ProjectileManager Projectiles { get; }
    public CrystalEntity Crystal { get; }
}

// One upgrade offered between two waves: a title, a short description, what it does,
// and (optionally) when it makes sense to offer it.
public sealed record Upgrade(
    string Title,
    string Description,
    Action<UpgradeTargets> Apply,
    Func<UpgradeTargets, bool>? IsAvailable = null);

public static class UpgradeCatalog
{
    public static readonly IReadOnlyList<Upgrade> All = new List<Upgrade>
    {
        new("Rapid fire", "Fire rate +30%",
            t => t.Projectiles.Cooldown *= 0.77f,
            t => t.Projectiles.Cooldown > 0.08f),

        new("Double shot", "+1 projectile per shot, in a fan",
            t => t.Projectiles.ProjectilesPerShot++,
            t => t.Projectiles.ProjectilesPerShot < 5),

        new("Reinforced armour", "+1 max health and full heal",
            t => t.Player.IncreaseMaxHealth(1)),

        new("Healing potion", "Restores all of the knight's health",
            t => t.Player.HealFully(),
            t => t.Player.Health < t.Player.MaxHealth),

        new("Crystal repair", "Restores 30 health to the crystal",
            t => t.Crystal.Repair(30.0f),
            t => t.Crystal.Health < CrystalEntity.MaxHealth),

        new("Sharpened blade", "Sword damage +1",
            t => t.Player.SwordDamage++),

        new("Light boots", "Movement speed +15%",
            t => t.Player.Speed *= 1.15f,
            t => t.Player.Speed < 12.0f),

        new("Agile dash", "Dash cooldown -30%",
            t => t.Player.DashCooldown *= 0.7f,
            t => t.Player.DashCooldown > 0.3f),
    };

    // Three different upgrades, chosen at random among the ones that make sense right now.
    public static Upgrade[] PickThree(UpgradeTargets targets, Random random) =>
        All.Where(upgrade => upgrade.IsAvailable?.Invoke(targets) ?? true)
           .OrderBy(_ => random.Next())
           .Take(3)
           .ToArray();
}
