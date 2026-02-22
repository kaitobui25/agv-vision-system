# YOLO Fine-Tuning Workflow for Warehouse AGV — Production-Ready Approach

---

## Phase 1: Define Requirements (Week 1)

**Clearly define the problem before collecting images:**

Answer the following questions:

- What environment will the AGV operate in?
- What are the lighting conditions (industrial LED, partial darkness)?
- What is the camera angle (top-down, eye-level, low angle)?
- What is the AGV speed (affects motion blur)?

Define the object classes to detect:

home demo

- `truck`
- `fan`
- `bolling-pin`

real-world

- `pallet` — wooden/plastic pallets
- `box_small`, `box_large` — different carton sizes
- `forklift` — forklifts (highest safety risk)
- `person` — retained from COCO
- `rack` — warehouse racks (collision avoidance)
- `door` — warehouse doors (open/closed state awareness)

**Common mistake:** Defining too many classes with limited data → poor overall performance.
Start with the 4–6 most critical classes.

---

## Phase 2: Data Collection (Weeks 2–3)

**Practical minimum dataset size:**

- ~300–500 images per class to begin fine-tuning
- ~1,000–2,000 images per class for production-grade quality
- Total: typically 3,000–10,000 images for a warehouse dataset

**Proper data collection strategy:**

Capture diverse perspectives — use the actual camera mounting position intended for the AGV, not a convenient handheld angle.
If the camera will be mounted 50 cm above ground looking upward, collect data from that exact angle.

Capture diverse conditions — morning, afternoon, lights on/off, shadows, epoxy floor reflections.
A real warehouse environment is very different from internet demo images.

Capture diverse object states — new boxes, damaged boxes, high-stacked pallets, low-stacked pallets, partially occluded objects.

**Negative samples** — capture images with no target objects so the model learns when _not_ to detect anything.

---

## Phase 3: Data Labeling (Weeks 3–4)

**Common industry tools:**

- **Roboflow** — widely used in industry, supports team collaboration, AI-assisted auto-labeling (reduces manual effort by 60–70%), exports directly to YOLO format.
- **LabelImg** — open-source, offline tool for sensitive data that cannot be uploaded to the cloud.

**Professional labeling workflow:**

One person labels, another reviews.
Avoid having one person do both — familiarity increases the risk of overlooked errors.

Define clear labeling guidelines beforehand:

- Should bounding boxes include occluded regions?
- When should an object be labeled vs. ignored (too small, too blurry)?

**Quality check:**
After labeling, analyze class distribution.
If `box` has 2,000 images but `forklift` only 100, the model will become biased.
Balance the dataset or use class weights during training.

---

## Phase 4: Data Augmentation & Split (Week 4)

**Standard split:**

- Train: 70%
- Validation: 20%
- Test: 10%

**Critical principle:** Split by _scene_, not randomly by image.
If 50 images are captured from the same location and you split 45 for training and 5 for testing, the test set is effectively "leaked" due to similarity.
The test set must contain different scenes or capture days.

**Warehouse-appropriate augmentation:**

- Horizontal flip (valid)
- Brightness/contrast adjustment (simulate lighting)
- Noise (low-cost cameras)
- Motion blur (AGV in motion)

Avoid vertical flip — inverted pallets are unrealistic.

---

## Phase 5: Training Strategy (Weeks 5–6)

**Do not train from scratch — always fine-tune.**

Start from `yolo11s.pt` pretrained weights (retain COCO feature extraction).
Initially fine-tune only the final layers, then progressively unfreeze more layers (_gradual unfreezing_).
This approach converges faster and requires less data.

**Training monitoring:**

Track `mAP@0.5` on the validation set — the primary performance metric.

- If training loss decreases but validation loss increases → overfitting.
  Solution: add more data or stronger augmentation.
- Use early stopping instead of a fixed number of epochs.

**Hardware reality:**

With an NVIDIA GPU, training is 10–20× faster.
Without a GPU, use Google Colab (free tier suitable for small datasets) or Roboflow Train (paid cloud solution, convenient).

---

## Phase 6: Evaluation (Weeks 6–7)

**Key metrics:**

- `mAP@0.5` — overall detection accuracy (target > 0.85 for production)
- `Precision` — when predicting “pallet,” how often is it correct?
- `Recall` — of all real pallets, how many are detected?

**For AGV safety, Recall is more critical than Precision.**
Missing a hazardous obstacle is worse than a false alarm.

