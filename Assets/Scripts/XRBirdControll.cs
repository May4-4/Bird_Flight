using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class XRBirdController : MonoBehaviour
{
    [Header("Références XR")]
    public Transform head;          // Main Camera
    public Transform leftHand;
    public Transform rightHand;
    public Transform cameraOffset;  // parent de la caméra et des manettes

    [Header("Visuel")]
    public Transform birdModel;     // modèle de l'oiseau (enfant du rig), optionnel

    [Header("Morphologie")]
    public float birdEyeHeight = 0.4f;
    public float armToEyeRatio = 0.47f;

    [Header("Battement")]
    public float directionThreshold = 0.2f;
    public float minFlapRatio = 0.3f;
    public float fullFlapRatio = 1.0f;

    [Header("Poussée (m/s²)")]
    public float thrustDuration = 0.3f;
    public float maxThrust = 1.5f;
    public float takeoffUpAccel = 30f;
    public float flapUpAccel = 20f;
    public float flapForwardAccel = 15f;

    [Header("Descente")]
    public float sinkSpeed = 3f;   // m/s de chute sans battre
    public float sinkAccel = 3f;     // rapidité pour atteindre cette vitesse

    [Header("Traînée / vitesse")]
    public float dragCoefficient = 0.08f;
    public float groundFriction = 4f;
    public float maxForwardSpeed = 12f;

    [Header("Virage")]
    public float turnSpeed = 50f;
    public float bankDeadZone = 0.15f;
    public float bankMaxRatio = 0.8f;

    [Header("Sol")]
    public LayerMask groundMask;
    public float groundProbeHeight = 2f;

    [Header("Debug")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private Vector3 velocity;

    private Rigidbody rb;
    private bool calibrated;
    private float armLength;

    private float lastLeftY, lastRightY;
    private bool isDownstroke;
    private float startLeftY, startRightY;
    private float thrustAmount;
    private float bankInput;

    // Décalage entre l'avant du rig et le regard du casque au moment de la calibration
    private Quaternion forwardOffset = Quaternion.identity;

    // ==================================================
    // INITIALISATION
    // ==================================================
    IEnumerator Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate; // évite les saccades caméra

        // Rig strictement à plat (lacet uniquement) : horizon droit
        rb.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.rotation = rb.rotation;

        yield return new WaitForSeconds(0.5f);   // attendre le tracking
        Calibrate();
    }

    // Peut être rappelée à tout moment (ex : bouton de manette) pour recentrer
    public void Calibrate()
    {
        // --- Direction de vol = regard du casque (la vue ne tourne pas) ---
        Vector3 f = transform.InverseTransformDirection(head.forward);
        float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        forwardOffset = Quaternion.Euler(0f, yaw, 0f);

        if (birdModel != null)
            birdModel.localRotation = forwardOffset;

        // --- Hauteur oiseau ---
        float eyeHeight = head.localPosition.y;
        armLength = eyeHeight * armToEyeRatio;

        Vector3 p = cameraOffset.localPosition;
        p.y = birdEyeHeight - eyeHeight;
        cameraOffset.localPosition = p;

        lastLeftY  = transform.InverseTransformPoint(leftHand.position).y;
        lastRightY = transform.InverseTransformPoint(rightHand.position).y;

        calibrated = true;
    }

    // ==================================================
    // LECTURE DES BRAS
    // ==================================================
    void Update()
    {
        if (!calibrated) return;

        float leftY  = transform.InverseTransformPoint(leftHand.position).y;
        float rightY = transform.InverseTransformPoint(rightHand.position).y;

        // --- Battement ---
        float leftVy  = (leftY  - lastLeftY)  / Time.deltaTime;
        float rightVy = (rightY - lastRightY) / Time.deltaTime;
        lastLeftY  = leftY;
        lastRightY = rightY;

        bool bothGoingDown = leftVy < -directionThreshold && rightVy < -directionThreshold;

        if (bothGoingDown && !isDownstroke)
        {
            isDownstroke = true;
            startLeftY  = leftY;
            startRightY = rightY;
        }
        else if (!bothGoingDown && isDownstroke)
        {
            isDownstroke = false;
            float amplitude = Mathf.Min(startLeftY - leftY, startRightY - rightY);
            float ratio = amplitude / armLength;

            if (ratio >= minFlapRatio)
            {
                float strength = Mathf.InverseLerp(minFlapRatio, fullFlapRatio, ratio);
                thrustAmount = Mathf.Min(thrustAmount + strength, maxThrust);
            }
        }

        // --- Virage (bloqué pendant un battement) ---
        if (isDownstroke || thrustAmount > 0f)
        {
            bankInput = 0f;
        }
        else
        {
            float bankRatio = (rightY - leftY) / armLength;
            float absRatio = Mathf.Abs(bankRatio);
            bankInput = absRatio < bankDeadZone
                ? 0f
                : Mathf.Sign(bankRatio) * Mathf.InverseLerp(bankDeadZone, bankMaxRatio, absRatio);
        }
    }

    // ==================================================
    // MOUVEMENT
    // ==================================================
    void FixedUpdate()
    {
        if (!calibrated) return;
        float dt = Time.fixedDeltaTime;

        HandleTurn(dt);
        ApplyThrust(dt);
        ApplySink(dt);
        ApplyDrag(dt);
        LimitForwardSpeed();
        Move(dt);
    }

    Vector3 Forward()
    {
        Vector3 f = rb.rotation * forwardOffset * Vector3.forward;
        f.y = 0f;
        return f.normalized;
    }

    void HandleTurn(float dt)
    {
        if (isGrounded || bankInput == 0f) return;

        Quaternion turn = Quaternion.Euler(0f, bankInput * turnSpeed * dt, 0f);
        rb.MoveRotation(rb.rotation * turn);
        velocity = turn * velocity;
    }

    void ApplyThrust(float dt)
    {
        if (thrustAmount <= 0f) return;

        float upAccel = isGrounded ? takeoffUpAccel : flapUpAccel;
        Vector3 accel = Vector3.up * upAccel + Forward() * flapForwardAccel;

        velocity += accel * thrustAmount * dt;
        thrustAmount = Mathf.MoveTowards(thrustAmount, 0f, dt / thrustDuration);
    }

    void ApplySink(float dt)
    {
        if (isGrounded && velocity.y <= 0f) return;

        // Remplace la gravité : on tend doucement vers -sinkSpeed
        velocity.y = Mathf.MoveTowards(velocity.y, -sinkSpeed, sinkAccel * dt);
    }

    void ApplyDrag(float dt)
    {
        Vector3 h = new Vector3(velocity.x, 0f, velocity.z);
        float speed = h.magnitude;
        if (speed < 0.01f) return;

        float decel = speed * speed * dragCoefficient + (isGrounded ? groundFriction : 0f);
        float newSpeed = Mathf.Max(0f, speed - decel * dt);

        h = h / speed * newSpeed;
        velocity.x = h.x;
        velocity.z = h.z;
    }

    void LimitForwardSpeed()
    {
        Vector3 f = Forward();
        float forwardSpeed = Vector3.Dot(velocity, f);
        if (forwardSpeed > maxForwardSpeed)
            velocity -= f * (forwardSpeed - maxForwardSpeed);
    }

    void Move(float dt)
    {
        Vector3 next = rb.position + velocity * dt;

        float groundY = GetGroundY(next);
        isGrounded = next.y <= groundY + 0.01f;

        if (isGrounded)
        {
            next.y = groundY;
            if (velocity.y < 0f) velocity.y = 0f;
        }

        rb.MovePosition(next);
    }

    float GetGroundY(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * groundProbeHeight;
        Debug.DrawRay(origin, Vector3.down * 100f, Color.red);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f,
                            groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        return float.NegativeInfinity;
    }
}