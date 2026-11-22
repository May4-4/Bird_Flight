using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.InputSystem;


public class MiniMap : MonoBehaviour
{
    public GameObject carte;
    public RectTransform zoneCarte;
    public RectTransform icone_oiseau;
    public RectTransform icone_arbre;

    public Transform arbre;
    public Transform bird;
    public InputActionReference openMapAction; 
    private Vector2 vect_arbre;

    public float tailleMonde;

    private bool isOpen = false;

    void OnEnable()  => openMapAction.action.performed += OnOpenMap;
    void OnDisable() => openMapAction.action.performed -= OnOpenMap;

        void Start()
    {
        vect_arbre = new Vector2(arbre.position.x, arbre.position.z);
        PlacerIcone(icone_arbre, arbre, vect_arbre);
        PlacerIcone(icone_oiseau, bird, vect_arbre);
        //TODO ajouter close ici quand j'aurais finit les tests
        
    }

    void OnOpenMap(InputAction.CallbackContext ctx)

{
    isOpen = !isOpen;
    if (isOpen ){
            OpenMap();
        }
    else{CloseMap();}
}
void Update()
{
    if (isOpen) MettreAJour();
}
void OpenMap()
    {
        carte.SetActive(true);
    }
void CloseMap()
    {
        carte.SetActive(false);
    }

void MettreAJour()
    {
        
        PlacerIcone(icone_oiseau, bird, vect_arbre);
    }


void PlacerIcone(RectTransform icone, Transform cible, Vector2 centre)
    {
       
        Vector2 delta = new Vector2(cible.position.x, cible.position.z) - centre;
        Vector2 norm = new Vector2(delta.x / tailleMonde, delta.y / tailleMonde);

        // Reste collé au bord si hors de la carte
        norm.x = Mathf.Clamp(norm.x, -0.5f, 0.5f);
        norm.y = Mathf.Clamp(norm.y, -0.5f, 0.5f);

        Vector2 taille = zoneCarte.rect.size;
        icone.anchoredPosition = new Vector2(norm.x * taille.x, norm.y * taille.y);
        Debug.Log($"delta={delta} norm={norm} taille={zoneCarte.rect.size}");
    }
}

