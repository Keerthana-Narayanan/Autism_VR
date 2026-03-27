## Export your BiLSTM (`best_model.pt`) to ONNX for Unity (Sentis)

Your notebook trains a PyTorch model:
- **Input**: `(1, 512, 4)` float32 (2 seconds @ 256 Hz, 4 channels)
- **Output**: `(1, 3)` logits float32
- **Classes**: `0 Focused`, `1 Unfocused`, `2 Drowsy`

Unity side uses `Assets/scripts/AttentionSentisRunner.cs` (Sentis).

### 1) Make sure you have `best_model.pt`
From your notebook you already do:

- `torch.save(model.state_dict(), SAVE_FOLDER + 'best_model.pt')`

Copy `best_model.pt` into your Unity project (recommended):
- `Assets/Models/best_model.pt` (for storage only), and export ONNX next.

### 2) Add this ONNX export cell to the notebook (after training)
Run this in the same environment where you have the `BiLSTM` class defined and `best_model.pt` available.

```python
import torch

# Recreate model (same as training)
class BiLSTM(torch.nn.Module):
    def __init__(self):
        super().__init__()
        self.bilstm = torch.nn.LSTM(
            input_size=4,
            hidden_size=256,
            num_layers=2,
            batch_first=True,
            bidirectional=True,
            dropout=0.5
        )
        self.drop = torch.nn.Dropout(0.5)
        self.fc1  = torch.nn.Linear(512, 256)
        self.fc2  = torch.nn.Linear(256, 128)
        self.fc3  = torch.nn.Linear(128, 3)
        self.relu = torch.nn.ReLU()

    def forward(self, x):
        out, _ = self.bilstm(x)
        out = out[:, -1, :]
        out = self.drop(self.relu(self.fc1(out)))
        out = self.drop(self.relu(self.fc2(out)))
        return self.fc3(out)

device = torch.device("cpu")
model = BiLSTM().to(device)
state = torch.load(SAVE_FOLDER + "best_model.pt", map_location=device)
model.load_state_dict(state)
model.eval()

dummy = torch.zeros(1, 512, 4, dtype=torch.float32)

onnx_path = SAVE_FOLDER + "attention_bilstm.onnx"
torch.onnx.export(
    model,
    dummy,
    onnx_path,
    input_names=["input"],
    output_names=["logits"],
    dynamic_axes=None,   # keep fixed (1,512,4) for simplest Unity runtime
    opset_version=17
)
print("Saved:", onnx_path)
```

Download `attention_bilstm.onnx` and place it into:
- `Assets/Models/attention_bilstm.onnx`

Unity will import it as a **ModelAsset**. Assign it in the inspector to `AttentionSentisRunner.onnxModel`.

### 3) IMPORTANT: normalization needs to match training
In your notebook, you do `StandardScaler().fit_transform(...)` **per CSV session**.

For real-time inference, you should switch to a **single global scaler**:
- Fit scaler on training set (all sessions combined, or train split only)
- Save `mean_` and `scale_` (std) for the 4 channels
- Use those same 4 mean/std values in Unity (`AttentionSentisRunner.mean/std`)

If you want, I can modify the notebook to compute and export global mean/std cleanly.

