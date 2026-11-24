using UnityEngine;


// The swing plane on the floor, so the angle it has turned can be read: a line along the current (measured) plane,
// a darker line along the starting plane, and an arc between them a little outside the swing. Sits on the Plane
// object, a child of the Hall just above the platform (local x-z = floor plan, Unity (x, z) = model (East, North));
// the line renderers are on children and draw in this object's local space. In the star view the Hall turns: then the
// current line stands still in space and the start line turns away with the floor, as it should.
public class PlaneDisplay : MonoBehaviour {
    [SerializeField] private FoucaultController controller;
    [SerializeField] private LineRenderer current;
    [SerializeField] private LineRenderer startLine;
    [SerializeField] private LineRenderer arc;
    [SerializeField] private float overhang = 0.3f;                             // m beyond the swing at each end
    [SerializeField] private float arcGap = 0.12f;                              // m between the arc and the line ends
    [SerializeField] private float arcStepDeg = 2f;

    private const int MaxArcPoints = 181;


    void Awake() {
        foreach (LineRenderer line in new[] { current, startLine, arc }) {
            line.useWorldSpace = false;
            line.loop = false;
        }
        current.positionCount = 2;
        startLine.positionCount = 2;
    }


    void LateUpdate() {
        if (controller.Model == null) return;
        float radius = controller.Amplitude + overhang;
        SetLine(current, controller.PlaneAzimuth, radius);
        SetLine(startLine, controller.PlaneStartAzimuth, radius);

        // from the start to the current plane, the way it turned; beyond a full turn only the last one
        float start = (float)controller.PlaneStartAzimuth;
        float turned = (float)controller.PlaneTurned;
        turned = Mathf.Sign(turned) * Mathf.Min(Mathf.Abs(turned), 2 * Mathf.PI);
        int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(turned) * Mathf.Rad2Deg / arcStepDeg), 1, MaxArcPoints - 1) + 1;
        float r = radius + arcGap;
        arc.positionCount = n;
        for (int i = 0; i < n; i++) {
            float a = start + turned * i / (n - 1);
            arc.SetPosition(i, new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a)));
        }
        arc.enabled = Mathf.Abs(turned) > 0.2f * Mathf.Deg2Rad;
    }


    private static void SetLine(LineRenderer line, double azimuth, float radius) {
        var d = new Vector3((float)System.Math.Cos(azimuth), 0f, (float)System.Math.Sin(azimuth)) * radius;
        line.SetPosition(0, -d);
        line.SetPosition(1, d);
    }
}
