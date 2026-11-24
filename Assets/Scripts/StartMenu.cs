using UnityEngine;

// Menu de début affiché par-dessus la scène : le jeu est en pause jusqu'au clic sur Play.
// À placer sur le Canvas du menu ; les boutons appellent Play() et Quit().
public class StartMenu : MonoBehaviour
{
    void Awake()
    {
        Show(true);
    }

    public void Play()
    {
        Show(false);
    }

    public void Quit()
    {
        Application.Quit();
    }

    void Show(bool open)
    {
        gameObject.SetActive(open);
        Time.timeScale = open ? 0f : 1f;

        // L'oiseau ne réagit pas (et ne capture pas la souris) tant que le menu est ouvert
        var bird = FindAnyObjectByType<BirdFirstPersonController>(FindObjectsInactive.Include);
        if (bird != null) bird.enabled = !open;
        var foraging = FindAnyObjectByType<BirdForaging>(FindObjectsInactive.Include);
        if (foraging != null) foraging.enabled = !open;

        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }
}
