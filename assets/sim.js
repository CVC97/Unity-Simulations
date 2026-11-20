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

  // right mouse button and wheel belong to the camera
  canvas.addEventListener("contextmenu", (e) => e.preventDefault());

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


// ---- controls ----

// tiny element helper: el("div", {class: "x"}, child, "text", …)
export function el(tag, attributes = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attributes)) {
    if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
    else if (value === true) node.setAttribute(key, "");
    else if (value !== false && value != null) node.setAttribute(key, value);
  }
  node.append(...children);
  return node;
}

let ids = 0;

// slider with label and value; format(value) gives the shown text; onInput(value) on every move
export function slider({ label, min, max, step = 1, value, format = String, onInput }) {
  const id = `slider-${++ids}`;
  const output = el("output", { for: id }, format(value));
  const input = el("input", { id, type: "range", min, max, step, value });
  input.addEventListener("input", () => {
    output.textContent = format(+input.value);
    onInput(+input.value);
  });
  const row = el("div", { class: "slider-row" }, el("label", { for: id }, label), output, input);
  row.set = (v) => { input.value = v; output.textContent = format(+input.value); };
  return row;
}

// checkbox switch; swatch: a colour dot (the quantity's colour in the simulation)
export function toggle({ label, checked, swatch, onChange }) {
  const input = el("input", { type: "checkbox" });
  input.checked = checked;
  input.addEventListener("change", () => onChange(input.checked));
  const row = el("label", { class: "switch-row" }, input,
                 swatch ? el("span", { class: "swatch", style: `background:${swatch}` }) : "", label);
  row.set = (v) => { input.checked = v; };
  row.enable = (on) => { input.disabled = !on; row.classList.toggle("disabled", !on); };
  row.get = () => input.checked;
  return row;
}

export function button({ label, primary = false, onClick }) {
  return el("button", { type: "button", class: primary ? "button primary" : "button", onclick: onClick }, label);
}

export function group(title, ...rows) {
  return el("div", { class: "panel-group" }, el("span", { class: "label-caps" }, title), ...rows);
}
