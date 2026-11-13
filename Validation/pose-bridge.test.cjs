const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const source = fs.readFileSync('Assets/WebGLTemplates/InfiniteRunnerPose/TemplateData/pose-bridge.js', 'utf8');
let checks = 0;
function expect(condition, message) { checks++; assert.ok(condition, message); }
function harness(saved = null) {
  const nodes = new Map(), sent = [];
  let storage = saved;
  const makeNode = () => ({value: '65', textContent: '', disabled: false, dataset: {},
    classList: { entries: new Set(), add(...n) { n.forEach(x => this.entries.add(x)); }, remove(...n) { n.forEach(x => this.entries.delete(x)); }, toggle(n, on) { if (on) this.entries.add(n); else this.entries.delete(n); } },
    addEventListener() {}, setAttribute() {}, focus() {},
    getContext() { return {clearRect() {}}; }, videoWidth: 640, videoHeight: 480, clientWidth: 320, clientHeight: 240
  });
  const cards = [makeNode(), makeNode()]; cards[0].dataset.gesture = 'Jump'; cards[1].dataset.gesture = 'Crouch';
  const context = vm.createContext({ console, performance: {now: () => 1000}, requestAnimationFrame() {},
    navigator: {mediaDevices: {getUserMedia() { throw Error('Tests must never access a camera'); }}},
    document: {querySelector(id) { if (!nodes.has(id)) nodes.set(id, makeNode()); return nodes.get(id); }, querySelectorAll() { return cards; }},
    window: {runnerUnityInstance: {SendMessage(...args) { sent.push(args); }}, localStorage: {getItem() {return storage;}, setItem(_key, value) {storage = value;}}, addEventListener() {}, setTimeout() {}, clearTimeout() {} }
  });
  vm.runInContext(source, context);
  return {context, nodes, cards, sent, run: code => vm.runInContext(code, context), storage: () => storage};
}
let h = harness(JSON.stringify({run: 42, jump: 77}));
expect(h.nodes.get('#run-sensitivity').value === '42' && h.nodes.get('#jump-sensitivity').value === '77', 'Existing sensitivity choices must survive');
expect(h.nodes.get('#crouch-sensitivity').value === '65', 'Old saved settings need a crouch default');
h.nodes.get('#crouch-sensitivity').value = '86'; h.run('updateSensitivityFromInputs(true)');
expect(JSON.parse(h.storage()).crouch === 86, 'Crouch choice must persist');
expect(JSON.parse(h.sent.at(-1)[2]).crouch === 0.86, 'Crouch slider must reach Unity');
h = harness(h.storage());
expect(h.nodes.get('#crouch-sensitivity').value === '86', 'Crouch setting must restore on reload');
for (const value of [null, '{broken', JSON.stringify({run: null, jump: 'bad', crouch: null})]) {
  const fallback = harness(value);
  expect(fallback.nodes.get('#crouch-sensitivity').value === '65', 'Missing or malformed settings must use defaults');
}
h.run('selectedMode = "camera"; mediaStream = {}; poseLandmarker = {};');
h.run('updateBodyZone(0.5, 0); updateBodyZone(0.5, 10000)');
expect(!h.nodes.get('#tracking-status').textContent.includes('ready'), 'Browser timer cannot claim calibration succeeded');
h.context.window.runnerPoseFeedback({tracking:true, calibrated:false, progress:0.42, instruction:'Keep still', action:'Keep still', lane:-1});
expect(h.nodes.get('#tracking-status').textContent.includes('42%'), 'Real calibration progress must be displayed');
expect(h.nodes.get('#pose-stage').classList.entries.has('is-calibrating'), 'Calibration guides must remain active');
h.context.window.runnerPoseFeedback({tracking:true, calibrated:true, progress:1, instruction:'Ready', action:'Crouch', lane:0});
expect(h.nodes.get('#tracking-status').textContent.includes('Crouch detected'), 'Recognized crouch must be shown');
expect(h.cards[1].classList.entries.has('is-detected'), 'Crouch card must light up');
expect(h.nodes.get('#pose-stage').dataset.bodyZone === 'left', 'Preview must show the actual game lane');
h.context.window.runnerPoseFeedback({tracking:false, calibrated:true, progress:1, instruction:'Show your full body', action:'Ready', lane:-1});
expect(!h.cards[1].classList.entries.has('is-detected') && h.nodes.get('#tracking-status').textContent.includes('paused'), 'Tracking loss must clear gesture feedback');
h.run('var landmarks = Array.from({length:33}, () => ({x:0.5,y:0.5,visibility:1,presence:1})); landmarks[0].visibility = 0.01; sendPoseFrame(landmarks, 100);');
expect(JSON.parse(h.sent.at(-1)[2]).tracking, 'A hidden face must not discard visible body joints during crouches');
h.run('landmarks[27].visibility = 0.1; sendPoseFrame(landmarks, 200)');
let frame = JSON.parse(h.sent.at(-1)[2]);
expect(frame.tracking, 'Cropped or weak feet must not stop tracking');
expect(!frame.anklesVisible && frame.kneesVisible, 'Weak feet must not create jump evidence');
h.run('landmarks[25].visibility = 0.1; landmarks[27].visibility = 1; sendPoseFrame(landmarks, 300)');
frame = JSON.parse(h.sent.at(-1)[2]);
expect(frame.tracking && !frame.kneesVisible && !frame.anklesVisible, 'Ankles without knees are not trusted');
h.run('landmarks[25].visibility = 1; landmarks[28].y = 1.2; sendPoseFrame(landmarks, 400)');
expect(!JSON.parse(h.sent.at(-1)[2]).anklesVisible, 'Out-of-frame feet must not be used');
expect(JSON.parse(h.sent.at(-1)[2]).wristsVisible, 'Visible wrists must be reported for arm-pump running');
h.run('landmarks[28].y = 0.5; landmarks[23].visibility = 0.2; sendPoseFrame(landmarks, 500)');
expect(!JSON.parse(h.sent.at(-1)[2]).tracking, 'A hidden hip must pause tracking');
h = harness();
h.run('activeDelegate = "GPU"; activeModel = "full"; resetInferenceStats(); lastPerfReportAt = 0;');
h.run('for (let i = 0; i < 30; i++) trackInferenceCost(12, 500 + i)');
h.run('trackInferenceCost(12, 1200)');
expect(h.nodes.get('#pose-perf').textContent.startsWith('GPU · full ·'), 'Pose speed readout must show the delegate and model');
expect(h.run('activeModel') === 'full' && !h.run('modelSwitchPending'), 'A fast full model must be kept');
h.run('mediaPipeModule = {PoseLandmarker: {createFromOptions: async () => ({close() {}})}}; poseLandmarker = {close() {}};');
h.run('for (let i = 0; i < 60; i++) trackInferenceCost(35, 1300 + i)');
expect(h.run('modelSwitchPending') || h.run('activeModel') === 'lite', 'A slow full model must switch to lite');
const html = fs.readFileSync('Assets/WebGLTemplates/InfiniteRunnerPose/index.html', 'utf8');
expect(html.includes('id="crouch-sensitivity"'), 'Crouch control must be present in the template');
console.log(`POSE BROWSER CHECKS PASSED: ${checks} settings, calibration, gesture feedback, confidence, and template checks. No camera accessed.`);
