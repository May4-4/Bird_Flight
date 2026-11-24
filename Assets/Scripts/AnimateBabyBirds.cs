using UnityEngine;

// Oisillon affamé qui quémande, animé par le code, comme les vrais oisillons :
//  - le quémandage vient par vagues (l'intensité monte et descend, jamais une boucle parfaite) ;
//  - cou tendu, bec grand ouvert (bord jaune vif) qui s'ouvre à chaque cri ;
//  - ailes qui frémissent vite, avec une amplitude faible et irrégulière ;
//  - il sautille, se bouscule et se tourne vers le parent ; il s'excite quand le parent approche ;
//  - une fois nourri, il se calme : les cris s'éteignent, les ailes se replient.
// wantFood, leftWingPivot et rightWingPivot gardent leur nom (utilisés par les prefabs et NestGiver).
[RequireComponent(typeof(AudioSource))]
public class AnimateBabyBirds : MonoBehaviour
{
    public bool wantFood = true;
    public Transform leftWingPivot;
    public Transform rightWingPivot;
    [HideInInspector] public Transform beak;   // trouvé par son nom, utilisé par BirdForaging pour donner le ver

    // Réglages fins (constantes)
    const float FlapFrequency = 9f, FlapAngle = 45f, FlapIrregularity = 0.35f, WingLift = 20f;
    const float NeckStretch = 0.35f, BodyBounce = 0.12f, BounceFrequency = 3.5f, SwayAngle = 12f, HeadWobble = 10f;
    const float GapeAngle = 35f, ParentRadius = 15f, TurnTowardParent = 35f;
    const float BoutSpeed = 0.35f, RiseSpeed = 4f, CalmSpeed = 1.2f, BaseVolume = 0.7f;
    static readonly Color GapeColor = new Color(1f, 0.78f, 0.1f);

    AudioSource voice;
    Transform parentBird, body, head, upperJaw, lowerJaw;
    Vector3 bodyBasePos, headBasePos;
    Quaternion bodyBaseRot, headBaseRot, leftBaseRot, rightBaseRot, beakBaseRot;
    float intensity, seed, wingPhase, callPhase, basePitch, parentYaw, time;

    void Awake()
    {
        voice = GetComponent<AudioSource>();
        seed = Random.Range(0f, 1000f);
        basePitch = Random.Range(0.95f, 1.35f);      // chaque oisillon a sa propre voix

        body = FindChild("BodyVisuals");
        head = FindChild("Head");
        beak = FindChild("Beak");
        if (leftWingPivot == null) leftWingPivot = FindChild("LeftWingPivot");
        if (rightWingPivot == null) rightWingPivot = FindChild("RightWingPivot");

        // Les oisillons restent dans le nid : pas de chute physique, mais les colliders restent actifs
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        bodyBasePos = body.localPosition; bodyBaseRot = body.localRotation;
        headBasePos = head.localPosition; headBaseRot = head.localRotation;
        leftBaseRot = leftWingPivot.localRotation;
        rightBaseRot = rightWingPivot.localRotation;
        BuildJaws();

        // Cris spatialisés et désynchronisés : les oisillons ne crient jamais à l'unisson
        voice.loop = true;
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.minDistance = 2f;
        voice.maxDistance = 60f;
        voice.volume = 0f;
    }

    void Start()
    {
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
        upperJaw.localRotation = beakBaseRot * Quaternion.Euler(-GapeAngle * 0.3f * open, 0f, 0f);
        lowerJaw.localRotation = beakBaseRot * Quaternion.Euler(GapeAngle * open, 0f, 0f);
    }

    // Volume et hauteur suivent l'excitation ; une fois nourri, le cri s'éteint
    void UpdateVoice()
    {
        if (voice.clip == null) return;
        voice.volume = BaseVolume * Mathf.Clamp01(intensity * 1.2f);
        voice.pitch = basePitch * (1f + 0.15f * intensity);
        if (intensity < 0.01f && voice.isPlaying) voice.Stop();
        else if (intensity >= 0.01f && wantFood && !voice.isPlaying) StartCalling();
    }

    void StartCalling()
    {
        if (voice.clip == null) return;
        voice.time = Random.Range(0f, voice.clip.length);   // départ au hasard pour désynchroniser
        voice.Play();
    }

    // Le bec (un seul cube) est coupé en deux mandibules articulées à sa base pour pouvoir s'ouvrir.
    // La mandibule inférieure prend la couleur jaune vif de l'intérieur du bec.
    void BuildJaws()
    {
        var mf = beak.GetComponent<MeshFilter>();
        Vector3 ext = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.extents : Vector3.one * 0.5f;
        float halfLen = ext.z * beak.localScale.z, halfHeight = ext.y * beak.localScale.y;
        beakBaseRot = beak.localRotation;
        Vector3 hinge = beak.localPosition - beak.localRotation * (Vector3.forward * halfLen);

        upperJaw = NewHinge("UpperJaw", hinge);
        lowerJaw = NewHinge("LowerJaw", hinge);
        Transform lower = Instantiate(beak.gameObject, beak.parent).transform;
        lower.localPosition = beak.localPosition;
        lower.localRotation = beak.localRotation;
        lower.localScale = beak.localScale;

        beak.SetParent(upperJaw, true);
        lower.SetParent(lowerJaw, true);
        Vector3 thin = new Vector3(beak.localScale.x, beak.localScale.y * 0.55f, beak.localScale.z);
        beak.localScale = lower.localScale = thin;
        beak.localPosition = new Vector3(0f, halfHeight * 0.45f, halfLen);
        lower.localPosition = new Vector3(0f, -halfHeight * 0.45f, halfLen);

        var r = lower.GetComponent<Renderer>();
        if (r != null)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", GapeColor);
            r.SetPropertyBlock(block);
        }
    }

    Transform NewHinge(string name, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(beak.parent, false);
        t.localPosition = localPos;
        t.localRotation = beakBaseRot;
        return t;
    }

    Transform FindChild(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }
}
