// Vorlesungskreisel – the page around the simulation: scenarios, controls, readouts, texts.
// The simulation (Unity, build in sim/) takes commands of GyroscopeBridge (unity/Assets/Gyroscope/Scripts) and
// reports its state ~10 times per second; see the contract in ../design_notes.md.
import { loadSimulation, slider, toggle, button, group, el } from "../assets/sim.js";

const sim = loadSimulation(document.getElementById("stage"), { buildUrl: "sim", name: "sim" });

const BALANCE_GRAMS = 120;                     // add-on weight on the hub that puts the centre of mass into the tip


// ---- helpers (what the simulation may draw) ----

const HELPERS = [
  { key: "L", label: "Drehimpuls L", swatch: "#f5d10a" },
  { key: "omega", label: "Winkelgeschwindigkeit ω", swatch: "#ffffff" },
  { key: "D", label: "Drehmoment D", swatch: "#e0192f" },
  { key: "labels", label: "Beschriftungen" },
  { key: "trail", label: "Spur der Stabspitze" },
  { key: "precessionCone", label: "Präzessionskegel", swatch: "#dcdfe6" },
  { key: "nutationCone", label: "Nutationskegel", swatch: "#aaafb9" },
  { key: "spaceCone", label: "Rastpolkegel", swatch: "#f5d10a" },
  { key: "bodyCone", label: "Gangpolkegel", swatch: "#ffffff" },
];
const ALL_ON = Object.fromEntries(HELPERS.map((h) => [h.key, true]));


// ---- scenarios ----

