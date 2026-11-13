window.runnerPoseBridgeLoaded = true;

const MEDIAPIPE_VERSION = "1.0.1";
const MEDIAPIPE_MODULE = `https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@${MEDIAPIPE_VERSION}/vision_bundle.mjs`;
const MEDIAPIPE_WASM = `https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@${MEDIAPIPE_VERSION}/wasm`;
// The full model tracks knees and feet far more reliably than lite. It runs on the
// GPU when available; slower CPU-only devices fall back to the lite model.
const POSE_MODEL_FULL = "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_full/float16/1/pose_landmarker_full.task";
const POSE_MODEL_LITE = "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task";
const INFERENCE_INTERVAL_MS = 1000 / 30;
// Torso joints are required. Legs and arms only add evidence when clearly visible.
const CORE_CONFIDENCE = 0.5;
const OPTIONAL_CONFIDENCE = 0.55;
// If the full model averages slower than this on the GPU, switch to the lite model:
// a fast, slightly noisier pose beats an accurate but laggy one for gameplay.
const SLOW_INFERENCE_MS = 20;
const MODEL_PROBE_FRAMES = 45;
const SENSITIVITY_STORAGE_KEY = "infinite-runner.pose-sensitivity.v1";
const DEFAULT_SENSITIVITY = 65;

const startButton = document.querySelector("#start-camera");
const stopButton = document.querySelector("#stop-camera");
const recalibrateButton = document.querySelector("#recalibrate");
const statusElement = document.querySelector("#tracking-status");
const trackingLight = document.querySelector("#tracking-light");
const poseStage = document.querySelector("#pose-stage");
const video = document.querySelector("#pose-video");
const overlay = document.querySelector("#pose-overlay");
const overlayContext = overlay.getContext("2d");
const unityCanvas = document.querySelector("#unity-canvas");
const controlModeDialog = document.querySelector("#control-mode-dialog");
const keyboardModeButton = document.querySelector("#choose-keyboard");
const cameraModeButton = document.querySelector("#choose-camera");
const changeModeButton = document.querySelector("#change-control-mode");
const controlModeHelp = document.querySelector("#control-mode-help");
const runSensitivityInput = document.querySelector("#run-sensitivity");
const jumpSensitivityInput = document.querySelector("#jump-sensitivity");
const runSensitivityValue = document.querySelector("#run-sensitivity-value");
const jumpSensitivityValue = document.querySelector("#jump-sensitivity-value");
const crouchSensitivityInput = document.querySelector("#crouch-sensitivity");
const crouchSensitivityValue = document.querySelector("#crouch-sensitivity-value");

const drawnLandmarks = [0, 11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28];
// Face visibility can dip during a squat, and many rooms crop the feet. Only the
// shoulders and hips gate tracking; knees, ankles and wrists are optional evidence.
const trackingLandmarks = [11, 12, 23, 24];
const kneeLandmarks = [25, 26];
const ankleLandmarks = [27, 28];
const wristLandmarks = [15, 16];
const skeletonConnections = [
  [11, 12], [11, 23], [12, 24], [23, 24],
  [11, 13], [13, 15], [12, 14], [14, 16],
  [23, 25], [25, 27], [24, 26], [26, 28]
];

let poseLandmarker = null;
let poseLandmarkerPromise = null;
let mediaStream = null;
let sessionId = 0;
let sequence = 0;
let lastInferenceAt = -Infinity;
let lastVideoTime = -1;
let consecutiveInferenceErrors = 0;
let lastUnityStatus = "BODY INPUT  Waiting for camera";
let unityReady = Boolean(window.runnerUnityInstance);
let selectedMode = window.runnerControlMode || "menu";
let permissionHintTimer = 0;
let calibrationPending = false;
let controlsReady = false;
let inferenceIntervalMs = INFERENCE_INTERVAL_MS;
let activeDelegate = "none";
let activeModel = "none";
let visionFileset = null;
let averageInferenceMs = 0;
let inferenceSamples = 0;
let modelSwitchPending = false;
let samplesSinceReport = 0;
let lastPerfReportAt = 0;
const perfElement = document.querySelector("#pose-perf");
let sensitivitySettings = loadSensitivitySettings();
const cameraApiAvailable = Boolean(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);

poseStage.dataset.bodyZone = "none";

function sendToUnity(method, value) {
  if (!window.runnerUnityInstance) {
    return;
  }

  window.runnerUnityInstance.SendMessage("MediaPipeBridge", method, value);
}

