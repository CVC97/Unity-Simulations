using UnityEngine;


// The vectors: the Earth's angular velocity Ω (white, like ω on the Kreisel) split into its vertical part Ω sin φ, the
// one that turns the plane, and its horizontal part Ω cos φ (both light grey); and the Coriolis force on the bob (red,
// like D: forces and torques). Ω is the same everywhere, so it stands on the pendulum's axis above the bob (the anchor, a
// child of the Hall: it turns with the Earth in the star view), at a fixed length; the Coriolis force is drawn at the bob, greatly
// enlarged (a few mN on 25 kg), and only while the bob is drawn (not at high Zeitraffer).
// Model vectors are in the hall's frame; the Hall's rotation takes them to world space.
public class FoucaultVectors : MonoBehaviour {
    [SerializeField] private FoucaultController controller;
    [SerializeField] private Transform omegaAnchor;
    [SerializeField] private ArrowBase arrowOmega;
    [SerializeField] private ArrowBase arrowOmegaVertical;
    [SerializeField] private ArrowBase arrowOmegaHorizontal;
    [SerializeField] private ArrowBase arrowCoriolis;

    [Header("Scale")]
    [SerializeField] private float omegaLength = 1.2f;                          // m for Ω_E, at every latitude
    [SerializeField] private float coriolisScale = 150f;                        // m per N

    public bool ShowOmega { get; set; } = true;
    public bool ShowCoriolis { get; set; } = true;


    void LateUpdate() {
        FoucaultModel model = controller.Model;
        if (model == null) return;
        Quaternion hall = controller.Hall.rotation;

        Vector3 origin = omegaAnchor.position;
        float scale = omegaLength / (float)model.EarthRate;
        Vector3 omega = hall * PhysicsToUnity.Vector(model.Omega) * scale;
        Vector3 vertical = new Vector3(0f, omega.y, 0f);                       // the hall's up is world up
        Draw(arrowOmega, ShowOmega, origin, omega);
        Draw(arrowOmegaVertical, ShowOmega, origin, vertical);
        Draw(arrowOmegaHorizontal, ShowOmega, origin, omega - vertical);

        Vector3 force = hall * PhysicsToUnity.Vector(model.CoriolisAcceleration) * (controller.BobMassKg * coriolisScale);
        Draw(arrowCoriolis, ShowCoriolis && controller.BobVisible, controller.BobWorldPosition, force);
    }


    // a hidden arrow is drawn with length zero (VectorArrow3D hides it, and its label with it)
    private static void Draw(ArrowBase arrow, bool show, Vector3 origin, Vector3 vector) {
        arrow.Draw(origin, show ? vector : Vector3.zero);
    }
}
