# Saigon Infinite Runner — MediaPipe Edition

A hot-afternoon run through District 1, Saigon: pastel tube houses with balconies and shop signs, tangled power lines, parked scooters, street-food carts, Tet lanterns, and a skyline with Landmark 81, Bitexco Tower, the cathedral and Ben Thanh Market. Built in Unity Web and controlled by either the keyboard or a person's body through MediaPipe Pose Landmarker. The camera is processed locally in the browser; camera frames are not sent to Unity or uploaded by this project.

## Open and play

1. Open **Unity Hub**.
2. Select **Unity 6.5.3f1** (already installed on this machine), or a newer Unity 6 editor. The basic desktop editor module is enough for Play mode.
3. Choose **Add project from disk** and select this `InfiniteRunner` folder.
4. Let Unity finish its first import. The setup script creates and opens `Assets/InfiniteRunner/Scenes/Main.unity` automatically.
5. Press the **Play** button. Unity Editor Play mode uses the keyboard controls below.

If the scene does not open automatically, use **Infinite Runner > Open Main Scene** in Unity's top menu.

## Keyboard controls

| Action | Keys |
| --- | --- |
| Run forward | Hold `W` |
| Change lane | `A` / `D` or left / right arrows |
| Jump | `Space` or up arrow |
| Crouch | Hold `S` or down arrow |
| Recalibrate body tracking | `C` |
| Restart after a crash | `R` or `Enter` |

The runner only moves forward while `W` is held or the camera recognizes running-in-place. Dodge the taxis, fruit trucks and green city minibuses, jump the sidewalk hotpot tables, and crouch beneath the low Tet banners strung between bamboo poles. All obstacles keep their original collision shapes, so jump and crouch timing is unchanged.

## Metro rooftop sections

The first Metro Line 1 route starts around 120 meters into the course, then repeats every 360 meters. Follow the yellow-edged concrete ramps up 4.2 meters and run across about 80 meters of train roofs before descending to the avenue. The first layout has ramps on lanes 1 and 3 and construction containers blocking lane 2. Later routes alternate single lanes, adjacent pairs, split outer lanes, and all three lanes. The selected lanes stay consistent from ascent to descent; closed lanes have no hidden rooftop bridge. Hitting a closed lane or the side of a train ends the run. Signs, the HUD, and coin trails announce the usable lanes, with a clear approach before each ramp. Rooftop air-conditioning units can be jumped; dodging is possible wherever adjacent train roofs connect.

## Build and play the body-controlled Web version

1. Use **Infinite Runner > Build & Run Web Game** in Unity's top menu. The first Web build may take several minutes.
2. Unity builds into `Builds/Web` and opens it using its local server. The included `InfiniteRunnerPose` Web template is selected automatically.
3. At the opening menu, choose **Play with keyboard** or **Play with camera**.
4. Camera mode immediately asks for browser permission. Choose **Allow**, then step back until your shoulders, hips, knees, and ankles are visible.
5. Move into the camera preview's **CENTER** section, then stand normally and still while body controls calibrate. Use **Controls: Camera/Keyboard** above the game to switch modes later.

Camera access works on Unity's local **Build and Run** server. A deployed game must use HTTPS; opening the generated `index.html` directly is not supported. The first camera start downloads the pinned MediaPipe `1.0.1` runtime and the full pose model (GPU), falling back to the lite model on CPU-only browsers, so an internet connection is currently required.

If no camera prompt appears, check the camera icon beside the browser address bar. A previously blocked site must be changed back to **Allow** before it can ask again. Also make sure the browser itself has camera access in macOS **System Settings > Privacy & Security > Camera**.

## Body controls

| Body action | Game action |
| --- | --- |
| Alternate knee lifts / run in place, or pump your arms | Run forward |
| Move your torso into the LEFT camera section | Select the left lane |
| Move your torso into the CENTER camera section | Select the center lane |
| Move your torso into the RIGHT camera section | Select the right lane |
| Hop up (both feet if they are visible) | Jump |
| Crouch and hold | Stay crouched |

The two visible guide lines divide the preview into three directly mapped lanes. Calibration needs about 1 second of a steady, upright pose in the center section with at least your shoulders and hips visible; moving restarts that interval. The preview shows the recognizer's actual calibration progress and selected lane. Use the page's **Recalibrate** button—or press `C`—if the camera position changes.

Open **Gesture sensitivity** beside the camera preview to tune running, jumping, and crouching independently. Higher values react to smaller body movements. Existing saved run/jump choices are preserved, with crouch starting at 65%. These choices apply immediately and are saved by the browser for future visits to the same Web address.