function sendStatusToUnity(message) {
  lastUnityStatus = message;
  sendToUnity("ReceivePoseStatus", message);
}

function clampSensitivity(value) {
  return Math.min(100, Math.max(0, Number.isFinite(value) ? value : DEFAULT_SENSITIVITY));
}

function loadSensitivitySettings() {
  try {
    const saved = JSON.parse(window.localStorage.getItem(SENSITIVITY_STORAGE_KEY));
    return {
      run: clampSensitivity(saved?.run),
      jump: clampSensitivity(saved?.jump),
      crouch: clampSensitivity(saved?.crouch)
    };
  } catch (_error) {
    return { run: DEFAULT_SENSITIVITY, jump: DEFAULT_SENSITIVITY, crouch: DEFAULT_SENSITIVITY };
  }
}

function saveSensitivitySettings() {
  try {
    window.localStorage.setItem(SENSITIVITY_STORAGE_KEY, JSON.stringify(sensitivitySettings));
  } catch (_error) {
    // Private browsing or storage policies may disable persistence; live tuning still works.
  }
}

function sensitivityLabel(value) {
  if (value < 35) {
    return "Deliberate";
  }

  if (value < 60) {
    return "Balanced";
  }

  if (value < 80) {
    return "Responsive";
  }

  return "Very responsive";
}

function refreshSensitivityUi() {
  runSensitivityInput.value = String(sensitivitySettings.run);
  jumpSensitivityInput.value = String(sensitivitySettings.jump);
  crouchSensitivityInput.value = String(sensitivitySettings.crouch);
  runSensitivityValue.value = `${sensitivityLabel(sensitivitySettings.run)} · ${Math.round(sensitivitySettings.run)}%`;
  jumpSensitivityValue.value = `${sensitivityLabel(sensitivitySettings.jump)} · ${Math.round(sensitivitySettings.jump)}%`;
  crouchSensitivityValue.value = `${sensitivityLabel(sensitivitySettings.crouch)} · ${Math.round(sensitivitySettings.crouch)}%`;
}

function sendSensitivitySettings() {
  sendToUnity("SetPoseSensitivity", JSON.stringify({
    run: sensitivitySettings.run / 100,
    jump: sensitivitySettings.jump / 100,
    crouch: sensitivitySettings.crouch / 100
  }));
}

function updateSensitivityFromInputs(save) {
  sensitivitySettings = {
    run: clampSensitivity(Number(runSensitivityInput.value)),
    jump: clampSensitivity(Number(jumpSensitivityInput.value)),
    crouch: clampSensitivity(Number(crouchSensitivityInput.value))
  };
  refreshSensitivityUi();
  sendSensitivitySettings();

  if (save) {
    saveSensitivitySettings();
  }
}

function setControlMode(mode) {
  selectedMode = mode;
  window.runnerControlMode = mode;
  sendToUnity("SetControlMode", mode);

  if (mode === "keyboard") {
    changeModeButton.textContent = "Controls: Keyboard";
  } else if (mode === "camera") {
    changeModeButton.textContent = "Controls: Camera";
  } else {
    changeModeButton.textContent = "Choose controls";
  }
}

function closeControlMenu() {
  controlModeDialog.classList.add("is-hidden");
  controlModeDialog.setAttribute("aria-hidden", "true");
}

function showControlMenu(message = "Camera processing stays in this browser tab.", isError = false) {
  stopCamera(false);

  setControlMode("menu");
  controlModeHelp.textContent = message;
  controlModeHelp.classList.toggle("is-error", isError);
  keyboardModeButton.disabled = false;
  cameraModeButton.disabled = !cameraApiAvailable;
  controlModeDialog.classList.remove("is-hidden");
  controlModeDialog.setAttribute("aria-hidden", "false");
  keyboardModeButton.focus();
}

function chooseKeyboardMode() {
  stopCamera(false);

  setControlMode("keyboard");
  closeControlMenu();
  setStatus(
    "Keyboard mode active. Hold W to run.",
    "idle",
    "KEYBOARD MODE  Hold W to run"
  );
  unityCanvas.focus();
}

