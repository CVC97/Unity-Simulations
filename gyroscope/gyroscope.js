// Vorlesungskreisel – the page around the simulation: scenarios, controls, readouts, texts.
// The simulation (Unity, build in sim/) takes commands of GyroscopeBridge (unity-simulations/Assets/Gyroscope/Scripts) and
// reports its state ~10 times per second; see the contract in ../design_notes.md.
import { loadSimulation, fullscreen, slider, segmented, switchButton, chip, button, iconButton, icon, section, el }
  from "../assets/sim.js";

const sim = loadSimulation(document.getElementById("stage"), { buildUrl: "sim", name: "sim" });

const BALANCE_GRAMS = 120;                     // add-on weight on the hub that puts the centre of mass into the tip
const vec = (symbol) => `<span class="vec">${symbol}</span>`;
const num = (value, digits = 0) =>             // German notation: decimal comma
  value.toLocaleString("de-DE", { minimumFractionDigits: digits, maximumFractionDigits: digits });


// ---- helpers (what the simulation may draw) ----

// symbol: shown on the chip instead of the name (vectors); color: the colour in the simulation
const HELPERS = {
  L:              { symbol: "L", color: "var(--vec-L)", name: "Drehimpuls" },
  omega:          { symbol: "ω", color: "var(--vec-omega)", name: "Winkelgeschwindigkeit" },
  D:              { symbol: "D", color: "var(--vec-torque)", name: "Drehmoment" },
  precessionCone: { color: "var(--cone-precession)", name: "Präzessionskegel" },
  nutationCone:   { color: "var(--cone-nutation)", name: "Nutationskegel" },
  spaceCone:      { color: "var(--vec-L)", name: "Rastpolkegel" },
  bodyCone:       { color: "var(--vec-omega)", name: "Gangpolkegel" },
};
// no chips, only the master switch: the labels (each hides with its vector or cone in the simulation) and the trail
const WITH_MASTER = ["labels", "trail"];
const ALL_ON = Object.fromEntries(Object.keys(HELPERS).map((k) => [k, true]));
const ALL_OFF = Object.fromEntries([...Object.keys(HELPERS), ...WITH_MASTER].map((k) => [k, false]));
const WITH_TORQUE = ["L", "omega", "D", "precessionCone"];
const FORCE_FREE = ["L", "omega", "nutationCone", "spaceCone", "bodyCone"];


// ---- scenarios ----
// preset: the complete start state; controls: the sliders of this scenario; helpers: the display chips that can show
// anything here (the simulation gates the cones by physics anyway); show: which of all helpers start on.

