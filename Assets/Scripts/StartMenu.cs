using UnityEngine;
using UnityEngine.SceneManagement;

// Menu affiché par-dessus la scène (début ou fin) : le jeu est en pause tant qu'il est visible.
// À placer sur le Canvas du menu. Menu de début : Canvas actif dans la scène. Menu de fin : Canvas
// désactivé, que GameManager active à la victoire. Les boutons appellent Play(), Replay() ou Quit().
public class StartMenu : MonoBehaviour
{
    // Scripts désactivés tant que le menu est ouvert (l'oiseau ne réagit pas et ne capture pas la souris)
    public Behaviour[] pausedWhileOpen;

    void OnEnable()
    {
        Pause(true);
    }

    public void Play()
    {
        Pause(false);
        gameObject.SetActive(false);
    }

    // Recommence la partie (la scène doit être dans File > Build Profiles > Scene List)
    public void Replay()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameObject.scene.name);
    }

    public void Quit()
    {
        Application.Quit();
    }

    void Pause(bool open)
    {
        Time.timeScale = open ? 0f : 1f;
        foreach (var b in pausedWhileOpen) b.enabled = !open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }
}