async function chooseCameraMode() {
  if (!cameraApiAvailable) {
    showControlMenu("Camera access requires HTTPS or localhost in a supported browser.", true);
    return;
  }

  setControlMode("camera");
  cameraModeButton.disabled = true;
  keyboardModeButton.disabled = false;
  controlModeHelp.textContent = "Requesting camera access…";
  controlModeHelp.classList.remove("is-error");

  const started = await startCamera();
  cameraModeButton.disabled = !cameraApiAvailable;
  keyboardModeButton.disabled = false;

  if (selectedMode !== "camera") {
    return;
  }

  if (started) {
    closeControlMenu();
    return;
  }

  const failureMessage = statusElement.textContent;
  showControlMenu(failureMessage, true);
}

function setStatus(message, kind = "idle", unityMessage = message) {
  statusElement.textContent = message;
  statusElement.classList.toggle("is-error", kind === "error");
  trackingLight.classList.toggle("is-live", kind === "live");
  trackingLight.classList.toggle("is-error", kind === "error");
  sendStatusToUnity(unityMessage);
}

function refreshButtons() {
  const cameraRunning = Boolean(mediaStream);
  startButton.disabled = !cameraApiAvailable || cameraRunning;
  stopButton.disabled = !cameraRunning;
  recalibrateButton.disabled = !cameraRunning || !poseLandmarker;
}

window.addEventListener("runner-unity-ready", () => {
  unityReady = true;
  sendToUnity("SetControlMode", selectedMode);
  sendSensitivitySettings();
  sendStatusToUnity(lastUnityStatus);
  if (selectedMode === "menu") {
    setStatus("Choose keyboard or camera to start playing.");
  } else if (selectedMode === "keyboard") {
    setStatus("Keyboard mode active. Hold W to run.", "idle", "KEYBOARD MODE  Hold W to run");
  } else if (!mediaStream && cameraApiAvailable) {
    setStatus("Camera mode selected. Click Use camera to try again.");
  }
  refreshButtons();
});

setStatus(cameraApiAvailable
  ? "Choose keyboard or camera to start playing."
  : "Camera is unavailable here. Choose keyboard mode.",
cameraApiAvailable ? "idle" : "error");
refreshButtons();

let mediaPipeModule = null;
async function loadFilesetResolver() {
  mediaPipeModule = mediaPipeModule || await import(MEDIAPIPE_MODULE);
  return mediaPipeModule.FilesetResolver;
}

async function buildLandmarker(modelAssetPath, delegate) {
  mediaPipeModule = mediaPipeModule || await import(MEDIAPIPE_MODULE);
  return mediaPipeModule.PoseLandmarker.createFromOptions(visionFileset, {
    baseOptions: { modelAssetPath, delegate },
    runningMode: "VIDEO",
    numPoses: 1,
    minPoseDetectionConfidence: 0.5,
    minPosePresenceConfidence: 0.5,
    minTrackingConfidence: 0.5,
    outputSegmentationMasks: false
  });
}

function resetInferenceStats() {
  averageInferenceMs = 0;
  inferenceSamples = 0;
  samplesSinceReport = 0;
  lastPerfReportAt = performance.now();
}

// Called with each inference cost. Switches a slow full model to lite once.
function trackInferenceCost(costMs, now) {
  inferenceSamples++;
  samplesSinceReport++;
  averageInferenceMs += (costMs - averageInferenceMs) / Math.min(inferenceSamples, 20);

  if (activeModel === "full" && !modelSwitchPending && inferenceSamples >= MODEL_PROBE_FRAMES &&
      averageInferenceMs > SLOW_INFERENCE_MS) {
    modelSwitchPending = true;
    const switchSession = sessionId;
    buildLandmarker(POSE_MODEL_LITE, activeDelegate).then((lite) => {
      if (switchSession !== sessionId) { lite.close(); return; }
      const previous = poseLandmarker;
      poseLandmarker = lite;
      activeModel = "lite";
      resetInferenceStats();
      previous?.close?.();
    }).catch((error) => console.warn("Could not switch to the lite pose model", error))
      .finally(() => { modelSwitchPending = false; });
  }

  if (now - lastPerfReportAt >= 1000) {
    const rate = samplesSinceReport * 1000 / (now - lastPerfReportAt);
    perfElement.textContent = `${activeDelegate} · ${activeModel} · ${Math.round(rate)} Hz · ${averageInferenceMs.toFixed(0)} ms`;
    perfElement.classList.toggle("is-slow", rate < 18 || averageInferenceMs > 30);
    samplesSinceReport = 0;
    lastPerfReportAt = now;
  }
}

