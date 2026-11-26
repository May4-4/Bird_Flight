using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Panneau (minimap + scores) fixé en bas, un peu à gauche de la vue : enfant de la caméra, il suit la tête.
// On ne peut pas tourner la tête vers lui, seulement les yeux : il reste donc proche du centre
// (environ 8° à gauche, 19° en bas), avec la même position avec ou sans casque.
public class HudVR : MonoBehaviour
{
    public Transform head;      // caméra du casque
    public Vector3 positionVue = new Vector3(-0.12f, -0.31f, 0.90f);   // m : droite, haut, avant

    void Awake()
    {
        transform.SetParent(head, false);

        // Toujours dessiné par-dessus le décor (sinon le sol ou une branche le cache).
        // Les textes ont déjà un matériau "Overlay".
        Material overlay = new Material(Canvas.GetDefaultCanvasMaterial());
        overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        foreach (Image image in GetComponentsInChildren<Image>(true)) image.material = overlay;
    }

    void LateUpdate()
    {
        transform.localPosition = positionVue;
        transform.localRotation = Quaternion.LookRotation(positionVue);   // face aux yeux
    }
}