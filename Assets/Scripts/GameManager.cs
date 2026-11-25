using UnityEngine;
using UnityEngine.SceneManagement;



public class GameManager : MonoBehaviour
{
    public AudioSource verSound;
    public AudioSource babyBirdSound;
    public AudioSource victorySound;
    public const int maxVers = 3;       // vers tenus en même temps dans le bec
    public const int nbOisillons = 3;   // oisillons à nourrir pour gagner
    private int score;
    private int toBabyBirds;

    void Start()
    {
        score = 0;
        toBabyBirds = 0;
    }

    public void scoreModify(int value)
        { score += value;
        Debug.Log("[Score] vers dans le bec : " + score + "/" + maxVers);
        if (value>0 && verSound != null){verSound.Play();} }

    public int getScore()
    { return score; }

    public void feedBabyBirds(int value){
        score -= value;
        toBabyBirds += value;
        Debug.Log("[Score] oisillons nourris : " + toBabyBirds + "/" + nbOisillons + ", vers restants : " + score);
        if (value>0 && babyBirdSound != null){babyBirdSound.Play();}
        if (toBabyBirds == nbOisillons){
            Debug.Log("[Score] VICTOIRE, scène de fin dans 5 s");
            if (victorySound != null) victorySound.Play();
            Invoke("LoadScene", 5f);

        }
        

    }

    public int getBabyBirdsScore()
    { return toBabyBirds; }

    public void LoadScene()
    {
        Debug.Log("[Scène] chargement de la fin");
        SceneManager.LoadScene(2);
    
    }
}