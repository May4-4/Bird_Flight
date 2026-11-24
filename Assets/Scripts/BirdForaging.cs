using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Boucle de nourrissage : attraper les vers avec le bec et les donner aux oisillons.
//  - Visée : un rayon (sphere cast) part de l'œil dans l'axe du regard, donc du bec.
//    PC : la souris oriente le regard. VR : c'est le casque.
//  - Coup de bec : Attack (clic, gâchette XR) ou Interact (E). La tête plonge et la cible est
//    vérifiée à nouveau au bout du mouvement (un ver qui se tortille peut s'échapper).
//  - Attraper : le ver est figé et tenu au bout du bec (BeakTip, visible à la première personne).
//  - Nourrir : un coup de bec vers un oisillon affamé lui donne le ver.
// Références, sons et HUD (Canvas "HUD" de la scène) sont assignés dans l'Inspector.
[RequireComponent(typeof(BirdFirstPersonController), typeof(AudioSource))]
public class BirdForaging : MonoBehaviour
{
    public int maxCarried = 3;
    public float beakReach = 2.2f;          // portée du bec depuis l'œil (cou + bec)
    public float carriedWormScale = 0.1f;   // les vers de la scène sont très grands

    public Camera eye;
    public Transform beakTip;
    public GameManager gm;
    public InputActionReference attackAct, interactAct;
    public AudioClip catchClip, feedClip, missClip;
    public Image reticle;                   // vert = action possible, jaune = impossible
    public TMP_Text hint, score;

    const float BeakCastRadius = 0.25f, PeckDuration = 0.22f, PeckCooldown = 0.35f, FeedTransferTime = 0.35f;

    enum Target { None, Worm, HungryChick, FedChick }

    BirdFirstPersonController bird;
    BirdBodyAnimator body;
    AudioSource sfx;
    Transform targetWorm;
    AnimateBabyBirds targetChick;
    Target target;
    readonly List<GameObject> carried = new List<GameObject>();
    readonly RaycastHit[] hits = new RaycastHit[16];
    float peckTimer = -1f, cooldown;

    void Start()
    {
        bird = GetComponent<BirdFirstPersonController>();
        body = GetComponent<BirdBodyAnimator>();
        sfx = GetComponent<AudioSource>();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        cooldown -= dt;
        UpdateTarget();
        UpdateHud();

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
        foreach (var a in new[] { attackAct.action, interactAct.action })
            if (a.WasPressedThisFrame() && !(mouseFree && a.activeControl?.device is Mouse)) return true;
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
        else sfx.PlayOneShot(missClip, 0.5f);   // coup dans le vide ou impossible : claquement de bec
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
        gm.scoreModify(1);
        sfx.PlayOneShot(catchClip);
    }

    void Feed(AnimateBabyBirds chick)
    {
        GameObject worm = carried[carried.Count - 1];
        carried.RemoveAt(carried.Count - 1);
        chick.wantFood = false;
        gm.feedBabyBirds(1);
        StartCoroutine(TransferWorm(worm.transform, chick.beak));
        sfx.PlayOneShot(feedClip);
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

    // Point de visée avec un mot d'aide, et une ligne de score
    void UpdateHud()
    {
        reticle.color = target == Target.None ? new Color(1f, 1f, 1f, 0.6f) : CanAct() ? Color.green : Color.yellow;
        reticle.rectTransform.sizeDelta = Vector2.one * (target == Target.None ? 4f : 8f);

        hint.text = target == Target.Worm ? (CanAct() ? "Attraper" : bird.IsAirborne ? "Posez-vous" : "Bec plein")
                  : target == Target.HungryChick ? (CanAct() ? "Nourrir" : "Rien dans le bec")
                  : target == Target.FedChick ? "Déjà nourri" : "";
        score.text = $"Bec : {carried.Count}/{maxCarried}   Oisillons nourris : {gm.getBabyBirdsScore()}/3";
    }
}
