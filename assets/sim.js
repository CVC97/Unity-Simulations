// Engine Simulations – shared page code: load a Unity web build into the page's canvas, talk to it, build controls.
// Plain ES module, no dependencies, no build step.
//
// Page → simulation:  sim.send("SetRpm", 400)  → unityInstance.SendMessage("WebBridge", "SetRpm", 400)
// Simulation → page:  sim.onMessage((m) => …)  ← SimBridge.jslib calls window.onSimMessage(m) with {source, type, …}
//                     (type "ready" once, "state" ~10 times per second)
// Commands sent before Unity runs are queued and sent in order once it does.


// ---- loading ----

// buildUrl: folder of the build (e.g. "sim"), name: its file prefix (the build folder's name, e.g. "sim").
// stage: the .stage element with a canvas and the loading overlay (see gyroscope/index.html).
export function loadSimulation(stage, { buildUrl, name, gameObject = "WebBridge" }) {
  const canvas = stage.querySelector("canvas");
  const overlay = stage.querySelector(".stage-loading");
  const fill = overlay.querySelector(".fill");
  const percent = overlay.querySelector(".percent");
  const error = overlay.querySelector(".error");

  const listeners = [];
  const queued = [];
  let instance = null;

  window.onSimMessage = (message) => listeners.forEach((fn) => fn(message));

  function sendNow(cmd, value) {
    if (value === undefined) instance.SendMessage(gameObject, cmd);
    else if (typeof value === "object") instance.SendMessage(gameObject, cmd, JSON.stringify(value));
    else if (typeof value === "boolean") instance.SendMessage(gameObject, cmd, value ? 1 : 0);
    else instance.SendMessage(gameObject, cmd, value);
  }

  const sim = {
    // value: number, string, boolean (sent as 1/0), object (sent as JSON), or nothing
    send(cmd, value) {
      if (instance) sendNow(cmd, value);
      else queued.push([cmd, value]);
    },
    onMessage(fn) { listeners.push(fn); },
    get ready() { return instance !== null; },
  };

  // right mouse button belongs to the camera
  canvas.addEventListener("contextmenu", (e) => e.preventDefault());
  guardWheel(stage, canvas);

  const script = document.createElement("script");
  script.src = `${buildUrl}/Build/${name}.loader.js`;
  script.onerror = () => showError("Die Simulation wurde nicht gefunden.");
  script.onload = () => {
    window.createUnityInstance(canvas, {
      dataUrl: `${buildUrl}/Build/${name}.data.unityweb`,
      frameworkUrl: `${buildUrl}/Build/${name}.framework.js.unityweb`,
      codeUrl: `${buildUrl}/Build/${name}.wasm.unityweb`,
      streamingAssetsUrl: `${buildUrl}/StreamingAssets`,
      companyName: "Physik und ihre Didaktik",
      productName: name,
      productVersion: "1",
      // sharp on HiDPI screens, but capped at 2×: 3× phone displays cost a lot of GPU time for little gain
      devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2),
    }, (progress) => {
      fill.style.width = `${100 * progress}%`;
      percent.textContent = `${Math.round(100 * progress)} %`;
    }).then((unityInstance) => {
      instance = unityInstance;
      window.unityInstance = unityInstance;        // for tests in the browser console
      queued.splice(0).forEach(([cmd, value]) => sendNow(cmd, value));
      overlay.classList.add("done");
    }).catch((e) => showError(String(e)));
  };
  document.body.appendChild(script);

  function showError(message) {
    error.textContent = `Die Simulation konnte nicht gestartet werden: ${message}`;
    error.hidden = false;
    overlay.querySelector(".bar").hidden = true;
  }

  return sim;
}


// The mouse wheel over the canvas scrolls the page, until the simulation is clicked: from then on it zooms, until the
// pointer leaves the stage. In fullscreen it always zooms. A short hint says how.
// Listening on window in the capture phase, registered before Unity starts, so this runs before any of Unity's own
// wheel listeners; stopping the event there means Unity never sees it, and the page scrolls as usual.
function guardWheel(stage, canvas) {
  let engaged = false;
  let hintTimer = 0;
  const hint = el("p", { class: "stage-hint", "aria-hidden": "true" }, "Zum Zoomen mit dem Mausrad erst in die Simulation klicken");
  stage.append(hint);

  stage.addEventListener("pointerdown", () => { engaged = true; hint.classList.remove("shown"); });
  stage.addEventListener("pointerleave", () => { engaged = false; });

  window.addEventListener("wheel", (e) => {
    if (e.target !== canvas || engaged || stage.closest(".is-full")) return;
    e.stopImmediatePropagation();
    hint.classList.add("shown");
    clearTimeout(hintTimer);
    hintTimer = setTimeout(() => hint.classList.remove("shown"), 1600);
  }, { capture: true, passive: true });
}


// Fullscreen for a whole block (stage, toolbar and panel): the Fullscreen API where there is one, else (iPhone) a
// fixed overlay. Either way the block gets the class is-full; onChange(full) after every change, also on Esc.
export function fullscreen(block, onChange) {
  const native = document.fullscreenEnabled && block.requestFullscreen;
  const set = (full) => { block.classList.toggle("is-full", full); onChange(full); };
  if (native) document.addEventListener("fullscreenchange", () => set(document.fullscreenElement === block));
  else document.addEventListener("keydown", (e) => { if (e.key === "Escape" && block.classList.contains("is-full")) set(false); });
  return {
    get on() { return block.classList.contains("is-full"); },
    toggle() {
      if (!native) set(!block.classList.contains("is-full"));
      else if (document.fullscreenElement) document.exitFullscreen();
      else block.requestFullscreen().catch(() => set(true));
    },
  };
}


