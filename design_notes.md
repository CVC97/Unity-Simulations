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
| Simulations | `gyroscope/` (Vorlesungskreisel), `foucault/` (Foucault-Pendel, from 2026-11-24, section 11) |


## 2 Layout

```
unity-simulations/Assets/Shared/        the noir look and what every simulation uses
  Materials/                the apparatus materials every simulation uses (LectureSteel, Chrome, DarkSteel, CastIron,
                            Floor, Black; values in NoirEditor.LectureMaterials, since 2026-11-25); arrows L (yellow),
                            ω (white), D (red), marker (red), snowflake, trail, label (black halo)
  NoirVolumeProfile.asset   post-processing: bloom, tonemapping, contrast, vignette, film grain
  Textures/                 studio_reflection.hdr (small cubemap: reflections for metal)
  Meshes/                   SmoothCylinder (96 sides)
  Scripts/                  SimulationTime, OrbitCamera, VectorArrow(3D), ArrowBase, VectorLabel, ConeMesh, HelperCone,
                            PhysicsToUnity
  Shaders/                  LightShaft (a visible beam: soft at its silhouette, additive)
  Physics/                  Vec3d in the plain assembly SharedPhysics (no engine references); both physics
                            assemblies reference it (moved from Gyroscope/Physics on 2026-11-24, GUID kept)
  Plugins/SimBridge.jslib   C# → page: window.onSimMessage(message)
  Editor/                   NoirEditor (shared materials, FindInScene, labels, smooth cylinder), SimBuild (web builds
                            into the site)
unity-simulations/Assets/Gyroscope/     the Vorlesungskreisel: scene, apparatus, controller, helpers, bridge, physics, tests, setup
unity-simulations/Assets/Foucault/      the Foucault-Pendel: the same parts (section 11)
unity-simulations/Assets/WebGLTemplates/Noir   loading screen of the full-window version (<sim>/sim/index.html)
assets/                     the site: noir.css, sim.js, fonts/ (Bebas Neue, Inter; SIL OFL), thumbs/ (start page)
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
Gangpolkegel white (contains ω⃗). The Nutationskegel is a darker mid grey (110, 112, 118; Carlo, 2026-11-23): the cones
are additive and faint, so the earlier light grey at 0.14 looked as bright as the white Gangpolkegel at 0.1.

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
- **One set of materials, a light per apparatus** (Carlo, 2026-11-25). The Foucault-Pendel's own copies drifted apart
  over four rounds of "too bright" and still didn't reach the Kreisel's look: the cause was the light, not the
  surfaces. So the metals and the floor are shared assets (`Assets/Shared/Materials`, moved from the Kreisel's folder
  with their GUIDs), and each simulation gets the lighting its shape needs: the Kreisel, a tall silhouette, a soft spot
  from the side; the pendulum, which lies flat, a hard shaft from above (section 11). A scene keeps its own material
  only for parts the others don't have (the Kreisel's lacquer, rubber, disk).
- Spot shadows need the URP shadow distance (`PC_RPAsset`, the Web quality level): 3 m, set for the Kreisel's camera
  at 2.9 m, cut off every shadow in the Foucault hall (camera 7.5 m). 20 m since 2026-11-25; on the Kreisel this only
  keeps the shadows when zoomed out. The Mobile asset has no spot shadows at all.

The disk shows a picture (`Assets/Gyroscope/Textures/DiskArt.png`, square, the disk is the inscribed circle); until it
exists a generated placeholder (moon behind cell bars, one red spot). It must be original art: no characters from
films or comics.


## 5 Page ↔ simulation contract

### Vorlesungskreisel

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


### Foucault-Pendel

Same mechanics (`WebBridge`, `sim.send`, `SimPostToPage`), `FoucaultBridge`:

| Command | Value | Effect |
|---|---|---|
| `ApplyPreset` | JSON, complete state `{latitudeDeg, amplitude, frame, timeScale, paused, show}` (missing keys → defaults of `FoucaultPreset`) | one release |
| `SetHelpers` | JSON, partial `{omega, coriolis, plane, compass, labels, trace}` | live |
| `SetLatitude` | number, deg, north positive | the pendulum swings on (segment + 1) |
| `SetAmplitude` | number, m horizontally, 0.8–1.6 | released anew |
| `SetFrame` | `"earth"` / `"stars"` | live; stars is only shown at ±90° |
| `SetTimeScale` | number, the Zeitraffer factor (1 … 2000) | live; *not* Unity's time scale |
| `SetPaused` | 0–1 | |
| `Restart` | — | released anew |

Simulation → page: `{source: "foucault", type: "ready" | "state", …}`; state ~10 Hz real time, also paused: `run,
segment, time (s simulated), latitudeDeg, planeRateDegPerHour (signed: < 0 = clockwise), turnedDeg (measured, signed),
amplitude, swingPeriod, timeScale, actualTimeScale, lagging, frame (shown), starsAllowed, pinsDown, pinsTotal,
bobVisible, paused`.

The Coriolis arrow hides while the fan replaces the bob; labels and trace follow the master switch only (as on the
Kreisel); the fan, the degree ring and the pins are not helpers.


## 6 The site

- **Plain static pages**, no framework and no build step: a few pages with sliders and text don't need one, and Carlo
  reads Python/C more easily than TypeScript. Shared look in `assets/noir.css`, shared code in `assets/sim.js`
  (loading with progress and errors, command queue, wheel guard, fullscreen, control builders).
- **One simulation, several scenarios** per page (tabs): one WebGL load; each tab sends a complete preset and swaps
  controls, display chips and text. Vorlesungskreisel: Hängend, Ausbalanciert (preset + automatic push), Kopflastig, Frei.
- Always noir, German, formal "Sie". Fonts are bundled (`assets/fonts`, woff2 from Fontsource, OFL): no external
  fonts or CDNs (GDPR). Bebas Neue has one weight, so headings are weight 400 (no faux bold).

**The simulation block** (Carlo, 2026-11-20: the first panel looked "all over the place"): one bordered block of
scenario tabs on top of the stage, a toolbar under it, a side panel beside it (below it on narrow screens).
- Sharp corners everywhere; every button, tab and chip is `--control` high (2.25 rem). No underlined links: navigation
  is small caps with a red bar growing in on hover; buttons are bordered boxes.
- **Readouts on the stage** as a HUD (top left), not in the panel: they belong to what is seen.
- **Toolbar**: pause/play and restart as icon buttons, Stoß as the one red primary button, **Tempo as a slider**
  (0.05–1 × real time; Carlo: not a Zeitlupe button).
- **Side panel**: *Versuch* (only this scenario's sliders; start mode as a segmented control) and *Anzeige* (display
  chips in the simulation's colours, vectors by symbol (three per row), named ones as many per row as fit at the longest name's width, i.e. one per row in the side panel (2026-11-25: two per row cut off "Himmelsrichtungen"); a master switch greys them out but keeps their state). Each
  scenario lists only the chips that can show something there (no nutation cones while there is torque, no D⃗ and
  precession cone while balanced); the simulation gates the cones by physics anyway.
  Labels and trail have no chips (Carlo): they follow the master switch only. Each label already hides with its vector
  (`VectorLabel`) or cone (ω_P, `PrecessionDisplay`), so labels are coupled to what they label.
- On wide screens the panel is as tall as tabs + stage + toolbar (`contain: size`) and scrolls inside.
- **Fullscreen** takes the whole block (stage, toolbar, panel; the panel hides on narrow screens) via the Fullscreen
  API, Esc returns; without the API (iPhone) a fixed overlay. This replaced the header link to `sim/`, which had no
  way back. The full-window build `<sim>/sim/` still exists and now has a "Zurück" link to its page.
- **Mouse wheel**: over the canvas it scrolls the page until the simulation is clicked; then it zooms until the pointer
  leaves the stage (always in fullscreen). A hint says so. Done in the page (`sim.js`, a capture listener on window
  that keeps the event from Unity), no change in Unity.
- **Text** spans the page width in two columns (h2 across both), instead of one narrow column beside empty space.
  All running text is justified (Blocksatz, Carlo) with the browser's German hyphenation (`lang="de"`). The lead
  under the title ends at the line between simulation and panel; on narrow screens it takes the full width.
- Numbers in German notation (decimal comma). Vectors in the page text are bold italic, without arrows (Carlo: the CSS
  arrows didn't look like TeX).
- Texts are short (Carlo, 2026-11-23): what to look at and one thing to try; no apparatus numbers (masses, the
  balancing weight, rpm limits) – those are here, in section 3.
- **Start page** (Carlo, 2026-11-25): the experiments as one bordered, numbered list, a thin row each (number,
  thumbnail, tag + title, description, arrow), not cards side by side: more experiments will follow. Numbered in the
  order of the lecture (Vorlesungsplan Experimentalphysik 1: Bewegte Bezugssysteme before Starre Körper, so Foucault
  01, Kreisel 02), not by topic; topic subheadings once there are more than ~6–8. Hover as on the navigation: a red
  bar grows in on the left, the number turns red.
- **Thumbnails** `assets/thumbs/<name>.svg`, 4:3: greyscale line drawings of the apparatus in its spot pool (written
  by hand/script, no build step). They may be replaced by a greyscale still from the simulation later; keep 4:3.
- **No cross-links between simulation pages** (Carlo, 2026-11-25): the forced "Weitere Versuche" links don't scale;
  the way to another experiment is the overview (header and footer).
- Open: English as the main language with a header toggle was asked for and then put on hold (2026-11-20) – the
  simulation itself stays German (D for the torque) in any case.


## 7 Build and deploy

- `SimBuild` builds a simulation's profile straight into its site folder (`../gyroscope/sim`, `../foucault/sim`); the folder name gives
  the file names the page loads (`sim.loader.js`, `sim.data.unityweb`, …). Profile *Web - Gyroscope - Release*:
  derived from the precession original's Pages profile (gzip **with decompression fallback**, because GitHub Pages
  sends no `Content-Encoding`), scene list = the gyroscope scene only, template Noir.
- The builds are committed (Pages can't run Unity); the workflow publishes on a `site-v*` tag only, so half-finished
  work on master never goes live, and it uploads only the site (start page, `assets/`, simulation folders), never
  `unity-simulations/`.


## 8 Adding a simulation

1. `unity-simulations/Assets/<Name>/`: scene (start from a copy of the gyroscope scene for camera, lights, volume, snow, floor),
   scripts, a bridge MonoBehaviour on a `WebBridge` object posting through `SimPostToPage`, an editor setup script
   with rerunnable menu steps. Materials from `NoirEditor.LectureMaterials()`; the light designed for the apparatus'
   shape (section 4).
2. Build profile *Web - <Name> - Release* (duplicate the gyroscope's, change the scene list) and a menu item in
   `SimBuild` with output `../<name>/sim`.
3. Site folder `<name>/` with `index.html` + `<name>.js` (use `assets/sim.js`), a row in the start page's list at
   its place in the lecture (renumber the rows after it) and a thumbnail `assets/thumbs/<name>.svg` (section 6).
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
- Touch orbit (one finger) and pinch zoom (two) are in `OrbitCamera` (Shared) since 2026-11-20; they need a rebuild
  and a test on a real phone/tablet. The precession repo's copy of `OrbitCamera` doesn't have them.
- Page language: English + German toggle on hold (section 6).
- **The disk is too bright** (Carlo, 2026-11-23): seen from above, the top of the disk is a white blob in the bloom,
  its picture and edge lost. Likely the white placeholder art (bars, moon) fully lit by the spot and then pushed over
  the bloom threshold. To look at, in this order: the art's brightness (the final `DiskArt.png` may already fix it),
  the disk material (base colour / smoothness, so it stays below the bloom threshold), then the spot's intensity on the
  disk. A scene change: through the setup menu steps, then rebuild. Not started.
- **Foucault-Pendel** (section 11): written 2026-11-24, not yet run in the editor. Carlo: open
  `Assets/Foucault/FoucaultScene.unity`, *Foucault → Set up everything*, play (WebBridge → ⋮ → *Apply test preset*:
  pole, star view, 1000×), run the tests, then *Foucault → Build web version* and look at the page. Everything visual
  (light, materials, sizes, label offsets, fan and trace brightness) is a first guess to tune on screenshots.
- **Foucault look, fifth round** (Carlo, 2026-11-25: the fourth was better but low in contrast, uniformly dark grey,
  and the snow filled close views like a starfield; the Kreisel, "Sin City", is the reference). The light rethought
  (section 11, *Light*) and the materials made the Kreisel's (section 4). Needs *Foucault → Set up everything*, a
  rebuild and screenshots; also a look at the Kreisel (*Gyroscope → Set up everything* is not needed: its scene
  refers to the moved materials by GUID). To tune: the shaft's intensity (`ShaftMaterial(0.025f)`), the spot's (380), the snow's rates (100 + 50 /s), glow (0.12) and `flakeScale` (1.6). **Done** (Carlo, 2026-11-25: approved, "as the thumbnail"). **Dropped: a light that follows the swing plane** (a band of light along the plane, turning with it; following the bob was ruled out first: it strobes from 20× on and lights what moves, not the reference that turns). Why: it shows the plane a second time (trace, plane helper); it is a helper that can't be switched off, against "all switches off = the bare experiment" (section 5), and gating it would change the light when helpers are toggled; and it breaks the composition of concentric circles under one vertical beam. Worth reconsidering only for a lecture mode where helpers are part of the show.
- The precession repo's design notes still describe the lab/module side; keep any physics fix in mind for both copies.


## 11 The Foucault-Pendel: apparatus, physics, display (2026-11-24)

Built from `foucault_plan.md` (local note) after Carlo's answers to its open questions on 2026-11-24: no real pendulum
in Göttingen is known, so it is **estimated** (15 m, 25 kg steel bob of 26 cm, the Kreisel's grey metals, φ = 51.5° N); star-fixed view **only
at the poles** (option a); **true physics with a Zeitraffer** up to 2000×, no exaggerated Earth rotation; **ring of
pins** yes; **no Ellipse tab** (Airy precession stays in the tests only).

| Decision | Why |
|---|---|
| Physics: spherical pendulum in Cartesian East-North-Up, tension as constraint force, RK4 with a fixed 0.02 s step, then projected back onto \|r\| = L (`FoucaultModel`) | Spherical coordinates are singular at the bottom, which the bob passes every swing; the full nonlinear equations keep Airy precession. |
| Zeitraffer is the controller's own factor: each frame integrates `Time.deltaTime × factor` in fixed steps, at most 6000 per frame (`Lagging` beyond) | Unity's time scale is capped at 100 and would scale `fixedDeltaTime`. Pause still goes through `SimulationTime` (time scale 0). ≈ 13 M steps/s in .NET; 2000× needs 0.1 M. |
| Swing plane **measured**, not assumed: from the tensor ω₀² p pᵀ + v vᵀ of the horizontal motion (`SwingEllipse`), unwrapped every frame | It gives the plane at every instant, exact for a flat swing; the HUD's "Gedreht" and the plane helper show what the pendulum really does. The HUD's rate is the formula −15.04 °/h · sin φ. |
| **Fan** instead of the bob from 20× to 100× (`FanDisplay`): an additive sector from the suspension to the bob's arc, glowing at the arc; the bob hides at half | Above ~50× the bob swings several times per frame and only flickers. |
| **Trace** as chords between turning points (`TraceDisplay`), a new one only after the plane has turned 1.5° and from the other end of the swing (`traceSpacingDeg`); 120 kept = half a turn, a full star, older ones fade. With the bob hidden in the fan, the live piece is the newest whole swing | The Coriolis ellipse is far below a millimetre (b/a ≈ Ω sin φ / ω₀ ~ 10⁻⁴), so chords are exact. One per half swing (Carlo's screenshots, 2026-11-24) turned only ~0.02° apart and merged into a solid white wedge; spaced chords miss the centre by ≤ 1.6 cm, about the line width. |
| **Hall** = the Earth-fixed root (floor, platform, pins, trace, pendulum, helpers). Star view: the Hall turns at Ω sin φ beneath a fixed camera; switching back freezes it | The pendulum is its child, so in the star view its plane stands still without extra physics. A true inertial view elsewhere would tilt the hall around the Earth's axis (Carlo: poles only). |
| **Pins**: 48 on a ring 6 cm inside the stylus' reach, offset half a spacing from the starting plane; a pin falls when the stylus' path in one step comes within 1.5 cm (`PinRing`); the fall is drawn in real time | None falls on the first swing; in Göttingen the first one after ~15 min, all 48 after half a turn (~15 h); none at the equator. |
| **Light: an oculus** straight above the axis, 13 m up: a hard spot (16.6°, inner 14.5°) whose pool ends at 1.9 m, just beyond the swing at the slider's end; a subtle additive cone makes the shaft visible (`LightShaft` mesh, shader `Noir/LightShaft` in Shared: brightness ∝ \|n·v\| per face, i.e. the path length through the beam, so it fades at its silhouette; and upwards as v⁵). As a URP Unlit cone it read as a solid, hard-edged column. Snow through the hall (radius 4.5 m, 6 m high), flakes with a faint glow of their own (`FoucaultSnowflake`, emission 0.12) so they show dimly in the dark and light up in the shaft; uniform transform scale (a non-uniform one stretched the flakes), flakes 1.6 × the Kreisel's startSize (at its world size they were invisible at 7.5 m, at 3.3 × too big). Spot 380 (half the Kreisel's illuminance: darker, like the start page's thumbnail), ambient 0.015 (Kreisel 0.09). Platform `LectureBlack` (shared, 12/12/12, matte) so only what lies on it catches the light, as in the thumbnail (with the floor's grey the pool read as a grey disk); floor unlit black (`FoucaultVoid`): it is never in the light, and lit it mirrored the studio cubemap at grazing angles as a grey plane with a horizon | The Kreisel's soft side spot, even dimmed and narrowed, left the flat platform a large mid-grey area. Now the pool is a lit circle in black, the degree ring and letters glow outside it, the bob has a white cap and its shadow lies on the swing plane. On the axis, it is the same in the star view. |
| Ω (white, as ω on the Kreisel) on the pendulum's axis 2 m up, fixed length, with its parts Ω sin φ and Ω cos φ (light grey, no glow); Coriolis force (red, as D) at the bob, 150 m/N. Labels without the black halo (`FoucaultLabel.mat`) | Ω is the same everywhere; the vertical part is the teaching point. The force is a few mN. Labels have a fixed world size: beside the platform the orbiting camera came close and they grew huge. Closer than 6 m they shrink (`VectorLabel.nearDistance`, 2026-11-25): zoomed in at the bob, F_C still filled the view. The parts of Ω are labelled beside their middles, on the side away from Ω (`along`, `awayFrom`): at their tips, next to Ω's, the three overlapped in some views. F_C 0.3 m higher (`lift`): seen end-on its label sat on the bob. The halo is invisible on the Kreisel's black stage but showed as a dark border over the lit platform. |

Physics notes (tests in `Assets/Foucault/Tests/EditMode`, also run with `dotnet test`, see below):
- The plane turns at 0.9976 · Ω sin φ for 1.2 m amplitude: the 0.24 % is the expected finite-amplitude (Airy) part of
  the Coriolis ellipse, (3/8)(a/L)². Zero at the equator, opposite sense in the south.
- Airy: L = 10 m, a = 1 m, b = 1 cm gives 7.67 °/h (formula 7.66 °/h) – the plan's estimate holds.
- Without Ω the period matches the exact pendulum 4K(k)/ω₀ to 10⁻⁶; the Jacobi constant drifts < 10⁻⁶ of the swing
  energy in an hour.
- Pins: the stylus reaches every pin below its head for amplitudes 0.8–1.6 m (pins 11 cm; the stylus tip is 4.5 cm
  above the platform at rest and rises by r²/2L). Larger amplitudes would need taller pins.

Running the physics tests outside Unity: a scratch project with `Microsoft.NET.Test.Sdk`, `NUnit` 3.14,
`NUnit3TestAdapter`, `TargetFramework net10.0`, `EnableDefaultCompileItems false` and
`<Compile Include="…/Assets/Shared/Physics/*.cs; …/Assets/Foucault/Physics/*.cs; …/Assets/Foucault/Tests/EditMode/*.cs" />`
(likewise for Gyroscope), then `dotnet test`.

Scene: `FoucaultScene.unity` started as a file copy of the Kreisel's scene (own GUID), and the build profile
*Web - Foucault - Release* as a text copy of the Kreisel's (scene list, product name "Foucault-Pendel", new
productGUID). Everything else comes from the menu steps (*Foucault → 1 Build hall* removes the Kreisel's objects).

The page (`foucault/`): tabs Göttingen, Pol, Äquator, Südhalbkugel (presets of the latitude slider); panel *Versuch*
(Breitengrad, Auslenkung, Bezugssystem [Erde | Sterne], Sterne disabled away from the poles) and *Anzeige* (chips Ω,
F_C, Schwingungsebene, Himmelsrichtungen); toolbar pause, "Neu loslassen", Zeitraffer slider in 1-2-5 steps; HUD
Breitengrad, Drehung (°/h with sense), Volle Drehung, Zeit, Gedreht, Stifte.
