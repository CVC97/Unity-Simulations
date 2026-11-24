using UnityEngine;


// The ring of pins on the platform: places the pin objects where PinRing (physics) has them and lets a knocked pin
// fall outwards, away from the centre (the stylus comes from inside). Sits on the Pins object, a child of the Hall
// on the platform's top; each pin's pivot is its foot, its local up its axis. The fall is drawn in real time, so a pin
// falls at the same speed at any Zeitraffer; fallen pins lie until the next release.
public class PinRingDisplay : MonoBehaviour {
    [SerializeField] private Transform[] pins;
    [SerializeField] private float fallSeconds = 0.45f;                         // real time
    [SerializeField] private float pinRadius = 0.006f;                          // m: a lying pin rests on its side

    private PinRing ring;
    private float[] fallStart;                                                  // unscaled time, NaN = standing


    public int Count => pins.Length;


    // all pins up, on the ring's circle (local x-z = floor plan, Unity (x, z) = model (East, North))
    public void Place(PinRing pinRing) {
        ring = pinRing;
        if (fallStart == null || fallStart.Length != pins.Length) fallStart = new float[pins.Length];
        for (int i = 0; i < pins.Length; i++) {
            bool used = i < ring.Count;
            pins[i].gameObject.SetActive(used);
            fallStart[i] = float.NaN;
            if (!used) continue;
            pins[i].localPosition = new Vector3((float)ring.X(i), 0f, (float)ring.Y(i));
            pins[i].localRotation = Quaternion.identity;
        }
    }


    void LateUpdate() {
        if (ring == null) return;
        for (int i = 0; i < pins.Length && i < ring.Count; i++) {
            if (!ring.IsDown(i)) continue;
            if (float.IsNaN(fallStart[i])) fallStart[i] = Time.unscaledTime;
            float t = Mathf.Clamp01((Time.unscaledTime - fallStart[i]) / fallSeconds);
            if (t >= 1f && pins[i].localPosition.y >= pinRadius) continue;     // lying, nothing left to do
            float f = t * t;                                                    // falling faster and faster
            Vector3 outward = new Vector3((float)ring.X(i), 0f, (float)ring.Y(i)).normalized;
            pins[i].localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, outward), f);
            Vector3 p = pins[i].localPosition;
            pins[i].localPosition = new Vector3(p.x, pinRadius * f, p.z);
        }
    }
}