async function createPoseLandmarker() {
  if (poseLandmarker) {
    return poseLandmarker;
  }

  if (poseLandmarkerPromise) {
    return poseLandmarkerPromise;
  }

  setStatus(
    `Loading MediaPipe ${MEDIAPIPE_VERSION} and the pose model…`,
    "idle",
    "BODY INPUT  Loading MediaPipe pose model"
  );

  poseLandmarkerPromise = (async () => {
    const resolver = await loadFilesetResolver();
    visionFileset = visionFileset || await resolver.forVisionTasks(MEDIAPIPE_WASM);
    try {
      poseLandmarker = await buildLandmarker(POSE_MODEL_FULL, "GPU");
      activeDelegate = "GPU";
      activeModel = "full";
    } catch (gpuError) {
      console.warn("MediaPipe GPU delegate unavailable; using the CPU lite model", gpuError);
      poseLandmarker = await buildLandmarker(POSE_MODEL_LITE, "CPU");
      activeDelegate = "CPU";
      activeModel = "lite";
    }
    resetInferenceStats();

    return poseLandmarker;
  })();

  try {
    return await poseLandmarkerPromise;
  } finally {
    poseLandmarkerPromise = null;
  }
}

async function startCamera() {
  if (mediaStream) {
    return true;
  }

  if (!cameraApiAvailable) {
    setStatus(
      "Camera access requires HTTPS or localhost in a supported browser.",
      "error",
      "BODY ERROR  Camera API unavailable"
    );
    return false;
  }

  startButton.disabled = true;
  const startupSession = ++sessionId;
  let startupStage = "permission";
  setStatus(
    "Waiting for camera permission…",
    "idle",
    "BODY INPUT  Allow camera access in your browser"
  );

  window.clearTimeout(permissionHintTimer);
  permissionHintTimer = window.setTimeout(() => {
    if (!mediaStream) {
      const hint = "Still waiting. Look for a camera prompt or camera icon near the browser address bar.";
      setStatus(hint, "idle", "BODY INPUT  Waiting for browser camera permission");
      controlModeHelp.textContent = hint;
    }
  }, 3000);

  try {
    const requestedStream = await navigator.mediaDevices.getUserMedia({
      audio: false,
      video: {
        facingMode: "user",
        width: { ideal: 640 },
        height: { ideal: 480 },
        frameRate: { ideal: 30, max: 30 }
      }
    });
    window.clearTimeout(permissionHintTimer);

    if (startupSession !== sessionId) {
      for (const track of requestedStream.getTracks()) {
        track.stop();
      }
      return false;
    }

    mediaStream = requestedStream;

    video.srcObject = mediaStream;
    video.muted = true;
    video.playsInline = true;
    await video.play();
    poseStage.classList.add("is-live");
    refreshButtons();

    startupStage = "model";
    controlModeHelp.textContent = "Camera connected. Loading MediaPipe body tracking…";
    await createPoseLandmarker();
    if (startupSession !== sessionId || !mediaStream) {
      return false;
    }

    recalibrate();

    lastInferenceAt = -Infinity;
    lastVideoTime = -1;
    consecutiveInferenceErrors = 0;
    refreshButtons();
    requestAnimationFrame((time) => inferenceLoop(time, startupSession));
    unityCanvas.focus();
    return true;
  } catch (error) {
    window.clearTimeout(permissionHintTimer);
    if (startupSession !== sessionId) {
      return false;
    }

    console.error("Camera startup failed", error);
    stopCamera(false);
    const readableMessage = cameraErrorMessage(error, startupStage);
    setStatus(readableMessage, "error", `BODY ERROR  ${readableMessage}`);
    return false;
  }
}

function stopCamera(updateStatus = true) {
  sessionId++;
  window.clearTimeout(permissionHintTimer);

  if (mediaStream) {
    for (const track of mediaStream.getTracks()) {
      track.stop();
    }
  }

  mediaStream = null;
  video.srcObject = null;
  poseStage.classList.remove("is-live");
  poseStage.classList.remove("is-calibrating");
  poseStage.dataset.bodyZone = "none";
  perfElement.textContent = "";
  calibrationPending = false;
  controlsReady = false;
  for (const card of document.querySelectorAll("[data-gesture]")) card.classList.remove("is-detected");
  overlayContext.clearRect(0, 0, overlay.width, overlay.height);
  sendTrackingLost();

  if (updateStatus) {
    setStatus("Camera stopped. Keyboard controls are still available.", "idle", "BODY INPUT  Camera stopped");
  }

  refreshButtons();
}

