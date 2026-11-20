// C# → page for every simulation of this project (e.g. GyroscopeBridge.Post). Unity links every .jslib into WebGL
// builds as an Emscripten library; a function is only included where C# declares it with [DllImport("__Internal")].
// The page holds the Unity canvas itself and listens with window.onSimMessage = (message) => { … } (assets/sim.js).
mergeInto(LibraryManager.library, {
  SimPostToPage: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);                    // C# string → pointer into the WASM heap → JS string
    if (typeof window.onSimMessage === "function") window.onSimMessage(JSON.parse(json));
  },
});
