# Training — Fine-tune YOLO for Warehouse AGV

Fine-tune YOLOv11s to detect warehouse-specific objects (pallets, boxes, forklifts, etc.).

## Folder Structure

```
training/
├── TRAINING_WORKFLOW.md          # Full training workflow (7 phases)
├── README.md                     # This file
├── configs/                      # YOLO training config files
│   └── warehouse_finetune.yaml   # Dataset paths + class definitions
├── scripts/                      # Training & evaluation scripts
├── datasets/                     # [gitignored] Training data
│   ├── images/
│   │   ├── train/
│   │   ├── val/
│   │   └── test/
│   └── labels/
│       ├── train/
│       ├── val/
│       └── test/
└── runs/                         # [gitignored] Training output (weights, metrics)
```

## Quick Start

### 1. Prepare Dataset

Place images and YOLO-format labels into `datasets/`:

```
datasets/images/train/img_001.jpg
datasets/labels/train/img_001.txt   ← same name, .txt extension
```

Label format (YOLO): `class_id center_x center_y width height` (normalized 0-1)

```
0 0.45 0.52 0.30 0.40
```

### 2. Create Config

Edit `configs/warehouse_finetune.yaml`:

```yaml
path: ./datasets
train: images/train
val: images/val
test: images/test

names:
    0: truck
    1: fan
    2: bolling-pin
```

### 3. Train

```bash
cd vision-ai/
yolo detect train \
  model=yolo11s.pt \
  data=training/configs/warehouse_finetune.yaml \
  epochs=100 \
  patience=20 \
  batch=16 \
  imgsz=640 \
  project=training/runs \
  name=warehouse_v1
```

### 4. Evaluate

```bash
yolo detect val \
  model=training/runs/warehouse_v1/weights/best.pt \
  data=training/configs/warehouse_finetune.yaml
```

### 5. Use Trained Model

Replace pretrained model in `app.py`:

```python
MODEL_NAME = "training/runs/warehouse_v1/weights/best.pt"
```

## Data Split Ratio

| Split      | Ratio | Purpose                        |
| ---------- | ----- | ------------------------------ |
| Train      | 70%   | Model learns from these images |
| Validation | 20%   | Tune hyperparameters, monitor  |
| Test       | 10%   | Final evaluation only          |

> **Important**: Split by scene/day, not random per image. See `TRAINING_WORKFLOW.md` Phase 4.

## Key Metrics

| Metric    | Target | Why                                       |
| --------- | ------ | ----------------------------------------- |
| mAP@0.5   | > 0.85 | Overall detection accuracy                |
| Recall    | > 0.90 | Safety-critical — must not miss obstacles |
| Precision | > 0.80 | Avoid false stops                         |

## Notes

- Dataset files (`datasets/`, `runs/`) are gitignored — do not commit large files
- Base model `yolo11s.pt` is also gitignored — auto-downloads on first run
- See [TRAINING_WORKFLOW.md](TRAINING_WORKFLOW.md) for the complete 7-phase workflow
