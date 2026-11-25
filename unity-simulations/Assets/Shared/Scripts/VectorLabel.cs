using TMPro;
using UnityEngine;


// Symbol just beyond the tip of a vector arrow (L⃗, ω⃗, D⃗), or beside a point along it (the parts of a vector, as in
// a parallelogram drawing). Faces the camera and is drawn on top of everything (overlay material): it is a helper, not
// part of the apparatus. Hidden while its arrow is hidden.
// Runs after the orbit camera has moved (DefaultExecutionOrder), so it faces the camera of this frame.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(TextMeshPro))]
public class VectorLabel : MonoBehaviour {
    [SerializeField] private VectorArrow3D arrow;
    [SerializeField, Range(0f, 1f)] private float along = 1f;                  // where on the arrow: 1 = the tip, 0.5 = its middle
    [SerializeField] private float offset = 0.035f;                             // m further, along the arrow
    [SerializeField] private float sideOffset = 0f;                             // m beside it, across the arrow as seen on screen
    // optional: the side offset goes to the side away from this arrow's tip (Foucault: the parts of Ω⃗ are labelled
    // on the side away from Ω⃗, so the labels don't meet at the shared corner)
    [SerializeField] private VectorArrow3D awayFrom;
    [SerializeField] private float lift = 0f;                                   // m straight up (F⃗_C: off the bob when seen end-on)
    // m; with the camera closer than this the label shrinks, so on screen it never grows beyond its size at this
    // distance (Foucault: zoomed in at the bob, F⃗_C grew huge). 0 = a fixed size in the world (the Kreisel)
    [SerializeField] private float nearDistance = 0f;

    private TextMeshPro text;
    private Transform cam;
    private Vector3 baseScale;


    void Awake() {
        text = GetComponent<TextMeshPro>();
        cam = Camera.main.transform;
        baseScale = transform.localScale;
    }


    void LateUpdate() {
        bool visible = arrow.isActiveAndEnabled && arrow.Visible;               // Visible alone is stale once the arrow is switched off
        text.enabled = visible;
        if (!visible) {
            return;
        }
        Vector3 start = arrow.transform.position;
        Vector3 direction = (arrow.Tip - start).normalized;
        Vector3 point = Vector3.Lerp(start, arrow.Tip, along);
        Vector3 side = Vector3.Cross(direction, cam.forward).normalized;        // zero while looking along the arrow
        if (awayFrom != null && awayFrom.Visible && Vector3.Dot(side, awayFrom.Tip - point) > 0f) side = -side;
        transform.position = point + direction * offset + side * sideOffset + Vector3.up * lift;
        transform.rotation = cam.rotation;                                      // billboard: parallel to the screen
        if (nearDistance > 0f) {
            float d = Vector3.Distance(cam.position, transform.position);
            transform.localScale = baseScale * Mathf.Min(1f, d / nearDistance);
        }
    }
}