Review the confusion matrix to identify common misclassifications.
If `box` is frequently confused with `pallet`, collect more images containing both objects in close proximity.

**Test on real hardware.**
Run inference using the actual deployed camera and measure latency on the target embedded device — not on a development laptop.

---

## Phase 7: Iteration (Ongoing)

This phase never ends in real production systems.

Once deployed, the AGV continuously captures images.
Images where the model outputs low confidence (0.4–0.6) are “hard cases” — these are the most valuable samples for further labeling and retraining.

This process is known as **active learning** and is how production systems improve over time.

Any warehouse change (new inventory types, layout changes, floor repainting) may require additional fine-tuning.

---

## Realistic Timeline

| Phase                     | Duration       | Bottleneck                   |
| ------------------------- | -------------- | ---------------------------- |
| Define requirements       | 3–5 days       | Stakeholder alignment        |
| Data collection           | 1–2 weeks      | On-site warehouse access     |
| Labeling                  | 1–2 weeks      | Labor-intensive, error-prone |
| Training                  | 2–3 days       | Compute resources            |
| Evaluation & fixes        | 1–2 weeks      | Identifying edge cases       |
| **First iteration total** | **~6–8 weeks** |                              |

Subsequent iterations are significantly faster once the pipeline is established.

---

## Key Takeaway

**Data quality > Model architecture.**

Replacing YOLO11s with a larger YOLO11x model using poor data will still perform worse than YOLO11n trained on high-quality data.

In real-world machine learning projects, most time is spent on Phases 2–3 — not on training.

===============================================================================
Vietnamese
===============================================================================

# Quy trình Fine-tune YOLO cho Warehouse AGV — Thực tế Production

---

## Phase 1: Define Requirements (Tuần 1)

**Xác định rõ bài toán trước khi chụp ảnh:**

Cần trả lời: AGV sẽ hoạt động ở môi trường nào? Ánh sáng thế nào (đèn LED công nghiệp, bóng tối một phần)? Camera góc nào (top-down, eye-level, góc thấp)? Tốc độ AGV bao nhiêu (ảnh hưởng đến motion blur)?

Xác định class cần detect —

home demo

- `truck`
- `fan`
- `bolling-pin`

ví dụ thực tế:

- `pallet` — pallet gỗ/nhựa
- `box_small`, `box_large` — thùng carton các size
- `forklift` — xe nâng (nguy hiểm nhất)
- `person` — giữ lại từ COCO
- `rack` — kệ hàng (AGV không đâm vào)
- `door` — cửa kho (AGV cần biết mở/đóng)

**Sai lầm phổ biến**: Define quá nhiều class khi data ít → model kém hết. Bắt đầu với 4-6 class quan trọng nhất.

---

## Phase 2: Data Collection (Tuần 2-3)

**Số lượng tối thiểu thực tế:**

- ~300-500 ảnh mỗi class để bắt đầu fine-tune
- ~1000-2000 ảnh mỗi class để đạt production quality
- Tổng: thường cần 3,000-10,000 ảnh cho dataset warehouse

**Chiến lược chụp ảnh đúng cách:**

Đa dạng góc độ — chụp từ góc camera thực tế sẽ lắp trên AGV, không phải góc tiện tay. Nếu camera đặt thấp 50cm nhìn lên thì phải chụp đúng góc đó.

Đa dạng điều kiện — sáng buổi sáng, chiều, đèn bật/tắt, bóng đổ, phản chiếu sàn epoxy. Real warehouse rất khác ảnh demo trên internet.

Đa dạng trạng thái vật thể — thùng mới nguyên vẹn, thùng cũ móp méo, pallet xếp cao, pallet xếp thấp, vật bị che khuất một phần.

**Negative samples** — chụp ảnh không có vật thể nào để model học cách "không detect" khi không có gì.

---

## Phase 3: Data Labeling (Tuần 3-4)

**Tool thực tế:**

Roboflow — phổ biến nhất trong industry, có team collaboration, auto-label bằng AI (tiết kiệm 60-70% thời gian label thủ công), export thẳng ra YOLO format.

LabelImg — open source, offline, dùng khi data nhạy cảm không muốn upload lên cloud.

**Quy trình labeling chuyên nghiệp:**

Một người label, một người review — không để một người làm cả hai vì dễ nhìn quen bỏ sót lỗi. Định nghĩa labeling guideline rõ ràng trước: bbox có bao gồm phần bị che không? Khi nào thì label, khi nào thì bỏ qua (object quá nhỏ, quá mờ)?

