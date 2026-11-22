using UnityEngine;

public class NestGiver : MonoBehaviour
{
    
    private GameManager GM;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GM = FindAnyObjectByType<GameManager>();
    }

//on trigger stay au lieu de enter comme ça quand on est dans la zonne ça reste.
    private void OnTriggerStay(Collider other)
    
    {   if (GM.getScore()>0){

        if(other.gameObject.CompareTag("BabyBird"))
        {
            if (other.GetComponent<AnimateBabyBirds>().wantFood){
            GM.feedBabyBirds(1);
            other.GetComponent<AnimateBabyBirds>().wantFood = false;
            }
            
        }
       
    }
    }

}
