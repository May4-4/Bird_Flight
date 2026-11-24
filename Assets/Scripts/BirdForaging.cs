using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Boucle de nourrissage : attraper les vers avec le bec et les donner aux oisillons.
//  - Visée : un rayon (sphere cast) part de l'œil dans l'axe du regard, donc du bec.
//    PC : la souris oriente le regard. VR : c'est le casque.
//  - Coup de bec : Attack (clic, gâchette XR) ou Interact (E). La tête plonge et la cible est
//    vérifiée à nouveau au bout du mouvement (un ver qui se tortille peut s'échapper).
//  - Attraper : le ver est figé et tenu au bout du bec (visible à la première personne).
//  - Nourrir : un coup de bec vers un oisillon affamé lui donne le ver.
// Ajoute aussi la recherche de vers (WormLocator) et la minimap du nid (NestMiniMap).
[RequireComponent(typeof(BirdFirstPersonController))]
public class BirdForaging : MonoBehaviour
{
    public int maxCarried = 3;
    public float beakReach = 2.2f;          // portée du bec depuis l'œil (cou + bec)
    public float carriedWormScale = 0.1f;   // les vers de la scène sont très grands

    const float BeakCastRadius = 0.25f, PeckDuration = 0.22f, PeckCooldown = 0.35f, FeedTransferTime = 0.35f;

    enum Target { None, Worm, HungryChick, FedChick }

    BirdFirstPersonController bird;
    BirdBodyAnimator body;
    GameManager gm;
    Camera eye;
    Transform beakTip, targetWorm;
    AnimateBabyBirds targetChick;
    Target target;
    InputAction attackAct, interactAct;
    AudioClip catchClip, feedClip, missClip;
    readonly List<GameObject> carried = new List<GameObject>();
    readonly RaycastHit[] hits = new RaycastHit[16];
    float peckTimer = -1f, cooldown;

    void Awake()
    {
        bird = GetComponent<BirdFirstPersonController>();
        body = GetComponent<BirdBodyAnimator>();
        eye = GetComponentInChildren<Camera>();
        if (GetComponent<WormLocator>() == null) gameObject.AddComponent<WormLocator>();
        if (GetComponent<NestMiniMap>() == null) gameObject.AddComponent<NestMiniMap>();

        // Petits sons générés par le code (pas besoin de fichiers audio)
        catchClip = ProceduralAudio.Squelch(0.16f);
        feedClip = ProceduralAudio.Chirp(2600f, 1800f, 0.09f, 3, 0.03f);
        missClip = ProceduralAudio.Click(0.05f);
    }

    void Start()
    {
        gm = FindAnyObjectByType<GameManager>();
        attackAct = InputSystem.actions.FindAction("Player/Attack");
        interactAct = InputSystem.actions.FindAction("Player/Interact");

        // Point d'accroche au bout du bec. Créé dans Start, une fois le bec rattaché à la tête (Awake de BirdBodyAnimator).
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == "Beak")
            {
                beakTip = new GameObject("BeakTip").transform;
                beakTip.SetParent(t.parent, false);
                beakTip.position = t.TransformPoint(new Vector3(0f, 0f, 0.5f));
                beakTip.rotation = t.rotation;
                break;
            }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        cooldown -= dt;
        UpdateTarget();

        if (PeckPressed() && cooldown <= 0f && peckTimer < 0f)
        {
            peckTimer = 0f;
            cooldown = PeckCooldown;
            body.Peck(PeckDuration);
        }

