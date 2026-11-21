
using CrystalDefense.Core;
using Engine;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefense.PlayerAndProjectiles;

// The knight controlled by the player.
// Controls are read by PlayerInput: keyboard or keyboard + mouse.
// He can walk, aim, shoot, slash with his sword and dash.
// He has health points and respawns after being knocked out.
public sealed class Player : GameObject
{
    // --- settings ---

    public const float DashSpeed = 18.0f;
    public const float DashDuration = 0.18f;
    public const float InvincibleTimeAfterHit = 1.0f;
    public const float RespawnTime = 3.0f;

    public const float SwordDuration = 0.3f;
    public const float SwordCooldown = 0.5f;
    public const float SwordReach = 2.0f;

    // The sword hits in a cone of 150 degrees.
    public const float SwordHalfAngleDegrees = 75.0f;

    private readonly KnightModel _model;
    private readonly Vector3 _spawnPosition;

    // Timers count down to zero, in seconds.
    private float _dashTimer;
    private float _dashCooldownTimer;
    private float _invincibleTimer;
    private float _respawnTimer;
    private float _swordTimer;
    private float _swordCooldownTimer;

    private Vector3 _dashDirection;
    private bool _swordStrikeUsed;

    public Player(
        KnightModel model,
        Mesh mesh,
        Material material,
        Vector3 startPosition,
        float radius)
        : base(mesh, material, startPosition, radius)
    {
        _model = model
            ?? throw new ArgumentNullException(nameof(model));

        _spawnPosition = startPosition;
        Health = MaxHealth;
    }

    // Walking speed in units per second.
    public float Speed { get; set; } = 6.0f;

    // Current health.
    public int Health { get; private set; }

    // These values can grow with upgrades between waves.
    public int MaxHealth { get; private set; } = 5;
    public int SwordDamage { get; set; } = 2;
    public float DashCooldown { get; set; } = 1.0f;

    // The direction the knight looks at.
    // Projectiles and the sword use this direction.
    public Vector3 FacingDirection { get; private set; }
        = -Vector3.UnitZ;

    public bool IsKnockedOut =>
        _respawnTimer > 0.0f;

    public bool CanAct =>
        !IsKnockedOut;

    public bool IsDashing =>
        _dashTimer > 0.0f;

    public bool IsInvincible =>
        IsKnockedOut ||
        IsDashing ||
        _invincibleTimer > 0.0f;

    public bool IsDashReady =>
        _dashCooldownTimer <= 0.0f;

    // 0 = dash just used, 1 = ready again.
    // Used by the HUD.
    public float DashCharge =>
        DashCooldown <= 0.0f
            ? 1.0f
            : 1.0f - _dashCooldownTimer / DashCooldown;

    // 0 to 1 during a sword slash.
    // -1 when the knight is not attacking.
    public float SwordProgress =>
        _swordTimer > 0.0f
            ? 1.0f - _swordTimer / SwordDuration
            : -1.0f;

    // True during the frame when the player was hit.
    // Used for camera shake.
    public bool WasHitThisFrame { get; private set; }

    public void Update(
        PlayerInput input,
        float deltaTime,
        ArenaBounds bounds,
        Vector3 obstacleCenter,
        float obstacleRadius)
    {
        WasHitThisFrame = false;

        CountDownTimers(deltaTime);

        if (IsKnockedOut)
        {
            _respawnTimer -= deltaTime;

            if (_respawnTimer <= 0.0f)
            {
                Respawn();
            }

            _model.Animate(
                deltaTime,
                0.0f,
                -1.0f,
                false
            );

            return;
        }

        // 1. Movement direction relative to camera.
        Vector3 direction = input.MoveDirection;

        // 2. Look where the player aims.
        // Without aiming, look where he walks.
        UpdateFacingDirection(
            input.AimDirection,
            direction
        );

        // 3. Dash.
        if (
            input.DashPressed &&
            IsDashReady &&
            !IsDashing
        )
        {
            _dashDirection =
                direction != Vector3.Zero
                    ? direction
                    : FacingDirection;

            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown;
        }

        // 4. Sword slash.
        if (
            input.SwordPressed &&
            _swordCooldownTimer <= 0.0f &&
            _swordTimer <= 0.0f
        )
        {
            _swordTimer = SwordDuration;
            _swordCooldownTimer = SwordCooldown;
            _swordStrikeUsed = false;
        }

        // 5. Movement inside arena.
        Vector3 velocity = IsDashing
            ? _dashDirection * DashSpeed
            : direction * Speed;

        Vector3 positionBefore = Position;

        Move(
            velocity * deltaTime,
            bounds,
            obstacleCenter,
            obstacleRadius
        );

        // 6. Animate knight.
        float movedDistance =
            (Position - positionBefore).Length;

        _model.Animate(
            deltaTime,
            movedDistance,
            SwordProgress,
            IsDashing
        );
    }

