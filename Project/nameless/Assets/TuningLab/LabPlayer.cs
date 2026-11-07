using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 调试用的方块玩家：1 格大小、不旋转、竖直重力。
/// 关键点是"速度不死"：空中制动默认 0，所以 v1/v2 可以一直保持着飞；
/// 等级只是速度的另一种说法，不需要额外的"锁存"系统。
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class LabPlayer : MonoBehaviour
{
    public enum Scheme { P1, P2 }

    public TuningLab lab;
    public Scheme scheme;
    public LabPlayer other;
    /// <summary>碰撞箱边长（像素）。精灵和碰撞箱用同一个数，视觉 = 判定。</summary>
    public int pixels = 16;
    /// <summary>只有大框 A 有"边"这个概念，红热只在它身上判定。</summary>
    public bool canBeRedHot = false;

    // ---- 对外读数 ----
    public bool Grounded => grounded;
    public bool IsDead { get; private set; }
    public bool IsAlive => !IsDead;
    /// <summary>每次 <see cref="Teleport"/> 自增；重生点靠它区分“走进去”和“被传送/复活放进去”。</summary>
    public int TeleportCount { get; private set; }
    public bool Dashing => dashLock > 0f;
    /// <summary>充能接触：站在地形上，或碰到另一名玩家。贴墙、顶天花板不算。</summary>
    public bool Touching => grounded || playerContactTimer > 0f;
    /// <summary>正踩在冰块上：独立冰块直接看 support；合并地形按脚下格找冰。</summary>
    public bool OnIce
    {
        get
        {
            if (!grounded) return false;
            if (support != null && support.GetComponentInParent<LabIce>() != null) return true;
            if (support != null && support.GetComponent<LabTerrainField>() == null) return false;
            return IceUnderFeet();
        }
    }
    /// <summary>水平冲刺后速度仍处于 v1 区间、重力被关掉的状态。</summary>
    public bool Hovering => dashPhase == DashPhase.Dash;
    /// <summary>冲刺态：位移恒定、不受外力。大炮只吃这个状态撞进来的 B。</summary>
    public bool InDashState => dashPhase == DashPhase.Dash;
    /// <summary>本次冲刺的方向（单轴）。</summary>
    public Vector2 DashDirection => dashDir;

    /// <summary>
    /// 当前冲刺意图方向 —— 眼睛显示的就是它，冲刺用的也是它。
    /// 优先级：竖直输入 &gt; 水平输入 &gt; 空中的竖直速度 &gt; 持久朝向。
    /// </summary>
    public Vector2 Aim
    {
        get
        {
            if (Mathf.Abs(vertInput) > 0.01f) return new Vector2(0f, Mathf.Sign(vertInput));
            if (Mathf.Abs(moveX) > 0.01f) return new Vector2(Mathf.Sign(moveX), 0f);
            if (!grounded && Mathf.Abs(rb.linearVelocity.y) > 0.5f)
                return new Vector2(0f, Mathf.Sign(rb.linearVelocity.y));
            return new Vector2(Mathf.Sign(facing), 0f);
        }
    }
    /// <summary>水平 / 竖直分量各自的速度与等级。只认轴的游戏里，这两个才是真等级。</summary>
    public float SpeedAlong(Vector2 normal) => Mathf.Abs(Vector2.Dot(rb.linearVelocity, normal));
    public int TierAlong(Vector2 normal) => lab != null ? lab.TierOfSpeed(SpeedAlong(normal)) : 0;

    public float HorizontalSpeed => Mathf.Abs(rb.linearVelocity.x);
    public float VerticalSpeed => Mathf.Abs(rb.linearVelocity.y);
    public int HorizontalTier => lab != null ? lab.TierOfSpeed(HorizontalSpeed) : 0;
    public int VerticalTier => lab != null ? lab.TierOfSpeed(VerticalSpeed) : 0;

    /// <summary>最近一次撞地形（墙/地/天花板）按法线分量算出的结果。</summary>
    public Vector2 LastImpactNormal { get; private set; }
    public float LastImpactSpeed { get; private set; }
    public int LastImpactTier { get; private set; } = -1;
    public float LastImpactTime { get; private set; } = -999f;
    /// <summary>冲刺态：位移恒定、不受外力、屏蔽输入。</summary>
    public enum DashPhase { None, Dash }
    /// <summary>这次充能已经被接触"上弦"，之后即使离地也会继续回复。</summary>
    public bool RefillArmed => refillArmed;
    public float Charge { get; private set; } = 1f;
    /// <summary>
    /// 碰撞箱的竖直半径。碰撞箱关掉时（进膛、死亡）bounds 会报 0，
    /// 但 BoxCollider2D.size 还在，按它算才能把尸体/膛里的人放到正确高度。
    /// </summary>
    public float HalfHeight
    {
        get
        {
            if (col is BoxCollider2D box)
                return box.size.y * 0.5f * Mathf.Abs(transform.lossyScale.y);
            return col != null && col.enabled ? col.bounds.extents.y : 0.5f;
        }
    }

    /// <summary>当前碰撞箱的世界包围盒；碰撞箱关掉时是零尺寸，调用方要先用 IsAlive / Suspended 过滤。</summary>
    public Bounds BodyBounds => col != null && col.enabled
        ? col.bounds
        : new Bounds(rb != null ? rb.position : (Vector2)transform.position, Vector3.zero);
    public float FootY => rb.position.y - HalfHeight;
    public Vector2 Velocity => rb.linearVelocity;
    public float Speed => rb.linearVelocity.magnitude;

    /// <summary>离地点（跳起或走下台）的脚底高度。</summary>
    public float LaunchFootY { get; private set; }
    /// <summary>本次腾空最高点的脚底高度。</summary>
    public float ApexFootY { get; private set; }

    /// <summary>落地点格：从起跳点算起，站跳永远接近 0（跳高与下落互相抵消）。</summary>
    public float LandedNetDescent { get; private set; }
    /// <summary>落地点格：从最高点算起，站跳 = 跳高。</summary>
    public float LandedApexDescent { get; private set; }

    public float NetDescent => grounded ? LandedNetDescent : Mathf.Max(0f, LaunchFootY - FootY);
    public float ApexDescent => grounded ? LandedApexDescent : Mathf.Max(0f, ApexFootY - FootY);

    /// <summary>本体基色（A 蓝 / B 红），速度反馈的残影和拖尾用它保持同色。</summary>
    public Color BaseColor => visual != null ? visual.BaseColor : Color.white;

    public int SpeedTier => lab != null ? lab.TierOfSpeed(Speed) : 0;
    public int LaunchTier => lab != null ? lab.TierOfCells(NetDescent) : 0;
    public int ApexTier => lab != null ? lab.TierOfCells(ApexDescent) : 0;

    public float HitFlash { get; private set; }

    // ---- 形态与膛内（Issue #14 / #15）----

    /// <summary>哪几条边碎成了灵体；形态完全由它算出来。</summary>
    public int SpiritMask => spiritMask;
    public LabForm Form => form;
    /// <summary>
    /// 灵体态：四条边全碎，穿透一切实体、不能冲刺，唯一保留的交互是钥匙（ADR-0007）。
    /// 由 form 派生，不另开一套状态位。
    /// </summary>
    public bool IsIntangible => form == LabForm.Spirit;
    /// <summary>灵体剩余存在时间（秒）；不在灵体态时为 0。</summary>
    public float SpiritTimeLeft => IsIntangible ? Mathf.Max(0f, spiritTimer) : 0f;
    /// <summary>已装入身体、尚未被门消耗的钥匙数量。</summary>
    public int KeyCount { get; private set; }
    /// <summary>捡钥匙只看人在不在场外：碎边、形态都不拦。A 和 B 一样，
    /// 只有被装进队友炮膛（<see cref="Suspended"/>）时捡不到；死亡是 Destroy，对象已经没了。</summary>
    public bool CanCollectKey => !Suspended;
    /// <summary>形态读数，带朝向，给 HUD 用。</summary>
    public string FormLabel => LabForms.Label(spiritMask, form);
    /// <summary>炮口朝向 = 唯一（或最后）碎掉的那条边。</summary>
    public Vector2 MouthNormal => LabForms.EdgeNormal(LabForms.MouthEdge(spiritMask));

    /// <summary>膛里有人（A 侧）。</summary>
    public bool Loaded => loaded != null;
    /// <summary>L 形转向器正在等待自动出射。</summary>
    public bool ConverterLoaded => loaded != null && loadedConverter;
    public float ConverterTimeLeft => ConverterLoaded ? Mathf.Max(0f, converterLoadTimer) : 0f;
    /// <summary>我被装在大炮 / 幻影膛里（B 侧）：不移动、不充能、不算地面。</summary>
    public bool Suspended => host != null || phantomHost != null;
    public LabPlayer Host => host;
    public LabPhantom PhantomHost => phantomHost;
    /// <summary>大炮形态且膛空 = 可以装填，但还要求 B 处于冲刺态。</summary>
    public bool CanLoad => canBeRedHot && form == LabForm.Cannon && loaded == null;
    /// <summary>进入时记下的速度 V前；膛空时是零。</summary>
    public Vector2 ChamberVelocity => loaded != null ? loaded.chamberVelocity : Vector2.zero;
    /// <summary>下一发的出膛速度：√(Vb² + 一个等级间距的 v²)。</summary>
    public float LaunchSpeed => lab == null || loaded == null
        ? 0f
        : Mathf.Sqrt(Mathf.Max(0f, loaded.chamberVelocity.sqrMagnitude + lab.CannonBoostV2));

    Rigidbody2D rb;
    Collider2D col;
    Collider2D support;
    public LabVisual visual;

    // NonAlloc 查询缓冲（照 LabIce/LabPhantom 的现行写法）：容量按 1 格范围内的
    // 叠压上限给 16，远超实际（地形是合并碰撞体、玩家 2、钥匙/机关个位数）。
    readonly Collider2D[] overlapHits = new Collider2D[16];
    readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];

    bool grounded;
    float dashLock;
    float playerContactTimer;
    float coyoteTimer;
    float jumpBuffer;
    float nextHitTime;
    /// <summary>外抛撞击事件的最小接近速度（#82）：紧贴静止时的求解器残速远低于它，主动顶撞的走路速度 8 远高于它。</summary>
    const float HitEventMinClosingSpeed = 1f;
    float facing = 1f;
    float dashTimer;
    Vector2 dashDir;
    float dashSpeedNow;
    float dashExitSpeed;
    DashPhase dashPhase;
    bool refillArmed;
    Vector2 lastFixedVelocity;
    bool dying;                       // 一次死亡只结算一遍：接触和扫掠会在同一帧各报一次
    int magmaTouchNow;                // 本物理步接触到的岩浆面（每条边一位）
    int magmaTouchPrev;               // 上一物理步，用来判定“第一次接触”

    int spiritMask;
    LabForm form = LabForm.Normal;
    float spiritTimer;

    LabPlayer loaded;               // 膛里的 B（只有 A 有）
    LabPlayer host;                 // 我在谁的膛里（只有 B 有）
    LabPhantom phantomHost;         // 我在哪个幻影的膛里
    Vector2 chamberVelocity;        // B 进入时的速度 V前
    Vector2 chamberDir;             // B 进入时的冲刺轴，射出取它的反向
    bool gliding;                   // 水平射出：速度降到零级之前不受重力
    RigidbodyType2D teleportPreviousBodyType;
    float teleportPreviousGravityScale;
    bool teleportFrozen;

    bool loadedConverter;
    Edge converterExitEdge;
    DashState loadedDash;
    float converterLoadTimer;

    float moveX;
    float vertInput;
    bool jumpDownThisFrame;
    bool jumpHeld;
    bool dashDownThisFrame;
    bool dashQueued;
    bool recallDownThisFrame;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // 站立不动时刚体会休眠，OnCollisionStay 不再触发 → 充能会莫名卡住
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        LaunchFootY = FootY;
        ApexFootY = FootY;
    }

    void Update()
    {
        if (IsDead || (lab != null && (lab.TeleportBusy || lab.Paused))) return;
        ReadInput();

        // 膛里的 B：移动无效，冲刺键改成给宿主开炮。
        if (phantomHost != null)
        {
            if (dashDownThisFrame) phantomHost.Fire();
            return;
        }
        if (host != null)
        {
            if (dashDownThisFrame) host.Fire();
            return;
        }

        if (recallDownThisFrame && lab != null && lab.TryRecall(this)) return;

        if (jumpDownThisFrame && lab != null) jumpBuffer = lab.jumpBufferTime;
        else jumpBuffer = Mathf.Max(0f, jumpBuffer - Time.deltaTime);

        if (dashDownThisFrame) dashQueued = true;

        if (Mathf.Abs(moveX) > 0.01f) facing = Mathf.Sign(moveX);

        if (visual != null)
        {
            // 外圈：四个方向各自显示对应分量的等级（只亮"正在去"的方向）
            Vector2 vel = rb.linearVelocity;
            visual.SetRingTiers(
                vel.y > 0.01f ? lab.TierOfSpeed(vel.y) : 0,
                vel.y < -0.01f ? lab.TierOfSpeed(-vel.y) : 0,
                vel.x < -0.01f ? lab.TierOfSpeed(-vel.x) : 0,
                vel.x > 0.01f ? lab.TierOfSpeed(vel.x) : 0);

            visual.SetAim(Aim);             // 白眼睛 = 冲刺意图方向（四向）
        }
    }

    void FixedUpdate()
    {
        if (lab == null || IsDead || lab.Paused) return;

        if (teleportFrozen || lab.TeleportBusy)
        {
            dashQueued = false;
            dashPhase = DashPhase.None;
            dashTimer = 0f;
            gliding = false;
            rb.linearVelocity = Vector2.zero;
            return;             // 冻结期间保留接触面状态，取消选择后不会把同一面又算成新接触
        }

        // 碰撞回调在 FixedUpdate 之后跑：先把上一步的接触面挪到 prev，再收集这一步的新接触。
        magmaTouchPrev = magmaTouchNow;
        magmaTouchNow = 0;

        float dt = Time.fixedDeltaTime;
        var v = rb.linearVelocity;

        // 膛内：既不算地面、也不回复充能，只是被宿主带着走。
        if (phantomHost != null)
        {
            rb.position = phantomHost.transform.position;
            transform.position = phantomHost.transform.position;
            return;
        }
        if (host != null)
        {
            rb.position = host.rb.position;
            transform.position = host.transform.position;
            return;
        }

        if (loaded != null && loadedConverter)
        {
            converterLoadTimer -= dt;
            if (converterLoadTimer <= 0f) FireConverter();
        }

        // 灵体：存在时间不因为贴地、贴墙、在空中而停表；耗尽就是死亡。
        if (IsIntangible)
        {
            TickSpirit(dt);
            if (IsDead) return;
            SweepKeys();
        }

        bool wasGrounded = grounded;
        grounded = ProbeGround();

        // 灵体的碰撞箱是触发器，物理引擎不再托人：只保留"踩住实心顶面"这一条豁免（ADR-0007）。
        if (IsIntangible && grounded && v.y < 0f) v.y = 0f;

        if (grounded && !wasGrounded) OnLand();
        if (!grounded && wasGrounded) BeginAir();
        if (!grounded) ApexFootY = Mathf.Max(ApexFootY, FootY);

        coyoteTimer = grounded ? lab.coyoteTime : Mathf.Max(0f, coyoteTimer - dt);
        dashLock = Mathf.Max(0f, dashLock - dt);
        playerContactTimer = Mathf.Max(0f, playerContactTimer - dt);
        HitFlash = Mathf.Max(0f, HitFlash - dt * 4f);

        // 充能：接触地面或另一名玩家 = 给这一次充能"上弦"。
        // 上弦之后计时器自己走完，离开地面也照常回复；用掉之后需要重新接触才能再上弦。
        if (Touching) refillArmed = true;

        if (refillArmed && dashLock <= 0f && Charge < 1f)
        {
            Charge = Mathf.Min(1f, Charge + dt / Mathf.Max(0.01f, lab.dashCooldown));
            if (Charge >= 1f) ChargeCompleted?.Invoke(this);
        }

        if (Charge >= 1f) refillArmed = false;

        // ---- 冲刺：进入"冲刺态"，位移恒定，速度由进入时的 V前 分三档 ----
        // 向下冲刺只允许在空中；站在地上按下去= 什么都不发生，也不消耗充能。
        bool downDashBlocked = vertInput < -0.01f && grounded;
        if (dashQueued && Charge >= 1f && dashPhase != DashPhase.Dash && !downDashBlocked
            && !IsIntangible && (!lab.groundOnlyDash || grounded))
        {
            // 冲刺方向优先级：竖直输入 > 水平输入 > 空中的竖直速度方向 > 面朝方向
            // 方向由 Aim 统一决定（和眼睛显示的是同一个东西）
            Vector2 dir = Aim;

            // 抹掉垂直分量，只留沿冲刺方向的分量
            float vBefore = Vector2.Dot(v, dir);

            bool opposite = vBefore < -0.001f;                 // 和冲刺方向反向
            bool zeroTier = vBefore <= lab.V0Speed + 0.0001f;  // 零级（含静止）

            float dashSpeed, exitSpeed;
            if (opposite)
            {
                dashSpeed = lab.Vd1;      // 略大于 v0
                exitSpeed = 0f;           // 结束后归零
            }
            else if (zeroTier)
            {
                dashSpeed = lab.Vd1;      // 略大于 v0
                exitSpeed = Mathf.Max(0f, vBefore);   // 结束后回到 V前
            }
            else
            {
                dashSpeed = vBefore;      // 已经够快：原速穿过
                exitSpeed = vBefore;      // 结束后保持
            }

            Charge = 0f;
            gliding = false;
            dashLock = lab.dashLockTime;
            dashDir = dir;
            dashSpeedNow = dashSpeed;
            dashExitSpeed = exitSpeed;
            dashTimer = lab.DashDistance / Mathf.Max(0.01f, dashSpeed);   // 位移恒定 → 时长反算
            dashPhase = DashPhase.Dash;

            v = dir * dashSpeed;
            rb.linearVelocity = v;
            rb.gravityScale = 0f;
            lastFixedVelocity = v;
            dashQueued = false;
            return;                     // 本帧到此为止
        }
        dashQueued = false;

        // ---- 冲刺态推进：不受任何外力、屏蔽任何输入 ----
        if (dashPhase == DashPhase.Dash)
        {
            dashTimer -= dt;
            if (dashTimer > 0f)
            {
                v = dashDir * dashSpeedNow;
                rb.gravityScale = 0f;
                rb.linearVelocity = v;
                lastFixedVelocity = v;
                return;
            }

            // 冲刺结束：给出结束速度，本帧继续走常规流程
            dashPhase = DashPhase.None;
            v = dashDir * dashExitSpeed;
        }

        // ---- 水平控制 ----
        if (dashLock <= 0f)
        {
            // 空气制动：空中水平方向持续 -g，和竖直方向的重力对称。
            // 这样"一个间距"的水平位移和竖直上冲高度天然相等。
            if (!grounded && lab.horizontalAirDrag)
                v.x = Mathf.MoveTowards(v.x, 0f, lab.gravity * dt);

            // 冰面只认"受重力的行走态"：冲刺态会早退，这里再挡一次无重力状态。
            bool onIce = OnIce && !InDashState && rb.gravityScale > 0.01f;

            if (Mathf.Abs(moveX) > 0.01f)
            {
                facing = Mathf.Sign(moveX);
                float target = moveX * lab.walkSpeed;

                if (onIce)
                {
                    // a = g：v² 每 4 格增加一个等级间距；已经超速时不反向减速。
                    float cap = lab.IceMaxSpeed;
                    if (moveX * v.x < cap)
                        v.x = Mathf.MoveTowards(v.x, moveX * cap, lab.gravity * dt);
                }
                else if (grounded)
                {
                    // 地面永远有摩擦：按住同方向也照样把超出走路速度的部分拉回来。
                    // 否则"按住右"就能把冲刺速度永久锁死 = 无限滑行。
                    float rate = Mathf.Abs(v.x) > lab.walkSpeed ? lab.groundBrake : lab.walkAccel;
                    v.x = Mathf.MoveTowards(v.x, target, rate * dt);
                }
                else
                {
                    // 空中：只负责把速度往走路速度推，超过走路速度的部分交给上面的 -g 制动
                    if (Mathf.Abs(v.x) <= lab.walkSpeed)
                        v.x = Mathf.MoveTowards(v.x, target, lab.walkAccel * dt);
                }
            }
            else if (!onIce)
            {
                // 冰面松手只停止加速，不施加地面制动，保留滑行。
                float brake = grounded ? lab.groundBrake : lab.airBrake;
                if (brake > 0f) v.x = Mathf.MoveTowards(v.x, 0f, brake * dt);
            }
        }

        // ---- 跳跃：固定高度，无变高 ----
        // 按住连跳是个开关：默认关闭（必须重新按），打开后落地即跳。
        bool wantsJump = jumpBuffer > 0f || (lab.holdToJump && jumpHeld);
        if (wantsJump && (grounded || coyoteTimer > 0f))
        {
            v.y = lab.JumpVelocity;
            jumpBuffer = 0f;
            coyoteTimer = 0f;
            grounded = false;
            BeginAir();
            LabAudio.Play(LabAudio.Cue.Jump);
        }

        if (v.y < -lab.maxFallSpeed) v.y = -lab.maxFallSpeed;

        // ---- 驮着走：站在队友头上时横向跟随他，自己的方向键仍然有效（用来在头上挪位置）----
        // 冲刺那几段不接管，否则会在别人头上冲刺时被硬拉回来
        if (grounded && dashLock <= 0f && dashPhase == DashPhase.None && IsRidingOn(other))
            v.x = other.Velocity.x + moveX * lab.walkSpeed;

        // 冲刺态在上面的早退里已经把重力关掉了，走到这里一定是常规状态
        // 水平射出：速度降到零级之前不受重力（Issue #15）
        if (gliding && Mathf.Abs(v.x) >= lab.V0Speed) rb.gravityScale = 0f;
        else
        {
            gliding = false;
            rb.gravityScale = lab.GravityScale;
        }

        rb.linearVelocity = v;
        // 撞地形时用它算分量：碰撞回调触发时速度已经被物理解算改掉（贴地时竖直分量已经是 0）
        lastFixedVelocity = v;
    }

    void BeginAir()
    {
        LaunchFootY = FootY;
        ApexFootY = FootY;
    }

    void OnLand()
    {
        LandedNetDescent = Mathf.Max(0f, LaunchFootY - FootY);
        LandedApexDescent = Mathf.Max(0f, ApexFootY - FootY);
    }

    // ------------------------------------------------------------------
    // 碰撞：方块不旋转，所以接触只可能是水平面或竖直面，永不产生斜向。
    // 撞击是"设定"：被撞者直接升到 v2 顶端，撞击者不掉速。
    // ------------------------------------------------------------------

    void OnCollisionEnter2D(Collision2D c)
    {
        if (Suspended) return;         // 膛里的人没有碰撞箱，这里的回调只能是进膛那一刻的残留
        if (IsPhantom(c.collider)) return;   // 幻影只由 LabPhantom 自己结算
        bool isPlayer = c.collider != null && c.collider.GetComponent<LabPlayer>() != null;
        if (isPlayer) playerContactTimer = 0.12f;
        else if (!ResolveMagmaContact(c)) ForgeFromTerrain(c);   // 只有进入的那一帧锻造，贴墙摩擦不算

        TryResolveHit(c);
    }

    void OnCollisionStay2D(Collision2D c)
    {
        if (Suspended) return;
        if (IsPhantom(c.collider)) return;
        bool isPlayer = c.collider != null && c.collider.GetComponent<LabPlayer>() != null;
        if (isPlayer) playerContactTimer = 0.12f;
        else
        {
            ResolveMagmaContact(c);      // 新贴上的岩浆面在这里结算第一次接触
            // 独立冰块每块有自己的 Enter 回调；合并地形不会，所以 Stay 里补扫冰格。
            // 修复方块同理，而且它是"贴着就修"：边在贴着期间才变红也认。
            if (c.collider != null && c.collider.GetComponent<LabTerrainField>() != null)
            {
                ResolveIceContact(c);
                ResolveRepairContact(c);
            }
        }

        TryResolveHit(c);
    }

    /// <summary>
    /// 岩浆接触：每一条边只在“第一次接触”时强制红热；持续贴着不再重复触发。
    /// 强制红热走 StrikeEdge 的死亡逻辑：Normal → Hot2，已经 Hot2 → 塌框死亡。
    /// 同一帧以 v2 以上撞上来时，撞击本身再点一次红热，两个红热叠加也是死亡。
    /// v3 撞击仍然整组破坏。返回 false 表示这次碰撞没有岩浆接触，交给普通地形锻造。
    /// </summary>
    bool ResolveMagmaContact(Collision2D c)
    {
        if (c.collider == null) return false;

        // 关卡里地形和岩浆共用一个 CompositeCollider2D，接触回调拿到的是整块合并体；
        // 测试/手摆的独立岩浆块则直接是自身的 BoxCollider2D。
        var field = c.collider.GetComponent<LabTerrainField>();
        var direct = field != null ? null : c.collider.GetComponentInParent<LabMagma>();
        if (field == null && direct == null) return false;
        if (lab == null || c.contactCount == 0) return true;

        bool anyMagma = false;
        LabMagma v3Hit = null;

        for (int i = 0; i < c.contactCount; i++)
        {
            ContactPoint2D contact = c.GetContact(i);
            Vector2 n = AxisNormal(contact.normal);

            LabMagma magma = direct;
            if (field != null)
                magma = LabMagma.AtCell(contact.point - n * 0.02f);
            if (magma == null) continue;

            anyMagma = true;
            Edge edge = EdgeFor(-n);
            int bit = 1 << (int)edge;
            bool wasTouching = ((magmaTouchPrev | magmaTouchNow) & bit) != 0;
            magmaTouchNow |= bit;
            if (wasTouching) continue;

            float impact = Mathf.Abs(Vector2.Dot(lastFixedVelocity, n));
            if (lab.TierOfSpeed(impact) >= LabMagma.BreakTier) v3Hit = magma;

            if (IsDead) continue;

            // 第一次接触：强制红热。热二再吃热会直接走塌框/死亡。
            StrikeEdge(edge, true);

            // 二级及以上的撞击本身也是红热；上一步已经把常态边点成热二，这里必然致死。
            if (impact >= lab.RedHotSpeed) StrikeEdge(edge, true);
        }

        if (v3Hit != null) v3Hit.BreakGroup();
        return anyMagma;
    }

    /// <summary>接触法线归到单轴；方块不旋转，永不产生斜向。</summary>
    static Vector2 ContactNormal(Collision2D c) => AxisNormal(c.GetContact(0).normal);

    static Vector2 AxisNormal(Vector2 n)
    {
        return Mathf.Abs(n.x) >= Mathf.Abs(n.y)
            ? new Vector2(Mathf.Sign(n.x), 0f)
            : new Vector2(0f, Mathf.Sign(n.y));
    }

    /// <summary>
    /// 撞墙 / 撞地 / 撞天花板：只取沿接触法线的速度分量来算等级，顺便结算锻造。
    /// 合速度 v3 但只是横向飞得快的人，撞地板时竖直分量可能还是 v0。
    /// 法线从物件指向自己，取反才是被砸的那一条边。
    /// </summary>
    void ForgeFromTerrain(Collision2D c)
    {
        if (lab == null || c.contactCount == 0) return;

        // 修复方块和冰都不参与普通地形的锻造状态机：碰到修复面就只结算修复。
        if (ResolveRepairContact(c)) return;

        // 冰不参与普通地形的锻造状态机；合并地形里要先按接触点找到冰格。
        if (ResolveIceContact(c)) return;

        Vector2 n = ContactNormal(c);

        // 用上一帧 FixedUpdate 写进去的速度：碰撞回调里速度已经被解算改掉了
        float impact = Mathf.Abs(Vector2.Dot(lastFixedVelocity, n));

        LastImpactNormal = n;
        LastImpactSpeed = impact;
        LastImpactTier = lab.TierOfSpeed(impact);
        LastImpactTime = Time.time;

        // 绳不是锻造面：接触 / 下落 / 穿过都不动边态。
        // 唯一的例外是绳自己判定反弹（三级以上），由绳组件调 ApplyTerrainImpact 结算。
        if (IsRope(c.collider)) return;

        ApplyTerrainImpact(n, impact);
    }

    /// <summary>
    /// 冰块接触：只在承重豁免之外结算。合并地形没有单块身份，用接触点找回冰格。
    /// 玩家在冰面上滑行时 grounded + footprint 豁免，所以 v2/v3 滑行不会碎冰。
    /// 返回 false 表示这次碰撞没有冰接触，交给普通地形或绳。
    /// </summary>
    bool ResolveIceContact(Collision2D c)
    {
        if (lab == null || c.contactCount == 0) return false;

        var direct = c.collider != null ? c.collider.GetComponentInParent<LabIce>() : null;
        var field = c.collider != null ? c.collider.GetComponent<LabTerrainField>() : null;
        if (direct == null && field == null) return false;

        Vector2 n = ContactNormal(c);
        float impact = Mathf.Abs(Vector2.Dot(lastFixedVelocity, n));
        LabIce ice = direct;

        if (ice == null)
        {
            // 合并地形没有单块身份：找第一个落在冰块上的接触点。
            for (int i = 0; i < c.contactCount; i++)
            {
                ContactPoint2D contact = c.GetContact(i);
                Vector2 cn = AxisNormal(contact.normal);
                var candidate = LabIce.AtCell(contact.point - cn * 0.02f);
                if (candidate == null) continue;
                ice = candidate;
                n = cn;
                impact = Mathf.Abs(Vector2.Dot(lastFixedVelocity, cn));
                break;
            }

            if (ice == null) return false;
        }

        LastImpactNormal = n;
        LastImpactSpeed = impact;
        LastImpactTier = lab.TierOfSpeed(impact);
        LastImpactTime = Time.time;

        // 冰格落在玩家脚下 footprint 内时一律不结算：交界处侧向接触也可能碰到承重块，
        // 融掉它会让玩家落下去并被地面制动停住。合并地形没有接缝侧面，滑行只产生
        // 顶面接触，所以 v2/v3 滑行不会碎冰。
        if (grounded && IsIceUnderPlayer(ice)) return true;

        // 沿接触法线撞进去的分量到 v2 → 整组破坏（优先于红热）。
        if (lab.TierOfSpeed(impact) >= lab.IceBreakTier)
        {
            ice.BreakGroup();
            return true;
        }

        // 非承重的红热边（Hot2）→ 只融这一块，边强制回普通。
        if (canBeRedHot)
        {
            Edge edge = EdgeFor(-n);
            if (GetEdge(edge) == EdgeState.Hot2)
            {
                ice.BreakOne();
                SetEdge(edge, EdgeState.Normal);
                LabAudio.Play(LabAudio.Cue.IceMelt);
            }
        }

        return true;
    }

    /// <summary>
    /// 修复方块接触：按每一个接触点的法线找回被碰的那条边，交给 <see cref="TryRepairEdge"/>。
    /// 方块不会被消耗，所以同一次接触里几条边各自结算，重复贴同一条边也只是空转。
    /// 返回 false 表示这次碰撞没碰到修复方块，交给冰和普通地形。
    /// </summary>
    bool ResolveRepairContact(Collision2D c)
    {
        if (c.collider == null || c.contactCount == 0) return false;

        var field = c.collider.GetComponent<LabTerrainField>();
        // 关卡生成时修复方块并进地形合并体；测试或手摆的独立方块直接是自身的碰撞箱。
        var direct = field != null ? null : c.collider.GetComponentInParent<LabRepairBlock>();
        if (field == null && direct == null) return false;

        bool anyRepair = false;
        for (int i = 0; i < c.contactCount; i++)
        {
            ContactPoint2D contact = c.GetContact(i);
            Vector2 n = AxisNormal(contact.normal);

            var block = field != null ? LabRepairBlock.AtCell(contact.point - n * 0.02f) : direct;
            if (block == null) continue;

            anyRepair = true;
            TryRepairEdge(EdgeFor(-n));
        }

        return anyRepair;
    }

    /// <summary>
    /// 结算一次场景撞击。普通地形和绳反弹共用；冰、岩浆和修复方块有自己的结算，不走这里。
    /// normal 从物件指向玩家，impact 是碰撞前沿 normal 的分量。
    /// </summary>
    public void ApplyTerrainImpact(Vector2 normal, float impact)
    {
        if (lab == null) return;
        if (impact < lab.ForgeSpeed) return;
        StrikeEdge(EdgeFor(-normal), impact >= lab.RedHotSpeed);
        LabAudio.PlayImpact(LastImpactTier);
    }

    static bool IsPhantom(Collider2D collider) =>
        collider != null && collider.GetComponentInParent<LabPhantom>() != null;

    /// <summary>绳子只负责反弹，不参与地形锻造；见 <see cref="ForgeFromTerrain"/>。</summary>
    static bool IsRope(Collider2D collider)
    {
        if (collider == null) return false;
        return collider.GetComponentInParent<LabVerticalRope>() != null
            || collider.GetComponentInParent<LabHorizontalRope>() != null;
    }

    /// <summary>
    /// 这块冰是否正踩在玩家脚下：水平方向与玩家 footprint 重叠，且冰面高度在脚底附近。
    /// 用来拦掉交界处的侧向接触，避免把玩家脚下的承重块融掉。
    /// </summary>
    bool IsIceUnderPlayer(LabIce ice)
    {
        if (col == null || ice == null) return false;
        var iceBody = ice.GetComponent<BoxCollider2D>();
        if (iceBody == null) return false;

        Bounds player = col.bounds;
        Bounds cell = iceBody.bounds;
        bool horizontal = player.max.x > cell.min.x + 0.01f && player.min.x < cell.max.x - 0.01f;
        bool atFeet = cell.max.y >= player.min.y - 0.12f && cell.max.y <= player.min.y + 0.12f;
        return horizontal && atFeet;
    }

    /// <summary>合并地形里，脚下 footprint 是否有一块是冰。独立冰块由 support 直接判定。</summary>
    bool IceUnderFeet()
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        float y = b.min.y - 0.02f;
        float quarter = b.size.x * 0.25f;
        return LabIce.AtCell(new Vector2(b.center.x, y)) != null
            || LabIce.AtCell(new Vector2(b.min.x + quarter, y)) != null
            || LabIce.AtCell(new Vector2(b.max.x - quarter, y)) != null;
    }

    void TryResolveHit(Collision2D c)
    {
        if (lab == null || other == null) return;
        if (c.collider == null || c.collider.GetComponent<LabPlayer>() == null) return;
        // ---- 大炮装填优先于动量互换，也不受对撞冷却限制 ----
        // 冷却是给"互换"防抖的；两人顶在一起时（B 站在 A 头上起冲）
        // 把装填一起挡掉，炮就永远吃不到人。
        LabPlayer cannon = canBeRedHot ? this : other;
        LabPlayer dart = canBeRedHot ? other : this;
        if (dart.Suspended) return;
        if (cannon.TryAbsorb(dart)) return;
        if (cannon.TryAbsorbConverter(dart)) return;

        if (Time.time < nextHitTime) return;

        Vector2 delta = (Vector2)other.transform.position - (Vector2)transform.position;
        if (delta.sqrMagnitude < 0.0001f) return;
        Vector2 axis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
            ? new Vector2(Mathf.Sign(delta.x), 0f)
            : new Vector2(0f, Mathf.Sign(delta.y));

        if (Mathf.Abs(axis.y) > 0.5f && !lab.verticalHitEnabled) return;

        // 只看沿碰撞轴的有符号分量：只有这一条轴上的东西才会有"同向/反向"
        // 必须读碰撞前速度：回调里 rb.linearVelocity 已经被解算改掉了（对撞时可能已经是 0）
        Vector2 vA = lastFixedVelocity;
        Vector2 vB = other.PreImpactVelocity;
        float a = Vector2.Dot(vA, axis);
        float b = Vector2.Dot(vB, axis);

        // ---- 锻造与红热：双方都严格低于第一道门槛就不判 ----
        float cellsA = lab.CellsForSpeed(Mathf.Abs(a));
        float cellsB = lab.CellsForSpeed(Mathf.Abs(b));
        bool anyAboveV0 = Mathf.Max(cellsA, cellsB) >= lab.Boundary(0) - 0.0001f;

        bool redHot = false;
        float Vp = 0f;
        if (anyAboveV0)
        {
            float a2 = a * a;
            float b2 = b * b;
            bool sameDir = a * b > 0f;
            Vp = sameDir ? Mathf.Abs(a2 - b2) : a2 + b2;

            float redHotV2 = lab.RedHotSpeed;
            redHotV2 *= redHotV2;
            float forgeV2 = lab.ForgeSpeed;
            forgeV2 *= forgeV2;

            redHot = Vp >= redHotV2;

            // 撞地形和双人撞击共用同一套门槛：够锻造、不够红热 = 冷撞击，
            // 只把热二框砸成灵体，不给常态边加热。
            // 对面静止时 b=0、Vp 就是自己那一记的 v²，所以 A 冲撞静止的 B 同样算一级碰撞。
            if (redHot || Vp >= forgeV2)
            {
                // 有"边"这个概念的只有 A（大框），B 那一路 StrikeEdge 自己会挡掉
                StrikeEdge(EdgeFor(axis), redHot);
                other.StrikeEdge(EdgeFor(-axis), redHot);
            }
        }

        // ---- 动量交换：速度 + 冲刺态整体互换 ----
        // 冲刺态带着"什么速度、还剩多少距离"一起交出去，接手的人从同一位置继续冲。
        DashState myDash = CaptureDash();
        DashState theirDash = other.CaptureDash();

        SetVelocity(vB);
        ApplyDash(theirDash);          // 对方在冲刺的话，速度会被冲刺速度接管

        other.SetVelocity(vA);
        other.ApplyDash(myDash);

        nextHitTime = Time.time + 0.18f;
        other.SetHitCooldown(Time.time + 0.18f);

        // 只有真的迎面撞上才算一次「撞击事件」（#82，2026-11-07）：
        // 两块紧贴静止时 Stay 回调每过 0.18s 冷却就会重跑一次互换，
        // 旧逻辑无条件外抛，订阅方的碰撞音效便跟着无限重播。
        // 接近速度 = 自己沿轴分量 − 对方沿轴分量（轴由自己指向对方）；
        // 有一方主动顶着（走路 8）或冲刺/坠落撞入时它远大于门槛，照常外抛，
        // 锻造/红热的判定在上面已经结算完，不受这道门影响。
        float closingSpeed = a - b;
        if (lab != null && closingSpeed > HitEventMinClosingSpeed)
        {
            lab.RaisePlayerHit(new TuningLab.HitInfo
            {
                hitter = Mathf.Abs(a) >= Mathf.Abs(b) ? this : other,
                victim = Mathf.Abs(a) >= Mathf.Abs(b) ? other : this,
                normal = axis,
                horizontal = Mathf.Abs(axis.x) > 0.5f,
                tierBefore = lab.TierOfSpeed(Mathf.Min(Mathf.Abs(a), Mathf.Abs(b))),
                tierAfter = lab.TierOfSpeed(Mathf.Max(Mathf.Abs(a), Mathf.Abs(b))),
                speed = Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)),
                vp = Vp,
                redHot = redHot
            });
        }
    }

    /// <summary>把自己沿 axis 的分量改成 value，垂直分量不动。</summary>
    public void ApplyAxisVelocity(Vector2 axis, float value)
    {
        var v = rb.linearVelocity;
        v += axis * (value - Vector2.Dot(v, axis));
        rb.linearVelocity = v;
    }

    public void SetVelocity(Vector2 v) => rb.linearVelocity = v;

    /// <summary>被场景物件弹开：结束冲刺态，速度设成给定方向 × 速度。</summary>
    public void ApplyBounce(Vector2 normal, float speed)
    {
        if (normal.sqrMagnitude < 0.0001f) normal = Vector2.up;
        normal.Normalize();

        dashPhase = DashPhase.None;
        dashTimer = 0f;
        dashLock = 0f;
        rb.gravityScale = lab != null ? lab.GravityScale : 5.1f;
        rb.linearVelocity = normal * speed;
    }

    /// <summary>冲刺态的可交换快照：方向、速度、剩余距离（= 剩余时长 × 速度）、结束速度。</summary>
    public struct DashState
    {
        public bool active;
        public Vector2 dir;
        public float speed;
        public float exitSpeed;
        public float timer;
        public float RemainingDistance => timer * speed;
    }

    public DashState CaptureDash()
    {
        return new DashState
        {
            active = dashPhase == DashPhase.Dash,
            dir = dashDir,
            speed = dashSpeedNow,
            exitSpeed = dashExitSpeed,
            timer = dashTimer
        };
    }

    public void ApplyDash(DashState s)
    {
        if (!s.active)
        {
            dashPhase = DashPhase.None;
            dashTimer = 0f;
            return;
        }

        dashPhase = DashPhase.Dash;
        dashDir = s.dir;
        dashSpeedNow = s.speed;
        dashExitSpeed = s.exitSpeed;
        dashTimer = s.timer;
        rb.gravityScale = 0f;
        rb.linearVelocity = dashDir * dashSpeedNow;
    }

    /// <summary>上一个 FixedUpdate 写进刚体的速度，也就是碰撞前的速度。</summary>
    public Vector2 PreImpactVelocity => lastFixedVelocity;

    // ---- 红热态 ----

    public enum Edge { Top = 0, Bottom = 1, Left = 2, Right = 3 }
    public enum EdgeState { Normal = 0, Hot2 = 1, Spirit = 2 }

    readonly EdgeState[] edgeStates = new EdgeState[4];

    public EdgeState GetEdge(Edge e) => edgeStates[(int)e];
    public bool IsSpirit(Edge e) => edgeStates[(int)e] == EdgeState.Spirit;
    public bool AnyHot => edgeStates[0] != EdgeState.Normal || edgeStates[1] != EdgeState.Normal
                       || edgeStates[2] != EdgeState.Normal || edgeStates[3] != EdgeState.Normal;

    /// <summary>四条边的状态快照；A 的形态（u/L/=/‖/矛/灵体）和红热/灵体边都由它决定。</summary>
    public struct FormState
    {
        public EdgeState left;
        public EdgeState bottom;
        public EdgeState right;
        public EdgeState top;

        public static FormState Normal => new FormState
        {
            left = EdgeState.Normal,
            bottom = EdgeState.Normal,
            right = EdgeState.Normal,
            top = EdgeState.Normal
        };
    }

    public FormState CaptureForm() => new FormState
    {
        left = edgeStates[(int)Edge.Left],
        bottom = edgeStates[(int)Edge.Bottom],
        right = edgeStates[(int)Edge.Right],
        top = edgeStates[(int)Edge.Top]
    };

    /// <summary>把四条边恢复成快照；灵体遮罩、形态和贴图都由 SetEdge 同步。</summary>
    public void ApplyForm(FormState state)
    {
        SetEdge(Edge.Top, state.top);
        SetEdge(Edge.Bottom, state.bottom);
        SetEdge(Edge.Left, state.left);
        SetEdge(Edge.Right, state.right);
    }

    /// <summary>红热态变化的外抛接口。形态由"哪几条边成了灵体"决定，挂这里。</summary>
    public event System.Action<LabPlayer, Edge, EdgeState, EdgeState> EdgeStateChanged;

    /// <summary>
    /// 热二的边再吃一记红热级撞击：框塌到本体。实验台还没有生命系统，
    /// 这里只将事件抛出，谁接入谁决定后果。
    /// </summary>
    public event System.Action<LabPlayer, Edge> EdgeCollapsed;

    /// <summary>形态变化（碎边组合变了）。膛内的退出逻辑挂这里。</summary>
    public event System.Action<LabPlayer, LabForm> FormChanged;

    /// <summary>冲刺充能从不满补到满；初始满充、传送/复活复位不触发。</summary>
    public event System.Action<LabPlayer> ChargeCompleted;

    public void SetEdge(Edge e, EdgeState next)
    {
        int i = (int)e;
        var prev = edgeStates[i];
        if (prev == next) return;
        edgeStates[i] = next;

        if (next == EdgeState.Spirit)
        {
            spiritMask |= 1 << i;
            // 掉成灵体 = 这一格的框脱落，掉落音在这里响（碰撞和调试砸边都走这条）。
            LabAudio.Play(LabAudio.Cue.CrateFall);
        }
        else spiritMask &= ~(1 << i);

        if (visual != null) visual.SetEdge(e, next);
        if (EdgeStateChanged != null) EdgeStateChanged(this, e, prev, next);
        UpdateForm();
    }

    /// <summary>碎边组合一变，形态跟着变；只有仍是 L 形的转向装填才能继续保留。</summary>
    void UpdateForm()
    {
        LabForm next = LabForms.Of(spiritMask);
        if (next == form) return;
        bool wasSpirit = form == LabForm.Spirit;
        form = next;
        if (form == LabForm.Spirit)
        {
            if (!wasSpirit) EnterSpirit();
        }
        else if (wasSpirit) ExitSpirit();
        if (loaded != null && (!loadedConverter || form != LabForm.Converter)) Eject();
        if (FormChanged != null) FormChanged(this, form);
    }

    /// <summary>
    /// 一记撞击落在某条边上。hot = 到了红热门槛（挡位 2 及以上），否则只到了锻造门槛。
    /// 常态遇红热 → 直接热二（没有限时的热一）；热二遇冷→灵体（砸碎）；
    /// 热二遇热→框塌到本体；灵体不可复锻。
    /// </summary>
    public void StrikeEdge(Edge e, bool hot)
    {
        if (!canBeRedHot) return;            // 没有"边"这个概念的只有不参与

        switch (edgeStates[(int)e])
        {
            case EdgeState.Normal:
                if (hot) SetEdge(e, EdgeState.Hot2);        // 红热级撞击直接点满
                break;
            case EdgeState.Hot2:
                if (hot)
                {
                    Eject();                       // 膛里的人不能跟着宿主一起没
                    EdgeCollapsed?.Invoke(this, e);
                    Kill();                        // 检查点/出生点/销毁走同一个死亡入口
                }
                else
                {
                    // Hot2 再遭遇冷碰撞，对应的框掉落并变为 Spirit。
                    SetEdge(e, EdgeState.Spirit);
                }
                break;
        }
    }

    // ------------------------------------------------------------------
    // 灵体态（Issue #64 / ADR-0007）：限时的穿透态，只认钥匙，时间耗尽即死
    // ------------------------------------------------------------------

    /// <summary>
    /// 进入灵体：把碰撞箱改成触发器。这一步一次关掉所有交互 —— 碰撞回调不再触发
    /// （锻造、岩浆、冰、双人动量、大炮装填全停），触发器对触发器也不发回调（地刺、门都不算）。
    /// 代价是钥匙也一起没了回调，所以唯一保留的交互要在 FixedUpdate 里自己扫。
    /// </summary>
    void EnterSpirit()
    {
        if (col != null) col.isTrigger = true;
        spiritTimer = lab != null ? lab.spiritDuration : 6f;
    }

    /// <summary>离开灵体（复活、传送、形态被改写）：交还实心碰撞箱。</summary>
    void ExitSpirit()
    {
        if (col != null) col.isTrigger = false;
        spiritTimer = 0f;
    }

    /// <summary>灵体的存在时间：从进入那一刻开始计，中途不暂停；耗尽就是死亡。</summary>
    void TickSpirit(float dt)
    {
        spiritTimer -= dt;
        if (spiritTimer > 0f) return;
        DropTempKey();
        Kill();
    }

    /// <summary>
    /// 消散时把身上那把钥匙吐回世界：只在持有钥匙时生成，且方块中心所在格子没被实体占住。
    /// 另一名玩家（B）占用不算，各类触发器物件也不算。位置取格心，和关卡里的钥匙对齐。
    /// </summary>
    void DropTempKey()
    {
        if (KeyCount <= 0) return;
        ConsumeKey();

        Vector2 here = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 cellCenter = new Vector2(
            Mathf.FloorToInt(here.x) + 0.5f,
            Mathf.FloorToInt(here.y) + 0.5f);
        if (!IsCellFreeOfSolids(cellCenter)) return;
        LabKey.SpawnTemporary(cellCenter);
    }

    /// <summary>这个格子中心是不是被实体挡住：只认实心碰撞体，玩家和触发器都让开。</summary>
    bool IsCellFreeOfSolids(Vector2 point)
    {
        int count = Physics2D.OverlapPoint(point, ContactFilter2D.noFilter, overlapHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = overlapHits[i];
            if (hit == null || hit == col || hit.isTrigger) continue;
            if (hit.GetComponentInParent<LabPlayer>() != null) continue;
            return false;
        }
        return true;
    }

    /// <summary>灵体捡钥匙：碰撞箱是触发器，钥匙自己的 OnTriggerEnter 不会响，只能按包围盒扫。</summary>
    void SweepKeys()
    {
        if (col == null || KeyCount >= MaxHeldKeys) return;
        Bounds b = col.bounds;
        int count = Physics2D.OverlapBox(b.center, b.size, 0f, ContactFilter2D.noFilter, overlapHits);
        for (int i = 0; i < count; i++)
        {
            LabKey key = overlapHits[i] != null ? overlapHits[i].GetComponent<LabKey>() : null;
            if (key != null && key.TryCollect(this)) return;
        }
    }

    /// <summary>
    /// 修复一条边：红热（热二）或灵体 → 普通。修复方块不消耗，所以随时再来。
    ///
    /// 两类 A 按 issue #63 排除：
    ///   装填态 —— 修好炮口等于把膛里的人吐出来并顺手拆掉他的炮；
    ///   幽魂态 —— 四条边全碎，修任意一条就整个换形态，等于白送一次重构。
    /// 灵体边回普通会重算碎边掩码，形态跟着变；这是设计里"修复"的直接后果，不是副作用。
    /// </summary>
    public bool TryRepairEdge(Edge e)
    {
        if (!canBeRedHot || IsDead) return false;       // B 没有"边"这个概念
        if (Loaded || form == LabForm.Spirit) return false;
        if (edgeStates[(int)e] == EdgeState.Normal) return false;

        SetEdge(e, EdgeState.Normal);
        return true;
    }

    /// <summary>
    /// 即死：踩到地刺，或热二的边再吃一记红热。
    /// 膛里的人先退膛，不能跟着宿主一起没；自己进了别人的膛也先出来。
    /// 死亡后先原地隐藏冻结，由 <see cref="TuningLab.NotifyPlayerDied"/> 决定
    /// 是等队友、回重生点、重开本关还是销毁。
    /// </summary>
    public void Kill()
    {
        if (dying) return;
        dying = true;
        IsDead = true;

        Eject();
        if (host != null) host.Eject();
        if (phantomHost != null) phantomHost.EjectLoaded(this);
        if (lab == null) { Destroy(gameObject); return; }

        SuspendDead();
        lab.NotifyPlayerDied(this);
    }

    /// <summary>死亡等待：不可视、无碰撞、运动学停在原地，输入和物理逻辑都早退。</summary>
    void SuspendDead()
    {
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }
        if (visual != null) visual.SetVisible(false);

        teleportFrozen = false;
        grounded = false;
        support = null;
        playerContactTimer = 0f;
        dashPhase = DashPhase.None;
        dashTimer = 0f;
        dashLock = 0f;
        dashQueued = false;
        jumpBuffer = 0f;
        gliding = false;
        refillArmed = false;
        ResetMagmaContacts();
    }

    /// <summary>复活前把碰撞箱和动力学交还给玩家；位置由 <see cref="ReviveAt"/> 的 Teleport 决定。</summary>
    void WakeFromDeath()
    {
        if (col != null) col.enabled = true;
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = lab != null ? lab.GravityScale : 5.1f;
        }
        if (visual != null) visual.SetVisible(true);
    }

    /// <summary>四条边全部回到常态（复活时用）。</summary>
    public void ResetEdges()
    {
        for (int i = 0; i < 4; i++) SetEdge((Edge)i, EdgeState.Normal);
        ResetMagmaContacts();
    }

    /// <summary>位置/状态切换后，岩浆接触重新按“第一次接触”算。</summary>
    void ResetMagmaContacts()
    {
        magmaTouchPrev = 0;
        magmaTouchNow = 0;
    }

    public static Edge EdgeFor(Vector2 axis)
    {
        if (Mathf.Abs(axis.x) > 0.5f) return axis.x > 0f ? Edge.Right : Edge.Left;
        return axis.y > 0f ? Edge.Top : Edge.Bottom;
    }

    public void SetHitCooldown(float until) => nextHitTime = until;

    /// <summary>持钥上限：一名玩家身上同时只能带一把，带着再捡无效，钥匙留在原处。</summary>
    public const int MaxHeldKeys = 1;

    public bool TryCollectKey(LabKey key)
    {
        if (key == null || !CanCollectKey) return false;
        if (KeyCount >= MaxHeldKeys) return false;
        KeyCount++;
        return true;
    }

    public bool ConsumeKey()
    {
        if (KeyCount <= 0) return false;
        KeyCount--;
        return true;
    }

    public void ClearKeys() => KeyCount = 0;

    // ------------------------------------------------------------------
    // 大炮（Issue #15）：冲刺态进入 → 存速 → 反向射出
    // ------------------------------------------------------------------

    /// <summary>
    /// 膛空的大炮只吃"从炮口撞进来的冲刺态 B"。撞的是哪条边按两人中心差的主分量判，
    /// 和加热、动量互换用的是同一个读数：那条边已经是灵体（= 炮口）才装填，
    /// 撞在还没碎的边上就退回常规对撞结算 —— 四个朝向到这里才第一次有区别。
    /// 装填成功返回 true，这次碰撞不再走进挡位互换。
    /// </summary>
    bool TryAbsorb(LabPlayer b)
    {
        if (!CanLoad || b == null || b.host != null) return false;
        if (!b.InDashState) return false;
        // 接触炮口本身不足以装填：冲刺必须朝炮膛内部，否则站在炮口上
        // 横向或反向冲刺时，持续接触回调也会把 B 错误吸入。
        if (Vector2.Dot(b.DashDirection, -MouthNormal) <= 0.5f) return false;

        Vector2 to = (Vector2)b.transform.position - (Vector2)transform.position;
        Edge struck = EdgeFor(Mathf.Abs(to.x) >= Mathf.Abs(to.y)
            ? new Vector2(Mathf.Sign(to.x), 0f)
            : new Vector2(0f, Mathf.Sign(to.y)));
        if (!IsSpirit(struck)) return false;

        loaded = b;
        b.host = this;
        b.chamberVelocity = b.PreImpactVelocity;     // 进入时的 v前
        b.chamberDir = b.dashDir;                    // 冲刺轴：射出方向取它的反向
        b.SuspendIn();

        nextHitTime = Time.time + 0.18f;
        b.SetHitCooldown(Time.time + 0.18f);
        LabAudio.Play(LabAudio.Cue.CannonLoad);
        return true;
    }

    bool TryAbsorbConverter(LabPlayer b)
    {
        if (!canBeRedHot || form != LabForm.Converter || loaded != null || b == null || b.host != null)
            return false;

        float speed = b.PreImpactVelocity.magnitude;
        if (lab == null || speed <= lab.V0Speed + 0.0001f)
            return false;

        Vector2 to = (Vector2)b.transform.position - (Vector2)transform.position;
        Edge entry = EdgeFor(Mathf.Abs(to.x) >= Mathf.Abs(to.y)
            ? new Vector2(Mathf.Sign(to.x), 0f)
            : new Vector2(0f, Mathf.Sign(to.y)));
        if (!IsSpirit(entry)) return false;

        Edge exit = entry;
        for (int i = 0; i < 4; i++)
        {
            Edge candidate = (Edge)i;
            if (candidate != entry && IsSpirit(candidate))
            {
                exit = candidate;
                break;
            }
        }
        if (exit == entry) return false;

        loaded = b;
        loadedConverter = true;
        converterExitEdge = exit;
        converterLoadTimer = 1f;
        loadedDash = b.CaptureDash();
        b.chamberVelocity = b.PreImpactVelocity;
        b.chamberDir = b.DashDirection;
        b.SuspendIn();

        nextHitTime = Time.time + 0.18f;
        b.SetHitCooldown(Time.time + 0.18f);
        return true;
    }

    /// <summary>
    /// 开炮：v' = √(Vb² + 一个等级间距的 v²)，不看 A 自己的速度；
    /// 方向取进入方向的反向 —— 从它进来的那个口退出去。
    /// </summary>
    public void Fire()
    {
        var b = loaded;
        if (b == null || lab == null || loadedConverter) return;

        loaded = null;
        loadedConverter = false;
        float speed = Mathf.Sqrt(Mathf.Max(0f, b.chamberVelocity.sqrMagnitude + lab.CannonBoostV2));
        Vector2 dir = -b.chamberDir;
        b.Wake(rb.position + dir * (HalfHeight + b.HalfHeight + 0.05f), dir * speed);
        b.gliding = Mathf.Abs(dir.x) > 0.5f;         // 只有水平射出才免重力
        LabAudio.Play(LabAudio.Cue.CannonFire);
    }

    void FireConverter()
    {
        var b = loaded;
        if (b == null) return;

        loaded = null;
        loadedConverter = false;
        Vector2 dir = LabForms.EdgeNormal(converterExitEdge);
        float speed = b.chamberVelocity.magnitude;
        b.Wake(rb.position + dir * (HalfHeight + b.HalfHeight + 0.05f), dir * speed);
        if (loadedDash.active)
        {
            loadedDash.dir = dir;
            b.ApplyDash(loadedDash);
            b.gliding = Mathf.Abs(dir.x) > 0.5f;
        }
        else
        {
            b.gliding = Mathf.Abs(dir.x) > 0.5f;
        }
        loadedDash = default(DashState);
    }

    /// <summary>A 结构改变（u→L、u→=）或死亡：没开出去的人按进入时的速度退回膛外。</summary>
    void Eject()
    {
        var b = loaded;
        if (b == null) return;
        loaded = null;
        loadedConverter = false;
        converterLoadTimer = 0f;
        loadedDash = default(DashState);

        Vector2 dir = b.chamberDir.sqrMagnitude > 0.0001f ? -b.chamberDir : Vector2.up;
        b.Wake(rb.position + dir * (HalfHeight + b.HalfHeight + 0.05f), b.chamberVelocity);
    }

    /// <summary>被吸进幻影的膛：和真人炮膛共用同一套隐藏/冻结逻辑，只是宿主换成幻影。</summary>
    internal void SuspendInPhantom(LabPhantom phantom)
    {
        phantomHost = phantom;
        SuspendIn();
    }

    /// <summary>从幻影膛里出来：恢复碰撞和动力学，由幻影决定落点和速度。</summary>
    internal void WakeFromHost(Vector2 position, Vector2 velocity, bool horizontalGlide)
    {
        Wake(position, velocity);
        gliding = horizontalGlide;
    }

    /// <summary>进膛：不可视、无碰撞箱、运动学跟随宿主，各种逻辑在 Update/FixedUpdate 里早退。</summary>
    void SuspendIn()
    {
        if (visual != null) visual.SetVisible(false);
        col.enabled = false;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        grounded = false;
        support = null;
        playerContactTimer = 0f;
        dashPhase = DashPhase.None;
        dashTimer = 0f;
        dashLock = 0f;
        gliding = false;
        ResetMagmaContacts();
    }

    /// <summary>出膛：碰撞箱和动力学交还给 B，落到炮口外侧，免得和 A 的整块碰撞箱重叠被弹开。</summary>
    void Wake(Vector2 position, Vector2 velocity)
    {
        host = null;
        phantomHost = null;
        chamberVelocity = Vector2.zero;
        chamberDir = Vector2.zero;

        if (visual != null) visual.SetVisible(true);
        col.enabled = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.position = position;
        transform.position = position;
        rb.gravityScale = lab != null ? lab.GravityScale : 5.1f;
        rb.linearVelocity = velocity;

        grounded = false;
        refillArmed = false;
        ResetMagmaContacts();
    }

    /// <summary>调试：把下一条边直接砸成灵体，用来演示碎边组合 → 形态（F1）。</summary>
    public void DebugShatterNextEdge()
    {
        if (!canBeRedHot) return;
        for (int i = 0; i < 4; i++)
        {
            if (edgeStates[i] == EdgeState.Spirit) continue;
            SetEdge((Edge)i, EdgeState.Spirit);
            return;
        }
    }

    public void Teleport(Vector2 position)
    {
        if (host != null) host.Eject();      // 我在别人膛里：先把我放出去
        if (phantomHost != null) phantomHost.EjectLoaded(this);

        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        transform.position = position;
        Charge = 1f;
        dashLock = 0f;
        grounded = false;
        playerContactTimer = 0f;
        dashTimer = 0f;
        dashPhase = DashPhase.None;
        refillArmed = false;
        gliding = false;
        ResetEdges();
        LandedNetDescent = 0f;
        LandedApexDescent = 0f;
        LaunchFootY = FootY;
        ApexFootY = FootY;
        TeleportCount++;
    }

    public void ReviveAt(Vector2 position)
    {
        if (IsDead) WakeFromDeath();
        IsDead = false;
        dying = false;
        Teleport(position);
    }

    public void TeleportPreservingState(Vector2 position, Vector2 velocity)
    {
        var savedEdges = new EdgeState[4];
        for (int i = 0; i < savedEdges.Length; i++) savedEdges[i] = edgeStates[i];

        Teleport(position);
        for (int i = 0; i < savedEdges.Length; i++) SetEdge((Edge)i, savedEdges[i]);
        rb.linearVelocity = velocity;
    }

    public void FreezeForTeleportSelection()
    {
        if (teleportFrozen) return;
        teleportPreviousBodyType = rb.bodyType;
        teleportPreviousGravityScale = rb.gravityScale;
        teleportFrozen = true;
        dashQueued = false;
        dashPhase = DashPhase.None;
        dashTimer = 0f;
        gliding = false;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
    }

    public void ResumeFromTeleportSelection()
    {
        if (!teleportFrozen) return;
        rb.bodyType = teleportPreviousBodyType;
        rb.gravityScale = teleportPreviousGravityScale;
        teleportFrozen = false;
    }

    // ------------------------------------------------------------------

    bool ProbeGround()
    {
        support = null;
        if (rb.linearVelocity.y > 0.1f) return false;   // 上升途中不要粘地，否则起跳当帧就会被判回地面
        Bounds b = col.bounds;
        var origin = new Vector2(b.center.x, b.min.y + 0.01f);
        var size = new Vector2(b.size.x * 0.9f, 0.02f);
        int count = Physics2D.BoxCast(origin, size, 0f, Vector2.down, ContactFilter2D.noFilter, groundHits, 0.06f);
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i].collider;
            if (hit == null || hit == col || hit.isTrigger) continue;
            support = hit;
            return true;
        }
        return false;
    }

    /// <summary>脚下踩的这块是不是那名玩家。</summary>
    bool IsRidingOn(LabPlayer p)
    {
        if (p == null || support == null) return false;
        return support.GetComponentInParent<LabPlayer>() == p;
    }

    /// <summary>正站在队友头上。</summary>
    public bool RidingOnOther => grounded && IsRidingOn(other);

    void ReadInput()
    {
        moveX = 0f;
        vertInput = 0f;
        jumpDownThisFrame = false;
        jumpHeld = false;
        dashDownThisFrame = false;
        recallDownThisFrame = false;

        var kb = Keyboard.current;
        if (kb == null) return;

        bool left, right, up, down;
        if (scheme == Scheme.P1)
        {
            // A（大框）：A/D 移动，W/S 冲刺方向，空格 跳，左 Shift 冲刺
            left = kb.aKey.isPressed;
            right = kb.dKey.isPressed;
            up = kb.wKey.isPressed;
            down = kb.sKey.isPressed;
            jumpHeld = kb.spaceKey.isPressed;
            jumpDownThisFrame = kb.spaceKey.wasPressedThisFrame;
            dashDownThisFrame = kb.leftShiftKey.wasPressedThisFrame;
            recallDownThisFrame = kb.qKey.wasPressedThisFrame;
        }
        else
        {
            // B（小块）：方向键控制上下左右，/ 跳，句号 . 冲刺
            up = kb.upArrowKey.isPressed;
            down = kb.downArrowKey.isPressed;
            left = kb.leftArrowKey.isPressed;
            right = kb.rightArrowKey.isPressed;
            jumpHeld = kb.slashKey.isPressed;
            jumpDownThisFrame = kb.slashKey.wasPressedThisFrame;
            dashDownThisFrame = kb.periodKey.wasPressedThisFrame;
            recallDownThisFrame = kb.rightShiftKey.wasPressedThisFrame;
        }

        moveX = (right ? 1f : 0f) - (left ? 1f : 0f);
        vertInput = (up ? 1f : 0f) - (down ? 1f : 0f);
    }

    // ---- 视觉分级：颜色就是等级 ----

    public static Color TintForTier(int tier)
    {
        switch (tier)
        {
            case 1: return new Color(1f, 0.86f, 0.2f);    // v1 黄
            case 2: return new Color(1f, 0.55f, 0.15f);   // v2 橙
            case 3: return new Color(0.3f, 0.8f, 1f);     // v3 蓝
            case 4: return Color.white;                   // v4 白
            default: return new Color(0.36f, 0.62f, 1f);  // v0
        }
    }
}