        // Le coup de bec se résout au bout du mouvement vers l'avant (mi-parcours)
        if (peckTimer >= 0f)
        {
            float before = peckTimer;
            peckTimer += dt;
            if (before < PeckDuration * 0.5f && peckTimer >= PeckDuration * 0.5f) ResolvePeck();
            if (peckTimer >= PeckDuration) peckTimer = -1f;
        }
    }

    bool PeckPressed()
    {
        // Un clic qui sert seulement à recapturer la souris ne doit pas donner de coup de bec
        bool mouseFree = Cursor.lockState != CursorLockMode.Locked;
        foreach (var a in new[] { attackAct, interactAct })
            if (a != null && a.WasPressedThisFrame() && !(mouseFree && a.activeControl?.device is Mouse)) return true;
        return false;
    }

    // Que touche le bec ? Oisillon (affamé ou non) ou ver libre, le plus proche.
    void UpdateTarget()
    {
        target = Target.None;
        targetWorm = null;
        targetChick = null;
        int n = Physics.SphereCastNonAlloc(eye.transform.position, BeakCastRadius, eye.transform.forward, hits, beakReach, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.collider.transform.IsChildOf(transform) || h.distance >= best) continue;

            var chick = h.collider.GetComponentInParent<AnimateBabyBirds>();
            Transform worm = chick == null ? WormRoot(h.collider.transform) : null;
            if (chick != null) { target = chick.wantFood ? Target.HungryChick : Target.FedChick; targetChick = chick; }
            else if (worm != null) { target = Target.Worm; targetWorm = worm; }
            else continue;
            best = h.distance;
        }
    }

    void ResolvePeck()
    {
        UpdateTarget();   // le ver a pu s'échapper pendant le mouvement
        if (CanAct() && target == Target.Worm) Catch(targetWorm);
        else if (CanAct() && target == Target.HungryChick) Feed(targetChick);
        else Play(missClip, eye.transform.position, 0.5f, 0f);   // coup dans le vide ou impossible : claquement de bec
    }

    void Catch(Transform worm)
    {
        // Le ver tenu devient purement visuel : plus de script, d'articulations, de colliders ni de Rigidbody
        foreach (var w in worm.GetComponentsInChildren<WormTipControl>()) Destroy(w);
        foreach (var j in worm.GetComponentsInChildren<Joint>()) DestroyImmediate(j);
        foreach (var c in worm.GetComponentsInChildren<Collider>()) Destroy(c);
        foreach (var r in worm.GetComponentsInChildren<Rigidbody>()) { r.isKinematic = true; r.detectCollisions = false; Destroy(r); }

        // Tenu en travers, juste sous le bout du bec ; les vers suivants se rangent derrière
        Transform center = worm.Find("seg_center");
        Vector3 centerLocal = center != null ? center.localPosition : Vector3.zero;
        worm.SetParent(beakTip, false);
        worm.localScale = Vector3.one * carriedWormScale;
        worm.localRotation = Quaternion.identity;
        worm.localPosition = -centerLocal * carriedWormScale + new Vector3(0f, -0.04f, -0.04f * carried.Count);

        carried.Add(worm.gameObject);
        gm?.scoreModify(1);
        Play(catchClip, beakTip.position, 1f, 0f);
    }

    void Feed(AnimateBabyBirds chick)
    {
        GameObject worm = carried[carried.Count - 1];
        carried.RemoveAt(carried.Count - 1);
        chick.wantFood = false;
        gm?.feedBabyBirds(1);
        StartCoroutine(TransferWorm(worm.transform, chick.beak != null ? chick.beak : chick.transform));
        Play(feedClip, chick.transform.position, 1f, 0.5f);
    }

    // Le ver glisse du bec du parent jusque dans le bec ouvert de l'oisillon, puis disparaît
    IEnumerator TransferWorm(Transform worm, Transform to)
    {
        worm.SetParent(null, true);
        Vector3 startPos = worm.position, startScale = worm.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime / FeedTransferTime)
        {
            worm.position = Vector3.Lerp(startPos, to.position, t);
            worm.localScale = Vector3.Lerp(startScale, startScale * 0.3f, t);
            yield return null;
        }
        Destroy(worm.gameObject);
    }

    // Attraper : posé au sol et bec pas plein. Nourrir : un ver dans le bec et un oisillon affamé.
    bool CanAct()
    {
        if (target == Target.Worm) return !bird.IsAirborne && carried.Count < maxCarried;
        return target == Target.HungryChick && carried.Count > 0;
    }

    Transform WormRoot(Transform t)
    {
        while (t != null && !t.CompareTag("Worm")) t = t.parent;
        return t != null && !carried.Contains(t.gameObject) ? t : null;
    }

    static void Play(AudioClip clip, Vector3 pos, float volume, float spatial)
    {
        var go = new GameObject("Son");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.spatialBlend = spatial;
        src.minDistance = 5f;
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    // Interface minimale : point de visée (vert = action possible, jaune = impossible) avec un mot d'aide,
    // et une ligne de score
    void OnGUI()
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        GUI.color = target == Target.None ? new Color(1f, 1f, 1f, 0.6f) : CanAct() ? Color.green : Color.yellow;
        float size = target == Target.None ? 4f : 8f;
        GUI.DrawTexture(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), Texture2D.whiteTexture);

        string hint = null;
        if (target == Target.Worm) hint = CanAct() ? "Attraper" : bird.IsAirborne ? "Posez-vous" : "Bec plein";
        else if (target == Target.HungryChick) hint = CanAct() ? "Nourrir" : "Rien dans le bec";
        else if (target == Target.FedChick) hint = "Déjà nourri";
        if (hint != null) GUI.Label(new Rect(cx - 100f, cy + 10f, 200f, 22f), hint, Centered());

        GUI.color = Color.white;
        GUI.Label(new Rect(cx - 200f, Screen.height - 40f, 400f, 22f),
                  $"Bec : {carried.Count}/{maxCarried}   Oisillons nourris : {(gm != null ? gm.getBabyBirdsScore() : 0)}/3", Centered());
    }

    static GUIStyle centered;
    static GUIStyle Centered()
    {
        return centered ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
    }
}

