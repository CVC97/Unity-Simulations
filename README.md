# Vorlesungsversuche – Engine Simulations

Interactive simulations of lecture demonstrations, all in one noir look, published as a static site on GitHub Pages.
A plain start page (`index.html`) links to one folder per simulation; each folder holds its page and its Unity web
build. All simulations come from one Unity project (`unity-simulations/`), so they share materials, post-processing, scripts.

Why it is built this way, conventions, pitfalls and open items: [design_notes.md](design_notes.md).

```
index.html            start page: one card per simulation
assets/               noir.css (the site's look), sim.js (load a build, send commands, controls), fonts/, favicon
gyroscope/            Vorlesungskreisel: index.html + gyroscope.js (page), sim/ (web build, committed)
foucault/             Foucault-Pendel: index.html + foucault.js (page), sim/ (web build, committed)
unity-simulations/    the Unity project (Unity 6000.6.4f1, URP): Assets/Shared (noir), Assets/Gyroscope, Assets/Foucault
.github/workflows/    pages.yml: publishes the site on a site-v* tag
```


## Set up

- Unity Hub → Add → `unity-simulations/` (Unity 6000.6.4f1 with Web Build Support). The first open imports for a few minutes.
- Open `Assets/Gyroscope/GyroscopeScene.unity`, press Play. In Play mode: *WebBridge* → ⋮ on `GyroscopeBridge` →
  *Apply test preset* (balanced), then *Gyroscope* → ⋮ on `GyroscopeController` → *Stoß*.
- Tests: Window → General → Test Runner → EditMode → Run All.


## Try the site locally

```bash
python3 -m http.server 8000          # in the repo root, then http://localhost:8000/
```

The page loads the committed build from `gyroscope/sim/`; after a new build, reload with the cache disabled.


## Build and publish

```bash
# Unity: menu Gyroscope → Build web version   (→ gyroscope/sim/, raise the version in the build profile first)
#        menu Foucault → Build web version    (→ foucault/sim/)
#   or, with the editor closed:
#   ~/Unity/Hub/Editor/6000.6.4f1/Editor/Unity -batchmode -nographics -projectPath unity-simulations \
#       -executeMethod SimBuild.BuildGyroscopeFromCommandLine -logFile build.log
python3 -m http.server 8000                                  # check it
git add -A && git commit -m "…"
git push && git tag site-v1.0.0 && git push origin site-v1.0.0   # the tag deploys
```

Repository settings → Pages → Source: *GitHub Actions* (once).


## Rules

- Scene changes go through the menus *Gyroscope → …* and *Foucault → …* (`Assets/<Name>/Editor/<Name>SceneSetup.cs`),
  not by hand; the steps are rerunnable and overwrite manual tweaks to what they touch.
- Shared things (look, view scripts, editor helpers, the bridge plugin) live in `unity-simulations/Assets/Shared/`; a new
  simulation gets its own `Assets/<Name>/` and its own site folder `<name>/` (see design_notes.md, section 8).
- Bridge changes (commands, state fields, preset keys): update the C# bridge, the page's JS and design_notes.md
  section 5 together.