const SCENARIOS = [
  {
    id: "hanging",
    title: "Hängend",
    preset: { rpm: 300, addOnMassGrams: 0, theta0Deg: 15, startMode: "guided" },
    controls: ["rpm", "theta"],
    helpers: WITH_TORQUE,
    show: { ...ALL_ON, nutationCone: false, spaceCone: false, bodyCone: false },
    text: `
      <h2>Der hängende Kreisel präzediert rückwärts</h2>
      <p>Ohne Zusatzgewicht liegt der Schwerpunkt unter der Spitze. Kippt der Stab, zieht die Schwerkraft ihn zurück
      zur Senkrechten: Das Drehmoment <b class="q-torque">${vec("D")}</b> zeigt genau andersherum als bei einem
      Spielkreisel, dessen Schwerpunkt über der Spitze liegt.</p>
      <p>Weil <span class="formula">${vec("D")} = d${vec("L")}/dt</span>, schiebt das Drehmoment den Drehimpuls
      <b class="q-L">${vec("L")}</b> seitlich weiter – und mit ihm den Stab. Die Spitze des Stabs läuft auf einem
      Kegel um die Senkrechte, aber <em>gegen</em> den Drehsinn der Scheibe.</p>
      <p class="note"><span class="label-caps">Probieren Sie</span>Drehen Sie die Drehzahl auf 0. Ohne Drehimpuls gibt
      es keine Präzession – der Kreisel schwingt nur noch wie ein Pendel durch die Senkrechte. Je schneller er sich
      dreht, desto langsamer präzediert er: <span class="formula">ω<sub>P</sub> ≈ D / (L sin θ)</span>, und L wächst
      mit der Drehzahl.</p>`,
  },
  {
    id: "balanced",
    title: "Ausbalanciert",
    preset: { rpm: 300, addOnMassGrams: BALANCE_GRAMS, theta0Deg: 10, startMode: "guided", pushThetaDot: 0.6 },
    controls: ["rpm", "push"],
    pushAfterPreset: true,
    helpers: FORCE_FREE,
    show: { ...ALL_ON, D: false, precessionCone: false },
    text: `
      <h2>Ohne Drehmoment: Nutation und die drei Kegel</h2>
      <p>Mit ${BALANCE_GRAMS}&nbsp;g auf der Nabe liegt der Schwerpunkt genau in der Spitze. Die Schwerkraft übt kein
      Drehmoment aus, also bleibt der Drehimpuls <b class="q-L">${vec("L")}</b> nach Betrag und Richtung fest. Nach
      einem Stoß zeigen Stab, <b>${vec("ω")}</b> und ${vec("L")} aber in verschiedene Richtungen – und der Stab beginnt
      zu nicken: die Nutation. Drei Kegel beschreiben sie:</p>
      <ul>
        <li><b>Nutationskegel</b> (grau): Der Stab läuft auf einem Kegel um das feste ${vec("L")}.</li>
        <li><b>Rastpolkegel</b> (gelb): Die momentane Drehachse ${vec("ω")} läuft um ${vec("L")}; dieser Kegel steht
        fest im Raum.</li>
        <li><b>Gangpolkegel</b> (weiß): ${vec("ω")} um den Stab; dieser Kegel sitzt fest im Kreisel und rollt auf dem
        Rastpolkegel ab. Ihre Berührlinie ist in jedem Moment ${vec("ω")}.</li>
      </ul>
      <p>Der Vorlesungskreisel ist ein „langer“ Kreisel: Um eine Querachse ist er viel träger als um den Stab. Dann
      liegt ${vec("ω")} zwischen Stab und ${vec("L")}, und der Gangpolkegel rollt <em>außen</em> auf dem
      Rastpolkegel.</p>
      <p class="note"><span class="label-caps">Probieren Sie</span>Stoßen Sie bei doppelter Drehzahl gleich stark an.
      Der Kreisel nickt doppelt so schnell, und die Kegel werden etwa halb so weit: <span class="formula">ω<sub>N</sub>
      = L / I<sub>1</sub></span> wächst mit der Drehzahl.</p>`,
  },
  {
    id: "topHeavy",
    title: "Kopflastig",
    preset: { rpm: 250, addOnMassGrams: 300, theta0Deg: 15, startMode: "released" },
    controls: ["rpm", "theta"],
    helpers: WITH_TORQUE,
    show: { ...ALL_ON, nutationCone: false, spaceCone: false, bodyCone: false },
    text: `
      <h2>Schnell gegen langsam</h2>
      <p>Mit 300&nbsp;g auf der Nabe liegt der Schwerpunkt über der Spitze. Jetzt verhält sich der Kreisel wie ein
      Spielkreisel: Er präzediert <em>im</em> Drehsinn der Scheibe – solange er schnell genug ist.</p>
      <p>Losgelassen fällt der Stab zuerst ein Stück und fängt sich dann in Nutationsbögen. Je langsamer der Kreisel,
      desto tiefer die Bögen. Eine gleichmäßige Präzession gibt es nur oberhalb einer Grenze,
      <span class="formula">I<sub>3</sub>²ω<sub>R</sub>² ≥ 4 m g s I<sub>1</sub> cos θ</span> (s: Abstand des
      Schwerpunkts von der Spitze); bei 15° Neigung liegt sie hier bei etwa 330&nbsp;U/min. Darunter fängt er sich
      nicht mehr: Er kippt, bis ein Bein des Dreibeins an den Stift schlägt.</p>
      <p class="note"><span class="label-caps">Probieren Sie</span>250, 400, 600 und 1000&nbsp;U/min, jeweils mit
      Neustart. Bei 250 kippt er, bei 400 schlägt er tiefe Bögen bis fast an den Stift, ab 600 nickt er nur noch
      leicht.</p>`,
  },
  {
    id: "free",
    title: "Frei",
    preset: { rpm: 400, addOnMassGrams: 60, theta0Deg: 15, startMode: "guided" },
    controls: ["rpm", "mass", "theta", "push", "startMode"],
    helpers: Object.keys(HELPERS),
    show: { ...ALL_ON },
    text: `
      <h2>Frei experimentieren</h2>
      <p>Alle Regler auf einmal. Das Zusatzgewicht verschiebt den Schwerpunkt: unter ${BALANCE_GRAMS}&nbsp;g hängt der
      Kreisel, bei ${BALANCE_GRAMS}&nbsp;g ist er ausbalanciert, darüber kopflastig. Der Präzessionskegel erscheint
      nur mit Drehmoment, die drei Nutationskegel nur ohne – und erst nach einem Stoß.</p>
      <p>„Geführt“ startet den Kreisel so, als hätte man ihn mit der Hand genau in die gleichmäßige Präzession
      geschoben; „losgelassen“ startet er aus der Ruhe und nickt.</p>`,
  },
];


