using System.Collections;
using UnityEngine;

/*
 * Rigidbody NON kinematic, SANS gravité.
 * La physique gère les collisions (nid, arbre, sol),
 * et c'est nous qui modifions rb.linearVitesse.
 */
[RequireComponent(typeof(Rigidbody))]
public class XRBirdController : MonoBehaviour
{
    [Header("Références XR")]
    public Transform teteControl;
    public Transform aileGaucheControl;   // Left Controller
    public Transform aileDroiteControl;  // Right Controller
    public Transform objetCameraOffset;

    public float HauteurYeuxOiseauxVoulue = 0.4f; // hauteur des yeux de l'oiseau

    [Header("Battement")]
    public float minConsiderationDescente = 0.2f;  // m/s : seuil à partir duquel on considère que ça descend
    public float minBattementDetecteRation = 0.3f;       // part du bras minimum parcourue pour un battement
    public float maxBattementDetecteRation = 1.0f;      // part du bras donnant un battement maximal

    [Header("Poussée (m/s²)")]
    public float pousseeDuree = 0.3f;    // durée de la poussée pour un battement de 1 (s)
    public float maxPoussee = 5f;
    public float SupEnvolForce = 30f;      // au sol
    public float volHautForce = 20f;         // en vol
    public float volAvantForce = 5f;     // on avance un peu à chaque battement
    public float maxAvantVitesse = 12f;

    [Header("Portance")]
    [Range(0f, 1f)]
    public float ratioMaxReductionDescente = 0.85f;    // réduit la descente jusqu'à 85 %
    public float vitesseAtteinteMaxReduction = 4f;           // vitesse avant pour la portance max //à mieux comprendre

    [Header("Descente")]
    public float vitesseDescente = 1.5f;    // m/s sans portance
    public float descenteAccel = 3f;        // rapidité pour atteindre cette vitesse //à retirer

    [Header("Traînée")]
    public float dragCoefficient = 0.08f; //à mieux comprendre


    [Header("Tourner")]
    public float maxDegreeTourner = 40f;           // degrés/s au maximum
    public float minConsiderationTourne = 0.15f;   // 15 % du bras : en dessous, on ignore
    public float maxConsiderationTourne = 0.8f;    // 80 % du bras : virage maximal

    
    private bool bSurSol;
    private Rigidbody rb;
    private bool bCalibre;
    private float tailleBras;

    private float dernierPosGauche, dernierPosDroite;
    private bool battement;
    private float debutGaucheY, debutDroiteY;
    private float Force;
    private float tourne;
    private float PousseeTimer; 
   

