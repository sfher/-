using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerMotor : MonoBehaviour
{
    public enum Scheme { WASD, Arrows }

    [SerializeField] Scheme scheme = Scheme.WASD;

    [Header("Move")]
    [SerializeField] float moveSpeed = 7f;
    [SerializeField] float groundAccel = 90f;
    [SerializeField] float airAccel = 45f;
    [SerializeField] float turnAccelMultiplier = 1.6f;

    [Header("Jump")]
    [SerializeField] float jumpVelocity = 13f;
    [SerializeField] float riseGravityScale = 3.2f;
    [SerializeField] float fallGravityScale = 5.2f;
    [SerializeField] float maxFallSpeed = 20f;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBufferTime = 0.1f;
    [SerializeField] float jumpCutMultiplier = 0.5f;

    [Header("Ground")]
    [SerializeField] LayerMask groundMask;
    [SerializeField] float groundProbeDistance = 0.08f;

    [Header("Being pushed")]
    [SerializeField] float pushControlLockTime = 0.12f;
    [SerializeField] float pushSpeedThreshold = 1.5f;

    Rigidbody2D rb;
    Collider2D body;

    float moveInput;
    bool jumpPressed;
    bool jumpReleased;

    float coyoteTimer;
    float bufferTimer;
    float controlLockTimer;
    bool grounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        ReadInput();
        bufferTimer = jumpPressed ? jumpBufferTime : bufferTimer - Time.deltaTime;
    }

    void FixedUpdate()
    {
        grounded = ProbeGround();
        coyoteTimer = grounded ? coyoteTime : Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
        controlLockTimer = Mathf.Max(0f, controlLockTimer - Time.fixedDeltaTime);

        Vector2 v = rb.linearVelocity;

        if (controlLockTimer <= 0f)
            v.x = StepTowards(v.x, moveInput * moveSpeed, grounded ? groundAccel : airAccel);

        if (jumpReleased && v.y > 0f)
            v.y *= jumpCutMultiplier;

        if (bufferTimer > 0f && coyoteTimer > 0f)
        {
            v.y = jumpVelocity;
            bufferTimer = 0f;
            coyoteTimer = 0f;
        }

        rb.gravityScale = v.y < 0f ? fallGravityScale : riseGravityScale;
        v.y = Mathf.Max(v.y, -maxFallSpeed);

        rb.linearVelocity = v;
    }

    float StepTowards(float current, float target, float accel)
    {
        bool turning = Mathf.Abs(target) > 0.01f
                       && Mathf.Sign(target) != Mathf.Sign(current)
                       && Mathf.Abs(current) > 0.1f;
        if (turning) accel *= turnAccelMultiplier;
        return Mathf.MoveTowards(current, target, accel * Time.fixedDeltaTime);
    }

    bool ProbeGround()
    {
        Bounds b = body.bounds;
        var probeSize = new Vector2(b.size.x * 0.9f, 0.04f);
        var origin = new Vector2(b.center.x, b.min.y + 0.02f);
        return Physics2D.BoxCast(origin, probeSize, 0f, Vector2.down, groundProbeDistance, groundMask).collider != null;
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (!c.collider.TryGetComponent(out PlayerMotor other)) return;

        float otherSpeed = Mathf.Abs(other.rb.linearVelocity.x);
        float mySpeed = Mathf.Abs(rb.linearVelocity.x);

        // 被撞的一方短暂交出水平控制权，物理推挤才不会被下一帧的速度覆盖吃掉
        if (otherSpeed - mySpeed > pushSpeedThreshold)
            controlLockTimer = pushControlLockTime;
    }

    void ReadInput()
    {
        jumpPressed = false;
        jumpReleased = false;
        moveInput = 0f;

        var kb = Keyboard.current;
        if (kb == null) return;

        Key left, right, jump;
        if (scheme == Scheme.WASD)
        {
            left = Key.A;
            right = Key.D;
            jump = Key.Space;
        }
        else
        {
            left = Key.LeftArrow;
            right = Key.RightArrow;
            jump = Key.RightShift;
        }

        var l = kb[left];
        var r = kb[right];
        var j = kb[jump];

        moveInput = (r.isPressed ? 1f : 0f) - (l.isPressed ? 1f : 0f);
        jumpPressed = j.wasPressedThisFrame;
        jumpReleased = j.wasReleasedThisFrame;
    }
}
