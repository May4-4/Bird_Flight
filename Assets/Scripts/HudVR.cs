using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Panneau (minimap + scores) attaché à la tête, dans le coin bas gauche de la vue.
// Sans casque (vue Game), le panneau se cale dans le coin bas gauche de l'écran.
public class HudVR : MonoBehaviour
{
    public Transform head;      // caméra du casque
    public Vector3 offset = new Vector3(-0.32f, -0.22f, 0.65f);   // m : droite, haut, avant

    const float Marge = 0.02f;  // m, entre le panneau et le bord de la vue Game

    Camera cam;

    void Awake()
    {
        cam = head.GetComponent<Camera>();
        transform.SetParent(head, false);   // attaché à la tête

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
            transform.localPosition = offset;
            transform.localRotation = Quaternion.LookRotation(offset);   // face aux yeux
        }
        else
        {
            transform.localPosition = OffsetSansCasque();
            transform.localRotation = Quaternion.identity;
        }
    }

    // Collé au coin bas gauche de ce que la caméra affiche
    Vector3 OffsetSansCasque()
    {
        Vector2 demi = ((RectTransform)transform).rect.size * transform.localScale.x * 0.5f;
        float hauteur = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * offset.z;
        float largeur = hauteur * cam.aspect;
        return new Vector3(
            demi.x + Marge - largeur,
            demi.y + Marge - hauteur,
            offset.z);
    }
}