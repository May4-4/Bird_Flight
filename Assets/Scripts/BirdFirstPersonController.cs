using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Oiseau à la première personne : marche sur ses pattes, décollage (accroupi puis saut),
// vol battu ou plané avec un modèle aérodynamique simple, et atterrissage naturel.
//
// Commandes (Input Actions du projet, map "Player") :
//   au sol : Move = marcher, Look = regarder, Sprint = sautiller,
//            Jump maintenu = s'accroupir, Jump relâché = sauter et décoller
//   en vol : Jump = battre des ailes, Move / Look = piquer, cabrer, s'incliner, Sprint = replier les ailes
//   pour se poser : arrêter de battre et planer vers une surface, l'arrondi est automatique
//   Échap libère la souris, un clic la capture à nouveau.
// Rigidbody (réglé dans l'Inspector) : gravité, rotation bloquée (pilotée par le script),
// Linear Damping 0 (traînée calculée par l'aérodynamique), Interpolate, Continuous Dynamic.
[RequireComponent(typeof(Rigidbody))]
public class BirdFirstPersonController : MonoBehaviour
{
    public enum BirdState { Walking, Crouching, Takeoff, Flying, Landing }

    // Actions du projet (InputSystem_Actions, map Player) : Move, Look, Jump, Sprint
    public InputActionReference moveAct, lookAct, flapAct, tuckAct;

    // Pattes : ressort amorti entre la hanche et le sol
    public float legLength = 0.75f;
    public float crouchLegLength = 0.42f;
    public float legStiffness = 900f;
    public float legDamping = 90f;

    // Marche et regard
    public float walkSpeed = 1.3f;
    public float hopSpeed = 2.8f;
    public float mouseSensitivity = 0.12f;

    // Décollage : le saut des pattes donne l'essentiel de la vitesse initiale
    public float minLegLaunchSpeed = 2.2f;
    public float maxLegLaunchSpeed = 4.5f;
    public float launchAngle = 40f;
    public float takeoffDuration = 0.9f;

    // Ailes et battements
    public float wingArea = 0.65f;
    public float wingSpan = 2.8f;
    public float stallAngle = 16f;
    public float flapFrequency = 3.2f;
    public float flapPower = 110f;
    public float staminaDrainRate = 0.012f;

    // Atterrissage
    public float flareHeight = 3.5f;
    public float maxLandingSpeed = 7f;

    // Infos de debug dans la console
    public bool debugLog = true;

    // Réglages fins (constantes)
    public const float DownstrokeRatio = 0.55f;      // part du battement consacrée à l'abaissement
    const float LegSpeed = 1.5f;                     // vitesse de flexion des pattes (m/s)
    const float ProbeRadius = 0.25f, ProbeLift = 0.2f, MaxGroundSlope = 50f;
    const float GroundAcceleration = 10f, FootFriction = 0.9f;
    const float GamepadLookSpeed = 150f, HeadPitchLimit = 70f;
    const float CrouchTime = 0.35f, TakeoffPitch = 20f, TakeoffFlapBoost = 1.35f, TakeoffFlapFrequency = 4.2f;
    const float AirDensity = 1.225f, LiftSlope = 5f, MaxLift = 1.5f, ZeroLiftDrag = 0.03f, Oswald = 0.8f;
    const float BodyDragArea = 0.04f, WingIncidence = 4f, TuckedArea = 0.35f, GroundEffect = 0.25f;
    const float ThrustMaxSpeed = 22f, UpstrokePenalty = 0.1f, HoverStrokeAngle = 20f, CruiseStrokeAngle = 60f, CruiseSpeed = 12f;
    const float ExhaustedPower = 0.65f, RegenGliding = 0.04f, RegenPerched = 0.25f, TakeoffStaminaCost = 0.05f;
    const float KeyboardPitchRate = 70f, KeyboardRollRate = 120f, MouseRollGain = 1.5f, MaxPitch = 60f, MaxRoll = 70f;
    const float RollAutoLevel = 45f, WeathercockRate = 180f, LowSpeedTurnRate = 60f;
    const float TrimAngleOfAttack = 6f, PitchStabilityRate = 15f, TrimPitchMin = -40f, TrimPitchMax = 30f, StallMargin = 2f;
    const float FlareTimeToContact = 0.9f, FlarePitch = 55f, FlarePitchRate = 120f, LandingLegReach = 0.95f;
    const float ApproachSpeed = 8f, BalloonWingFlex = 0.45f, BalloonPitchCorrection = 12f;
    const float LandingTau = 0.6f, MinTouchdownSink = 0.7f, MaxSinkRate = 2f;
    const float BrakingMinPitch = 10f, BrakingStrokeSpeed = 2.5f, BrakingStrokeAngle = 5f, LandingStrokePower = 0.7f;
    const float LegImpactAbsorption = 0.7f, HardLandingKeep = 0.5f, StumbleTime = 0.6f, HardLandingStaminaCost = 0.1f;

