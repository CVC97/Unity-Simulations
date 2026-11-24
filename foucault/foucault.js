// Foucault-Pendel – the page around the simulation: places (scenarios), controls, readouts, texts.
// The simulation (Unity, build in sim/) takes commands of FoucaultBridge (unity-simulations/Assets/Foucault/Scripts) and
// reports its state ~10 times per second; see the contract in ../design_notes.md.
import { loadSimulation, fullscreen, slider, segmented, switchButton, chip, iconButton, icon, section, el }
  from "../assets/sim.js";

const sim = loadSimulation(document.getElementById("stage"), { buildUrl: "sim", name: "sim" });

const vec = (symbol) => `<span class="vec">${symbol}</span>`;
const num = (value, digits = 0) =>             // German notation: decimal comma
  value.toLocaleString("de-DE", { minimumFractionDigits: digits, maximumFractionDigits: digits });
const OMEGA = vec("Ω");
const FC = `${vec("F")}<sub>C</sub>`;

// Zeitraffer in 1-2-5 steps (Carlo: true physics, time-lapse up to ~2000×; the slider picks the index)
const FACTORS = [1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000];
const isPole = (deg) => Math.abs(deg) >= 89.99;   // the star-fixed view exists only there (design_notes, Foucault)


// ---- helpers (what the simulation may draw) ----

// html: shown on the chip instead of the name (vectors); color: the colour in the simulation
const HELPERS = {
  omega:    { html: OMEGA, color: "var(--vec-omega)", name: "Winkelgeschwindigkeit der Erde" },
  coriolis: { html: FC, color: "var(--vec-force)", name: "Corioliskraft" },
  plane:    { color: "var(--cone-precession)", name: "Schwingungsebene" },
  compass:  { color: "var(--cone-nutation)", name: "Himmelsrichtungen" },
};
// no chips, only the master switch: the labels (each hides with its vector) and the trace on the floor
const WITH_MASTER = ["labels", "trace"];
const ALL_ON = Object.fromEntries(Object.keys(HELPERS).map((k) => [k, true]));
const ALL_OFF = Object.fromEntries([...Object.keys(HELPERS), ...WITH_MASTER].map((k) => [k, false]));


// ---- scenarios (places) ----
// preset: the complete start state (the Zeitraffer stays as it is); text: what to look at, one thing to try.

const SCENARIOS = [
  {
    id: "goettingen",
    title: "Göttingen",
    preset: { latitudeDeg: 51.5 },
    text: `
      <h2>Die Schwingungsebene dreht sich</h2>
      <p>Das Pendel schwingt im Raum immer in derselben Ebene, und die Erde dreht sich darunter weg. Im Hörsaal sieht
      es deshalb so aus, als drehe sich die Ebene: auf der Nordhalbkugel im Uhrzeigersinn, von oben gesehen. Im
      mitdrehenden Bezugssystem der Erde ist es die Corioliskraft <span class="q-force">${FC}</span> (rot, stark
      vergrößert), die das Pendel bei jedem Schwung ein wenig nach rechts drückt.</p>
      <p>Für die Drehung zählt nur der senkrechte Anteil der Erddrehung, <span class="formula">Ω sin φ</span>. In
      Göttingen braucht die Ebene deshalb länger als einen Tag für eine volle Drehung.</p>
      <p class="note"><span class="label-caps">Probieren Sie</span>Den Zeitraffer hochdrehen und von oben auf die Spur
      schauen: Sie fächert sich auf, und ein Stift nach dem anderen fällt.</p>`,
  },
  {
    id: "pole",
    title: "Pol",
    preset: { latitudeDeg: 90 },
    text: `
      <h2>Am Pol: einmal pro Sterntag</h2>
      <p>Am Nordpol steht die Erdachse senkrecht, <span class="q-omega">${OMEGA}</span> zeigt gerade nach oben. Die
      Schwingungsebene dreht sich in einem Sterntag einmal ganz herum – so schnell wie sonst nirgends.</p>
      <p class="note"><span class="label-caps">Probieren Sie</span>Bezugssystem „Sterne“: Jetzt steht die
      Schwingungsebene still, und der Boden dreht sich unter dem Pendel weg. Dieselbe Bewegung, von zwei
      Bezugssystemen aus gesehen – nur im mitdrehenden gibt es die Corioliskraft.</p>`,
  },
  {
    id: "equator",
    title: "Äquator",
    preset: { latitudeDeg: 0 },
    text: `
      <h2>Am Äquator dreht sich nichts</h2>
      <p>Hier liegt <span class="q-omega">${OMEGA}</span> waagerecht und zeigt nach Norden; sein senkrechter Anteil ist
      null. Die Corioliskraft <span class="q-force">${FC}</span> gibt es trotzdem, aber sie zeigt senkrecht nach oben
      oder unten: Sie zieht nur ein wenig am Draht und dreht die Ebene nicht.</p>
      <p class="note"><span class="label-caps">Probieren Sie</span>Den Breitengrad langsam erhöhen: Mit dem senkrechten
      Anteil <span class="formula">Ω sin φ</span> wächst die Drehung.</p>`,
  },
  {
    id: "south",
    title: "Südhalbkugel",
    preset: { latitudeDeg: -51.5 },
    text: `
      <h2>Südhalbkugel: andersherum</h2>
      <p>Auf der Südhalbkugel zeigt der senkrechte Anteil von <span class="q-omega">${OMEGA}</span> nach unten. Die
      Schwingungsebene dreht sich gegen den Uhrzeigersinn, die Corioliskraft drückt das Pendel nach links.</p>
      <p class="note"><span class="label-caps">Probieren Sie</span>Im Zeitraffer zwischen Göttingen und Südhalbkugel
      wechseln und auf die Spur achten.</p>`,
  },
];


