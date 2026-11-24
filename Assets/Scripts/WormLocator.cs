using System.Collections.Generic;
using UnityEngine;

// Aide à trouver les vers depuis le ciel, bien au-delà de la portée du bec :
// une voix annonce "ver de terre à proximité" quand l'oiseau survole un ver.
// Les vers déjà attrapés sont ignorés.
// La voix (WormNearby_fr ou WormNearby_en dans Assets/audio) est assignée dans l'Inspector.
[RequireComponent(typeof(BirdFirstPersonController), typeof(AudioSource))]
public class WormLocator : MonoBehaviour
{
    public AudioClip voice;
    public float voiceRadius = 20f;         // distance horizontale qui déclenche l'annonce (m)
    public float voiceCooldown = 6f;        // délai minimum entre deux annonces (s)

    const float PerWormCooldown = 25f, MaxHeightAboveWorm = 80f, RescanInterval = 1f;

    BirdFirstPersonController bird;
    readonly List<Transform> worms = new List<Transform>();
    readonly Dictionary<Transform, float> lastVoice = new Dictionary<Transform, float>();
    float rescanTimer, lastVoiceTime = -999f;

    void Awake()
    {
        bird = GetComponent<BirdFirstPersonController>();
    }

    void Update()
    {
        // La liste des vers est rafraîchie une fois par seconde
        if ((rescanTimer -= Time.deltaTime) <= 0f)
        {
            rescanTimer = RescanInterval;
            worms.Clear();
            foreach (var go in GameObject.FindGameObjectsWithTag("Worm")) worms.Add(go.transform);
        }
        if (bird.IsAirborne) Announce();
    }

    // Un ver attrapé est rattaché à l'oiseau et n'a plus de collider
    bool IsFree(Transform w)
    {
        return w != null && !w.IsChildOf(transform) && w.GetComponentInChildren<Collider>() != null;
    }

    // Annonce vocale du ver libre le plus proche, s'il est assez près et pas annoncé récemment
    void Announce()
    {
        if (Time.time - lastVoiceTime < voiceCooldown) return;

        Transform nearest = null;
        float best = voiceRadius;
        foreach (var w in worms)
        {
            if (!IsFree(w)) continue;
            Vector3 d = w.position - transform.position;
            float h = new Vector2(d.x, d.z).magnitude;
            if (-d.y <= MaxHeightAboveWorm && h < best) { best = h; nearest = w; }
        }
        if (nearest == null) return;
        if (lastVoice.TryGetValue(nearest, out float last) && Time.time - last < PerWormCooldown) return;

        lastVoice[nearest] = lastVoiceTime = Time.time;
        GetComponent<AudioSource>().PlayOneShot(voice);
    }
}
