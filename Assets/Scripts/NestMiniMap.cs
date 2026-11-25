using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Minimap ronde (reprise de la branche jeu_pc) pour retrouver le nid des oisillons :
//  - centrée sur l'oiseau, ce qui est devant lui est en haut de la carte ;
//  - l'arbre du nid est affiché ; hors de portée, il reste collé au bord avec une flèche vers lui ;
//  - au-dessus : distance horizontale du nid et différence de hauteur ;
//  - touche M (PC) ou gâchette gauche (VR, zoomAction) : zoom (150 / 400 / 1000 m).
// L'interface est dans la scène (Canvas "Minimap") ; ce script ne fait que placer les icônes.
public class NestMiniMap : MonoBehaviour
{
    public Transform nest;
    public RectTransform map, tree, arrow;
    public TMP_Text info;
    public InputActionReference zoomAction;   // bouton de manette, optionnel

    static readonly float[] Zooms = { 150f, 400f, 1000f };

    int zoom = 1;
    float radius;

    void Start()
    {
        radius = map.rect.width * 0.5f;
    }

    void Update()
    {
        bool touche = Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
        bool manette = zoomAction != null && zoomAction.action.WasPressedThisFrame();
        if (touche || manette)
        {
            zoom = (zoom + 1) % Zooms.Length;
            Debug.Log("[Carte] zoom " + Zooms[zoom] + " m");
        }
    }

    void LateUpdate()
    {
        float heading = transform.eulerAngles.y;

        // Le nid sur la carte, ou collé au bord avec une flèche s'il est trop loin
        Vector2 delta = new Vector2(nest.position.x - transform.position.x, nest.position.z - transform.position.z);
        Vector2 p = ToMap(delta, heading) / Zooms[zoom] * radius;
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

    // Décalage horizontal (monde XZ) -> position sur la carte, l'avant de l'oiseau en haut
    static Vector2 ToMap(Vector2 d, float headingDeg)
    {
        float h = headingDeg * Mathf.Deg2Rad;
        return new Vector2(d.x * Mathf.Cos(h) - d.y * Mathf.Sin(h), d.x * Mathf.Sin(h) + d.y * Mathf.Cos(h));
    }
}