**Quality check**: Sau khi label xong, chạy thống kê phân phối — nếu `box` có 2000 ảnh mà `forklift` chỉ có 100 ảnh thì model sẽ bị bias. Cần balance hoặc dùng class weights khi train.

---

## Phase 4: Data Augmentation & Split (Tuần 4)

**Split chuẩn:**

- Train: 70%
- Validation: 20%
- Test: 10%

**Quan trọng**: Split theo _scene_, không phải random từng ảnh. Nếu bạn chụp 50 ảnh cùng một góc kho thì 45 train + 5 test vẫn là "cheat" vì quá giống nhau. Test set phải là cảnh/ngày chụp khác.

**Augmentation cho warehouse:**

Flip ngang (hợp lý), thay đổi brightness/contrast (mô phỏng đèn), thêm noise (camera rẻ tiền), motion blur (AGV đang chạy). Không nên flip dọc vì pallet lộn ngược không có trong thực tế.

---

## Phase 5: Training Strategy (Tuần 5-6)

**Không train from scratch — luôn fine-tune:**

Bắt đầu từ `yolo11s.pt` pretrained (giữ lại feature extraction từ COCO), chỉ fine-tune các layer cuối trước, sau đó mở rộng dần ra toàn bộ model (gọi là _gradual unfreezing_). Cách này hội tụ nhanh hơn và ít cần data hơn.

**Monitoring khi train:**

Theo dõi `mAP@0.5` trên validation set — đây là metric chính. Nếu train loss giảm nhưng val loss tăng là đang overfit → cần thêm data hoặc augmentation mạnh hơn. Dùng early stopping, không train cố định số epoch.

**Thực tế về hardware:**

Có GPU (NVIDIA) thì train nhanh hơn 10-20x. Không có GPU thì dùng Google Colab (free tier đủ cho dataset nhỏ) hoặc Roboflow Train (cloud, trả phí nhưng tiện).

---

## Phase 6: Evaluation (Tuần 6-7)

**Metrics cần xem:**

`mAP@0.5` — accuracy tổng quan, target >0.85 cho production. `Precision` — khi nói "đây là pallet" thì đúng bao nhiêu %. `Recall` — trong tất cả pallet thực tế, phát hiện được bao nhiêu %.

**Với AGV safety thì Recall quan trọng hơn Precision** — bỏ sót một chướng ngại vật nguy hiểm hơn là báo nhầm.

Confusion matrix — xem model hay nhầm class nào với class nào. Nếu `box` hay bị nhầm thành `pallet` thì cần thêm data có cả hai gần nhau.

**Test trên real hardware** — chạy model trên camera thực, đo latency trên thiết bị thực tế (không phải laptop dev). Embedded board yếu hơn nhiều.

---

## Phase 7: Iteration (Ongoing)

Đây là phase không bao giờ kết thúc trong production thực tế.

Khi AGV deploy rồi, camera sẽ liên tục chụp ảnh. Những ảnh mà model có confidence thấp (0.4-0.6) là "hard cases" — đây chính là data quý nhất để label thêm và train lại. Quy trình này gọi là **active learning** và là cách các hệ thống production cải thiện theo thời gian.

Mỗi khi kho thay đổi (loại hàng mới, sắp xếp khác, sơn lại sàn) đều có thể cần fine-tune lại.

---

## Timeline thực tế

| Phase               | Thời gian     | Bottleneck                |
| ------------------- | ------------- | ------------------------- |
| Define requirements | 3-5 ngày      | Họp với stakeholder       |
| Data collection     | 1-2 tuần      | Phải vào kho thực tế chụp |
| Labeling            | 1-2 tuần      | Tedious, dễ sai           |
| Training            | 2-3 ngày      | Compute                   |
| Evaluation & fix    | 1-2 tuần      | Discover edge cases       |
| **Tổng lần 1**      | **~6-8 tuần** |                           |

Sau lần 1, các vòng iteration tiếp theo nhanh hơn nhiều vì đã có pipeline sẵn.

---

## Điểm mấu chốt

**Data quality > Model architecture.** Thay YOLO11s bằng YOLO11x (to hơn) với data xấu vẫn tệ hơn YOLO11n với data tốt. Phần lớn thời gian của một ML engineer thực tế là Phase 2-3, không phải Phase 5.
