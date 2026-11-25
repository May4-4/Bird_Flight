using UnityEngine;

public class WormCollect : MonoBehaviour
{
    
    
    private GameManager GM;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GM = FindAnyObjectByType<GameManager>();
        if (GM == null) Debug.LogError("[Ver] GameManager absent de la scène");
    }

    private void OnTriggerEnter(Collider other)
    {
        if(!other.CompareTag("Worm")) return;

        if(GM.getScore() >= GameManager.maxVers)
        {
            Debug.Log("[Ver] touché mais bec plein");
        }
        else
        {
            Debug.Log("[Ver] attrapé à " + other.transform.position);
            
            GM.scoreModify(1);
            Destroy(other.gameObject);
        }
    }

}
