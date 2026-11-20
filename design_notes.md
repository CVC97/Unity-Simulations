# Design notes — Vorlesungsversuche (engine simulations)

What was built and why: the decisions behind the site and its simulations, for anyone (or any Claude session) who
extends it or adds the next simulation. **How to** set up, build and publish is in `README.md`.

Built by Carlo (physics education group, Göttingen) with Claude, from 2026-11-18. Claude writes code and editor
scripts; Carlo checks in the editor and decides the look.


## 1 Origin and status (2026-11-20)

The first simulation, the **Vorlesungskreisel**, was made in the precession repo (`ilias-precession-simulation`,
commit `5f36b9b`) as a third build next to its toy top and lab gyroscope, then moved here: it doesn't belong to the
ILIAS learning module (gated learning path for the lab experiment, light theme, panel rules), and further lecture
experiments will follow in the same style. The precession repo was returned to its state before (module and original
untouched); its physics and view scripts were **copied**, not shared: a fix on one side doesn't reach the other.

| | |
|---|---|
| Site | static, GitHub Pages, published on a `site-v*` tag (`.github/workflows/pages.yml`) |
| Unity | one project `unity-simulations/` for all simulations, Unity 6000.6.4f1, URP, Web |
| Simulations | `gyroscope/` (Vorlesungskreisel) |


## 2 Layout

```
unity-simulations/Assets/Shared/        the noir look and what every simulation uses
  Materials/                arrows L (yellow), ω (white), D (red), marker (red), snowflake, trail, label (black halo)
  NoirVolumeProfile.asset   post-processing: bloom, tonemapping, contrast, vignette, film grain
  Textures/                 studio_reflection.hdr (small cubemap: reflections for metal)
  Meshes/                   SmoothCylinder (96 sides)
  Scripts/                  SimulationTime, OrbitCamera, VectorArrow(3D), ArrowBase, VectorLabel, ConeMesh, HelperCone,
                            PhysicsToUnity
  Plugins/SimBridge.jslib   C# → page: window.onSimMessage(message)
  Editor/                   NoirEditor (FindInScene, labels, smooth cylinder), SimBuild (web builds into the site)
unity-simulations/Assets/Gyroscope/     the Vorlesungskreisel: scene, apparatus, controller, helpers, bridge, physics, tests, setup
unity-simulations/Assets/WebGLTemplates/Noir   loading screen of the full-window version (<sim>/sim/index.html)
assets/                     the site: noir.css, sim.js
<name>/                     one simulation's page (index.html + <name>.js) and its build (sim/)
```

**One Unity project for all simulations**, `unity-simulations/` (Carlo, 2026-11-20; named so for the Hub and file managers): the look is shared by construction, one set of
packages and settings. Each simulation has its own scene and build profile and builds only its own scene.

**The page holds the canvas itself** (no iframe): commands go straight to `unityInstance.SendMessage`, the state
comes back through a global function. No postMessage, origin check or allowlist — those were needed in the module
only because Angular hosted the build in an iframe.


## 3 The Vorlesungskreisel: apparatus and physics

A thin, light disk (with a picture on top) on a long rod; the rod ends in a heavy tripod with a ring at its feet.
Disk, rod, tripod and ring are one rigid body spinning about the rod; the tripod's apex rests on the tip of a thin fixed
pin on a pedestal. Numbers are **estimates** (`LectureGyroscope.asset`): disk 150 g, R 0.20 m at +0.75 m; rod 0.35 kg;
legs 0.6 kg; ring 2.5 kg, R 0.09–0.14 m at −0.119 m. I₃ ≈ 0.041 kg m², I₁ ≈ 0.21 + m·0.75² kg m².

