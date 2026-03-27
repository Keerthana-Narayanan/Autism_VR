import os
import socket
import time
from collections import deque

import numpy as np
import torch
import torch.nn as nn
from pylsl import StreamInlet, resolve_streams

# =========================
# CONFIG
# =========================
SAMPLING_RATE = 256
WINDOW_SIZE = 768
STEP_SIZE = 256
CHANNELS = 4
CONF_THRESHOLD = 0.5

UDP_IP = "127.0.0.1"
UDP_PORT = 12345

LABELS = {
    0: "Focused",
    1: "Unfocused",
    # Drowsy is mapped to Unfocused (we no longer use a separate drowsy state)
    2: "Unfocused",
}

# Re-weighting used by your previous approach
# Keep 3 weights to match 3-class logits, but make class-2 behave like class-1.
BASE_WEIGHTS = torch.tensor([1.35, 0.7, 0.7], dtype=torch.float32)

# Load model from Assets/Models by default (edit if needed)
DEFAULT_MODEL_PATH = os.path.join(
    os.path.dirname(__file__),
    "..",
    "Assets",
    "Models",
    "best_model.pt",
)

MODEL_PATH = os.environ.get("MUSE_BILSTM_MODEL_PATH", DEFAULT_MODEL_PATH)

"""
We ALWAYS send UDP for every accepted prediction window.
Unity's "N times continuously" logic requires repeated messages even when the state is unchanged.
"""

# =========================
# MODEL
# =========================
class BiLSTM(nn.Module):
    def __init__(self):
        super().__init__()
        self.bilstm = nn.LSTM(
            input_size=4,
            hidden_size=256,
            num_layers=2,
            batch_first=True,
            bidirectional=True,
            dropout=0.5,
        )
        self.drop = nn.Dropout(0.5)
        self.fc1 = nn.Linear(512, 256)
        self.fc2 = nn.Linear(256, 128)
        self.fc3 = nn.Linear(128, 3)
        self.relu = nn.ReLU()

    def forward(self, x):
        out, _ = self.bilstm(x)
        out = out[:, -1, :]  # last timestep
        out = self.drop(self.relu(self.fc1(out)))
        out = self.drop(self.relu(self.fc2(out)))
        return self.fc3(out)


def main():
    if not os.path.exists(MODEL_PATH):
        raise FileNotFoundError(f"Model not found at: {MODEL_PATH}")

    # =========================
    # UDP SETUP
    # =========================
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    print(f"📡 Sending UDP data to {UDP_IP}:{UDP_PORT}")

    # =========================
    # MODEL LOAD
    # =========================
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    print("🔄 Loading model from:", MODEL_PATH)

    model = BiLSTM().to(device)
    model.load_state_dict(torch.load(MODEL_PATH, map_location=device))
    model.eval()

    base_weights = BASE_WEIGHTS.to(device)
    print("✅ Model Loaded on", device)

    # =========================
    # CONNECT EEG STREAM (Muse2 via LSL)
    # =========================
    print("🔍 Searching for EEG stream (Muse2 via LSL)...")
    streams = resolve_streams()
    eeg_streams = [s for s in streams if s.type() == "EEG" and "Muse" in s.name()]
    if len(eeg_streams) == 0:
        raise RuntimeError("❌ No EEG stream found (Muse*)")

    inlet = StreamInlet(eeg_streams[0])
    print("✅ Connected to EEG stream:", eeg_streams[0].name())

    # =========================
    # BUFFER + LOOP
    # =========================
    buffer = deque(maxlen=WINDOW_SIZE)
    step_counter = 0
    pred_history = deque(maxlen=5)

    def preprocess(window_2d):
        """
        window_2d: list/np of shape (WINDOW_SIZE, CHANNELS)
        Returns: torch tensor shape (1, WINDOW_SIZE, CHANNELS)
        """
        window_2d = np.array(window_2d, dtype=np.float32)
        mean = np.mean(window_2d, axis=0)
        std = np.std(window_2d, axis=0) + 1e-6
        z = (window_2d - mean) / std
        return torch.tensor(z, dtype=torch.float32).unsqueeze(0).to(device)

    def is_noisy(window_2d_raw):
        variance = np.var(window_2d_raw)
        max_amp = np.max(np.abs(window_2d_raw))
        return variance > 200 or max_amp > 400

    print("🚀 Running real-time prediction (UDP every accepted window)...\n")
    seq = 0

    while True:
        sample, _ = inlet.pull_sample()

        # Muse LSL stream ordering: this keeps your existing assumption (first 4 channels)
        eeg_sample = np.array(sample[:CHANNELS], dtype=np.float32)
        buffer.append(eeg_sample)
        step_counter += 1

        if len(buffer) != WINDOW_SIZE:
            continue

        if step_counter < STEP_SIZE:
            continue

        step_counter = 0
        window_np = np.array(buffer, dtype=np.float32)

        window = preprocess(buffer)
        noisy = is_noisy(window_np)

        with torch.no_grad():
            output = model(window)
            probs = torch.softmax(output, dim=1)

            # Apply re-weighting (your existing approach)
            weights = base_weights.clone()
            if noisy:
                # Treat drowsy the same as unfocused (both map to Unfocused).
                weights[1] *= 0.95
                weights[2] *= 0.95

            probs = probs * weights
            probs = probs / probs.sum(dim=1, keepdim=True)

            probs_np = probs.cpu().numpy()[0]
            f, u, d = probs_np

            # decision logic (kept as you wrote it)
            if (d > 0.30 and abs(u - d) < 0.15):
                pred = 2
                confidence = float(d)
            elif (f > 0.40 and f > u):
                pred = 0
                confidence = float(f)
            else:
                pred = int(np.argmax(probs_np))
                confidence = float(np.max(probs_np))

        if confidence < CONF_THRESHOLD:
            continue

        pred_history.append(pred)
        counts = {i: pred_history.count(i) for i in range(3)}

        smoothing_weights = {0: 1.25, 1: 0.75, 2: 1.3}
        final_pred = max(counts, key=lambda x: counts[x] * smoothing_weights[x])

        # Map class-2 to Unfocused (class-1) for streaming.
        if final_pred == 2:
            final_pred = 1
        state = LABELS[final_pred]
        seq += 1

        # Always send every accepted window (required for Unity streak counting).
        sock.sendto(state.encode("utf-8"), (UDP_IP, UDP_PORT))

        print(f"🧠 UDP State: {state} | Conf: {confidence:.2f} | seq={seq}")


if __name__ == "__main__":
    main()

