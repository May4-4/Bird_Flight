using UnityEngine;

public class WormGenerator : MonoBehaviour
{
    public GameObject worm;
    private Bounds groundBounds;
    private Bounds wormBounds;
    public int maxWorm = 10;
    private float wormOffsetX, wormOffsetZ;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
        groundBounds = GameObject.Find("Ground").GetComponent<Renderer>().bounds;
        wormBounds = worm.GetComponent<Renderer>().bounds;
        Debug.Log(wormBounds);
        wormOffsetX = wormBounds.size.x / 2f;
        wormOffsetZ = wormBounds.size.z / 2f;
        for (int i = 0;i < maxWorm; i++)
        {
            createWorm();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void createWorm()
    {
        Instantiate(worm, new Vector3(Random.Range(groundBounds.min.x+wormOffsetX, groundBounds.max.x-wormOffsetX), 0.5f, Random.Range(groundBounds.min.z+wormOffsetZ, groundBounds.max.z-wormOffsetZ)), transform.rotation);
        
    }


}