using CrystalDefense.Core;
using Engine;
using Engine.Materials;
using Engine.OpenGL;
using OpenTK.Mathematics;

namespace CrystalDefense.PlayerAndProjectiles;

// The knight controlled by the player (the controls are read by PlayerInput: keyboard only or keyboard + mouse).
// He can walk, look/aim, shoot (ProjectileManager), slash with his sword and dash (short, fast, invincible move).
// He has health points. When he has none left he is knocked out and comes back after a few seconds.
public sealed class Player : GameObject
{
    // --- settings ---
    public const int MaxHealth = 5;
    public const float DashSpeed = 18.0f;
    public const float DashDuration = 0.18f;
    public const float DashCooldown = 1.0f;
    public const float InvincibleTimeAfterHit = 1.0f;
    public const float RespawnTime = 3.0f;
    public const float SwordDuration = 0.3f;
    public const float SwordCooldown = 0.5f;
    public const float SwordReach = 2.0f;
    public const float SwordHalfAngleDegrees = 75.0f;   // the sword hits in a cone of 150° in front of the knight
    public const int SwordDamage = 2;

    private readonly KnightModel _model;
    private readonly Vector3 _spawnPosition;

    // timers count down to 0, in seconds
    private float _dashTimer;
    private float _dashCooldownTimer;
    private float _invincibleTimer;
    private float _respawnTimer;
    private float _swordTimer;
    private float _swordCooldownTimer;

    private Vector3 _dashDirection;
    private bool _swordStrikeUsed;

    public Player(KnightModel model, Mesh mesh, Material material, Vector3 startPosition, float radius)
        : base(mesh, material, startPosition, radius)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _spawnPosition = startPosition;
        Health = MaxHealth;
    }

    // Walking speed in units per second.
    public float Speed { get; set; } = 6.0f;

    public int Health { get; private set; }

    // The direction the knight looks at (length 1). Projectiles and the sword go in this direction.
    public Vector3 FacingDirection { get; private set; } = -Vector3.UnitZ;

    public bool IsKnockedOut => _respawnTimer > 0.0f;
    public bool CanAct => !IsKnockedOut;
    public bool IsDashing => _dashTimer > 0.0f;
    public bool IsInvincible => IsKnockedOut || IsDashing || _invincibleTimer > 0.0f;
    public bool IsDashReady => _dashCooldownTimer <= 0.0f;

    // 0 → 1 during a sword slash, -1 when the knight does not attack.
    public float SwordProgress => _swordTimer > 0.0f ? 1.0f - _swordTimer / SwordDuration : -1.0f;

    // true during the frame in which the player was hit (used to shake the camera).
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
                Respawn();

            _model.Animate(deltaTime, 0.0f, -1.0f, false);
            return;
        }

        // 1. Movement direction (already relative to the camera, length 0 or 1).
        Vector3 direction = input.MoveDirection;

        // 2. Look where he aims (mouse or arrow keys). Without aiming, look where he walks.
        UpdateFacingDirection(input.AimDirection, direction);

        // 3. Dash: a short burst of speed in the walking direction (or forward), invincible.
        if (input.DashPressed && IsDashReady && !IsDashing)
        {
            _dashDirection = direction != Vector3.Zero ? direction : FacingDirection;
            _dashTimer = DashDuration;
            _dashCooldownTimer = DashCooldown;
        }

        // 4. Sword slash.
        if (input.SwordPressed && _swordCooldownTimer <= 0.0f && _swordTimer <= 0.0f)
        {
            _swordTimer = SwordDuration;
            _swordCooldownTimer = SwordCooldown;
            _swordStrikeUsed = false;
        }

        // 5. Move, staying inside the arena and outside the crystal.
        Vector3 velocity = IsDashing ? _dashDirection * DashSpeed : direction * Speed;
        Vector3 positionBefore = Position;
        Move(velocity * deltaTime, bounds, obstacleCenter, obstacleRadius);

        // 6. Animate the knight from what he did in this frame.
        float movedDistance = (Position - positionBefore).Length;
        _model.Animate(deltaTime, movedDistance, SwordProgress, IsDashing);
    }

    // Returns true only once per slash, in the middle of the swing: that is when the sword hits.
    // The collision system calls it to know when to look for enemies in front of the knight.
    public bool ConsumeSwordStrike()
    {
        if (_swordStrikeUsed || SwordProgress < 0.35f)
            return false;

        _swordStrikeUsed = true;
        return true;
    }

    // Returns true if the hit counted (not while invincible).
    public bool TakeDamage(int amount)
    {
        if (IsInvincible || amount <= 0)
            return false;

        Health = Math.Max(0, Health - amount);
        WasHitThisFrame = true;

        if (Health == 0)
        {
            _respawnTimer = RespawnTime;
            _swordTimer = 0.0f;
            _dashTimer = 0.0f;
            Console.WriteLine("The knight is knocked out!");
        }
        else
        {
            _invincibleTimer = InvincibleTimeAfterHit;
        }

        return true;
    }

    public override void Draw(OpenGlRenderer renderer, Camera camera)
    {
        if (IsKnockedOut)
            return;

        // Blink while invincible after a hit: drawn every other tenth of a second.
        if (_invincibleTimer > 0.0f && (int)(_invincibleTimer * 10.0f) % 2 == 0)
            return;

        _model.Draw(renderer, camera, ModelMatrix);
    }

    private void UpdateFacingDirection(Vector3? aimDirection, Vector3 walkDirection)
    {
        Vector3 look = aimDirection ?? walkDirection;

        if (look == Vector3.Zero)
            return;

        FacingDirection = look.Normalized();
        RotationY = MathF.Atan2(-FacingDirection.X, -FacingDirection.Z);
    }

    // Moves along X and Z separately, so that the knight slides along the crystal instead of getting stuck.
    private void Move(Vector3 displacement, ArenaBounds bounds, Vector3 obstacleCenter, float obstacleRadius)
    {
        Vector3 nextX = Position;
        nextX.X += displacement.X;
        nextX = bounds.Clamp(nextX, Radius);
        if (!OverlapsObstacle(nextX, obstacleCenter, obstacleRadius))
            Position = nextX;

        Vector3 nextZ = Position;
        nextZ.Z += displacement.Z;
        nextZ = bounds.Clamp(nextZ, Radius);
        if (!OverlapsObstacle(nextZ, obstacleCenter, obstacleRadius))
            Position = nextZ;
    }

    private bool OverlapsObstacle(Vector3 playerPosition, Vector3 obstacleCenter, float obstacleRadius)
    {
        float dx = playerPosition.X - obstacleCenter.X;
        float dz = playerPosition.Z - obstacleCenter.Z;
        float minimumDistance = Radius + obstacleRadius;
        return dx * dx + dz * dz < minimumDistance * minimumDistance;
    }

    private void CountDownTimers(float deltaTime)
    {
        _dashTimer = MathF.Max(0.0f, _dashTimer - deltaTime);
        _dashCooldownTimer = MathF.Max(0.0f, _dashCooldownTimer - deltaTime);
        _invincibleTimer = MathF.Max(0.0f, _invincibleTimer - deltaTime);
        _swordTimer = MathF.Max(0.0f, _swordTimer - deltaTime);
        _swordCooldownTimer = MathF.Max(0.0f, _swordCooldownTimer - deltaTime);
    }

    private void Respawn()
    {
        _respawnTimer = 0.0f;
        Position = _spawnPosition;
        Health = MaxHealth;
        _invincibleTimer = 2.0f;   // a short protection after coming back
        Console.WriteLine("The knight is back!");
    }
}
