using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
public class AppManager : MonoBehaviour
{
    // Fonction a associer au clic du bouton Quit
    public void Quit()
    {
        Application.Quit();
    }

    // Fonction à associer au bouton Start
    public void LoadScene()
    {
        // L'id utilisé ici est celui de la scene list dans le build profile du projet.
        SceneManager.LoadScene(1);
    }
}