function recalibrate() {
  if (!mediaStream) {
    return;
  }

  sendToUnity("RequestCalibration", "");
  calibrationPending = true;
  controlsReady = false;
  poseStage.classList.add("is-calibrating");
  setStatus(
    "Move your body into the CENTER section to begin calibration.",
    "live",
    "CALIBRATING  Move your body into the CENTER section"
  );
  unityCanvas.focus();
}

function inferenceLoop(now, activeSession) {
  if (activeSession !== sessionId || !mediaStream || !poseLandmarker) {
    return;
  }

  if (
    video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA &&
    video.currentTime !== lastVideoTime &&
    // requestAnimationFrame ticks jitter by a few ms; without this tolerance a
    // 33 ms target regularly slips to the next 50 ms tick.
    now - lastInferenceAt >= inferenceIntervalMs - 4
  ) {
    lastInferenceAt = now;
    lastVideoTime = video.currentTime;

    try {
      const inferenceStartedAt = performance.now();
      const result = poseLandmarker.detectForVideo(video, now);
      // Faster gesture samples on capable devices, with room left for the game to render.
      // Smooth the cost estimate so one slow frame does not halve the sample rate.
      const inferenceCost = performance.now() - inferenceStartedAt;
      trackInferenceCost(inferenceCost, performance.now());
      // Leave the game ~25% of each sample period to render; never drop below 15 Hz.
      const targetInterval = Math.max(INFERENCE_INTERVAL_MS, Math.min(1000 / 15, averageInferenceMs * 1.25));
      inferenceIntervalMs += (targetInterval - inferenceIntervalMs) * 0.2;
      const landmarks = result.landmarks && result.landmarks.length > 0
        ? result.landmarks[0]
        : null;

      if (landmarks && landmarks.length >= 29) {
        consecutiveInferenceErrors = 0;
        sendPoseFrame(landmarks, now);
        drawPose(landmarks);
      } else {
        sendTrackingLost(now);
        drawPose(null);
      }
    } catch (error) {
      console.error("MediaPipe pose inference failed", error);
      consecutiveInferenceErrors++;
      setStatus(
        "Pose inference paused because MediaPipe returned an error.",
        "error",
        "BODY ERROR  MediaPipe inference failed"
      );
      sendTrackingLost(now);
      if (consecutiveInferenceErrors >= 3) {
        stopCamera(false);
        return;
      }
    }
  }

  requestAnimationFrame((time) => inferenceLoop(time, activeSession));
}

function jointsVisible(landmarks, indices) {
  return indices.every((index) => {
    const landmark = landmarks[index];
    return landmark && landmarkConfidence(landmark) >= OPTIONAL_CONFIDENCE &&
      landmark.y > -0.02 && landmark.y < 1.02 && landmark.x > -0.02 && landmark.x < 1.02;
  });
}

function sendPoseFrame(landmarks, timestampMs) {
  const confidence = Math.min(...trackingLandmarks.map((index) => landmarkConfidence(landmarks[index])));
  if (confidence < CORE_CONFIDENCE) {
    sendTrackingLost(timestampMs);
    return;
  }

  const nose = mirroredPoint(landmarks[0]);
  const leftShoulder = mirroredPoint(landmarks[11]);
  const rightShoulder = mirroredPoint(landmarks[12]);
  const leftHip = mirroredPoint(landmarks[23]);
  const rightHip = mirroredPoint(landmarks[24]);
  const leftKnee = mirroredPoint(landmarks[25]);
  const rightKnee = mirroredPoint(landmarks[26]);
  const leftAnkle = mirroredPoint(landmarks[27]);
  const rightAnkle = mirroredPoint(landmarks[28]);
  const leftWrist = mirroredPoint(landmarks[15]);
  const rightWrist = mirroredPoint(landmarks[16]);
  const kneesVisible = jointsVisible(landmarks, kneeLandmarks);
  const anklesVisible = kneesVisible && jointsVisible(landmarks, ankleLandmarks);
  const wristsVisible = jointsVisible(landmarks, wristLandmarks);
  const rawBodyCenterX = (
    landmarks[11].x + landmarks[12].x + landmarks[23].x + landmarks[24].x
  ) * 0.25;
  const bodyCenterX = visibleMirroredX(rawBodyCenterX);

  updateBodyZone(bodyCenterX, timestampMs);

  const payload = {
    sequence: ++sequence,
    timestampMs,
    tracking: true,
    confidence,
    aspect: video.videoWidth / Math.max(1, video.videoHeight),
    bodyCenterX,
    noseX: nose.x,
    noseY: nose.y,
    leftShoulderX: leftShoulder.x,
    leftShoulderY: leftShoulder.y,
    rightShoulderX: rightShoulder.x,
    rightShoulderY: rightShoulder.y,
    leftHipX: leftHip.x,
    leftHipY: leftHip.y,
    rightHipX: rightHip.x,
    rightHipY: rightHip.y,
    leftKneeX: leftKnee.x,
    leftKneeY: leftKnee.y,
    rightKneeX: rightKnee.x,
    rightKneeY: rightKnee.y,
    leftAnkleX: leftAnkle.x,
    leftAnkleY: leftAnkle.y,
    rightAnkleX: rightAnkle.x,
    rightAnkleY: rightAnkle.y,
    leftWristX: leftWrist.x,
    leftWristY: leftWrist.y,
    rightWristX: rightWrist.x,
    rightWristY: rightWrist.y,
    kneesVisible,
    anklesVisible,
    wristsVisible
  };

  sendToUnity("ReceivePoseFrame", JSON.stringify(payload));
}