// ---- controls ----

// tiny element helper: el("div", {class: "x"}, child, "text", …)
export function el(tag, attributes = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attributes)) {
    if (value == null) continue;
    if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
    else if (key === "html") node.innerHTML = value;
    else if (key.startsWith("aria-") || key.startsWith("data-")) node.setAttribute(key, String(value));   // "true"/"false"
    else if (value === true) node.setAttribute(key, "");
    else if (value !== false && value != null) node.setAttribute(key, value);
  }
  node.append(...children.filter((c) => c != null && c !== false));
  return node;
}

// line icons, 24 × 24, drawn with the current text colour
const ICONS = {
  play:     '<path d="M7 4.5v15L19.5 12z" fill="currentColor" stroke="none"/>',
  pause:    '<path d="M7 4.5h3.5v15H7zM13.5 4.5H17v15h-3.5z" fill="currentColor" stroke="none"/>',
  restart:  '<path d="M4.5 12a7.5 7.5 0 1 0 2.2-5.3"/><path d="M4.5 3.5v4h4"/>',
  kick:     '<path d="M4 12h11"/><path d="M11 7l5 5-5 5"/><path d="M20 4v16"/>',
  expand:   '<path d="M4 9V4h5M15 4h5v5M20 15v5h-5M9 20H4v-5"/>',
  collapse: '<path d="M9 4v5H4M20 9h-5V4M15 20v-5h5M4 15h5v5"/>',
  back:     '<path d="M20 12H5"/><path d="M11 6l-6 6 6 6"/>',
  forward:  '<path d="M4 12h15"/><path d="M13 6l6 6-6 6"/>',
};

export function icon(name) {
  const span = document.createElement("span");
  span.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="square" aria-hidden="true">${ICONS[name]}</svg>`;
  return span.firstChild;
}

let ids = 0;

// slider with label and value; format(value) gives the shown text; onInput(value) on every move
export function slider({ label, min, max, step = 1, value, format = String, onInput }) {
  const id = `slider-${++ids}`;
  const output = el("output", { for: id });
  const input = el("input", { id, type: "range", min, max, step, value });
  const show = () => {
    output.textContent = format(+input.value);
    input.style.setProperty("--fill", `${100 * (input.value - min) / (max - min)}%`);
  };
  input.addEventListener("input", () => { show(); onInput(+input.value); });
  show();
  const row = el("div", { class: "field" }, el("label", { for: id }, label), output, input);
  row.input = input;
  return row;
}

// one of a few values: [A | B]
export function segmented({ label, options, value, onChange }) {
  const buttons = options.map((o) => el("button", { type: "button", "aria-pressed": o.value === value,
    onclick: () => {
      buttons.forEach((b) => b.setAttribute("aria-pressed", b === buttons[options.indexOf(o)]));
      onChange(o.value);
    } }, o.label));
  return el("div", { class: "field", role: "group", "aria-label": label },
            el("span", { class: "field-label" }, label), el("span"),
            el("div", { class: "segmented" }, ...buttons));
}

// on/off switch with a caption
export function switchButton({ label, checked, onChange }) {
  const button = el("button", { type: "button", class: "switch", role: "switch", "aria-checked": checked,
    onclick: () => {
      const on = button.getAttribute("aria-checked") !== "true";
      button.setAttribute("aria-checked", on);
      onChange(on);
    } }, el("span", {}, label), el("span", { class: "track", "aria-hidden": "true" }));
  return button;
}

// toggle chip; color: the quantity's colour (CSS colour or var(--…)); content: text or nodes
export function chip({ content, color, pressed, title, onChange }) {
  const button = el("button", { type: "button", class: "chip", "aria-pressed": pressed, title,
    style: color ? `--c:${color}` : null,
    onclick: () => {
      const on = button.getAttribute("aria-pressed") !== "true";
      button.setAttribute("aria-pressed", on);
      onChange(on);
    } }, el("span", { class: "dot", "aria-hidden": "true" }), ...[content].flat());
  return button;
}

export function button({ label, icon: iconName, primary = false, onClick }) {
  return el("button", { type: "button", class: primary ? "button primary" : "button", onclick: onClick },
            iconName ? icon(iconName) : null, label);
}

// square icon button; label for screen readers and the tooltip
export function iconButton({ icon: iconName, label, onClick }) {
  const b = el("button", { type: "button", class: "icon-button", "aria-label": label, title: label, onclick: onClick },
               icon(iconName));
  b.setIcon = (name, newLabel) => {
    b.replaceChildren(icon(name));
    b.setAttribute("aria-label", newLabel);
    b.title = newLabel;
  };
  return b;
}

// a titled section of the side panel; extra: something on the right of the title (e.g. a switch)
export function section(title, extra, ...rows) {
  return el("div", { class: "panel-section" },
            el("div", { class: "panel-head" }, el("span", { class: "label-caps" }, title), extra), ...rows);
}
