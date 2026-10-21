using UnityEngine;

public class WormCollect : MonoBehaviour
{
    
    
    private GameManager GM;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GM = FindAnyObjectByType<GameManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Worm"))
        {
            
            GM.scoreModify(1);
            Destroy(other.gameObject);
        }
    }

}
