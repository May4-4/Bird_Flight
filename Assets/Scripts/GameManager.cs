using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{

    private int score;
    private int toBabyBirds;

    void Start()
    {
        score = 0;
        toBabyBirds = 0;
    }

    public void scoreModify(int value)
        { score += value; }

    public int getScore()
    { return score; }

    public void feedBabyBirds(int value){
        toBabyBirds += value;
                if (toBabyBirds == 3){
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