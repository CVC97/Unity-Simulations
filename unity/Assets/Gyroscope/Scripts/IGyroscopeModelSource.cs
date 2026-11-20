// anything that runs a GyroscopeModel (GyroscopeController);
// views like VectorDisplay only need the model, not the controller behind it
public interface IGyroscopeModelSource {
    GyroscopeModel Model { get; }
}
