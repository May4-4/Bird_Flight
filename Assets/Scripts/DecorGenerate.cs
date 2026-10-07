using UnityEngine;
using System.Collections.Generic;
public class DecorGenerate : MonoBehaviour
{
    public GameObject[] trees;

    public float minScale = 1;
    public float maxScale = 4;
    public int count = 30;
    public GameObject[] notAround;
    public float distance = 10;
    public int maxAttempts = 30;
    private List<(Vector3 pos, float radius)> placed = new List<(Vector3, float)>();

    private Bounds groundBounds;

    void Start()
    {
        groundBounds = GameObject.Find("Ground").GetComponent<Renderer>().bounds;

        foreach (GameObject tree in trees)
        {
            for (int i = 0; i < count; i++)
            {
                GenerateTree(tree);
            }
        }
    }

    void GenerateTree(GameObject prefab)
    {
        // mise a l'echelle + rotation
        float scale = Random.Range(minScale, maxScale);
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject obj = Instantiate(prefab, Vector3.zero, rotation, transform);
        obj.transform.localScale = Vector3.one * scale;

        // Taille reelle de l'arbre 
        Vector3 size = GetBounds(obj).size;
        float offsetX = size.x / 2f;
        float offsetZ = size.z / 2f;
        float radius = Mathf.Max(offsetX, offsetZ);

        // On essaie plusieurs positions jusqu'a en trouver une valide
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 pos = new Vector3(
                Random.Range(groundBounds.min.x + offsetX, groundBounds.max.x - offsetX),
                groundBounds.max.y,
                Random.Range(groundBounds.min.z + offsetZ, groundBounds.max.z - offsetZ));

            if (IsFarEnough(pos) && !TouchesOtherTree(pos, radius))
            {
                obj.transform.position = pos;
                placed.Add((pos, radius));
                return;
            }
        }

        // Aucune place trouvee : on supprime l'arbre
        Destroy(obj);
    }

    //verification qu'on n'est pas sur un objet autour duquel il doit y avoir de la distance
    bool IsFarEnough(Vector3 pos)
    {
        foreach (GameObject other in notAround)
        {
            if (other == null) continue;

            // Distance sur le plan (on ignore la hauteur)
            Vector3 delta = other.transform.position - pos;
            delta.y = 0f;
            if (delta.magnitude < distance) return false;
        }
        return true;
    }

     bool TouchesOtherTree(Vector3 pos, float radius)
    {
        foreach (var other in placed)
        {
            Vector3 delta = other.pos - pos;
            delta.y = 0f;
            if (delta.magnitude < radius + other.radius) return true;
        }
        return false;
    }

    Bounds GetBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}