| Decision | Why |
|---|---|
| **Heavy ring below the tip → the bare top hangs** (k < 0) | The real apparatus carries most weight in the tripod (Carlo). A hanging top is stable without spin and precesses *against* its spin sense — the first insight of the page. |
| **Add-on weight on the hub** (0–300 g): 120 g balance it (k = 0 exactly within ±5 g), more makes it top-heavy | One apparatus shows all three regimes. The snap to k = 0 lets the force-free helpers (nutation cones) appear; physically it is a fine-tuned weight. The ring's height is chosen so that 120 g is the balance. |
| **Stop at 32°** (`ApparatusRig.stopThetaDeg`): a tripod leg touches the pin; the physics stops, `fallen` until restart | Slow top-heavy tops tip; better an honest stop than passing through the pin. A slider change after the stop restarts. |
| **Vectors drawn from the spike on the hub** (`VectorDisplay.anchorHeight`) | From the pivot, L⃗ and ω⃗ would run hidden inside the 75 cm rod. |
| Physics = the precession repo's `GyroscopeModel` (symmetric heavy top, Euler angles, reduced form, RK4 with substeps, double) | Tested; k < 0 works unchanged (tests: energy, reversed steady rate, stability limit, tipping). |

Physics notes:
- Steady precession exists for a hanging top at any spin; for a top-heavy one only if I₃²ω₃² ≥ 4 k I₁ cos θ
  (300 g, 15°: ≈ 330 rpm). Released at 15° with 300 g: tips below ≈ 350 rpm, dips to 26° at 400 rpm, < 18° from 600.
- Euler angles are singular at θ = 0. The hanging top released at low spin swings back towards the vertical: with spin
  it turns just short of it (≈ 0.07° at 1 rpm), at exactly 0 rpm it swings through (θ < 0, harmless). Checked stable at
  the real step (0.2 ms); a very small but nonzero spin with a much larger step would not be.
- The rest (conventions, display spin limiter, trail fed from the integration, `SimulationTime`) is as in the precession
  repo's design notes, section 3.


## 4 The noir look (Carlo, tuned on screenshots 2026-11-18)

The original toy top's look (black stage, spotlight pool, snow lit only in the spot, emissive vectors with bloom,
film grain), scaled ×4 to an apparatus 1.6 m tall. Colour only for the red marker and the vectors; everything else
greys. **Precession light grey** (Carlo: no blue). Cones: Nutationskegel grey, Rastpolkegel yellow (around L⃗),
Gangpolkegel white (contains ω⃗).

Lessons (each was a visible fault first):
- **Metal needs something to reflect.** Without baked lighting there is no reflection probe: steel and chrome mirror the
  black stage and turn black. A small studio cubemap at intensity 0.55, plus ambient 0.09 (original 0.04).
- **The original's rim light** (directional, on the camera) came from behind 20° above: at low camera angles every
  horizontal top (foot, collar, floor) mirrored it into the bloom as a white ball. From 3° below the horizon it still
  rims the vertical parts but no upward face can catch it. With that fixed the materials could be glossy again
  (chrome 0.85, lacquer 0.75).
- **Floor rough** (smoothness 0.08): at grazing angles the Fresnel highlight of the spot bloomed. The camera stays above
  4° pitch.