Hop up, then land before jumping again. When your feet are in view both must leave the ground; when the camera crops them, the torso must rise a bit further instead. Jumps can be combined with a lane change. Crouch by lowering your hips and shoulders, hold that position, and stand back up to release. The Jump and Crouch cards light up when recognized. A brief noisy frame is rejected, and a jump arriving just before the avatar lands is buffered for up to 180 ms. Losing tracking clears queued actions.

Once you are running in camera mode, the avatar keeps its forward pace throughout a jump and for 350 ms after landing, giving you time to resume stepping. Brief gaps in the running signal are smoothed over. A jump from standing still does not start a run, and tracking loss or switching controls clears this assistance. Running is detected from alternating knees, alternating feet, or alternating arm swings, so it still works when the camera crops your legs. You can resume with either foot after landing. Keyboard hold-to-run behavior is unchanged.

Body smoothing uses elapsed time so it behaves consistently at different camera rates. Pose sampling targets up to 30 Hz and reduces its rate when inference takes longer. Only the shoulders and hips gate tracking; knees, feet and wrists are used when MediaPipe sees them clearly and ignored when they are cropped or faint. The neutral standing posture follows slow drift (stepping toward the camera, swaying, changing lanes) while freezing during jumps and crouches, so calibration does not go stale mid-run.

To check controls without a camera, use **Infinite Runner > Validate Body Controls** in Unity and `node Validation/pose-bridge.test.cjs` from the project directory. These replay synthetic landmarks and exercise browser settings/feedback; they do not substitute for trying the camera under your own lighting and framing.

## Included systems

- Three-lane movement with smooth lane changes
- Forward movement gated by keyboard or recognized running-in-place
- CharacterController-based jumping, gravity, and held crouching
- MediaPipe Pose Landmarker integration for Unity Web
- Automatic neutral-pose calibration, landmark smoothing, gesture hysteresis, and input cooldowns
- Visible camera preview and skeleton overlay with safe tracking-loss behavior
- Increasing run speed and distance scoring
- Collectible coins and saved best score
- Pooled, recycled 30-meter track sections
- Procedurally varied but passable obstacle patterns
- Game-over and instant keyboard restart
- Procedural Saigon streets, a District 1 skyline, and a daylight HUD
- Scenery batched by material per recycled block, with generated meshes and materials released on restart
- Compatibility with either Unity's legacy input backend or the Input System package

## Where to customize it

- Player tuning: `Assets/InfiniteRunner/Scripts/RunnerController.cs`
- Keyboard/body input routing: `Assets/InfiniteRunner/Scripts/RunnerInput.cs`
- Pose calibration and gesture thresholds: `Assets/InfiniteRunner/Scripts/PoseGestureRecognizer.cs`
- Browser-to-Unity bridge: `Assets/InfiniteRunner/Scripts/MediaPipeBridge.cs`
- Web camera and MediaPipe integration: `Assets/WebGLTemplates/InfiniteRunnerPose/TemplateData/pose-bridge.js`
- Speed and scoring: `Assets/InfiniteRunner/Scripts/GameManager.cs`
- Metro route schedule, ramps, and train roofs: `Assets/InfiniteRunner/Scripts/MetroRoofRoute.cs`
- Slope ground contact: `Assets/InfiniteRunner/Scripts/RunnerGrounding.cs`
- Track length and pool size: `Assets/InfiniteRunner/Scripts/TrackManager.cs`
- Street-object models and shared meshes: `Assets/InfiniteRunner/Scripts/StreetObstacleLibrary.cs`
- Obstacle collision shapes and pooling: `Assets/InfiniteRunner/Scripts/PooledObstacle.cs`
- Obstacle patterns: `Assets/InfiniteRunner/Scripts/TrackSegment.cs`
- Prototype colors and materials: `Assets/InfiniteRunner/Scripts/PrototypeFactory.cs`
- Saigon streets and landmark skyline: `Assets/InfiniteRunner/Scripts/SaigonScenery.cs`
- Tropical sky: `Assets/InfiniteRunner/Resources/SaigonSky.shader`
- World and player construction: `Assets/InfiniteRunner/Scripts/GameBootstrap.cs`

For production hosting, copy the pinned MediaPipe JavaScript, WebAssembly files, and pose model into the Web template instead of loading them from CDNs. The platform-neutral pose sample and C# gesture recognizer can later be reused by a native iOS MediaPipe provider.