    // Returns true once per sword slash,
    // when the sword reaches its hit moment.
    public bool ConsumeSwordStrike()
    {
        if (
            _swordStrikeUsed ||
            SwordProgress < 0.35f
        )
        {
            return false;
        }

        _swordStrikeUsed = true;

        return true;
    }

    // Increase maximum health through upgrades.
    public void IncreaseMaxHealth(int amount)
    {
        MaxHealth += Math.Max(0, amount);
        Health = MaxHealth;
    }

    // Restore health when the player is active.
    public void HealFully()
    {
        if (!IsKnockedOut)
        {
            Health = MaxHealth;
        }
    }

    // Returns true if damage was applied.
    // Invincible players cannot take damage.
    public bool TakeDamage(int amount)
    {
        if (IsInvincible || amount <= 0)
        {
            return false;
        }

        Health = Math.Max(0, Health - amount);

        WasHitThisFrame = true;

        if (Health == 0)
        {
            _respawnTimer = RespawnTime;

            _swordTimer = 0.0f;
            _dashTimer = 0.0f;

            Console.WriteLine(
                "The knight is knocked out!"
            );
        }
        else
        {
            _invincibleTimer =
                InvincibleTimeAfterHit;
        }

        return true;
    }

    public override void Draw(
        OpenGlRenderer renderer,
        Camera camera)
    {
        if (IsKnockedOut)
        {
            return;
        }

        // Blink while invincible after a hit.
        if (
            _invincibleTimer > 0.0f &&
            (int)(_invincibleTimer * 10.0f) % 2 == 0
        )
        {
            return;
        }

        _model.Draw(
            renderer,
            camera,
            ModelMatrix
        );
    }

    private void UpdateFacingDirection(
        Vector3? aimDirection,
        Vector3 walkDirection)
    {
        Vector3 look =
            aimDirection ?? walkDirection;

        if (look == Vector3.Zero)
        {
            return;
        }

        FacingDirection = look.Normalized();

        RotationY = MathF.Atan2(
            -FacingDirection.X,
            -FacingDirection.Z
        );
    }

    // Move along X and Z separately.
    // This allows the knight to slide along the crystal.
    private void Move(
        Vector3 displacement,
        ArenaBounds bounds,
        Vector3 obstacleCenter,
        float obstacleRadius)
    {
        Vector3 nextX = Position;

        nextX.X += displacement.X;

        nextX = bounds.Clamp(
            nextX,
            Radius
        );

        if (
            !OverlapsObstacle(
                nextX,
                obstacleCenter,
                obstacleRadius
            )
        )
        {
            Position = nextX;
        }

        Vector3 nextZ = Position;

        nextZ.Z += displacement.Z;

        nextZ = bounds.Clamp(
            nextZ,
            Radius
        );

        if (
            !OverlapsObstacle(
                nextZ,
                obstacleCenter,
                obstacleRadius
            )
        )
        {
            Position = nextZ;
        }
    }

    private bool OverlapsObstacle(
        Vector3 playerPosition,
        Vector3 obstacleCenter,
        float obstacleRadius)
    {
        float dx =
            playerPosition.X - obstacleCenter.X;

        float dz =
            playerPosition.Z - obstacleCenter.Z;

        float minimumDistance =
            Radius + obstacleRadius;

        return (
            dx * dx + dz * dz
            < minimumDistance * minimumDistance
        );
    }

    private void CountDownTimers(float deltaTime)
    {
        _dashTimer = MathF.Max(
            0.0f,
            _dashTimer - deltaTime
        );

        _dashCooldownTimer = MathF.Max(
            0.0f,
            _dashCooldownTimer - deltaTime
        );

        _invincibleTimer = MathF.Max(
            0.0f,
            _invincibleTimer - deltaTime
        );

        _swordTimer = MathF.Max(
            0.0f,
            _swordTimer - deltaTime
        );

        _swordCooldownTimer = MathF.Max(
            0.0f,
            _swordCooldownTimer - deltaTime
        );
    }

    private void Respawn()
    {
        _respawnTimer = 0.0f;

        Position = _spawnPosition;

        Health = MaxHealth;

        // Short protection after respawning.
        _invincibleTimer = 2.0f;

        Console.WriteLine(
            "The knight is back!"
        );
    }
}