- **Light cones**: additive URP Unlit with an alpha ramp along the cone (HelperCone's UV v: apex 0 → rim 1), faint
  (surface 0.1–0.14); rim lines below the bloom threshold. Brighter ones dominated the scene.
- The skybox is gone (the camera clears to black; it cost 2 MB).

The disk shows a picture (`Assets/Gyroscope/Textures/DiskArt.png`, square, the disk is the inscribed circle); until it
exists a generated placeholder (moon behind cell bars, one red spot). It must be original art: no characters from
films or comics.


## 5 Page ↔ simulation contract (Vorlesungskreisel)

Page → simulation: `unityInstance.SendMessage("WebBridge", cmd, value)`, through `sim.send(cmd, value)`
(`assets/sim.js`: objects as JSON, booleans as 1/0, queued until Unity runs).

| Command | Value | Effect |
|---|---|---|
| `ApplyPreset` | JSON, complete state (missing keys → defaults of `GyroscopePreset`) | one restart |
| `SetHelpers` | JSON, partial `{L, omega, D, labels, precessionCone, nutationCone, spaceCone, bodyCone, trail}` | live |
| `SetRpm`, `SetAddOnMassGrams`, `SetTheta0Deg` | number | the running top continues (segment + 1) |
| `SetStartMode` | `"guided"` / `"released"` | restart |
| `SetFriction`, `SetPushThetaDot`, `SetPushPhiDot` | number | no restart |
| `SetTimeScale`, `SetPaused` | number / 0–1 | |
| `Restart`, `Push` | — | a `Push` right after a preset hits the new run |

Simulation → page: `window.onSimMessage({source: "gyroscope", type: "ready" | "state", …})`; state ~10 Hz real time,
also paused: `run, segment, time, thetaDeg, phiDeg, phiDot (signed: < 0 = against the spin), omegaR, omegaP,
addOnMassGrams, balanced, paused, spinLimited, fallen`.

The cones are gated by physics in the simulation (precession cone only with torque; nutation cones only balanced and
after a push), the switches only say what *may* be shown. All switches off = the bare experiment.


## 6 The site

- **Plain static pages**, no framework and no build step: a few pages with sliders and text don't need one, and Carlo
  reads Python/C more easily than TypeScript. Shared look in `assets/noir.css`, shared code in `assets/sim.js`
  (loading with progress and errors, command queue, slider/switch/button builders).
- **One simulation, several scenarios** per page (chips): one WebGL load; each chip sends a complete preset and swaps
  controls and text. Vorlesungskreisel: Hängend, Ausbalanciert (preset + automatic push), Kopflastig, Frei.
- Always noir, German, formal "Sie". No external fonts or CDNs (GDPR).
- `<sim>/sim/index.html` (from the Noir template) is the full-window version without text, linked as "Vollbild".


## 7 Build and deploy

- `SimBuild` builds a simulation's profile straight into its site folder (`../gyroscope/sim`); the folder name gives
  the file names the page loads (`sim.loader.js`, `sim.data.unityweb`, …). Profile *Web - Gyroscope - Release*:
  derived from the precession original's Pages profile (gzip **with decompression fallback**, because GitHub Pages
  sends no `Content-Encoding`), scene list = the gyroscope scene only, template Noir.
- The builds are committed (Pages can't run Unity); the workflow publishes on a `site-v*` tag only, so half-finished
  work on master never goes live, and it uploads only the site (start page, `assets/`, simulation folders), never
  `unity-simulations/`.


## 8 Adding a simulation

1. `unity-simulations/Assets/<Name>/`: scene (start from a copy of the gyroscope scene for camera, lights, volume, snow, floor),
   scripts, a bridge MonoBehaviour on a `WebBridge` object posting through `SimPostToPage`, an editor setup script
   with rerunnable menu steps.
2. Build profile *Web - <Name> - Release* (duplicate the gyroscope's, change the scene list) and a menu item in
   `SimBuild` with output `../<name>/sim`.
3. Site folder `<name>/` with `index.html` + `<name>.js` (use `assets/sim.js`), a card in `index.html`.
4. Keep the physics in a plain C# assembly without engine references and test it with `dotnet` (precession repo,
   design notes §6).


## 9 Pitfalls

- **Menu steps in Play mode**: material assets save, the scene doesn't, and the change is lost on exit. The setup
  steps refuse to run in Play mode.
- Renamed C# classes keep their scene references only if the `.cs.meta` (GUID) stays and the file name matches the
  class; renamed serialized fields need `[FormerlySerializedAs]` (the controller's `rig` was `lectureRig`).
- Batch mode needs the editor closed for that project (lock file).
- The TMP font assets get re-serialized by TextMeshPro now and then; don't commit those changes by accident.
- Precession repo pitfalls (shadowing, struct copies, prefabs, pink shaders from Editor folders, build profiles
  override Player settings, Strip Engine Code) apply here too: its `design_notes.md`, section 6.


## 10 Open items

- Carlo's disk picture (`DiskArt.png`); real masses and sizes of the lecture apparatus, if anyone measures them.
- Touch: the orbit camera has no touch rotation; the page is usable on phones only for watching.
- Mouse wheel over the canvas zooms instead of scrolling the page.
- The precession repo's design notes still describe the lab/module side; keep any physics fix in mind for both copies.
