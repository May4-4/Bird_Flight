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
        // Unity sends trigger messages even to disabled scripts: respect the enabled flag
        // (the first-person bird catches worms with its beak instead, see BirdForaging).
        if (!enabled || GM == null) return;
        if(other.CompareTag("Worm"))
        {
            
            GM.scoreModify(1);
            Destroy(other.gameObject);
        }
    }

}
