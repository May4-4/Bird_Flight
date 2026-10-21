using UnityEngine;
using TMPro;


public class UIScore : MonoBehaviour
{
    private TMP_Text  m_TextMeshPro;
    private GameManager GM;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // On r�cup�re le component "TextMeshPro - Text (UI)"
        m_TextMeshPro = GetComponent<TMP_Text>();
        GM = FindAnyObjectByType<GameManager>();
    }

    // Update is called once per frame
    void Update()
    {
       m_TextMeshPro.text = "Score : " + GM.getScore();
    }
}
