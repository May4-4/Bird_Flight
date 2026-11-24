using UnityEngine;

// Partie visuelle de l'oiseau à la première personne :
//  - la tête (les yeux = la caméra) reste stable quand le corps s'incline, comme chez les oiseaux ;
//  - en marchant, un hochement de tête par pas (la tête reste fixe puis avance, comme un pigeon) ;
//  - les ailes : repliées au sol, levées en s'accroupissant, battement complet en vol, ouvertes en plané ;
//  - seul le bec est visible pour la caméra, le reste du corps ne fait qu'une ombre.
// Réglé dans la scène : la tête, le bec et la caméra (les yeux) sont enfants de HeadPivot ;
// les renderers du corps sont en "Shadows Only", seul le bec est rendu normalement.
[RequireComponent(typeof(BirdFirstPersonController))]
public class BirdBodyAnimator : MonoBehaviour
{
    public Transform headPivot, leftWing, rightWing;
    public Camera eye;
    // 0 = caméra parfaitement stable (conseillé en VR), 1 = hochement de tête naturel complet
    public float headBobStrength = 0.4f;

    // Réglages fins (constantes)
    const float RollStabilization = 0.75f, PitchStabilization = 0.35f, LandingPitchStabilization = 0.9f;
    const float CrouchHeadDip = 8f, LandingLookDown = 12f, TouchdownDipPerMps = 0.012f, MaxTouchdownDip = 0.12f;
    const float StepLength = 0.3f, HoldFraction = 0.6f, ThrustDip = 0.012f, HopThreshold = 1.4f, HopLength = 0.5f, HopHeight = 0.05f;
    const float FlapAmplitude = 55f, FoldedSweep = 75f, FoldedElevation = -10f, UpstrokeSweep = 30f, TuckedSweep = 60f;
    const float GlideDihedral = 6f, FlareElevation = 25f, FlareSweepForward = 15f, WingBalanceTime = 0.4f, WingPoseSharpness = 10f;
    const float BaseFov = 75f, MaxFovBoost = 15f, FovBoostSpeed = 25f;
    const float PeckReach = 0.2f, PeckDip = 25f;

    BirdFirstPersonController bird;
    Vector3 headBasePos, gaitOffset;
    Quaternion headBaseRot, leftBaseRot, rightBaseRot;
    float stepPhase, gaitBlend, wingElevation = FoldedElevation, wingSweep = FoldedSweep, touchdownDip;
    float peckTime = -1f, peckDuration = 0.2f;

    void Awake()
    {
        bird = GetComponent<BirdFirstPersonController>();
        headBasePos = headPivot.localPosition;
        headBaseRot = headPivot.localRotation;
        leftBaseRot = leftWing.localRotation;
        rightBaseRot = rightWing.localRotation;
    }

    void OnEnable() { bird.Landed += OnLanded; }
    void OnDisable() { bird.Landed -= OnLanded; }

    // À l'impact, la tête s'enfonce avec les pattes qui amortissent
    void OnLanded(float impactSpeed) { touchdownDip = Mathf.Min(MaxTouchdownDip, impactSpeed * TouchdownDipPerMps); }

    // Coup de bec : la tête plonge vite vers l'avant puis revient plus lentement
    public void Peck(float duration)
    {
        peckDuration = Mathf.Max(0.05f, duration);
        peckTime = 0f;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        AnimateHead(dt);
        AnimateWings(dt);

        // Le champ de vision s'élargit avec la vitesse
        eye.fieldOfView = Mathf.Lerp(eye.fieldOfView, BaseFov + MaxFovBoost * Mathf.Clamp01(bird.Airspeed / FovBoostSpeed), 1f - Mathf.Exp(-3f * dt));
    }

    void AnimateHead(float dt)
    {
        UpdateGait(dt);

        // La tête compense légèrement le soulèvement du corps à chaque battement
        float heave = bird.IsFlapping ? -0.015f * Mathf.Sin(bird.FlapPhase * Mathf.PI * 2f) : 0f;
        touchdownDip = Mathf.Lerp(touchdownDip, 0f, 1f - Mathf.Exp(-6f * dt));
        float peck = PeckAmount(dt);
        headPivot.localPosition = headBasePos + gaitOffset + new Vector3(0f, heave - touchdownDip, peck * PeckReach);

        // Regard + stabilisation : on annule une partie de l'inclinaison du corps pour garder l'horizon.
        // Pendant l'arrondi, le regard reste fixé sur le point d'atterrissage.
        bool landing = bird.State == BirdFirstPersonController.BirdState.Landing;
        float x = -bird.HeadPitch
                  + bird.Pitch * (landing ? LandingPitchStabilization : PitchStabilization)
                  + bird.Crouch01 * CrouchHeadDip
                  + (landing ? LandingLookDown * bird.Flare01 : 0f)
                  + peck * PeckDip;
        headPivot.localRotation = headBaseRot * Quaternion.Euler(x, 0f, bird.Roll * RollStabilization);
    }

