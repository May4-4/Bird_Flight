using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Panneau (minimap + scores) affiché dans le casque, dans le coin bas droit de la vue.
// Il suit la position de la tête mais l'orientation de l'oiseau, pas celle du regard :
// il reste hors du champ central et on tourne les yeux vers lui pour le lire.
// Sans casque (vue Game), le champ est plus étroit : le panneau se cale dans le coin de l'écran.
public class HudVR : MonoBehaviour
{
    public Transform head;      // caméra du casque
    public Transform origin;    // XR Origin
    public Vector3 offset = new Vector3(0.42f, -0.30f, 0.65f);   // m : droite, haut, avant

    const float Marge = 0.02f;  // m, entre le panneau et le bord de la vue Game

    Camera cam;

    void Awake()
    {
        cam = head.GetComponent<Camera>();

        // Toujours dessiné par-dessus le décor (sinon le sol ou une branche le cache).
        // Les textes ont déjà un matériau "Overlay".
        Material overlay = new Material(Canvas.GetDefaultCanvasMaterial());
        overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        foreach (Image image in GetComponentsInChildren<Image>(true)) image.material = overlay;
    }

    void LateUpdate()
    {
        if (cam.stereoEnabled)
        {
            transform.position = head.position + origin.rotation * offset;
            transform.rotation = Quaternion.LookRotation(transform.position - head.position, origin.up);
        }
        else
        {
            transform.position = head.position + origin.rotation * OffsetSansCasque();
            transform.rotation = origin.rotation;
        }
    }

    // Collé au coin bas droit de ce que la caméra affiche
    Vector3 OffsetSansCasque()
    {
        Vector2 demi = ((RectTransform)transform).rect.size * transform.localScale.x * 0.5f;
        float hauteur = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * offset.z;
        float largeur = hauteur * cam.aspect;
        return new Vector3(
            largeur - demi.x - Marge,
            demi.y + Marge - hauteur,
            offset.z);
    }
}