function sendTrackingLost(timestampMs = performance.now()) {
  updateBodyZone(null, timestampMs);
  sendToUnity("ReceivePoseFrame", JSON.stringify({
    sequence: ++sequence,
    timestampMs,
    tracking: false,
    confidence: 0
  }));
}

function mirroredPoint(landmark) {
  return { x: 1 - landmark.x, y: landmark.y };
}

function visibleMirroredX(rawX) {
  const sourceWidth = Math.max(1, video.videoWidth || 640);
  const sourceHeight = Math.max(1, video.videoHeight || 480);
  const stageWidth = Math.max(1, poseStage.clientWidth || sourceWidth);
  const stageHeight = Math.max(1, poseStage.clientHeight || sourceHeight);
  const sourceAspect = sourceWidth / sourceHeight;
  const stageAspect = stageWidth / stageHeight;
  let visibleX = rawX;

  // object-fit: cover crops the sides when the camera is wider than the preview.
  // Mapping through that crop keeps the visible guide lines and Unity zones exact.
  if (sourceAspect > stageAspect) {
    const visibleFraction = stageAspect / sourceAspect;
    const crop = (1 - visibleFraction) * 0.5;
    visibleX = (rawX - crop) / visibleFraction;
  }

  return 1 - visibleX;
}

function updateBodyZone(bodyCenterX, _timestampMs) {
  if (controlsReady) return;
  poseStage.dataset.bodyZone = !Number.isFinite(bodyCenterX) ? "none" :
    bodyCenterX < 1 / 3 ? "left" : bodyCenterX > 2 / 3 ? "right" : "center";
}

// Unity owns calibration and recognized actions. Do not show a separate timer-based "ready" state.
window.runnerPoseFeedback = (feedback) => {
  if (selectedMode !== "camera" || !mediaStream || !poseLandmarker) return;
  controlsReady = feedback.tracking && feedback.calibrated;
  calibrationPending = !feedback.calibrated;
  poseStage.classList.toggle("is-calibrating", calibrationPending);
  trackingLight.classList.toggle("is-live", feedback.tracking);
  trackingLight.classList.remove("is-error");
  statusElement.classList.remove("is-error");
  if (!feedback.tracking) {
    poseStage.dataset.bodyZone = "none";
    statusElement.textContent = "Tracking paused. Step back until at least your shoulders and hips are in view.";
  } else if (!feedback.calibrated) {
    statusElement.textContent = `${feedback.instruction}. Calibration ${Math.round(feedback.progress * 100)}%.`;
  } else {
    poseStage.dataset.bodyZone = ["left", "center", "right"][feedback.lane] || "none";
    statusElement.textContent = feedback.action === "Ready"
      ? "Ready. Run in place or pump your arms, jump, or squat to crouch."
      : `${feedback.action} detected · ${["Left", "Center", "Right"][feedback.lane] || "Center"} lane`;
  }
  for (const card of document.querySelectorAll("[data-gesture]")) {
    card.classList.toggle("is-detected", controlsReady && card.dataset.gesture === feedback.action);
  }
};