    float PeckAmount(float dt)
    {
        if (peckTime < 0f) return 0f;
        float u = (peckTime += dt) / peckDuration;
        if (u >= 1f) { peckTime = -1f; return 0f; }
        return u < 0.4f ? Mathf.SmoothStep(0f, 1f, u / 0.4f) : Mathf.SmoothStep(1f, 0f, (u - 0.4f) / 0.6f);
    }

    // Un hochement de tête par pas, calé sur la distance parcourue :
    //  - appui : la tête reste immobile dans le monde pendant que le corps avance dessous ;
    //  - poussée : la tête avance en douceur jusqu'à sa position suivante.
    // En sautillant (rapide), petit bond vertical à chaque saut.
    void UpdateGait(float dt)
    {
        float speed = bird.IsGrounded && !bird.IsAirborne ? bird.HorizontalSpeed : 0f;
        bool hopping = speed > bird.walkSpeed * HopThreshold;

        gaitBlend = Mathf.MoveTowards(gaitBlend, speed > 0.1f ? 1f : 0f, 4f * dt);   // pas d'à-coup au départ ni à l'arrêt
        if (speed > 0.05f) stepPhase = (stepPhase + speed / (hopping ? HopLength : StepLength) * dt) % 1f;
        else if (gaitBlend <= 0f) stepPhase = 0f;

        Vector3 offset;
        if (hopping) offset = new Vector3(0f, HopHeight * Mathf.Sin(Mathf.PI * stepPhase), 0f);
        else
        {
            float amplitude = 0.5f * StepLength * HoldFraction * headBobStrength;
            if (stepPhase < HoldFraction)
                offset = new Vector3(0f, 0f, amplitude * (1f - 2f * stepPhase / HoldFraction));
            else
            {
                float u = (stepPhase - HoldFraction) / (1f - HoldFraction);
                offset = new Vector3(0f, -ThrustDip * headBobStrength * Mathf.Sin(Mathf.PI * u),
                                     -amplitude + 2f * amplitude * Mathf.SmoothStep(0f, 1f, u));
            }
        }
        gaitOffset = offset * gaitBlend;
    }

    void AnimateWings(float dt)
    {
        float elevation, sweep;
        if (bird.IsFlapping && !bird.WingsTucked)
        {
            // Battement : abaissement (+A -> -A) puis relèvement aile fléchie (-A -> +A), sans lissage
            float p = bird.FlapPhase, ds = BirdFirstPersonController.DownstrokeRatio;
            if (p < ds) { wingElevation = FlapAmplitude * Mathf.Cos(Mathf.PI * p / ds); wingSweep = 0f; }
            else
            {
                float u = (p - ds) / (1f - ds);
                wingElevation = -FlapAmplitude * Mathf.Cos(Mathf.PI * u);
                wingSweep = UpstrokeSweep * Mathf.Sin(Mathf.PI * u);
            }
            ApplyWingPose();
            return;
        }

        if (!bird.IsAirborne)
        {
            // Repliées ; levées en s'accroupissant ; à demi ouvertes pour l'équilibre juste après l'atterrissage
            elevation = Mathf.Lerp(FoldedElevation, 45f, bird.Crouch01);
            sweep = Mathf.Lerp(FoldedSweep, 10f, bird.Crouch01);
            if (bird.TimeSinceLanding < WingBalanceTime) { elevation = 30f; sweep = 10f; }
        }
        else if (bird.State == BirdFirstPersonController.BirdState.Landing)
        {
            // Arrondi : ailes relevées et avancées, comme des aérofreins
            elevation = Mathf.Lerp(GlideDihedral, FlareElevation, bird.Flare01);
            sweep = -FlareSweepForward * bird.Flare01;
        }
        else if (bird.WingsTucked) { elevation = 0f; sweep = TuckedSweep; }
        else { elevation = GlideDihedral; sweep = 0f; }

        float k = 1f - Mathf.Exp(-WingPoseSharpness * dt);
        wingElevation = Mathf.Lerp(wingElevation, elevation, k);
        wingSweep = Mathf.Lerp(wingSweep, sweep, k);
        ApplyWingPose();
    }

    // L'aile gauche s'étend vers -X : l'élévation est inversée d'un côté à l'autre
    void ApplyWingPose()
    {
        leftWing.localRotation = leftBaseRot * Quaternion.Euler(0f, -wingSweep, -wingElevation);
        rightWing.localRotation = rightBaseRot * Quaternion.Euler(0f, wingSweep, wingElevation);
    }
}