// ---- page state ----

let scenario = SCENARIOS[0];
let values = {};                                            // the current settings (start: the scenario's preset)
let show = { ...ALL_ON };                                   // display chips; kept while the master switch is off
let helpersOn = true;                                       // the master switch over the chips
let paused = false;
let factorIndex = 0;                                        // Zeitraffer: index into FACTORS, 0 = real time
let frameControl = null;                                    // the Bezugssystem buttons, to enable at the poles

const block = document.getElementById("sim");
const tabs = document.getElementById("scenarios");
const toolbar = document.getElementById("toolbar");
const panel = document.getElementById("panel");
const hud = document.getElementById("hud");
const textBox = document.getElementById("scenario-text");
const notice = document.getElementById("notice");
const fullscreenButton = document.getElementById("fullscreen");

const flags = () => (helpersOn ? { ...show, labels: true, trace: true } : ALL_OFF);
const sendHelpers = () => sim.send("SetHelpers", flags());

const latitudeText = (deg) =>
  deg === 0 ? "0° · Äquator"
    : isPole(deg) ? `90° ${deg > 0 ? "N · Nordpol" : "S · Südpol"}`
      : `${num(Math.abs(deg), 1)}° ${deg > 0 ? "N" : "S"}`;


function applyScenario(next) {
  scenario = next;
  values = { amplitude: 1.2, frame: "earth", ...next.preset };
  sim.send("ApplyPreset", { ...values, timeScale: FACTORS[factorIndex], paused, show: flags() });
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
const speedSlider = slider({ label: "Zeitraffer", min: 0, max: FACTORS.length - 1, step: 1, value: factorIndex,
                             format: (i) => `${num(FACTORS[i])} ×`,
                             onInput: (i) => { factorIndex = i; sim.send("SetTimeScale", FACTORS[i]); } });
speedSlider.className = "speed";
speedSlider.querySelector("label").className = "label-caps";
speedSlider.title = "Zeitraffer: 1 = Echtzeit";
toolbar.append(
  pauseButton,
  iconButton({ icon: "restart", label: "Neu loslassen", onClick: () => sim.send("Restart") }),
  speedSlider);

const full = fullscreen(block, (on) => {
  const label = on ? "Vollbild beenden (Esc)" : "Vollbild";
  fullscreenButton.replaceChildren(icon(on ? "collapse" : "expand"));
  fullscreenButton.setAttribute("aria-label", label);
  fullscreenButton.title = label;
});
fullscreenButton.addEventListener("click", () => full.toggle());


// ---- side panel (again on every scenario change) ----

function setFrame(frame) {
  values.frame = frame;
  sim.send("SetFrame", frame);
  frameControl?.querySelectorAll("button").forEach((b, i) => b.setAttribute("aria-pressed", (i === 1) === (frame === "stars")));
}

// Sterne only at the poles; elsewhere the button is off and the view falls back to the Earth
function updateFrameControl() {
  const stars = frameControl?.querySelectorAll("button")[1];
  if (!stars) return;
  const allowed = isPole(values.latitudeDeg);
  stars.disabled = !allowed;
  stars.title = allowed ? "Bezugssystem der Fixsterne: der Boden dreht sich" : "Nur an den Polen";
  if (!allowed && values.frame === "stars") setFrame("earth");
}

function renderPanel() {
  const latitude = slider({ label: "Breitengrad φ", min: -90, max: 90, step: 0.5, value: values.latitudeDeg,
                            format: latitudeText,
                            onInput: (v) => { values.latitudeDeg = v; sim.send("SetLatitude", v); updateFrameControl(); } });
  const amplitude = slider({ label: "Auslenkung", min: 0.8, max: 1.6, step: 0.1, value: values.amplitude,
                             format: (v) => `${num(v, 1)} m`,
                             onInput: (v) => { values.amplitude = v; sim.send("SetAmplitude", v); } });
  frameControl = segmented({ label: "Bezugssystem", value: values.frame,
                             options: [{ value: "earth", label: "Erde" }, { value: "stars", label: "Sterne" }],
                             onChange: (v) => setFrame(v) });
  updateFrameControl();
  const experiment = section("Versuch", null, latitude, amplitude, frameControl);

  // display: vectors as symbol chips, the rest by name; with the master switch off the chips are greyed out but keep
  // their state
  const chipFor = (key) => {
    const h = HELPERS[key];
    const c = chip({
      content: h.html ? el("span", { html: h.html }) : h.name,
      color: h.color, pressed: show[key], title: h.name,
      onChange: (on) => { show[key] = on; sendHelpers(); },
    });
    if (h.html) c.setAttribute("aria-label", h.name);
    c.disabled = !helpersOn;
    return c;
  };
  const keys = Object.keys(HELPERS);
  const vectorChips = keys.filter((k) => HELPERS[k].html).map(chipFor);
  const otherChips = keys.filter((k) => !HELPERS[k].html).map(chipFor);
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
for (const [key, label, wideOnly] of [["latitude", "Breitengrad"], ["rate", "Drehung"], ["turn", "Volle Drehung", true],
                                      ["time", "Zeit"], ["turned", "Gedreht"], ["pins", "Stifte", true]]) {
  const cls = wideOnly ? "wide-only" : null;
  readout[key] = el("dd", { class: cls }, "–");
  hud.append(el("dt", { class: cls }, label), readout[key]);
}

// simulated time as h:mm:ss
function clock(seconds) {
  const s = Math.floor(seconds);
  const pad = (n) => String(n).padStart(2, "0");
  return `${Math.floor(s / 3600)}:${pad(Math.floor(s / 60) % 60)}:${pad(s % 60)} h`;
}

sim.onMessage((m) => {
  if (m.type !== "state") return;
  readout.latitude.textContent = latitudeText(m.latitudeDeg);

  const rate = Math.abs(m.planeRateDegPerHour);
  const turning = rate > 1e-3;
  readout.rate.replaceChildren(`${num(rate, 1)} °/h `,
    el("small", {}, !turning ? "keine" : m.planeRateDegPerHour < 0 ? "im Uhrzeigersinn" : "gegen den Uhrzeigersinn"));
  readout.turn.textContent = turning ? `${num(360 / rate, 1)} h` : "–";
  readout.time.textContent = clock(m.time);
  const turned = Math.abs(m.turnedDeg);
  readout.turned.textContent = `${num(turned, turned < 10 ? 2 : 1)}°`;
  readout.pins.textContent = `${m.pinsDown} von ${m.pinsTotal} gefallen`;

  // the simulation falls back to the Earth view away from the poles; the page follows
  if (m.frame !== values.frame && !m.starsAllowed && values.frame === "stars") setFrame("earth");

  const lagging = m.lagging && !m.paused;
  notice.hidden = !lagging;
  notice.textContent = lagging
    ? `Ihr Gerät schafft diesen Zeitraffer nicht ganz: etwa ${num(Math.round(m.actualTimeScale))} × statt ${num(m.timeScale)} ×.`
    : "";
});


// ---- start ----

applyScenario(SCENARIOS[0]);
