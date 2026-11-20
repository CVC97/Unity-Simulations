using UnityEngine;


// The apparatus as seen in the scene: its parameters, the parts the controller moves, its pedestal, and the settings
// that depend on its size. Sits on the LectureRig object under the Gyroscope object.
public class ApparatusRig : MonoBehaviour {
    [SerializeField] private ApparatusParameters parameters;
    [SerializeField] private Transform rotor;                                   // spins about its local up axis
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private GameObject stand;                                  // separate root object, doesn't move

    [Header("Limits and display")]
    [SerializeField] private float minTheta0Deg = 5f;
    [SerializeField] private float maxTheta0Deg = 25f;
    [SerializeField] private Vector3 vectorScales = new Vector3(0.28f, 0.008f, 0.8f);   // m per unit: L, ω, D
    [SerializeField] private float coneLength = 0.81f;                          // m, apex to rim of the precession cone
    // deg; θ of L⃗ at which the apparatus hits its pedestal and stops (Fallen); 0 = no stop
    [SerializeField] private float stopThetaDeg = 32f;

    public ApparatusParameters Parameters => parameters;
    public Transform Rotor => rotor;
    public TrailRenderer Trail => trail;
    public GameObject StandObject => stand;
    public float MinTheta0Deg => minTheta0Deg;
    public float MaxTheta0Deg => maxTheta0Deg;
    public Vector3 VectorScales => vectorScales;
    public float ConeLength => coneLength;
    public float StopThetaDeg => stopThetaDeg;
}
