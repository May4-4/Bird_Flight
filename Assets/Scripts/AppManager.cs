using UnityEngine;
using UnityEngine.SceneManagement;
public class AppManager : MonoBehaviour
{
    // Fonction a associer au clic du bouton Quit
    public void Quit()
    {
        Debug.Log("[Menu] Quit (sans effet dans l'éditeur)");
        Application.Quit();
    }

    // Fonction à associer au bouton Start
    public void LoadScene()
    {
        Debug.Log("[Menu] Play");
        // L'id utilisé ici est celui de la scene list dans le build profile du projet.
        SceneManager.LoadScene(1);
    }
}
