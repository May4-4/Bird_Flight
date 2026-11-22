using UnityEngine;

public class NestGiver : MonoBehaviour
{
    
    private GameManager GM;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GM = FindAnyObjectByType<GameManager>();
    }

    private void OnTriggerEnter(Collider other)
    //on pourra modifier la condition à GM.getScore()>1 si on fait le raycasting
    {   if (GM.getScore()==3){

        if(other.gameObject.CompareTag("BabyBird"))
        {
            if (other.GetComponent<AnimateBabyBirds>().wantFood){
            GM.feedBabyBirds(-1);
            other.GetComponent<AnimateBabyBirds>().wantFood = false;
            }
            
        }
       
    }
    }

}
