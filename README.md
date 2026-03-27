# Autism_VR (ML VR Lesson + Gemini Chat + Muse2 Attention Feedback)

## Current status (as of 2026-03-26)

### Learning flow
- Learner watches the `Logistic Regression` tutorial (`Assets/scripts/TeachingAudioManager.cs`).
- `AIDoubtSolver` (scene-serialized type) is implemented by `ChatbotUIBuilder.cs` (compat shim) and drives the AI chatbot UI + Gemini integration.

### Attention feedback loop (implemented)
`ChatbotUIBuilder.SetFocusState(state)` now drives the closed-loop behavior:
- `FOCUSED`:
  - resumes lesson if it was in break
  - closes chatbot *only if it was auto-opened by attention*
  - shows tutorial screen when chatbot is closed
- `UNFOCUSED`:
  - pauses the lesson
  - auto-opens chatbot (unless user already opened it)

To connect Muse2/BILSTM output into the loop, use:
- `Assets/scripts/AttentionStateBridge.cs`
  - call `OnAttentionClass(0|1|2)` or `OnAttentionStateChanged("FOCUSED"|"UNFOCUSED")`

### Chatbot UI (implemented and improved)
`Assets/scripts/ChatbotUIBuilder.cs` updates include:
- Rounded/pill-style backgrounds for panels, bubbles, typing indicator, input bar, and quick-reply chips
- Fixed input bar sizing (removed negative-width layout that was causing bad alignment)
- Added inspector-controlled readability scaling (`uiScale`, default `1.6`) to enlarge fonts, header, input controls, bubbles, and suggestion chips for easier VR readability
- Increased bubble text widths/heights so AI responses are visible and easier to read
- Added explicit bubble layout sizing (`EnsureBubbleLayoutVisible`) after message text assignment to prevent zero-height/collapsed bubbles (responses may parse correctly but remain invisible without this)
- Bubble rendering now uses direct script-built row creation per message (no prefab/template dependency), to avoid hierarchy/layout mismatch causing invisible messages
- Message text lookup/layout sizing is now prefab-hierarchy-agnostic (`FindMessageTextTMP`, `FindBubbleTransform`) so bot/user replies remain visible even if prefab child paths differ from `Bubble/MessageText`
- UI is now strictly script-only for chat rows, bubbles, and message text (prefab assignment and template cloning are removed from the chat setup path)
- Removed the header focus badge UI (green focused indicator) while keeping attention-driven behavior intact
- Default chat UI scale reduced (less “zoomed-in”)
- `Ask AI Teacher` button is forced interactable on startup by `ChatbotUIBuilder`
- `Ask AI Teacher` button now auto-finds by name/label and is force-enabled every frame to prevent accidental disabling by other scripts or parent `CanvasGroup`s
- Logistic tutorial slider default `Background` visuals are disabled in `SliderController` to remove the persistent dark-blue overlay rectangle
- Matches your prototype style more closely:
  - header + status badge for attention state
  - greeting + “quick reply chips” (purple buttons)
  - scrollable chat bubbles
  - mic + text input, Gemini text/voice requests

### Gemini integration (implemented)
- Gemini calls happen in `ChatbotUIBuilder`:
  - text via `...:generateContent`
  - voice via `inline_data` audio/wav (microphone recording)
- HTTP `429` (rate limit) is now handled with retry-aware cooldown:
  - reads `Retry-After` response header when available
  - starts cooldown timer automatically and shows a clear in-chat wait message instead of generic failure
- Chat message appearance/scroll timing now uses unscaled time (`WaitForSecondsRealtime` + `Time.unscaledDeltaTime`) so bubbles still render even if gameplay pauses with `Time.timeScale = 0`
- Scroll viewport clipping/layout has been hardened for visibility:
  - `Viewport` uses `RectMask2D` instead of `Mask`
  - chat rows now use explicit `LayoutElement` height + stretch anchors
  - meta/suggestion rows also provide fixed preferred/min heights for stable stacking

## Model / ML assets

### BiLSTM ONNX
- ONNX model placed at:
  - `Assets/Models/attention_bilstm.onnx`
- Notebook training summary (from `lstm-super.ipynb`):
  - Input: `(batch=1, timesteps=512, channels=4)` float32
    - channels order: `TP9, AF7, AF8, TP10`
  - Output: logits `(batch=1, classes=3)`
    - class mapping: `0 Focused | 1 Unfocused | 2 Drowsy`

### Unity inference runner (added)
- `Assets/scripts/AttentionSentisRunner.cs`:
  - uses Unity `com.unity.ai.inference` workflow (`Unity.InferenceEngine`)
  - runs ONNX inference and calls `AttentionStateBridge.OnAttentionClass(cls)`
  - rolling input API:
    - `PushSample(tp9, af7, af8, tp10)` (call per Muse2 sample at 256Hz)

## What you still need to do (to finish end-to-end)
1. In the scene, create an object (example: `AttentionSystem`) and attach:
   - `AttentionStateBridge`
   - `AttentionSentisRunner`
2. In Inspector:
   - assign `AttentionSentisRunner.onnxModel = Assets/Models/attention_bilstm.onnx`
   - assign `AttentionSentisRunner.bridge = AttentionSystem`
3. Wire your Muse2 receiver script to call:
   - `AttentionSentisRunner.PushSample(tp9, af7, af8, tp10)`
4. Fill correct `mean[4]` and `std[4]` in `AttentionSentisRunner`
   - these must match the StandardScaler used during training

## Notes
- Lesson pause/break is now implemented by `TeachingAudioManager.WaitForSecondsWithPause(...)`, so the lesson timeline does not continue while chatbot/break is active.
- `AttentionStateBridge` keyboard testing uses the Unity Input System. If you don’t need keyboard testing, turn `enableKeyboardTesting` OFF in Inspector.
- For keyboard testing keys in Inspector, `Alpha1/Alpha2/Alpha3` are mapped to InputSystem `Digit1/Digit2/Digit3`.
- If chat responses are logged but not visible, check:
  - `AttentionSentisRunner.onnxModel` is assigned in Inspector (to avoid startup errors)
  - `ChatbotUIBuilder` has no runtime exception in `BuildScrollView` (content layout must initialize successfully)
  - `ChatbotUIBuilder` logs `botTemplate=OK` and `[Bubble] text assigned OK ...` after sending a message
  - Unity Console is cleared after script updates to avoid confusing stale errors from previous runs
- UDP attention (Muse2 + Python -> Unity) integration:
  - Python streamer: `ML/muse_udp_streamer.py`
  - Unity receiver/thresholds: `Assets/scripts/MuseUdpAttentionThresholdController.cs`
  - Behavior:
    - `Unfocused` streak >= 20 consecutive UDP messages => open AI chat (`ChatbotUIBuilder.OpenChatbot()`)
  - Notes:
    - `staleResetSeconds` default increased to 60s to avoid streak resets due to brief UDP gaps
    - Python streamer always sends every accepted window (required for Unity streak counting)
    - Chat auto-open is rate-limited with `chatPopupCooldownSeconds` (default 20s) to avoid frequent popups
    - Drowsy is mapped to Unfocused in `ML/muse_udp_streamer.py` (model remains 3-class, but Unity only uses Focused/Unfocused)

