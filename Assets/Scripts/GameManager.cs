using UnityEngine;

public class GameManager : MonoBehaviour
{

    private int score;


    void Start()
    {
        score = 0;
    }

    public void scoreModify(int value)
        { score += value; }

    public int getScore()
    { return score; }
}