using UnityEngine;

public class XRBirdController : MonoBehaviour
{
    [Header("Références XR")]

    public Transform head;
    public Transform leftHand;   // Left Controller
    public Transform rightHand;  // Right Controller

    [Header("Battement")]
    public float descenteThreshold = 0.2f;   // m/s : indique le seuil à partir ducquel on considère que ça descend
    public float minFlapRatio = 0.3f;         // longueur du bras minimum parcourue pour faire un battement sur 1
    public float fullFlapRatio = 1.0f;        // longueur du bras maximum parcourue pour faire un battement sur 1


    private float FlapStrength;

    [Header("Poussée")]
    public float PousseeDuration = 0.3f;    // durée de la poussée après un battement (s)
    public float takeoffUpForce = 30f;     // au sol
    public float flapUpForce = 20f; //en vol 
    public float flapForwardForce = 5f; //on se déplace un peu en avant à chaque battement

    [Header("Portance")]
    [Range(0f, 1f)]
    public float glideLiftRatio = 0.85f;   // portance max = 85 % du poids → descente lente
    public float glideSpeed = 8f;          // vitesse avant pour atteindre la portance max

    [Header("Traînée")]
    public float dragCoefficient = 0.08f;

    [Header("Vitesse")]
    public float maxForwardSpeed = 12f;


    [Header("Tourner")]
    public float turnSpeed = 50f;          // degrés/s au maximum
    public float minBendTreshold = 0.15f;     // 15 % du bras : en dessous, on ignore
    public float maxBendTreshold = 0.8f;      // 80 % du bras : virage maximal

private float isTurning;  

    private float armLength;
    private float lastLeftY, lastRightY;
    private bool armsGoingDown;
    private float startLeftY, startRightY;





    private Rigidbody rb;
    private bool isGrounded;

    public float maxPoussee;
    private float PousseeTimer;  





    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Calibration();
    }
    public void Calibration()
    {   
        armLength =  transform.InverseTransformPoint(head.position).y * 0.50f;
    }
    void Update()
    {
        
        float leftY  = transform.InverseTransformPoint(leftHand.position).y;
        float rightY = transform.InverseTransformPoint(rightHand.position).y;

        float leftVy  = (leftY  - lastLeftY)  / Time.deltaTime;
        float rightVy = (rightY - lastRightY) / Time.deltaTime;
        lastLeftY  = leftY;
        lastRightY = rightY;

        bool bothGoingDown = leftVy < -descenteThreshold && rightVy < -descenteThreshold;

        //si les deux sont en train de descendre et qu'on était pas encore en train de descendre:
         if (bothGoingDown && !armsGoingDown)
        {
            // Début de la descente
            armsGoingDown = true;
            startLeftY  = leftY;
            startRightY = rightY;
        }else if (!bothGoingDown && armsGoingDown)
        {
            // Fin de la descente : on juge le battement
            armsGoingDown = false;

            float amplitude = Mathf.Min(startLeftY - leftY, startRightY - rightY);
            float ratio = amplitude / armLength;   // amplitude relative à la taille

            if (ratio >= minFlapRatio)
            {
                //on calcule la force du battement entre le min et max possible 
                FlapStrength = Mathf.InverseLerp(minFlapRatio, fullFlapRatio, ratio);
                PousseeTimer = Mathf.Min(PousseeTimer + FlapStrength, maxPoussee);
                
            }
        }

        float bendRatio = (leftY - rightY) / armLength;
        float absbendRatio = Mathf.Abs(bendRatio);

        if (absbendRatio < minBendTreshold)
        {
            isTurning = 0f;
        }
        else
            isTurning = Mathf.Sign(bendRatio) * Mathf.InverseLerp(minBendTreshold, maxBendTreshold, absbendRatio);
            }

    void FixedUpdate()
{
    if (PousseeTimer > 0f)
    {
        ApplyPoussee();
        // Se vide en thrustDuration pour une réserve de 1
        PousseeTimer = Mathf.MoveTowards(PousseeTimer, 0f, Time.fixedDeltaTime / PousseeDuration);
        ApplyDrag();
        ApplyLift();
        LimitForwardSpeed();
        Turn();
    }
}

void ApplyPoussee()
{
    Vector3 force = isGrounded
        ? Vector3.up * takeoffUpForce + transform.forward * flapForwardForce
        : Vector3.up * flapUpForce    + transform.forward * flapForwardForce;

    rb.AddForce(force * PousseeTimer, ForceMode.Force);
}

void ApplyLift()
{
    if (isGrounded) return;

    // Seule la vitesse vers l'avant crée de la portance
    float forwardSpeed = Mathf.Max(0f, Vector3.Dot(rb.linearVelocity, transform.forward));
    float liftRatio = Mathf.Clamp01(forwardSpeed / glideSpeed);

    float weight = rb.mass * Physics.gravity.magnitude;
    float lift = weight * glideLiftRatio * liftRatio * liftRatio;

    rb.AddForce(Vector3.up * lift, ForceMode.Force);
}

void ApplyDrag()
{
    Vector3 v = rb.linearVelocity;
    if (v.sqrMagnitude < 0.01f) return;

    // Opposée au mouvement, proportionnelle à la vitesse au carré
    rb.AddForce(-v.normalized * v.sqrMagnitude * dragCoefficient, ForceMode.Force);
}

void LimitForwardSpeed()
{
    Vector3 v = rb.linearVelocity;
    float forwardSpeed = Vector3.Dot(v, transform.forward);
    if (forwardSpeed <= maxForwardSpeed) return;

    // On retire seulement l'excès vers l'avant
    rb.linearVelocity = v - transform.forward * (forwardSpeed - maxForwardSpeed);
}

void Turn()
{
    if (isGrounded || isTurning == 0f) return;

    float yaw = isTurning * turnSpeed * Time.fixedDeltaTime;
    rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yaw, 0f));
}


   // ==================================================
    // SOL
    // ==================================================
    void OnCollisionEnter(Collision c) { if (c.gameObject.CompareTag("Ground")) isGrounded = true; }
    void OnCollisionStay(Collision c)  { if (c.gameObject.CompareTag("Ground")) isGrounded = true; }
    void OnCollisionExit(Collision c)  { if (c.gameObject.CompareTag("Ground")) isGrounded = false; }
    
}