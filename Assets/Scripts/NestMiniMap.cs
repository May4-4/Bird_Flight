using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Minimap ronde en bas à droite pour retrouver le nid des oisillons :
//  - centrée sur l'oiseau, ce qui est devant lui est en haut de la carte ;
//  - l'arbre du nid est affiché ; hors de portée, il reste collé au bord avec une flèche vers lui ;
//  - au-dessus : distance horizontale du nid et différence de hauteur ;
//  - points roses : vers libres ; "N" : le nord ; touche M : zoom (150 / 400 / 1000 m).
// L'interface est construite au lancement (rien à régler dans la scène). Ajoutée par BirdForaging.
public class NestMiniMap : MonoBehaviour
{
    public float mapSize = 220f;            // diamètre en pixels (écran de référence 1920x1080)

    static readonly float[] Zooms = { 150f, 400f, 1000f };
    const int MaxWormDots = 60;

    Transform nest;
    WormLocator locator;
    RectTransform map, tree, arrow, north;
    Text info;
    readonly List<RectTransform> wormDots = new List<RectTransform>();
    int zoom = 1;
    float radius;

    void Start()
    {
        // Le nid = parent des oisillons
        var chick = FindAnyObjectByType<AnimateBabyBirds>();
        if (chick != null) nest = chick.transform.parent != null ? chick.transform.parent : chick.transform;
        locator = GetComponent<WormLocator>();
        BuildUI();
    }

    void BuildUI()
    {
        var canvas = new GameObject("Minimap").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;

        // Cadre ancré en bas à droite
        map = NewRect("Carte", canvas.transform, mapSize);
        map.anchorMin = map.anchorMax = map.pivot = new Vector2(1f, 0f);
        map.anchoredPosition = new Vector2(-24f, 24f);
        radius = mapSize * 0.5f;

        Texture2D disc = Circle(128, false), ring = Circle(128, true), triangle = Triangle(64);
        AddImage(disc, new Color(0.12f, 0.22f, 0.12f, 0.75f), mapSize);
        AddImage(ring, new Color(1f, 1f, 1f, 0.85f), mapSize);
        for (int i = 0; i < MaxWormDots; i++)
        {
            var dot = AddImage(disc, new Color(1f, 0.55f, 0.55f), 6f);
            dot.gameObject.SetActive(false);
            wormDots.Add(dot);
        }
        north = AddLabel("N", 16, 24f);
        arrow = AddImage(triangle, new Color(1f, 0.55f, 0.15f), 16f);
        tree = AddImage(Resources.Load<Texture2D>("MiniMap/TreeIcon"), Color.white, 36f);
        AddImage(Resources.Load<Texture2D>("MiniMap/BirdIcon"), Color.white, 26f);       // l'oiseau, au centre
        AddImage(triangle, Color.white, 10f).anchoredPosition = new Vector2(0f, 20f);      // l'avant de l'oiseau

        info = AddLabel("", 16, mapSize + 60f).GetComponent<Text>();
        info.rectTransform.anchoredPosition = new Vector2(0f, radius + 18f);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) zoom = (zoom + 1) % Zooms.Length;
    }

    void LateUpdate()
    {
        if (map == null) return;
        float range = Zooms[zoom];
        float heading = transform.eulerAngles.y;

        north.anchoredPosition = ToMap(Vector2.up, heading) * (radius - 12f);

        if (nest != null)
        {
            // Le nid sur la carte, ou collé au bord avec une flèche s'il est trop loin
            Vector2 delta = new Vector2(nest.position.x - transform.position.x, nest.position.z - transform.position.z);
            Vector2 p = ToMap(delta, heading) / range * radius;
            bool outside = p.magnitude > radius - 20f;
            if (outside) p = p.normalized * (radius - 20f);
            tree.anchoredPosition = p;
            arrow.gameObject.SetActive(outside);
            if (outside)
            {
                arrow.anchoredPosition = p.normalized * (radius + 10f);
                arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg - 90f);
            }

            float dh = nest.position.y - transform.position.y;
            info.text = $"Nid : {delta.magnitude:F0} m" + (dh > 3f ? $"   ▲ {dh:F0} m" : dh < -3f ? $"   ▼ {-dh:F0} m" : "");
        }

        // Vers libres à portée
        int used = 0;
        if (locator != null)
            foreach (var w in locator.Worms)
            {
                if (used >= wormDots.Count || !locator.IsFree(w)) continue;
                Vector2 p = ToMap(new Vector2(w.position.x - transform.position.x, w.position.z - transform.position.z), heading) / range * radius;
                if (p.magnitude > radius - 5f) continue;
                wormDots[used].gameObject.SetActive(true);
                wormDots[used++].anchoredPosition = p;
            }
        for (int i = used; i < wormDots.Count; i++) wormDots[i].gameObject.SetActive(false);
    }

    // Décalage horizontal (monde XZ) -> position sur la carte, l'avant de l'oiseau en haut
    static Vector2 ToMap(Vector2 d, float headingDeg)
    {
        float h = headingDeg * Mathf.Deg2Rad;
        return new Vector2(d.x * Mathf.Cos(h) - d.y * Mathf.Sin(h), d.x * Mathf.Sin(h) + d.y * Mathf.Cos(h));
    }

    // ---------------------------------------------------------------- construction de l'interface
    static RectTransform NewRect(string name, Transform parent, float size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        return rt;
    }

    RectTransform AddImage(Texture tex, Color color, float size)
    {
        var rt = NewRect("Image", map, size);
        var img = rt.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    RectTransform AddLabel(string text, int fontSize, float width)
    {
        var rt = NewRect("Texte", map, width);
        rt.sizeDelta = new Vector2(width, 24f);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = text;
        t.raycastTarget = false;
        rt.gameObject.AddComponent<Shadow>();
        return rt;
    }

    // Disque (ou anneau) blanc anti-aliasé
    static Texture2D Circle(int size, bool ringOnly)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size, size) * 0.5f);
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (ringOnly) a *= Mathf.Clamp01(d - r * 0.92f + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    // Triangle blanc pointant vers le haut
    static Texture2D Triangle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float halfWidth = (1f - (float)y / size) * size * 0.5f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(halfWidth - Mathf.Abs(x + 0.5f - size * 0.5f) + 0.5f)));
            }
        tex.Apply();
        return tex;
    }
}
