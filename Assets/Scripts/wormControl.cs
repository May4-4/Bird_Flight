using UnityEngine;

public class WormTipControl : MonoBehaviour
{
    private Rigidbody rb;
    private float timeElapsed;
    private float nextInterval;

    public float force = 5.0f;
    public float minInterval = 1.0f;
    public float maxInterval = 3.0f;     // intervalle aleatoire
    public float verticalDamping = 0.7f; // pour eviter que le bout parte trop vers le haut/bas

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        timeElapsed = 0f;
        nextInterval = Random.Range(minInterval, maxInterval);
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed > nextInterval)
        {
            Vector3 randomForce = new Vector3(
                Random.Range(-2.0f, 2.0f) * force,
                Random.Range(-2.0f, 2.0f) * force * verticalDamping,
                Random.Range(-2.0f, 2.0f) * force
            );

            rb.AddForce(randomForce, ForceMode.Impulse); // Impulse pour que ce soit par moments 
            timeElapsed = 0f;
            nextInterval = Random.Range(minInterval, maxInterval);
        }
    }
}