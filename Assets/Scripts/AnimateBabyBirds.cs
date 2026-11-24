using UnityEngine;

// Oisillon affamé qui quémande, animé par le code, comme les vrais oisillons :
//  - le quémandage vient par vagues (l'intensité monte et descend, jamais une boucle parfaite) ;
//  - cou tendu, bec grand ouvert (mandibule inférieure jaune vif, matériau m_gape) qui s'ouvre à chaque cri ;
//  - ailes qui frémissent vite, avec une amplitude faible et irrégulière ;
//  - il sautille, se bouscule et se tourne vers le parent ; il s'excite quand le parent approche ;
//  - une fois nourri, il se calme : les cris s'éteignent, les ailes se replient.
// Tout est réglé dans le prefab Baby_bird : parties du corps, voix (AudioSource en boucle, 3D),
// Rigidbody cinématique (l'oisillon reste dans le nid).
public class AnimateBabyBirds : MonoBehaviour
{
    public bool wantFood = true;
    public Transform body, head, leftWingPivot, rightWingPivot;
    public Transform beak;                  // base du bec : BirdForaging y dépose le ver
    public Transform upperJaw, lowerJaw;    // charnières des mandibules
    public AudioSource voice;

    // Réglages fins (constantes)
    const float FlapFrequency = 9f, FlapAngle = 45f, FlapIrregularity = 0.35f, WingLift = 20f;
    const float NeckStretch = 0.35f, BodyBounce = 0.12f, BounceFrequency = 3.5f, SwayAngle = 12f, HeadWobble = 10f;
    const float GapeAngle = 35f, ParentRadius = 15f, TurnTowardParent = 35f;
    const float BoutSpeed = 0.35f, RiseSpeed = 4f, CalmSpeed = 1.2f, BaseVolume = 0.7f;

    Transform parentBird;
    Vector3 bodyBasePos, headBasePos;
    Quaternion bodyBaseRot, headBaseRot, leftBaseRot, rightBaseRot;
    float intensity, seed, wingPhase, callPhase, basePitch, parentYaw, time;

    void Start()
    {
        seed = Random.Range(0f, 1000f);
        basePitch = Random.Range(0.95f, 1.35f);      // chaque oisillon a sa propre voix
        bodyBasePos = body.localPosition; bodyBaseRot = body.localRotation;
        headBasePos = head.localPosition; headBaseRot = head.localRotation;
        leftBaseRot = leftWingPivot.localRotation;
        rightBaseRot = rightWingPivot.localRotation;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) parentBird = p.transform;
        intensity = wantFood ? 0.5f : 0f;
        if (wantFood) StartCalling();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        time += dt;

        // Excitation : vagues de quémandage (bruit de Perlin) + proximité du parent
        float proximity = parentBird != null ? Mathf.Clamp01(1f - Vector3.Distance(parentBird.position, transform.position) / ParentRadius) : 0f;
        float bout = Mathf.SmoothStep(0f, 1f, Mathf.PerlinNoise(seed, time * BoutSpeed));
        float target = wantFood ? Mathf.Clamp01(0.25f + 0.55f * bout + 0.9f * proximity) : 0f;
        intensity = Mathf.MoveTowards(intensity, target, (target > intensity ? RiseSpeed : CalmSpeed) * dt);

        AnimateWings(dt);
        AnimateBody(proximity, dt);
        AnimateHeadAndGape(dt);
        UpdateVoice();
    }

    // Frémissement rapide et irrégulier : vitesse et amplitude varient avec du bruit
    void AnimateWings(float dt)
    {
        wingPhase += dt * FlapFrequency * Mathf.PI * 2f * (1f + FlapIrregularity * (Mathf.PerlinNoise(seed + 10f, time * 2f) - 0.5f) * 2f);
        float amplitude = FlapAngle * intensity * Mathf.Lerp(0.5f, 1f, Mathf.PerlinNoise(seed + 20f, time * 1.3f));
        float angle = Mathf.Sin(wingPhase) * amplitude + WingLift * intensity;
        leftWingPivot.localRotation = leftBaseRot * Quaternion.Euler(0f, 0f, angle);
        rightWingPivot.localRotation = rightBaseRot * Quaternion.Euler(0f, 0f, -angle);
    }

    // Sautillements, bousculade, et rotation vers le parent quand il est proche
    void AnimateBody(float proximity, float dt)
    {
        Transform parent = body.parent;
        Vector3 localUp = parent.InverseTransformDirection(Vector3.up).normalized;
        float hop = Mathf.Abs(Mathf.Sin((time + seed) * BounceFrequency * Mathf.PI)) * BodyBounce * intensity;
        body.localPosition = bodyBasePos + localUp * hop;

        float sway = (Mathf.PerlinNoise(seed + 30f, time * 1.7f) - 0.5f) * 2f * SwayAngle * intensity;
        float desiredYaw = 0f;
        if (parentBird != null && proximity > 0f && wantFood)
        {
            Vector3 facing = Vector3.ProjectOnPlane(parent.rotation * bodyBaseRot * Vector3.forward, Vector3.up);
            Vector3 toParent = Vector3.ProjectOnPlane(parentBird.position - transform.position, Vector3.up);
            desiredYaw = Mathf.Clamp(Vector3.SignedAngle(facing, toParent, Vector3.up), -TurnTowardParent, TurnTowardParent);
        }
        parentYaw = Mathf.LerpAngle(parentYaw, desiredYaw, 3f * dt);
        body.rotation = Quaternion.AngleAxis(parentYaw + sway, Vector3.up) * parent.rotation * bodyBaseRot;
    }

    // Cou tendu, tête qui oscille, bec qui s'ouvre au rythme des cris
    void AnimateHeadAndGape(float dt)
    {
        callPhase += dt * Mathf.Lerp(1.5f, 5.5f, intensity) * Mathf.PI * 2f;

        Vector3 localUp = head.parent.InverseTransformDirection(Vector3.up).normalized;
        head.localPosition = headBasePos + localUp * NeckStretch * intensity * Mathf.Lerp(0.7f, 1f, 0.5f + 0.5f * Mathf.Sin(callPhase * 0.5f));
        float wobX = (Mathf.PerlinNoise(seed + 40f, time * 3f) - 0.5f) * 2f * HeadWobble * intensity;
        float wobZ = (Mathf.PerlinNoise(seed + 50f, time * 3f) - 0.5f) * 2f * HeadWobble * intensity;
        head.localRotation = headBaseRot * Quaternion.Euler(wobX, 0f, wobZ);

        float open = intensity * Mathf.Clamp01(0.55f + 0.45f * Mathf.Sin(callPhase));
        upperJaw.localRotation = Quaternion.Euler(-GapeAngle * 0.3f * open, 0f, 0f);
        lowerJaw.localRotation = Quaternion.Euler(GapeAngle * open, 0f, 0f);
    }

    // Volume et hauteur suivent l'excitation ; une fois nourri, le cri s'éteint
    void UpdateVoice()
    {
        voice.volume = BaseVolume * Mathf.Clamp01(intensity * 1.2f);
        voice.pitch = basePitch * (1f + 0.15f * intensity);
        if (intensity < 0.01f && voice.isPlaying) voice.Stop();
        else if (intensity >= 0.01f && wantFood && !voice.isPlaying) StartCalling();
    }

    void StartCalling()
    {
        voice.time = Random.Range(0f, voice.clip.length);   // départ au hasard pour désynchroniser
        voice.Play();
    }
}
