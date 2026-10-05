using UnityEngine;
using UnityEngine.InputSystem;

public class FlightController : MonoBehaviour
{
    private PlayerInput playerInput;

    [Header("Références")]
    public Transform leftWingPivot;
    public Transform rightWingPivot;

    [Header("Battement")]
    public float flapForwardForce = 10f;
    public float flapUpForce = 6f;
    public float takeoffUpForce = 14f;

    [Header("Portance")]
    [Range(0f, 1f)]
    public float glideLiftRatio = 0.85f;

    public float glideSpeed = 8f;

    [Header("Traînée")]
    public float dragCoefficient = 0.08f;

    [Header("Vitesses")]
    public float maxForwardSpeed = 12f;

    [Header("Direction")]
    public float turnSpeed = 50f;
    public float pitchSpeed = 30f;

    [Header("Animation des ailes")]
    public float flapFrequency = 4f;
    public float flapAngle = 45f;

    private Rigidbody rb;

    private Vector2 flightDirection;

    private bool flapHeld = false;
    private bool isGrounded = false;

    private float flapTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
    }

    void FixedUpdate()
    {
        // Lit l'état RÉEL actuel de Space à chaque pas physique
        flapHeld = playerInput.actions["Flap"].IsPressed();

        HandleFlightDirection();

        if (flapHeld)
        {
            ApplyFlappingForce();
        }

        ApplyLift();
        ApplyDrag();

        LimitForwardSpeed();

        Debug.Log(
            "Flap: " + flapHeld +
            " | Vy: " + rb.linearVelocity.y +
            " | Y: " + transform.position.y
        );
    }

    void Update()
    {
        AnimateWings();
    }

    // ==================================================
    // INPUT : BATTEMENT
    // ==================================================

    // ==================================================
    // BATTEMENT
    // ==================================================

    void ApplyFlappingForce()
    {
        if (isGrounded)
        {
            /*
             * Décollage :
             * forte poussée verticale +
             * début de vitesse vers l'avant.
             */
            Vector3 takeoffForce =
                Vector3.up * takeoffUpForce
                + transform.forward * flapForwardForce;

            rb.AddForce(
                takeoffForce,
                ForceMode.Force
            );
        }
        else
        {
            /*
             * En vol :
             * le battement donne surtout de la poussée
             * vers l'avant, avec une composante verticale.
             */
            Vector3 flapForce =
                transform.forward * flapForwardForce
                + Vector3.up * flapUpForce;

            rb.AddForce(
                flapForce,
                ForceMode.Force
            );
        }
    }

    // ==================================================
    // PORTANCE
    // ==================================================

    void ApplyLift()
    {
        if (isGrounded)
            return;

        /*
         * On mesure uniquement la vitesse dans
         * la direction vers laquelle regarde l'oiseau.
         */
        float forwardSpeed =
            Vector3.Dot(
                rb.linearVelocity,
                transform.forward
            );

        forwardSpeed = Mathf.Max(
            0f,
            forwardSpeed
        );

        /*
         * 0 = pas de portance
         * 1 = vitesse de plané suffisante
         */
        float liftRatio =
            Mathf.Clamp01(
                forwardSpeed / glideSpeed
            );

        /*
         * Poids réel du Rigidbody.
         * Avec Mass = 1 :
         * environ 9.81 N.
         */
        float weight =
            rb.mass
            * Physics.gravity.magnitude;

        /*
         * La portance maximale en plané
         * reste légèrement inférieure au poids.
         *
         * Donc sans battement :
         * l'oiseau descend progressivement.
         */
        float lift =
            weight
            * glideLiftRatio
            * liftRatio
            * liftRatio;

        rb.AddForce(
            Vector3.up * lift,
            ForceMode.Force
        );
    }

    // ==================================================
    // TRAÎNÉE
    // ==================================================

    void ApplyDrag()
    {
        Vector3 velocity =
            rb.linearVelocity;

        // Presque immobile : rien à faire.
        if (velocity.sqrMagnitude < 0.01f)
            return;

        /*
         * La traînée augmente avec le carré
         * de la vitesse.
         */
        Vector3 dragForce =
            -velocity.normalized
            * velocity.sqrMagnitude
            * dragCoefficient;

        rb.AddForce(
            dragForce,
            ForceMode.Force
        );
    }

    // ==================================================
    // INPUT : DIRECTION
    // ==================================================

    public void OnFlightDirection(InputValue value)
    {
        flightDirection =
            value.Get<Vector2>();
    }

    // ==================================================
    // DIRECTION
    // ==================================================

    void HandleFlightDirection()
    {
        if (isGrounded)
            return;

        float horizontal =
            flightDirection.x;

        float vertical =
            flightDirection.y;

        float yaw =
            horizontal
            * turnSpeed
            * Time.fixedDeltaTime;

        float pitch =
            -vertical
            * pitchSpeed
            * Time.fixedDeltaTime;

        Quaternion deltaRotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f
            );

        rb.MoveRotation(
            rb.rotation * deltaRotation
        );
    }

    // ==================================================
    // LIMITATION DE LA VITESSE AVANT
    // ==================================================

    void LimitForwardSpeed()
    {
        Vector3 velocity =
            rb.linearVelocity;

        /*
         * Vitesse uniquement selon l'axe avant.
         */
        float forwardSpeed =
            Vector3.Dot(
                velocity,
                transform.forward
            );

        if (forwardSpeed <= maxForwardSpeed)
            return;

        /*
         * On retire uniquement l'excès de vitesse
         * avant.
         *
         * On ne touche donc pas directement
         * à la vitesse verticale.
         */
        float excessSpeed =
            forwardSpeed - maxForwardSpeed;

        rb.linearVelocity =
            velocity
            - transform.forward * excessSpeed;
    }

    // ==================================================
    // ANIMATION DES AILES
    // ==================================================

    void AnimateWings()
    {
        if (
            leftWingPivot == null ||
            rightWingPivot == null
        )
        {
            return;
        }

        if (flapHeld)
        {
            /*
             * Oscillation périodique :
             * ailes haut / bas tant que Space
             * est maintenu.
             */
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
        else
        {
            /*
             * Sans battement :
             * ailes déployées horizontalement
             * pour le vol plané.
             */
            leftWingPivot.localRotation =
                Quaternion.Lerp(
                    leftWingPivot.localRotation,
                    Quaternion.identity,
                    5f * Time.deltaTime
                );

            rightWingPivot.localRotation =
                Quaternion.Lerp(
                    rightWingPivot.localRotation,
                    Quaternion.identity,
                    5f * Time.deltaTime
                );
        }
    }

    // ==================================================
    // SOL
    // ==================================================

    void OnCollisionEnter(Collision collision)
    {
        if (
            collision.gameObject.CompareTag("Ground")
        )
        {
            isGrounded = true;
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (
            collision.gameObject.CompareTag("Ground")
        )
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (
            collision.gameObject.CompareTag("Ground")
        )
        {
            isGrounded = false;
        }
    }
}