    // ==================================================
    // INITIALISATION
    // ==================================================
    IEnumerator Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // pas de culbute aux collisions
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        yield return new WaitForSeconds(0.5f); // attendre le tracking du casque et des contrôleurs pendant une demi-seconde
        Calibration();
    }

    public void Calibration()
    {
        float Height = teteControl.localPosition.y;   // hauteur réelle des yeux
        tailleBras = Height * 0.5f; // bras au mileu de la hauteur des yeux

        Vector3 p = objetCameraOffset.localPosition;
        p.y = HauteurYeuxOiseauxVoulue - Height;          // yeux ramenés à hauteur d'oiseau
        objetCameraOffset.localPosition = p;

        dernierPosGauche  = transform.InverseTransformPoint(aileGaucheControl.position).y;
        dernierPosDroite = transform.InverseTransformPoint(aileDroiteControl.position).y;

        //on ramène la camera à nous
        Vector3 teteControlLocal = transform.InverseTransformPoint(teteControl.position);
        objetCameraOffset.localPosition -= new Vector3(teteControlLocal.x, 0f, teteControlLocal.z);

        //bSurSol = true;
        bCalibre = true;
    }

    // ==================================================
    // LECTURE DES BRAS
    // ==================================================
    void Update()
    {
        if (!bCalibre) return;

        float leftY  = transform.InverseTransformPoint(aileGaucheControl.position).y;
        float rightY = transform.InverseTransformPoint(aileDroiteControl.position).y;

        float leftVy  = (leftY  - dernierPosGauche)  / Time.deltaTime;
        float rightVy = (rightY - dernierPosDroite) / Time.deltaTime;
        dernierPosGauche  = leftY;
        dernierPosDroite = rightY;

        bool bothGoingDown = leftVy < -minConsiderationDescente && rightVy < -minConsiderationDescente;
        // début du battement
        if (bothGoingDown && !battement)
        {
            // Début de la descente des bras
            battement = true;
            debutGaucheY  = leftY;
            debutDroiteY = rightY;
        }
        else if (!bothGoingDown && battement)
        {
            // Fin de la descente : on juge le battement
            battement = false;

            float amplitude = Mathf.Min(debutGaucheY - leftY, debutDroiteY - rightY);
            float ratio = amplitude / tailleBras;

            if (ratio >= minBattementDetecteRation)
            {
                Force = Mathf.InverseLerp(minBattementDetecteRation, maxBattementDetecteRation, ratio);
                PousseeTimer = Mathf.Min(PousseeTimer + Force, maxPoussee);
                Debug.Log($"Battement ! Ratio: {ratio:F2} | Force: {Force:F2} | Timer: {PousseeTimer:F2}");
            }
        }

        // Virage, bloqué pendant un battement
        if (battement || PousseeTimer > 0f)
        {
            tourne = 0f;
        }
        else
        {
            float bendRatio = (leftY - rightY) / tailleBras;
            float absbendRatio = Mathf.Abs(bendRatio);

            tourne = absbendRatio < minConsiderationTourne
                ? 0f
                : Mathf.Sign(bendRatio) * Mathf.InverseLerp(minConsiderationTourne, maxConsiderationTourne, absbendRatio);
        }
    }

    // ==================================================
    // MOUVEMENT
    // ==================================================
    void FixedUpdate()
    {   
        if (!bCalibre) return;
        float dt = Time.fixedDeltaTime;

        // On lit la vitesse actuelle (la physique a pu la modifier : collisions)
        Vector3 vitesse = rb.linearVelocity;
        Debug.Log($"on est au sol:{bSurSol}");
        if (PousseeTimer > 0f)
        {
            vitesse = ApplyPoussee(vitesse, dt);
            PousseeTimer = Mathf.MoveTowards(PousseeTimer, 0f, dt / pousseeDuree);
            Debug.Log($"applypoussee ! vitesse: {vitesse:F2} | PousseeTimer: {PousseeTimer:F2} ");
        }

        float freineDescente = ApplyLift(vitesse);
        vitesse = ApplyDescente(vitesse, dt, freineDescente );
        vitesse = ApplyDrag(vitesse, dt);
        vitesse = LimitForwardSpeed(vitesse);
        vitesse = Turn(vitesse, dt);

        // Friction au sol
        if (bSurSol)
        {
            vitesse.x *= 0.9f;
            vitesse.z *= 0.9f;
        }

        
        rb.linearVelocity = vitesse;
    }

    Vector3 ApplyPoussee(Vector3 vitesse, float dt)
    {
        Vector3 force = bSurSol
            ? Vector3.up * SupEnvolForce + transform.forward * volAvantForce
            : Vector3.up * volHautForce + transform.forward * volAvantForce;

        return vitesse + force * PousseeTimer * dt;
    }

    float ApplyLift(Vector3 vitesse)
    {
        
        if (bSurSol)
        {
            // On ne freine pas l'oiseau quand on décolle
            return 0f;
        }

        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(vitesse, transform.forward));
        float liftRatio = Mathf.Clamp01(forwardSpeed / vitesseAtteinteMaxReduction);
        Debug.Log($"Lift ratio : {liftRatio}");
        float freineDescente = ratioMaxReductionDescente * liftRatio;
        Debug.Log($"freineDescente : {freineDescente}");
        return freineDescente;
    }

    Vector3 ApplyDescente(Vector3 vitesse, float dt, float freinedescente)
    {
        if (bSurSol) return vitesse;

        // Remplace la gravité : la portance réduit la vitesse de descente
        float descente = vitesseDescente * (1f - freinedescente);
        vitesse.y = Mathf.MoveTowards(vitesse.y, -descente, descenteAccel * dt);
        return vitesse;
    }

    Vector3 ApplyDrag(Vector3 vitesse, float dt)
    {
        // Freine proportionnellement à la vitesse au carré
        return vitesse - vitesse * vitesse.magnitude * dragCoefficient * dt;
    }

    Vector3 LimitForwardSpeed(Vector3 vitesse)
    {
        float forwardSpeed = Vector3.Dot(vitesse, transform.forward);
        if (forwardSpeed > maxAvantVitesse)
            vitesse -= transform.forward * (forwardSpeed - maxAvantVitesse);
        return vitesse;
    }

    Vector3 Turn(Vector3 vitesse, float dt)
    {
        if (bSurSol || tourne == 0f) return vitesse;
        Quaternion rotation = Quaternion.Euler(0f, tourne * maxDegreeTourner * dt, 0f);
        rb.MoveRotation(rb.rotation * rotation);
        return rotation * vitesse;   // la vitesse tourne avec l'oiseau
    }

    // ==================================================
    // SOL : les collisions fonctionnent à nouveau
    // ==================================================
    void OnCollisionEnter(Collision c) { if (c.gameObject.CompareTag("Ground")) bSurSol = true; }
    void OnCollisionStay(Collision c)  { if (c.gameObject.CompareTag("Ground")) bSurSol = true; }
    void OnCollisionExit(Collision c)  { if (c.gameObject.CompareTag("Ground")) bSurSol = false; }
}