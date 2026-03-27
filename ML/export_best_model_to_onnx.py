import os
import argparse
import torch
import torch.nn as nn


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
        out = out[:, -1, :]
        out = self.drop(self.relu(self.fc1(out)))
        out = self.drop(self.relu(self.fc2(out)))
        return self.fc3(out)


def main():
    parser = argparse.ArgumentParser(description="Export BiLSTM .pt to ONNX")
    parser.add_argument("--pt", required=True, help="Path to best_model.pt")
    parser.add_argument("--onnx", required=True, help="Output ONNX path")
    parser.add_argument("--opset", type=int, default=17, help="ONNX opset")
    args = parser.parse_args()

    pt_path = os.path.abspath(args.pt)
    onnx_path = os.path.abspath(args.onnx)

    if not os.path.exists(pt_path):
        raise FileNotFoundError(f".pt file not found: {pt_path}")

    os.makedirs(os.path.dirname(onnx_path), exist_ok=True)

    device = torch.device("cpu")
    model = BiLSTM().to(device)
    state = torch.load(pt_path, map_location=device)
    model.load_state_dict(state)
    model.eval()

    dummy = torch.zeros(1, 512, 4, dtype=torch.float32, device=device)

    torch.onnx.export(
        model,
        dummy,
        onnx_path,
        input_names=["input"],
        output_names=["logits"],
        dynamic_axes=None,  # fixed shape expected by Unity runtime script
        opset_version=args.opset,
    )

    print("Export complete.")
    print("PT   :", pt_path)
    print("ONNX :", onnx_path)


if __name__ == "__main__":
    main()

