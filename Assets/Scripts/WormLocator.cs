using System.Collections.Generic;
using UnityEngine;

// Aide à trouver les vers depuis le ciel, bien au-delà de la portée du bec :
// une voix annonce "ver de terre à proximité" quand l'oiseau survole un ver.
// Fournit aussi la liste des vers à la minimap. Les vers déjà attrapés sont ignorés.
// Ajouté automatiquement par BirdForaging.
[RequireComponent(typeof(BirdFirstPersonController))]
public class WormLocator : MonoBehaviour
{
    public bool french = true;              // voix française, sinon anglaise ("Worm nearby!")
    public float voiceRadius = 20f;         // distance horizontale qui déclenche l'annonce (m)
    public float voiceCooldown = 6f;        // délai minimum entre deux annonces (s)

    const float PerWormCooldown = 25f, MaxHeightAboveWorm = 80f, RescanInterval = 1f;

    public List<Transform> Worms { get; } = new List<Transform>();

    BirdFirstPersonController bird;
    AudioClip voice;
    readonly Dictionary<Transform, float> lastVoice = new Dictionary<Transform, float>();
    float rescanTimer, lastVoiceTime = -999f;

    void Awake()
    {
        bird = GetComponent<BirdFirstPersonController>();
        voice = Resources.Load<AudioClip>(french ? "Audio/WormNearby_fr" : "Audio/WormNearby_en");
    }

    void Update()
    {
        // La liste des vers est rafraîchie une fois par seconde
        if ((rescanTimer -= Time.deltaTime) <= 0f)
        {
            rescanTimer = RescanInterval;
            Worms.Clear();
            foreach (var go in GameObject.FindGameObjectsWithTag("Worm")) Worms.Add(go.transform);
        }
        if (bird.IsAirborne) Announce();
    }

    // Un ver attrapé est rattaché à l'oiseau et n'a plus de collider
    public bool IsFree(Transform w)
    {
        return w != null && !w.IsChildOf(transform) && w.GetComponentInChildren<Collider>() != null;
    }

    // Annonce vocale du ver libre le plus proche, s'il est assez près et pas annoncé récemment
    void Announce()
    {
        if (voice == null || Time.time - lastVoiceTime < voiceCooldown) return;

        Transform nearest = null;
        float best = voiceRadius;
        foreach (var w in Worms)
        {
            if (!IsFree(w)) continue;
            Vector3 d = w.position - transform.position;
            float h = new Vector2(d.x, d.z).magnitude;
            if (-d.y <= MaxHeightAboveWorm && h < best) { best = h; nearest = w; }
        }
        if (nearest == null) return;
        if (lastVoice.TryGetValue(nearest, out float last) && Time.time - last < PerWormCooldown) return;

        lastVoice[nearest] = lastVoiceTime = Time.time;
        var go = new GameObject("Voix");
        go.AddComponent<AudioSource>().PlayOneShot(voice);
        Destroy(go, voice.length + 0.1f);
    }
}
