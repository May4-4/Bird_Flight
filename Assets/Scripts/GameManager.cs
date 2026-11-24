using UnityEngine;
using UnityEngine.SceneManagement;



public class GameManager : MonoBehaviour
{
    public AudioSource verSound;
    public AudioSource babyBirdSound;
    public AudioSource victorySound;
    public GameObject menuFin;   // optionnel : menu de fin dans la scène ; vide = chargement de la scène "fin"
    private int score;
    private int toBabyBirds;

    void Start()
    {
        score = 0;
        toBabyBirds = 0;
    }

    public void scoreModify(int value)
        { score += value;
        if (value>0 && verSound != null){verSound.Play();} }

    public int getScore()
    { return score; }

    public void feedBabyBirds(int value){
        score -= value;
        toBabyBirds += value;
        if (value>0 && babyBirdSound != null){babyBirdSound.Play();}
        if (toBabyBirds == 3){
            Debug.Log("VICTOIRE");
            if (victorySound != null) victorySound.Play();
            Invoke("LoadScene", 5f);

        }
        

    }

    public int getBabyBirdsScore()
    { return toBabyBirds; }

    public void LoadScene()
    {
        if (menuFin != null) menuFin.SetActive(true);
        else SceneManager.LoadScene(2);

    }
}