function landmarkConfidence(landmark) {
  const visibility = Number.isFinite(landmark.visibility) ? landmark.visibility : 1;
  const presence = Number.isFinite(landmark.presence) ? landmark.presence : 1;
  return Math.min(visibility, presence);
}

function drawPose(landmarks) {
  const width = video.videoWidth || 640;
  const height = video.videoHeight || 480;
  if (overlay.width !== width || overlay.height !== height) {
    overlay.width = width;
    overlay.height = height;
  }

  overlayContext.clearRect(0, 0, width, height);
  if (!landmarks) {
    return;
  }

  overlayContext.lineCap = "round";
  overlayContext.lineJoin = "round";
  overlayContext.lineWidth = Math.max(2, width / 230);
  overlayContext.strokeStyle = "rgba(77, 232, 255, 0.92)";
  overlayContext.shadowColor = "rgba(77, 232, 255, 0.7)";
  overlayContext.shadowBlur = 8;

  for (const [fromIndex, toIndex] of skeletonConnections) {
    const from = landmarks[fromIndex];
    const to = landmarks[toIndex];
    if (landmarkConfidence(from) < 0.35 || landmarkConfidence(to) < 0.35) {
      continue;
    }

    overlayContext.beginPath();
    overlayContext.moveTo(from.x * width, from.y * height);
    overlayContext.lineTo(to.x * width, to.y * height);
    overlayContext.stroke();
  }

  overlayContext.shadowBlur = 5;
  for (const index of drawnLandmarks) {
    const landmark = landmarks[index];
    if (landmarkConfidence(landmark) < 0.35) {
      continue;
    }

    overlayContext.beginPath();
    overlayContext.fillStyle = index === 0 ? "#ff9654" : trackingLandmarks.includes(index) ? "#ffe066" : "#eaffff";
    overlayContext.arc(landmark.x * width, landmark.y * height, Math.max(3, width / 170), 0, Math.PI * 2);
    overlayContext.fill();
  }

  overlayContext.shadowBlur = 0;
}

function cameraErrorMessage(error, startupStage = "permission") {
  if (startupStage === "model") {
    return "The camera opened, but MediaPipe could not load. Check your internet connection and try again.";
  }

  if (!window.isSecureContext) {
    return "Camera access requires HTTPS or localhost.";
  }

  if (error && error.name === "NotAllowedError") {
    return "Camera is blocked. Allow camera access from the icon beside the address bar, then try again.";
  }

  if (error && error.name === "NotFoundError") {
    return "No camera was found on this device.";
  }

  if (error && error.name === "NotReadableError") {
    return "The camera is busy in another app or browser tab. Close it there and try again.";
  }

  if (error && error.name === "OverconstrainedError") {
    return "This camera does not support the requested video settings.";
  }

  if (error && (error.name === "SecurityError" || error.name === "TypeError")) {
    return "This page is not allowed to use the camera. Open it through localhost or HTTPS.";
  }

  return "The camera could not start. Check browser camera permissions and try again.";
}

keyboardModeButton.addEventListener("click", chooseKeyboardMode);
cameraModeButton.addEventListener("click", chooseCameraMode);
changeModeButton.addEventListener("click", () => showControlMenu());
startButton.addEventListener("click", chooseCameraMode);
stopButton.addEventListener("click", chooseKeyboardMode);
recalibrateButton.addEventListener("click", recalibrate);
runSensitivityInput.addEventListener("input", () => updateSensitivityFromInputs(false));
jumpSensitivityInput.addEventListener("input", () => updateSensitivityFromInputs(false));
runSensitivityInput.addEventListener("change", () => updateSensitivityFromInputs(true));
jumpSensitivityInput.addEventListener("change", () => updateSensitivityFromInputs(true));
crouchSensitivityInput.addEventListener("input", () => updateSensitivityFromInputs(false));
crouchSensitivityInput.addEventListener("change", () => updateSensitivityFromInputs(true));
window.addEventListener("pagehide", () => stopCamera(false));

refreshSensitivityUi();
sendSensitivitySettings();

if (!cameraApiAvailable) {
  startButton.disabled = true;
  setStatus(
    "This browser does not provide camera access here. Use HTTPS or localhost.",
    "error",
    "BODY ERROR  Camera API unavailable"
  );
  controlModeHelp.textContent = "Camera access is unavailable here. Choose keyboard mode or open the game through localhost/HTTPS.";
  controlModeHelp.classList.add("is-error");
}
