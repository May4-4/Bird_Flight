using UnityEngine;

public class AnimateBabyBirds : MonoBehaviour
{   
    public float flapFrequency = 4f;
    public float flapAngle = 45f;
    public bool wantFood = true;
    public Transform leftWingPivot;
    public Transform rightWingPivot;
    private float flapTimer = 0f;
    private GameManager GM;
    private AudioSource audio;

    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GM = FindAnyObjectByType<GameManager>();
        audio = gameObject.GetComponent<AudioSource>();
  
        if(wantFood){
        float length = audio.clip.length;

        float offset = Random.Range(0f, length);
           

        audio.time = offset;
            Sound();}

    }

    // Update is called once per frame
    void Update()
    {
        if (wantFood){AnimateWings();
        }else{MuteSound();}
        
    }

     void AnimateWings()
    {
       
            flapTimer +=
                Time.deltaTime
                * flapFrequency
                * Mathf.PI
                * 2f;

            float angle =
                Mathf.Sin(flapTimer)
                * flapAngle;

            leftWingPivot.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle
                );

            rightWingPivot.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    -angle
                );
        }
    void Sound(){
         Debug.Log(
           "play sound"
        );
        audio.Play();

    }

    void MuteSound(){
        audio.Stop();
    }
        
    
}
