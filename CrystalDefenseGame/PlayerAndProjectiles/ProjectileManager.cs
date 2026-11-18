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

    // Number of projectiles per shot (an upgrade can increase it); they spread out like a fan.
    public int ProjectilesPerShot { get; set; } = 1;
    public float SpreadAngleDegrees { get; set; } = 10.0f;

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
        // The projectiles start just in front of the player, so that they do not appear inside him.
        Vector3 start = player.Position + player.FacingDirection * (player.Radius + ProjectileRadius);
        start.Y = ShootingHeight;

        for (int i = 0; i < ProjectilesPerShot; i++)
        {
            // Angle of this projectile in the fan: 0 in the middle, negative on one side, positive on the other.
            float angle = MathHelper.DegreesToRadians((i - (ProjectilesPerShot - 1) * 0.5f) * SpreadAngleDegrees);
            Vector3 direction = RotateAroundY(player.FacingDirection, angle);
            _projectiles.Add(new Projectile(_mesh, _material, start, direction, ProjectileSpeed, ProjectileRadius));
        }
    }

    private static Vector3 RotateAroundY(Vector3 v, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new Vector3(v.X * cos + v.Z * sin, v.Y, -v.X * sin + v.Z * cos);
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