const SCENARIOS = [
  {
    id: "hanging",
    title: "Hängend",
    preset: { rpm: 300, addOnMassGrams: 0, theta0Deg: 15, startMode: "guided" },
    controls: ["rpm", "theta"],
    show: { ...ALL_ON, nutationCone: false, spaceCone: false, bodyCone: false },
    text: `
      <h2>Der hängende Kreisel präzediert rückwärts</h2>
      <p>Ohne Zusatzgewicht liegt der Schwerpunkt unter der Spitze. Kippt der Stab, zieht die Schwerkraft ihn zurück
      zur Senkrechten: Das Drehmoment <b style="color:var(--red)"><span class="vec">D</span></b> zeigt genau andersherum als bei einem
      Spielkreisel, dessen Schwerpunkt über der Spitze liegt.</p>
      <p>Weil <span class="formula"><span class="vec">D</span> = d<span class="vec">L</span>/dt</span>, schiebt das Drehmoment den Drehimpuls
      <b style="color:var(--yellow)"><span class="vec">L</span></b> seitlich weiter – und mit ihm den Stab. Die Spitze des Stabs läuft auf
      einem Kegel um die Senkrechte, aber <em>gegen</em> den Drehsinn der Scheibe.</p>
      <p class="note">Probieren Sie: Drehen Sie die Drehzahl auf 0. Ohne Drehimpuls gibt es keine Präzession – der
      Kreisel schwingt nur noch wie ein Pendel durch die Senkrechte. Je schneller er sich dreht, desto langsamer
      präzediert er: <span class="formula">ω<sub>P</sub> ≈ D / (L sin θ)</span>, und L wächst mit der Drehzahl.</p>`,
  },
  {
    id: "balanced",
    title: "Ausbalanciert",
    preset: { rpm: 300, addOnMassGrams: BALANCE_GRAMS, theta0Deg: 10, startMode: "guided", pushThetaDot: 0.6 },
    controls: ["rpm", "push"],
    pushAfterPreset: true,
    show: { ...ALL_ON, D: false, precessionCone: false },
    text: `
      <h2>Ohne Drehmoment: Nutation und die drei Kegel</h2>
      <p>Mit ${BALANCE_GRAMS}&nbsp;g auf der Nabe liegt der Schwerpunkt genau in der Spitze. Die Schwerkraft übt
      kein Drehmoment aus, also bleibt der Drehimpuls <b style="color:var(--yellow)"><span class="vec">L</span></b> nach Betrag und Richtung
      fest. Nach einem Stoß zeigen Stab, <b><span class="vec">ω</span></b> und <span class="vec">L</span> aber in verschiedene Richtungen – und der Stab beginnt zu
      nicken: die Nutation. Drei Kegel beschreiben sie:</p>
      <ul>
        <li><b>Nutationskegel</b> (grau): Der Stab läuft auf einem Kegel um das feste <span class="vec">L</span>.</li>
        <li><b>Rastpolkegel</b> (gelb): Die momentane Drehachse <span class="vec">ω</span> läuft um <span class="vec">L</span>; dieser Kegel steht fest im Raum.</li>
        <li><b>Gangpolkegel</b> (weiß): <span class="vec">ω</span> um den Stab; dieser Kegel sitzt fest im Kreisel und rollt auf dem
        Rastpolkegel ab. Ihre Berührlinie ist in jedem Moment <span class="vec">ω</span>.</li>
      </ul>
      <p>Der Vorlesungskreisel ist ein „langer“ Kreisel: Um eine Querachse ist er viel träger als um den Stab. Dann
      liegt <span class="vec">ω</span> zwischen Stab und <span class="vec">L</span>, und der Gangpolkegel rollt <em>außen</em> auf dem Rastpolkegel.</p>
      <p class="note">Probieren Sie: Stoßen Sie bei doppelter Drehzahl gleich stark an. Der Kreisel nickt doppelt so
      schnell, und die Kegel werden etwa halb so weit: <span class="formula">ω<sub>N</sub> = L / I<sub>1</sub></span>
      wächst mit der Drehzahl.</p>`,
  },
  {
    id: "topHeavy",
    title: "Kopflastig",
    preset: { rpm: 250, addOnMassGrams: 300, theta0Deg: 15, startMode: "released" },
    controls: ["rpm", "theta"],
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
      <p class="note">Probieren Sie 250, 400, 600 und 1000&nbsp;U/min, jeweils mit Neustart. Bei 250 kippt er, bei
      400 schlägt er tiefe Bögen bis fast an den Stift, ab 600 nickt er nur noch leicht.</p>`,
  },
  {
    id: "free",
    title: "Frei",
    preset: { rpm: 400, addOnMassGrams: 60, theta0Deg: 15, startMode: "guided" },
    controls: ["rpm", "mass", "theta", "push", "startMode"],
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
let helpersOn = true;                                       // "Alle Hilfen" master switch
let show = { ...scenario.show };
let paused = false;
let slowMotion = false;
const SLOW = 0.25;

const panel = document.getElementById("panel");
const textBox = document.getElementById("scenario-text");
const notice = document.getElementById("notice");
const chips = document.getElementById("scenarios");
const readout = {};


function sendHelpers() {
  const flags = helpersOn ? show : Object.fromEntries(HELPERS.map((h) => [h.key, false]));
  sim.send("SetHelpers", flags);
}

function applyScenario(next) {
  scenario = next;
  show = { ...next.show };
  const preset = { pushThetaDot: 0.6, ...next.preset, timeScale: slowMotion ? SLOW : 1, paused,
                   show: helpersOn ? show : Object.fromEntries(HELPERS.map((h) => [h.key, false])) };
  sim.send("ApplyPreset", preset);
  if (next.pushAfterPreset) sim.send("Push");               // applied after the restart (same physics step)
  [...chips.children].forEach((c) => c.setAttribute("aria-pressed", c.dataset.id === next.id));
  textBox.innerHTML = next.text;
  buildPanel();
}


// ---- panel ----

const rpmFormat = (v) => `${v} U/min`;

function buildPanel() {
  const p = scenario.preset;
  const controls = {
    rpm: () => slider({ label: "Drehzahl", min: 0, max: 1000, step: 10, value: p.rpm, format: rpmFormat,
                        onInput: (v) => sim.send("SetRpm", v) }),
    mass: () => slider({ label: "Zusatzgewicht auf der Nabe", min: 0, max: 300, step: 10, value: p.addOnMassGrams,
                         format: (v) => `${v} g${v === BALANCE_GRAMS ? " · ausbalanciert" : v < BALANCE_GRAMS ? " · hängend" : " · kopflastig"}`,
                         onInput: (v) => sim.send("SetAddOnMassGrams", v) }),
    theta: () => slider({ label: "Neigung θ₀", min: 5, max: 25, step: 1, value: p.theta0Deg, format: (v) => `${v}°`,
                          onInput: (v) => sim.send("SetTheta0Deg", v) }),
    push: () => slider({ label: "Stoßstärke", min: 0.2, max: 2, step: 0.1, value: p.pushThetaDot ?? 0.6,
                         format: (v) => `${v.toFixed(1)} rad/s`, onInput: (v) => sim.send("SetPushThetaDot", v) }),
    startMode: () => toggle({ label: "Geführt starten (sonst losgelassen)", checked: p.startMode === "guided",
                              onChange: (on) => sim.send("SetStartMode", on ? "guided" : "released") }),
  };
  const experiment = group("Versuch", ...scenario.controls.map((c) => controls[c]()));

  // helpers: master switch + one per helper; with the master off the others are greyed out but keep their state
  const rows = HELPERS.map((h) => toggle({
    label: h.label, swatch: h.swatch, checked: show[h.key],
    onChange: (on) => { show[h.key] = on; sendHelpers(); },
  }));
  rows.forEach((r) => r.enable(helpersOn));
  const master = toggle({ label: "Alle Hilfen", checked: helpersOn, onChange: (on) => {
    helpersOn = on;
    rows.forEach((r) => r.enable(on));
    sendHelpers();
  }});
  const helpers = group("Hilfen", master, ...rows);

  const pause = button({ label: "Pause", onClick: () => {
    paused = !paused;
    pause.setAttribute("aria-pressed", paused);
    sim.send("SetPaused", paused);
  }});
  pause.setAttribute("aria-pressed", paused);
  const slow = button({ label: "Zeitlupe", onClick: () => {
    slowMotion = !slowMotion;
    slow.setAttribute("aria-pressed", slowMotion);
    sim.send("SetTimeScale", slowMotion ? SLOW : 1);
  }});
  slow.setAttribute("aria-pressed", slowMotion);
  const actions = el("div", { class: "buttons" },
    button({ label: "Stoß", primary: true, onClick: () => sim.send("Push") }),
    pause, slow,
    button({ label: "Neustart", onClick: () => sim.send("Restart") }));

  const list = el("dl", { class: "readouts" });
  for (const [key, label] of [["omegaR", "Drehzahl"], ["omegaP", "Präzession"], ["period", "Umlaufzeit"],
                              ["theta", "Neigung θ"], ["state", "Zustand"]]) {
    readout[key] = el("dd", {}, "–");
    list.append(el("dt", {}, label), readout[key]);
  }

  panel.replaceChildren(experiment, el("div", { class: "panel-group" }, el("span", { class: "label-caps" }, "Ablauf"),
                                       actions, el("span", { class: "label-caps" }, "Messwerte"), list), helpers);
}


// ---- readouts and notices from the simulation ----

sim.onMessage((m) => {
  if (m.type !== "state") return;
  const rpm = m.omegaR * 60 / (2 * Math.PI);
  readout.omegaR.textContent = `${Math.round(rpm)} U/min`;
  const precessing = m.omegaP > 1e-3 && !m.balanced && !m.fallen;
  readout.omegaP.textContent = precessing
    ? `${m.omegaP.toFixed(2)} rad/s, ${m.phiDot < 0 ? "gegen den" : "im"} Drehsinn`
    : "–";
  readout.period.textContent = precessing ? `${(2 * Math.PI / m.omegaP).toFixed(1)} s` : "–";
  readout.theta.textContent = `${m.thetaDeg.toFixed(1)}°`;
  readout.state.textContent = m.fallen ? "gekippt"
    : m.balanced ? "ausbalanciert"
    : m.addOnMassGrams < BALANCE_GRAMS ? "hängend" : "kopflastig";

  notice.hidden = !m.fallen;
  notice.textContent = m.fallen
    ? "Der Kreisel ist gegen den Stift gekippt – zu langsam für sein Gewicht. Neustart oder mehr Drehzahl."
    : "";
});


// ---- start ----

for (const s of SCENARIOS) {
  const chip = el("button", { type: "button", class: "chip", "data-id": s.id, "aria-pressed": "false",
                              onclick: () => applyScenario(s) }, s.title);
  chips.append(chip);
}
applyScenario(SCENARIOS[0]);