    // État lu par les autres scripts (animation, recherche de vers)
    public BirdState State { get; private set; } = BirdState.Walking;
    public bool IsGrounded { get; private set; }
    public bool IsAirborne => State == BirdState.Takeoff || State == BirdState.Flying || State == BirdState.Landing;
    public bool IsFlapping => flapActive;
    public bool WingsTucked { get; private set; }
    public float FlapPhase => flapPhase;
    public float Crouch01 { get; private set; }
    public float Flare01 { get; private set; }
    public float TimeSinceLanding { get; private set; } = 999f;
    public float Pitch => pitch;
    public float Roll => roll;
    public float HeadPitch => headPitch;
    public float Airspeed => rb.linearVelocity.magnitude;
    public float HorizontalSpeed => new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
    public event Action<float> Landed;               // vitesse d'impact (m/s)

    Rigidbody rb;
    Vector2 moveInput, lookInput;
    bool jumpHeld, jumpPressed, jumpReleased, tuckHeld, mouseRolling, mousePitching;
    float yaw, pitch, roll, headPitch;
    float legCurrent, crouchCharge, flapPhase, stamina = 1f, angleOfAttack;
    bool flapActive, stalled, walkableContact;
    float takeoffTimer, groundIgnoreTimer, coyoteTimer, stumbleTimer, landingGraceTimer, debugTimer;
    float groundDistance = float.PositiveInfinity;
    Vector3 groundNormal = Vector3.up, landingStrokeDir = Vector3.up;
    readonly RaycastHit[] probeHits = new RaycastHit[16];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        yaw = transform.eulerAngles.y;
        legCurrent = legLength;
    }

    void Start()
    {
        SetCursorLocked(true);
    }

    // ================================================================ ENTRÉES (Update)
    void Update()
    {
        ReadInput();
        ApplyLook(Time.deltaTime);
        if (debugLog) LogStatus();
    }

    void ReadInput()
    {
        bool jump = flapAct.action.IsPressed();
        if (jumpHeld && !jump) jumpReleased = true;     // mémorisé jusqu'au prochain FixedUpdate
        if (!jumpHeld && jump) jumpPressed = true;
        jumpHeld = jump;
        tuckHeld = tuckAct.action.IsPressed();           // même touche : sautiller au sol, replier les ailes en vol
        moveInput = Vector2.ClampMagnitude(moveAct.action.ReadValue<Vector2>(), 1f);

        // Regard : un stick donne une vitesse, la souris un déplacement en pixels
        Vector2 l = lookAct.action.ReadValue<Vector2>();
        var device = lookAct.action.activeControl?.device;
        if (device is Gamepad || device is Joystick) lookInput = l * GamepadLookSpeed * Time.deltaTime;
        else lookInput = Cursor.lockState == CursorLockMode.Locked ? l * mouseSensitivity : Vector2.zero;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetCursorLocked(false);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) SetCursorLocked(true);
    }

    void ApplyLook(float dt)
    {
        // Au sol : la souris tourne le corps et incline la tête
        if (!IsAirborne)
        {
            yaw += lookInput.x;
            headPitch = Mathf.Clamp(headPitch + lookInput.y, -HeadPitchLimit, HeadPitchLimit);
            return;
        }

        // En vol : la tête revient dans l'axe et la souris pilote le corps
        headPitch = Mathf.MoveTowards(headPitch, 0f, 90f * dt);
        if (State != BirdState.Flying) return;        // décollage et atterrissage sont automatiques

        float up = lookInput.y;
        if (up > 0f && angleOfAttack > stallAngle - StallMargin) up = 0f;   // protection anti-décrochage
        pitch = Mathf.Clamp(pitch + up, -MaxPitch, MaxPitch);
        roll = Mathf.Clamp(roll + lookInput.x * MouseRollGain, -MaxRoll, MaxRoll);
        mouseRolling = Mathf.Abs(lookInput.x) > 0.01f;
        mousePitching |= Mathf.Abs(lookInput.y) > 0.01f;
    }

    // ================================================================ PHYSIQUE (FixedUpdate)
    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        groundIgnoreTimer -= dt;
        ProbeGround();

        if (IsAirborne) AirStep(dt);
        else GroundStep(dt);

        rb.MoveRotation(Quaternion.Euler(-pitch, yaw, -roll));
        jumpPressed = jumpReleased = false;
        walkableContact = false;                       // rempli à nouveau par les collisions de ce pas
    }

    // Le contact réel du corps avec un sol praticable compte aussi comme posé :
    // l'oiseau ne peut jamais glisser au sol en restant "en vol".
    void OnCollisionStay(Collision c)
    {
        if (!IsAirborne || groundIgnoreTimer > 0f) return;
        for (int i = 0; i < c.contactCount; i++)
            if (c.GetContact(i).normal.y >= Mathf.Cos(MaxGroundSlope * Mathf.Deg2Rad)) { walkableContact = true; return; }
    }

    void OnCollisionEnter(Collision c) => OnCollisionStay(c);

    // ---------------------------------------------------------------- sol
    // Sphère lancée vers le bas depuis la hanche : distance et pente du sol.
    void ProbeGround()
    {
        Vector3 origin = transform.position + Vector3.up * ProbeLift;
        float maxDist = ProbeLift + Mathf.Max(wingSpan, flareHeight * 1.5f) + 0.2f;
        int n = Physics.SphereCastNonAlloc(origin, ProbeRadius, Vector3.down, probeHits, maxDist, ~0, QueryTriggerInteraction.Ignore);

        float best = float.PositiveInfinity;
        Vector3 normal = Vector3.up;
        for (int i = 0; i < n; i++)
        {
            var h = probeHits[i];
            if (h.distance <= 0f || h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best) { best = h.distance; normal = h.normal; }
        }

        groundDistance = float.IsPositiveInfinity(best) ? best : best + ProbeRadius - ProbeLift;
        groundNormal = normal;
        bool walkable = normal.y >= Mathf.Cos(MaxGroundSlope * Mathf.Deg2Rad);
        IsGrounded = groundIgnoreTimer <= 0f && walkable && groundDistance <= legCurrent + 0.15f;
    }

    void GroundStep(float dt)
    {
        TimeSinceLanding += dt;
        stumbleTimer -= dt;
        landingGraceTimer -= dt;

        // Plus de sol sous les pattes (bord de la branche) : l'oiseau tombe, ailes ouvertes
        if (!IsGrounded && landingGraceTimer <= 0f)
        {
            coyoteTimer += dt;
            if (coyoteTimer > 0.12f) { SetState(BirdState.Flying); pitch = -10f; legCurrent = legLength; Crouch01 = 0f; return; }
        }
        else coyoteTimer = 0f;

        // Jump maintenu = s'accroupir, relâché = décoller
        if (State == BirdState.Walking && jumpPressed && IsGrounded && stumbleTimer <= 0f)
        {
            SetState(BirdState.Crouching);
            crouchCharge = 0f;
        }
        if (State == BirdState.Crouching)
        {
            crouchCharge = Mathf.Min(1f, crouchCharge + dt / CrouchTime);
            if (jumpReleased || !jumpHeld) { Launch(); return; }
        }

        float targetLeg = State == BirdState.Crouching
            ? Mathf.Lerp(legLength, crouchLegLength, 1f - (1f - crouchCharge) * (1f - crouchCharge))
            : legLength;
        legCurrent = Mathf.MoveTowards(legCurrent, targetLeg, LegSpeed * dt);
        Crouch01 = Mathf.InverseLerp(legLength, crouchLegLength, legCurrent);

        // Ressort des pattes : poids + raideur x compression - amortissement x vitesse verticale
        if (IsGrounded)
        {
            float force = rb.mass * -Physics.gravity.y + legStiffness * (legCurrent - groundDistance) - legDamping * rb.linearVelocity.y;
            if (force > 0f) rb.AddForce(Vector3.up * force);
        }

        // Marche : on vise une vitesse, limitée par l'adhérence des pattes (mu x g)
        float speed = tuckHeld ? hopSpeed : walkSpeed;
        if (State == BirdState.Crouching) speed *= 0.25f;
        if (stumbleTimer > 0f) speed = 0f;            // reprend son équilibre après un atterrissage brutal
        Vector3 target = Quaternion.Euler(0f, yaw, 0f) * new Vector3(moveInput.x, 0f, moveInput.y) * speed;
        Vector3 v = rb.linearVelocity;
        float accel = Mathf.Min(GroundAcceleration, FootFriction * -Physics.gravity.y) * (IsGrounded ? 1f : 0.2f);
        Vector3 dv = Vector3.ClampMagnitude(target - new Vector3(v.x, 0f, v.z), accel * dt);
        if (IsGrounded) dv = Vector3.ProjectOnPlane(dv, groundNormal);
        rb.AddForce(dv, ForceMode.VelocityChange);

        // Le corps reste horizontal sur ses pattes
        pitch = Mathf.MoveTowards(pitch, 0f, 180f * dt);
        roll = Mathf.MoveTowards(roll, 0f, 180f * dt);
        WingsTucked = flapActive = false;
        flapPhase = 0f;
        stamina = Mathf.Min(1f, stamina + RegenPerched * dt);
    }

    // Extension des pattes : le saut lance l'oiseau, les premiers battements suivent aussitôt
    void Launch()
    {
        float launchSpeed = Mathf.Lerp(minLegLaunchSpeed, maxLegLaunchSpeed, crouchCharge);
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float a = launchAngle * Mathf.Deg2Rad;
        Vector3 v = rb.linearVelocity;
        v.y = Mathf.Max(0f, v.y);
        rb.linearVelocity = v + (forward * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * launchSpeed;

        SetState(BirdState.Takeoff);
        takeoffTimer = 0f;
        groundIgnoreTimer = 0.3f;
        IsGrounded = false;
        legCurrent = legLength;
        Crouch01 = crouchCharge = roll = 0f;
        flapActive = true;
        flapPhase = 0f;
        stamina = Mathf.Max(0f, stamina - TakeoffStaminaCost);
    }

    void Land()
    {
        Vector3 v = rb.linearVelocity;
        float impact = v.magnitude;
        bool hard = impact > maxLandingSpeed;

        // Les articulations des pattes absorbent l'essentiel du choc vertical
        if (v.y < 0f) v.y *= 1f - LegImpactAbsorption;
        // Atterrissage brutal : poitrine et ailes touchent le sol et freinent
        if (hard) { v.x *= HardLandingKeep; v.z *= HardLandingKeep; }
        rb.linearVelocity = v;

        SetState(BirdState.Walking);
        legCurrent = Mathf.Clamp(groundDistance, crouchLegLength, legLength);   // pattes comprimées par l'impact
        flapActive = WingsTucked = false;
        flapPhase = Flare01 = coyoteTimer = TimeSinceLanding = 0f;
        landingGraceTimer = 0.35f;
        if (hard)
        {
            stumbleTimer = StumbleTime;
            stamina = Mathf.Max(0f, stamina - HardLandingStaminaCost);
        }
        if (debugLog) Debug.Log($"[Oiseau] Posé à {impact:F1} m/s{(hard ? " (atterrissage brutal)" : "")}");
        Landed?.Invoke(impact);
    }

    // ---------------------------------------------------------------- vol
    void AirStep(float dt)
    {
        if (State == BirdState.Takeoff && (takeoffTimer += dt) >= takeoffDuration) SetState(BirdState.Flying);

        Vector3 vel = rb.linearVelocity;
        float tau = TimeToContact(vel);
        bool perchBelow = groundNormal.y >= Mathf.Cos(MaxGroundSlope * Mathf.Deg2Rad);

        // Approche : l'arrondi démarre seul en planant vers une surface (trop bas ou contact imminent).
        // Battre des ailes près du sol permet de raser ; un nouvel appui sur Jump annule l'arrondi.
        if (State == BirdState.Flying && !tuckHeld && !jumpHeld && perchBelow && vel.y < 0.5f && groundIgnoreTimer <= 0f &&
            (groundDistance < flareHeight || tau < FlareTimeToContact))
        {
            SetState(BirdState.Landing);
        }
        else if (State == BirdState.Landing &&
                 (jumpPressed || (groundDistance > flareHeight * 1.3f && tau > FlareTimeToContact * 1.5f)))
        {
            SetState(BirdState.Flying);
            Flare01 = 0f;
            legCurrent = legLength;
            pitch = Mathf.Min(pitch, 0f);              // remise des gaz : on quitte l'arrondi
        }

        WingsTucked = State == BirdState.Flying && tuckHeld;
        bool wantFlap = State == BirdState.Takeoff || (jumpHeld && !WingsTucked);

        if (State == BirdState.Takeoff) TakeoffAttitude(vel, dt);
        else if (State == BirdState.Landing) wantFlap = FlareControl(vel, tau, dt);
        else FlightAttitude(vel, dt);
        mouseRolling = mousePitching = false;

        // Le cap suit la trajectoire (effet girouette) ; l'inclinaison courbe la trajectoire
        Vector3 vh = new Vector3(vel.x, 0f, vel.z);
        float k = Mathf.Clamp01((vh.magnitude - 1.5f) / 4f);
        if (k > 0f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(vh.x, vh.z) * Mathf.Rad2Deg, WeathercockRate * k * dt);
        yaw += roll / MaxRoll * LowSpeedTurnRate * (1f - k) * dt;

        ApplyAerodynamics();
        ApplyFlapping(wantFlap, dt);
        if (!flapActive) stamina = Mathf.Min(1f, stamina + RegenGliding * dt);

        // Contact : les pattes touchent la surface, ou le corps touche un sol praticable.
        // Trop rapide = atterrissage brutal, jamais une glissade sans fin.
        if (groundIgnoreTimer <= 0f && vel.y <= 1f && (IsGrounded || walkableContact)) Land();
    }

    // Montée franche, puis le corps s'aligne sur la trajectoire pour prendre de la vitesse
    void TakeoffAttitude(Vector3 vel, float dt)
    {
        float p = takeoffTimer / takeoffDuration;
        float target = p < 0.6f ? TakeoffPitch : Mathf.Lerp(TakeoffPitch, TrimmedPitch(vel), (p - 0.6f) / 0.4f);
        pitch = Mathf.MoveTowards(pitch, target, 60f * dt);
        roll = Mathf.MoveTowards(roll, 0f, 90f * dt);
    }

    void FlightAttitude(Vector3 vel, float dt)
    {
        // Clavier : flèche bas = cabrer ; ignoré près du décrochage
        float up = -moveInput.y;
        if (up > 0f && angleOfAttack > stallAngle - StallMargin) up = 0f;
        pitch = Mathf.Clamp(pitch + up * KeyboardPitchRate * dt, -MaxPitch, MaxPitch);
        roll = Mathf.Clamp(roll + moveInput.x * KeyboardRollRate * dt, -MaxRoll, MaxRoll);
        if (Mathf.Abs(moveInput.x) < 0.1f && !mouseRolling) roll = Mathf.MoveTowards(roll, 0f, RollAutoLevel * dt);

        // Stabilité naturelle : sans commande, la queue aligne le corps sur la trajectoire
        // avec un petit angle d'attaque positif
        if (Mathf.Abs(moveInput.y) < 0.1f && !mousePitching)
            pitch = Mathf.MoveTowards(pitch, TrimmedPitch(vel), PitchStabilityRate * dt);

        // Protection anti-décrochage : au-delà de la marge, le nez est ramené vers le bas
        float over = angleOfAttack - (stallAngle - StallMargin);
        if (over > 0f) pitch -= Mathf.Min(90f * dt, over * 4f * dt);
        pitch = Mathf.Clamp(pitch, -MaxPitch, MaxPitch);
    }

    // Arrondi : le corps se cabre au-delà du décrochage, les ailes freinent comme des aérofreins.
    // Renvoie vrai quand il faut donner des battements de freinage.
    bool FlareControl(Vector3 vel, float tau, float dt)
    {
        // Avancement de l'arrondi : le plus urgent entre la hauteur et le temps avant contact (tau)
        float byHeight = 1f - Mathf.Clamp01((groundDistance - LandingLegReach) / (flareHeight - LandingLegReach));
        float byTau = 1f - Mathf.Clamp01((tau - 0.2f) / (FlareTimeToContact - 0.2f));
        Flare01 = Mathf.Max(byHeight, byTau);

        // Trop rapide : on se cabre plus tôt ; si l'oiseau remonte ("ballon"), on rend la main
        Vector3 forwardDir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float forwardSpeed = Vector3.Dot(vel, forwardDir);
        float excess = 0.6f * Mathf.Clamp01((forwardSpeed - ApproachSpeed) / ApproachSpeed);
        float target = Mathf.Lerp(5f, FlarePitch, Mathf.Max(Flare01, excess)) - Mathf.Max(0f, vel.y) * BalloonPitchCorrection;
        pitch = Mathf.MoveTowards(pitch, target, FlarePitchRate * dt);
        roll = Mathf.MoveTowards(roll, 0f, 120f * dt);

        // Les pattes se tendent vers l'avant pour toucher la surface
        legCurrent = Mathf.MoveTowards(legCurrent, LandingLegReach, LegSpeed * dt);

        // Descente à tau constant (comme les oiseaux) : vitesse de chute proportionnelle à la hauteur restante
        float targetSink = Mathf.Clamp(Mathf.Max(0f, groundDistance - LandingLegReach) / LandingTau, MinTouchdownSink, MaxSinkRate);
        bool sinkingTooFast = vel.y < -targetSink - 0.3f;
        bool stillFast = Flare01 > 0.4f && forwardSpeed > BrakingStrokeSpeed;

        // Battements de freinage : vers le haut et l'arrière tant qu'il reste de la vitesse, puis vers le haut
        float backShare = Mathf.Clamp01((forwardSpeed - BrakingStrokeSpeed) / ApproachSpeed);
        float a = Mathf.Lerp(0f, 90f - BrakingStrokeAngle, backShare * 0.6f) * Mathf.Deg2Rad;
        landingStrokeDir = (Vector3.up * Mathf.Cos(a) - forwardDir * Mathf.Sin(a)).normalized;
        return pitch > BrakingMinPitch && vel.y < -targetSink + 0.2f && (stillFast || sinkingTooFast);
    }

    // Assiette qui met l'aile à l'angle d'attaque d'équilibre sur la trajectoire actuelle
    float TrimmedPitch(Vector3 vel)
    {
        float flightPath = vel.magnitude > 0.5f ? Mathf.Atan2(vel.y, new Vector2(vel.x, vel.z).magnitude) * Mathf.Rad2Deg : 0f;
        return Mathf.Clamp(flightPath + TrimAngleOfAttack - WingIncidence, TrimPitchMin, TrimPitchMax);
    }

    // Temps avant que les pattes touchent la surface (infini si on ne descend pas)
    float TimeToContact(Vector3 vel)
    {
        if (vel.y >= -0.1f || float.IsPositiveInfinity(groundDistance)) return float.PositiveInfinity;
        return Mathf.Max(0f, groundDistance - LandingLegReach) / -vel.y;
    }

    // Portance L = 1/2 rho v² S CL et traînée D = 1/2 rho v² S (CD0 + CL² / (pi e AR)) + traînée du corps
    void ApplyAerodynamics()
    {
        Vector3 v = rb.linearVelocity;
        float speed = v.magnitude;
        if (speed < 0.3f) { angleOfAttack = 0f; stalled = false; return; }

        Vector3 vDir = v / speed;
        Vector3 local = transform.InverseTransformDirection(v);
        angleOfAttack = Mathf.DeltaAngle(0f, Mathf.Atan2(-local.y, local.z) * Mathf.Rad2Deg + WingIncidence);
        float cl = LiftCoefficient(angleOfAttack);

        float area = wingArea * (WingsTucked ? TuckedArea : 1f);
        if (State == BirdState.Landing && v.y > 0f) area *= Mathf.Lerp(1f, BalloonWingFlex, Mathf.Clamp01(v.y));  // ailes fléchies contre le "ballon"
        float span = wingSpan * (WingsTucked ? Mathf.Sqrt(TuckedArea) : 1f);
        float q = 0.5f * AirDensity * speed * speed;

        // Effet de sol : plus de portance à moins d'une envergure de la surface
        float ground = float.IsPositiveInfinity(groundDistance) ? 1f : 1f + GroundEffect * (1f - Mathf.Clamp01(groundDistance / wingSpan));

        float sinA = Mathf.Sin(angleOfAttack * Mathf.Deg2Rad);
        float cd = ZeroLiftDrag + cl * cl / (Mathf.PI * Oswald * span * span / area) / ground + (stalled ? 1.1f * sinA * sinA : 0f);

        Vector3 liftDir = Vector3.Cross(vDir, transform.right);
        if (liftDir.sqrMagnitude > 1e-4f) rb.AddForce(liftDir.normalized * (cl * q * area * ground));
        rb.AddForce(-vDir * (cd * q * area + q * BodyDragArea));
    }

    // CL linéaire jusqu'au décrochage, puis comportement de plaque plane
    float LiftCoefficient(float alpha)
    {
        float abs = Mathf.Abs(alpha);
        stalled = abs > stallAngle;
        if (!stalled) return Mathf.Clamp(LiftSlope * alpha * Mathf.Deg2Rad, -MaxLift, MaxLift);
        float clAtStall = Mathf.Min(MaxLift, LiftSlope * stallAngle * Mathf.Deg2Rad);
        float flatPlate = 0.9f * Mathf.Sin(2f * alpha * Mathf.Deg2Rad);
        return Mathf.Lerp(Mathf.Sign(alpha) * clAtStall * 0.8f, flatPlate, Mathf.InverseLerp(stallAngle, stallAngle + 15f, abs));
    }

    void ApplyFlapping(bool want, float dt)
    {
        if (!flapActive && want) { flapActive = true; flapPhase = 0f; }
        if (!flapActive) return;

        bool takeoff = State == BirdState.Takeoff;
        flapPhase += (takeoff ? TakeoffFlapFrequency : flapFrequency) * dt;
        if (flapPhase >= 1f)
        {
            flapPhase -= 1f;
            if (!want) { flapActive = false; flapPhase = 0f; return; }   // on termine toujours le battement
        }

        // Fatigué, l'oiseau garde une partie de sa puissance
        float boost = takeoff ? TakeoffFlapBoost : 1f;
        float power = flapPower * boost * Mathf.Lerp(ExhaustedPower, 1f, Mathf.Sqrt(stamina));
        stamina = Mathf.Max(0f, stamina - staminaDrainRate * boost * dt);

        // Abaissement : force principale ; relèvement : petite force inverse
        float force = flapPhase < DownstrokeRatio
            ? power * Mathf.Sin(Mathf.PI * flapPhase / DownstrokeRatio)
            : -UpstrokePenalty * flapPower * Mathf.Sin(Mathf.PI * (flapPhase - DownstrokeRatio) / (1f - DownstrokeRatio));

        if (State == BirdState.Landing)
        {
            rb.AddForce(landingStrokeDir * force * LandingStrokePower);
            return;
        }

        // Un battement donne (1) un soutien vertical, important à basse vitesse, et (2) une poussée vers
        // l'avant qui faiblit avec la vitesse, comme une hélice. Le soutien est vertical (pas selon le
        // corps) : cabrer ne transforme pas les ailes en fusée, la montée dépend de la puissance.
        float speed = rb.linearVelocity.magnitude;
        float tilt = Mathf.Lerp(HoverStrokeAngle, CruiseStrokeAngle, Mathf.Clamp01(speed / CruiseSpeed)) * Mathf.Deg2Rad;
        rb.AddForce(Vector3.up * (force * Mathf.Cos(tilt)));
        rb.AddForce(transform.forward * (force * Mathf.Sin(tilt) * Mathf.Max(0f, 1f - speed / ThrustMaxSpeed)));
    }

    // ---------------------------------------------------------------- outils
    void SetState(BirdState s)
    {
        if (debugLog && s != State) Debug.Log($"[Oiseau] {State} -> {s}");
        State = s;
    }

    // Résumé de debug dans la console, une fois par seconde
    void LogStatus()
    {
        if ((debugTimer -= Time.deltaTime) > 0f) return;
        debugTimer = 1f;
        Debug.Log($"[Oiseau] {State} | vitesse {Airspeed:F1} m/s, vy {rb.linearVelocity.y:F1} | " +
                  $"hauteur {(float.IsPositiveInfinity(groundDistance) ? -1f : groundDistance):F1} m | " +
                  $"assiette {pitch:F0}°, AoA {angleOfAttack:F0}°{(stalled ? " DÉCROCHAGE" : "")} | " +
                  $"endurance {stamina * 100f:F0}% | entrée ({moveInput.x:F1}, {moveInput.y:F1})");
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
