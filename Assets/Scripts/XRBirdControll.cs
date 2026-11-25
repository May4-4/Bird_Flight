using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class XRBirdController : MonoBehaviour
{
    [Header("Références XR")]

    public Transform head;
    public Transform leftHand;   // Left Controller
    public Transform rightHand;  // Right Controller

    public Transform cameraOffset;

    public float birdEyeHeight = 0.2f; // hauteur de l'oiseau pour qu'on voit à travers ses yeux

    [Header("Battement")]
    public float descenteThreshold = 0.2f;   // m/s : indique le seuil à partir duquel on considère que ça descend
    public float minFlapRatio = 0.3f;        // longueur du bras minimum parcourue pour faire un battement sur 1
    public float fullFlapRatio = 1.0f;       // longueur du bras maximum parcourue pour faire un battement sur 1

    private float FlapStrength;

    [Header("Poussée")]
    public float PousseeDuration = 0.3f;     // durée de la poussée après un battement (s)
    public float takeoffUpForce = 30f;       // au sol
    public float flapUpForce = 20f;          // en vol
    public float flapForwardForce = 5f;      // on se déplace un peu en avant à chaque battement

    [Header("Portance")]
    [Range(0f, 1f)]
    public float glideLiftRatio = 0.85f;     // portance max = 85 % du poids → descente lente
    public float glideSpeed = 8f;            // vitesse avant pour atteindre la portance max

    [Header("Traînée")]
    public float dragCoefficient = 0.08f;

    [Header("Vitesse")]
    public float maxForwardSpeed = 12f;

    [Header("Tourner")]
    public float turnSpeed = 50f;            // degrés/s au maximum
    public float minBendTreshold = 0.15f;    // 15 % du bras : en dessous, on ignore
    public float maxBendTreshold = 0.8f;     // 80 % du bras : virage maximal

    [Header("Descente")]
    public float vitesseDescente = 1.5f;

    private float isTurning;

    private float armLength;
    private float lastLeftY, lastRightY;
    private bool armsGoingDown;
    private float startLeftY, startRightY;

    private Rigidbody rb;
    private bool isGrounded;

    public float maxPoussee = 5;
    private float PousseeTimer;
    private Vector3 flyFast;

    private bool calibrated;

    private float lift;

    private const float MinArmLength = 0.35f;   // m, en dessous la mesure est fausse
    private float offsetInitial;                // hauteur de Camera Offset avant la première calibration
    private bool wasGrounded;
    private float lastTurnSign;
    private string surface = "";

    IEnumerator Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;  // sinon l'oiseau traverse le décor
        rb.useGravity = false;   // la descente est gérée par ApplyDescente
        rb.constraints = RigidbodyConstraints.FreezeRotation;   // un choc ne fait pas tourner le casque
        rb.interpolation = RigidbodyInterpolation.Interpolate;  // moins de saccades en VR
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.sleepThreshold = 0f;  // sinon OnCollisionStay s'arrête quand l'oiseau est posé

        // Horizon à plat : incliné, il roulerait à chaque virage
        Quaternion droit = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (Quaternion.Angle(transform.rotation, droit) > 0.1f)
        {
            Debug.LogWarning("[Oiseau] XR Origin était incliné (" + transform.eulerAngles + "), remis à plat");
            transform.rotation = droit;
            rb.rotation = droit;
        }

        yield return new WaitForSeconds(0.5f);

        // Avec un casque : on attend que la tête soit suivie (10 s au plus)
        float attente = 0f;
        while (XRSettings.isDeviceActive && head.localPosition == Vector3.zero && attente < 10f)
        {
            attente += Time.deltaTime;
            yield return null;
        }
        offsetInitial = cameraOffset.localPosition.y;
        Calibration();
    }

    public void Calibration()
    {
        float Height = head.localPosition.y;
        armLength = (Height + offsetInitial) * 0.50f;
        if (armLength < MinArmLength)
        {
            Debug.LogWarning("[Calibration] longueur de bras " + armLength.ToString("F2") + " m trop petite (casque non suivi ?), remplacée par " + MinArmLength);
            armLength = MinArmLength;
        }

        Vector3 p = cameraOffset.localPosition;
        p.y = birdEyeHeight - Height;   // l'œil arrive à birdEyeHeight, quelle que soit la taille du joueur
        cameraOffset.localPosition = p;

        lastLeftY = transform.InverseTransformPoint(leftHand.position).y;
        lastRightY = transform.InverseTransformPoint(rightHand.position).y;

        armsGoingDown = false;
        calibrated = true;

        Debug.Log("[Calibration] casque actif=" + XRSettings.isDeviceActive + " hauteur tête=" + Height.ToString("F2")
            + " bras=" + armLength.ToString("F2") + " offset caméra=" + p.y.ToString("F2")
            + " mains Y=" + lastLeftY.ToString("F2") + "/" + lastRightY.ToString("F2"));
        if (UnityEngine.InputSystem.XR.XRController.leftHand == null) Debug.LogWarning("[Calibration] manette GAUCHE non détectée");
        if (UnityEngine.InputSystem.XR.XRController.rightHand == null) Debug.LogWarning("[Calibration] manette DROITE non détectée");
    }

    void Update()
    {
        if (!calibrated) return;

        // Touche C du PC : recalibrer (casque mis après le lancement, recentrage SteamVR, autre joueur)
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) Calibration();

        float leftY = transform.InverseTransformPoint(leftHand.position).y;
        float rightY = transform.InverseTransformPoint(rightHand.position).y;

        float leftVy = (leftY - lastLeftY) / Time.deltaTime;
        float rightVy = (rightY - lastRightY) / Time.deltaTime;
        lastLeftY = leftY;
        lastRightY = rightY;

        bool bothGoingDown = leftVy < -descenteThreshold && rightVy < -descenteThreshold;

        // si les deux sont en train de descendre et qu'on était pas encore en train de descendre :
        if (bothGoingDown && !armsGoingDown)
        {
            // Début de la descente
            armsGoingDown = true;
            startLeftY = leftY;
            startRightY = rightY;
        }
        else if (!bothGoingDown && armsGoingDown)
        {
            // Fin de la descente : on juge le battement
            armsGoingDown = false;

            float amplitude = Mathf.Min(startLeftY - leftY, startRightY - rightY);
            float ratio = amplitude / armLength;   // amplitude relative à la taille

            if (ratio >= minFlapRatio)
            {
                // on calcule la force du battement entre le min et max possible
                FlapStrength = Mathf.InverseLerp(minFlapRatio, fullFlapRatio, ratio);
                PousseeTimer = Mathf.Min(PousseeTimer + FlapStrength, maxPoussee);
                Debug.Log("[Battement] ratio=" + ratio.ToString("F2") + " force=" + FlapStrength.ToString("F2")
                    + (isGrounded ? " au sol" : " en vol") + " vitesse=" + flyFast.ToString("F1"));
            }
            else if (ratio > 0.1f)
            {
                Debug.Log("[Battement] refusé, trop petit : ratio=" + ratio.ToString("F2") + " (min " + minFlapRatio + ")");
            }
        }

        float bendRatio = (leftY - rightY) / armLength;
        float absbendRatio = Mathf.Abs(bendRatio);

        if (absbendRatio < minBendTreshold)
        {
            isTurning = 0f;
        }
        else
        {
            isTurning = Mathf.Sign(bendRatio) * Mathf.InverseLerp(minBendTreshold, maxBendTreshold, absbendRatio);
        }

        float turnSign = isGrounded || isTurning == 0f ? 0f : Mathf.Sign(isTurning);
        if (turnSign != lastTurnSign)
        {
            lastTurnSign = turnSign;
            Debug.Log("[Virage] " + (turnSign > 0f ? "droite" : turnSign < 0f ? "gauche" : "fin") + " (écart mains " + bendRatio.ToString("F2") + ")");
        }
    }

    void FixedUpdate()
    {
        if (!calibrated) return;
        float dt = Time.fixedDeltaTime;
        flyFast = rb.linearVelocity;   // vitesse réelle, après d'éventuels chocs

        if (isGrounded != wasGrounded)
        {
            wasGrounded = isGrounded;
            Debug.Log(isGrounded ? "[Sol] posé sur " + surface : "[Sol] décollage à y=" + rb.position.y.ToString("F1"));
        }

        if (PousseeTimer > 0f)
        {
            ApplyPoussee(dt);
            PousseeTimer = Mathf.MoveTowards(PousseeTimer, 0f, Time.fixedDeltaTime / PousseeDuration);
        }

        ApplyDrag(dt);
        ApplyLift(dt);
        ApplyDescente(dt);
        LimitForwardSpeed();
        Turn(dt);
        Move();
    }

    void ApplyPoussee(float dt)
    {
        Vector3 force = isGrounded
            ? Vector3.up * takeoffUpForce + transform.forward * flapForwardForce
            : Vector3.up * flapUpForce + transform.forward * flapForwardForce;

        flyFast += force * PousseeTimer * dt;   // la poussée s'accumule
    }

    void ApplyDescente(float dt)
    {
        // aussi au sol : garde l'oiseau en contact avec ce sur quoi il est posé
        flyFast.y = Mathf.MoveTowards(flyFast.y, -vitesseDescente, 3f * dt);
    }

    void ApplyLift(float dt)
    {
        if (isGrounded)
        {
            lift = 0f;
            return;
        }

        // Seule la vitesse vers l'avant crée de la portance
        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(rb.linearVelocity, transform.forward));
        float liftRatio = Mathf.Clamp01(forwardSpeed / glideSpeed);

        // Même formule qu'avant, mais sans le poids
        lift = glideLiftRatio * liftRatio * liftRatio;
    }

    void ApplyDrag(float dt)
    {
        flyFast -= flyFast * flyFast.magnitude * dragCoefficient * dt;
    }

    void LimitForwardSpeed()
    {
        float forwardSpeed = Vector3.Dot(flyFast, transform.forward);
        if (forwardSpeed > maxForwardSpeed)
            flyFast -= transform.forward * (forwardSpeed - maxForwardSpeed);
    }

    void Turn(float dt)
    {
        if (isGrounded || isTurning == 0f) return;

        Quaternion rotation = Quaternion.Euler(0f, isTurning * turnSpeed * dt, 0f);
        rb.MoveRotation(rb.rotation * rotation);
    }

    void Move()
    {
        if (isGrounded)
        {
            flyFast.x *= 0.9f;   // friction au sol
            flyFast.z *= 0.9f;
        }

        rb.linearVelocity = flyFast;   // la physique gère les collisions
        isGrounded = false;            // remis à true par OnCollisionStay si on est posé
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude > 1f)
            Debug.Log("[Choc] " + collision.gameObject.name + " à " + collision.relativeVelocity.magnitude.ToString("F1") + " m/s");
    }

    void OnCollisionStay(Collision collision)
    {
        // Posé si la surface touchée est plutôt horizontale, quel que soit son tag
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                isGrounded = true;
                surface = collision.gameObject.name;
                return;
            }
        }
    }
}
