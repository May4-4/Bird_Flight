using UnityEngine;
using UnityEngine.SceneManagement;



public class GameManager : MonoBehaviour
{
    public AudioSource verSound;
    public AudioSource babyBirdSound;
    public AudioSource victorySound;
    private int score;
    private int toBabyBirds;

    void Start()
    {
        score = 0;
        toBabyBirds = 0;
    }

    public void scoreModify(int value)
        { score += value;
        if (value>0){verSound.Play();} }

    public int getScore()
    { return score; }

    public void feedBabyBirds(int value){
        score -= value;
        toBabyBirds += value;
        if (value>0){babyBirdSound.Play();}
        if (toBabyBirds == 3){
            Debug.Log("VICTOIRE");
            victorySound.Play();
            Invoke("LoadScene", 5f);

        }
        

    }

    public int getBabyBirdsScore()
    { return toBabyBirds; }

    public void LoadScene()
    {
        SceneManager.LoadScene(2);
    
    }
}