// Petits effets sonores générés par le code
public static class ProceduralAudio
{
    const int Rate = 44100;

    // "Pips" : sinusoïdes glissant de f0 à f1
    public static AudioClip Chirp(float f0, float f1, float dur, int count, float gap)
    {
        int per = Mathf.CeilToInt(dur * Rate), gapN = Mathf.CeilToInt(gap * Rate);
        var data = new float[count * per + (count - 1) * gapN];
        for (int c = 0, o = 0; c < count; c++, o += per + gapN)
        {
            double phase = 0;
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / per;
                phase += 2.0 * Mathf.PI * Mathf.Lerp(f0, f1, t) / Rate;
                data[o + i] = 0.5f * Mathf.Sin(Mathf.PI * t) * (float)System.Math.Sin(phase);
            }
        }
        return Make("Chirp", data);
    }

    // Bruit sourd et mouillé : bruit filtré sur une note grave descendante
    public static AudioClip Squelch(float dur)
    {
        var data = new float[Mathf.CeilToInt(dur * Rate)];
        var rng = new System.Random(7);
        float lp = 0f; double phase = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / data.Length;
            lp += 0.08f * ((float)(rng.NextDouble() * 2 - 1) - lp);
            phase += 2.0 * Mathf.PI * Mathf.Lerp(320f, 90f, t) / Rate;
            data[i] = Mathf.Pow(1f - t, 2f) * Mathf.Min(1f, t * 40f) * (2.1f * lp + 0.4f * (float)System.Math.Sin(phase));
        }
        return Make("Squelch", data);
    }

    // Court claquement de bec
    public static AudioClip Click(float dur)
    {
        var data = new float[Mathf.CeilToInt(dur * Rate)];
        var rng = new System.Random(3);
        for (int i = 0; i < data.Length; i++)
            data[i] = Mathf.Exp(-12f * i / data.Length) * ((float)(rng.NextDouble() * 2 - 1) * 0.5f + 0.5f * Mathf.Sin(i * 2f * Mathf.PI * 2200f / Rate));
        return Make("Click", data);
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
