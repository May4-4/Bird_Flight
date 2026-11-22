using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VRFlightController : MonoBehaviour
{
    [Header("Références XR")]
    public Transform head;          // Main Camera
    public Transform leftHand;      // Left Controller
    public Transform rightHand;     // Right Controller
    public CapsuleCollider bodyCollider; // optionnel : suit ta tête au sol

    [Header("Battement")]
    public float minFlapSpeed = 0.8f;      // m/s vers le bas, par main
    public float flapUpForce = 25f;
    public float takeoffMultiplier = 1.5f; // bonus au sol pour pas qu'on ait du mal à s'nevoler
    public float armsSpreadMin = 0.35f;    // écart horizontal mains/tête (m)
    [Range(0f, 1f)] public float symmetryTolerance = 0.5f;
    public float flapSmoothing = 10f;

    [Header("Inclinaison")]
    public float leanDeadzone = 0.08f;     // m
    public float leanMaxDistance = 0.30f;  // m
    public float leanForce = 12f;

    [Header("Portance")]
    [Range(0f, 1f)] public float glideLiftRatio = 0.85f;
    public float glideSpeed = 8f;

    [Header("Traînée / vitesse")]
    public float dragCoefficient = 0.08f;
    public float maxHorizontalSpeed = 12f;

    private Rigidbody rb;
    private bool isGrounded;

    private Vector3 prevLeftLocal, prevRightLocal;
    private float flapStrength;
    private Vector3 neutralHeadLocal;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        prevLeftLocal = HandLocal(leftHand);
        prevRightLocal = HandLocal(rightHand);
        neutralHeadLocal = HeadLocal();
    }

    // Position de la tête dans le repère de l'XR Origin
    Vector3 HeadLocal() => transform.InverseTransformPoint(head.position);

    // Position de la main relative à la tête, dans le repère de l'XR Origin
    Vector3 HandLocal(Transform hand) =>
        transform.InverseTransformPoint(hand.position) - HeadLocal();

    void Update()
    {
        UpdateFlap();
    }

    void FixedUpdate()
    {
        Vector3 headLocal = HeadLocal();

        if (isGrounded)
        {
            // Marche physique 
            neutralHeadLocal = headLocal;

            // Le collider suit la tête 
            if (bodyCollider != null)
            {
                Vector3 c = bodyCollider.center;
                bodyCollider.center = new Vector3(headLocal.x, c.y, headLocal.z);
            }
        }
        else
        {
            ApplyLean(headLocal);
        }

        ApplyFlapForce();
        ApplyLift();
        ApplyDrag();
        LimitHorizontalSpeed();
    }

    // ==================================================
    // DÉTECTION DU BATTEMENT
    // ==================================================
    void UpdateFlap()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 l = HandLocal(leftHand);
        Vector3 r = HandLocal(rightHand);

        // Vitesse verticale (vers le bas = positif)
        float downL = Mathf.Max(0f, -(l.y - prevLeftLocal.y) / dt);
        float downR = Mathf.Max(0f, -(r.y - prevRightLocal.y) / dt);

        prevLeftLocal = l;
        prevRightLocal = r;

        // Bras écartés ? (distance horizontale main/tête)
        bool armsSpread =
            new Vector2(l.x, l.z).magnitude > armsSpreadMin &&
            new Vector2(r.x, r.z).magnitude > armsSpreadMin;

        float slow = Mathf.Min(downL, downR);
        float fast = Mathf.Max(downL, downR);
        bool symmetric = fast > 0f && slow / fast >= symmetryTolerance;

        float target = 0f;
        if (armsSpread && symmetric && slow > minFlapSpeed)
            target = (downL + downR) * 0.5f;

        flapStrength = Mathf.Lerp(flapStrength, target, flapSmoothing * dt);
    }

    void ApplyFlapForce()
    {
        if (flapStrength < 0.01f) return;

        float force = flapStrength * flapUpForce;
        if (isGrounded) force *= takeoffMultiplier;

        rb.AddForce(Vector3.up * force, ForceMode.Force);
    }

    // ==================================================
    // INCLINAISON DU CORPS
    // ==================================================
    void ApplyLean(Vector3 headLocal)
    {
        Vector3 offset = headLocal - neutralHeadLocal;
        offset.y = 0f;

        float dist = offset.magnitude;
        if (dist < leanDeadzone) return;

        float strength = Mathf.Clamp01(
            (dist - leanDeadzone) / (leanMaxDistance - leanDeadzone));

        Vector3 worldDir = transform.TransformDirection(offset.normalized);
        rb.AddForce(worldDir * leanForce * strength, ForceMode.Force);
    }

    // ==================================================
    // PORTANCE (basée sur la vitesse horizontale)
    // ==================================================
    void ApplyLift()
    {
        if (isGrounded) return;

        Vector3 v = rb.linearVelocity;
        float horizontalSpeed = new Vector2(v.x, v.z).magnitude;

        float liftRatio = Mathf.Clamp01(horizontalSpeed / glideSpeed);
        float weight = rb.mass * Physics.gravity.magnitude;
        float lift = weight * glideLiftRatio * liftRatio * liftRatio;

        rb.AddForce(Vector3.up * lift, ForceMode.Force);
    }

    void ApplyDrag()
    {
        Vector3 v = rb.linearVelocity;
        if (v.sqrMagnitude < 0.01f) return;

        rb.AddForce(-v.normalized * v.sqrMagnitude * dragCoefficient,
                    ForceMode.Force);
    }

    void LimitHorizontalSpeed()
    {
        Vector3 v = rb.linearVelocity;
        Vector3 h = new Vector3(v.x, 0f, v.z);

        if (h.magnitude <= maxHorizontalSpeed) return;

        h = h.normalized * maxHorizontalSpeed;
        rb.linearVelocity = new Vector3(h.x, v.y, h.z);
    }

    // ==================================================
    // SOL
    // ==================================================
    void OnCollisionEnter(Collision c) { if (c.gameObject.CompareTag("Ground")) isGrounded = true; }
    void OnCollisionStay(Collision c)  { if (c.gameObject.CompareTag("Ground")) isGrounded = true; }
    void OnCollisionExit(Collision c)  { if (c.gameObject.CompareTag("Ground")) isGrounded = false; }
}