// ---- page state ----

let scenario = SCENARIOS[0];
let values = {};                                            // the current settings (start: the scenario's preset)
let show = {};                                              // display chips; kept while the master switch is off
let helpersOn = true;                                       // the master switch over the chips
let paused = false;
let speed = 1;                                              // time scale, 1 = real time

const block = document.getElementById("sim");
const tabs = document.getElementById("scenarios");
const toolbar = document.getElementById("toolbar");
const panel = document.getElementById("panel");
const hud = document.getElementById("hud");
const textBox = document.getElementById("scenario-text");
const notice = document.getElementById("notice");
const fullscreenButton = document.getElementById("fullscreen");

const flags = () => (helpersOn ? { ...show, labels: true, trail: true } : ALL_OFF);
const sendHelpers = () => sim.send("SetHelpers", flags());

const regime = (grams) => grams === BALANCE_GRAMS ? "ausbalanciert" : grams < BALANCE_GRAMS ? "hängend" : "kopflastig";


function applyScenario(next) {
  scenario = next;
  values = { pushThetaDot: 0.6, ...next.preset };
  show = { ...next.show };
  sim.send("ApplyPreset", { ...values, timeScale: speed, paused, show: flags() });
  if (next.pushAfterPreset) sim.send("Push");               // applied after the restart (same physics step)
  [...tabs.children].forEach((tab) => tab.setAttribute("aria-selected", tab.dataset.id === next.id));
  textBox.replaceChildren(el("div", { html: next.text }));
  renderPanel();
}


// ---- tabs, toolbar, fullscreen (built once) ----

tabs.append(...SCENARIOS.map((s) => el("button", {
  type: "button", class: "tab", role: "tab", "data-id": s.id, "aria-selected": "false", "aria-controls": "scenario-text",
  onclick: () => { if (s !== scenario) applyScenario(s); },
}, s.title)));

const pauseButton = iconButton({ icon: "pause", label: "Pause", onClick: () => {
  paused = !paused;
  sim.send("SetPaused", paused);
  pauseButton.setIcon(paused ? "play" : "pause", paused ? "Weiter" : "Pause");
}});
const speedSlider = slider({ label: "Tempo", min: 0.05, max: 1, step: 0.05, value: speed,
                             format: (v) => `${num(v, 2)} ×`,
                             onInput: (v) => { speed = v; sim.send("SetTimeScale", v); } });
speedSlider.className = "speed";
speedSlider.querySelector("label").className = "label-caps";
speedSlider.title = "Zeitlupe: 1 = Echtzeit";
toolbar.append(
  pauseButton,
  iconButton({ icon: "restart", label: "Neustart", onClick: () => sim.send("Restart") }),
  button({ label: "Stoß", primary: true, onClick: () => sim.send("Push") }),
  speedSlider);

const full = fullscreen(block, (on) => {
  const label = on ? "Vollbild beenden (Esc)" : "Vollbild";
  fullscreenButton.replaceChildren(icon(on ? "collapse" : "expand"));
  fullscreenButton.setAttribute("aria-label", label);
  fullscreenButton.title = label;
});
fullscreenButton.addEventListener("click", () => full.toggle());


// ---- side panel (again on every scenario change) ----

