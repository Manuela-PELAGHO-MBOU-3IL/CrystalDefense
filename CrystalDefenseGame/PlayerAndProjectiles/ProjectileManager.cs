using CrystalDefense.Core;
using Engine;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefense.PlayerAndProjectiles;

// Creates the projectiles when the player shoots (left mouse button), moves them and removes the destroyed ones.
public sealed class ProjectileManager
{
    private readonly List<Projectile> _projectiles = new();
    private readonly Mesh _mesh;
    private readonly Material _material;
    private float _cooldownTimer;

    public ProjectileManager(Mesh mesh, Material material)
    {
        _mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
        _material = material ?? throw new ArgumentNullException(nameof(material));
    }

    // Minimum time between two shots, in seconds.
    public float Cooldown { get; set; } = 0.25f;

    public float ProjectileSpeed { get; set; } = 15.0f;
    public float ProjectileRadius { get; set; } = 0.2f;

    // Height above the floor at which the projectiles fly.
    public float ShootingHeight { get; set; } = 0.5f;

    // Read-only access for the collision system (Ons): it calls Destroy() on the projectiles that hit an enemy.
    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    public void Update(bool isShooting, float deltaTime, Player player, ArenaBounds bounds)
    {
        // 1. Remove the projectiles destroyed during the last frame (hit an enemy or left the arena).
        //    This is done before the loop below: a list must not change while it is being walked through.
        _projectiles.RemoveAll(projectile => !projectile.IsAlive);

        // 2. Shoot while the button is held down, but at most once per Cooldown (not while knocked out).
        _cooldownTimer -= deltaTime;
        if (isShooting && player.CanAct && _cooldownTimer <= 0)
        {
            Shoot(player);
            _cooldownTimer = Cooldown;
        }

        // 3. Move all projectiles.
        foreach (Projectile projectile in _projectiles)
            projectile.Update(deltaTime, bounds);
    }

    private void Shoot(Player player)
    {
        // The projectile starts just in front of the player, so that it does not appear inside him.
        Vector3 start = player.Position + player.FacingDirection * (player.Radius + ProjectileRadius);
        start.Y = ShootingHeight;

        _projectiles.Add(new Projectile(_mesh, _material, start, player.FacingDirection, ProjectileSpeed, ProjectileRadius));
    }

    public void Draw(OpenGlRenderer renderer, Camera camera)
    {
        foreach (Projectile projectile in _projectiles)
        {
            // a projectile destroyed in this frame is not drawn any more
            if (projectile.IsAlive)
                projectile.Draw(renderer, camera);
        }
    }
}