function renderPanel() {
  const controls = {
    rpm: () => slider({ label: "Drehzahl", min: 0, max: 1000, step: 10, value: values.rpm,
                        format: (v) => `${num(v)} U/min`,
                        onInput: (v) => { values.rpm = v; sim.send("SetRpm", v); } }),
    mass: () => slider({ label: "Zusatzgewicht auf der Nabe", min: 0, max: 300, step: 10, value: values.addOnMassGrams,
                         format: (v) => `${num(v)} g · ${regime(v)}`,
                         onInput: (v) => { values.addOnMassGrams = v; sim.send("SetAddOnMassGrams", v); } }),
    theta: () => slider({ label: "Neigung θ₀", min: 5, max: 25, step: 1, value: values.theta0Deg,
                          format: (v) => `${num(v)}°`,
                          onInput: (v) => { values.theta0Deg = v; sim.send("SetTheta0Deg", v); } }),
    push: () => slider({ label: "Stoßstärke", min: 0.2, max: 2, step: 0.1, value: values.pushThetaDot,
                         format: (v) => `${num(v, 1)} rad/s`,
                         onInput: (v) => { values.pushThetaDot = v; sim.send("SetPushThetaDot", v); } }),
    startMode: () => segmented({ label: "Start", value: values.startMode,
                                 options: [{ value: "guided", label: "Geführt" }, { value: "released", label: "Losgelassen" }],
                                 onChange: (v) => { values.startMode = v; sim.send("SetStartMode", v); } }),
  };
  const experiment = section("Versuch", null, ...scenario.controls.map((c) => controls[c]()));

  // display: vectors as symbol chips, the rest by name; with the master switch off the chips are greyed out but keep
  // their state
  const chipFor = (key) => {
    const h = HELPERS[key];
    const c = chip({
      content: h.symbol ? el("span", { html: vec(h.symbol) }) : h.name,
      color: h.color, pressed: show[key], title: h.title ?? h.name,
      onChange: (on) => { show[key] = on; sendHelpers(); },
    });
    if (h.symbol) c.setAttribute("aria-label", h.name);
    c.disabled = !helpersOn;
    return c;
  };
  const vectorChips = scenario.helpers.filter((k) => HELPERS[k].symbol).map(chipFor);
  const otherChips = scenario.helpers.filter((k) => !HELPERS[k].symbol).map(chipFor);
  const master = switchButton({ label: "Hilfen", checked: helpersOn, onChange: (on) => {
    helpersOn = on;
    [...vectorChips, ...otherChips].forEach((c) => { c.disabled = !on; });
    sendHelpers();
  }});
  const display = section("Anzeige", master,
                          el("div", { class: "chip-grid vectors" }, ...vectorChips),
                          el("div", { class: "chip-grid" }, ...otherChips));

  panel.replaceChildren(experiment, display);
}


// ---- readouts on the stage, notices from the simulation ----

const readout = {};
for (const [key, label, wideOnly] of [["omegaR", "Drehzahl"], ["omegaP", "Präzession"], ["period", "Umlaufzeit", true],
                                      ["theta", "Neigung θ"], ["state", "Zustand", true]]) {
  const cls = wideOnly ? "wide-only" : null;
  readout[key] = el("dd", { class: cls }, "–");
  hud.append(el("dt", { class: cls }, label), readout[key]);
}

sim.onMessage((m) => {
  if (m.type !== "state") return;
  const rpm = m.omegaR * 60 / (2 * Math.PI);
  readout.omegaR.textContent = `${num(Math.round(rpm))} U/min`;

  const precessing = m.omegaP > 1e-3 && !m.balanced && !m.fallen;
  readout.omegaP.replaceChildren(precessing ? `${num(m.omegaP, 2)} rad/s ` : "–");
  if (precessing) readout.omegaP.append(el("small", {}, m.phiDot < 0 ? "gegen Drehsinn" : "im Drehsinn"));
  readout.period.textContent = precessing ? `${num(2 * Math.PI / m.omegaP, 1)} s` : "–";
  readout.theta.textContent = `${num(m.thetaDeg, 1)}°`;
  readout.state.textContent = m.fallen ? "gekippt" : m.balanced ? "ausbalanciert" : regime(m.addOnMassGrams);

  notice.hidden = !m.fallen;
  notice.textContent = m.fallen
    ? "Der Kreisel ist gegen den Stift gekippt – zu langsam für sein Gewicht. Neustart oder mehr Drehzahl."
    : "";
});


// ---- start ----

applyScenario(SCENARIOS[0]);
