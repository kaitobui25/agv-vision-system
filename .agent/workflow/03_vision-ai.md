# Claude AI chỉ ra những kỹ năng thiếu

## Đánh giá thực tế

**Những gì bạn đã làm:**

- Dùng YOLOv11 pretrained → fine-tune với custom dataset
- Tích hợp vào FastAPI pipeline
- Connect với toàn bộ hệ thống AGV

**Với yêu cầu JD "Image processing / Deep Learning / AI algorithm development" — level hiện tại của project chỉ đạt khoảng 30-40%.**

---

## Cái đang thiếu — nói thẳng

**"Image processing"** — bạn chưa làm gì với ảnh ngoài việc đưa thẳng vào YOLO. Thực tế cần: preprocessing pipeline (denoise, histogram equalization, contrast enhancement), xử lý ảnh từ camera stream thực tế, có thể cần undistort fisheye lens.

**"Deep Learning"** — bạn dùng YOLO như một black box. Chưa thể hiện hiểu biết về: loss function, backpropagation, tại sao chọn YOLOv11 thay vì EfficientDet hay RT-DETR, trade-off giữa model size và accuracy, tại sao fine-tune tốt hơn train from scratch.

**"AI algorithm development"** — đây là phần nặng nhất. "Development" ngụ ý bạn phải tự xây dựng hoặc modify algorithm, không chỉ dùng có sẵn. Với AGV cụ thể, đây là: distance estimation từ 2D camera (không có depth sensor), obstacle trajectory prediction, sensor fusion giữa camera và LiDAR/ultrasonic.

## Chatbot khuyên (me: sẽ thực hiện khi muốn đào sâu thêm)

## 1. Image Processing — thiếu hoàn toàn hiện tại

Hiện tại `app.py` nhận ảnh rồi đưa thẳng vào YOLO, không xử lý gì. Cần thêm **preprocessing pipeline** trước khi inference:

**Cụ thể cần implement:**

Grayscale + histogram equalization — chuẩn hóa độ sáng cho ảnh chụp trong điều kiện ánh sáng kém. Warehouse hay có vùng tối/sáng không đồng đều, bước này giúp YOLO nhìn rõ hơn.

Gaussian blur để giảm noise — camera rẻ tiền hay có noise, blur nhẹ trước khi detect giúp giảm false positive.

Contrast enhancement (CLAHE) — Contrast Limited Adaptive Histogram Equalization, chuẩn hơn histogram thường, dùng phổ biến trong industrial vision.

Đây là OpenCV thuần, không phức tạp về code nhưng thể hiện bạn biết **tại sao** cần xử lý ảnh trước khi đưa vào model.

## 2. Deep Learning — cần thêm hiểu biết thể hiện ra ngoài

Hiện tại không thấy bạn giải thích lý do kỹ thuật nào trong code hay docs.

**Cần làm:**

Trong `vision-ai/README.md` hoặc `PROJECT_NOTES.md`, thêm section giải thích kỹ thuật — tại sao chọn YOLOv11s thay vì nano hay medium, trade-off giữa speed và accuracy, tại sao fine-tune từ pretrained thay vì train from scratch, ý nghĩa của các metrics (mAP, precision, recall) và kết quả thực tế bạn đạt được (0.936 mAP).

Thêm **model evaluation script** — sau khi train xong, có script chạy evaluate trên test set, in ra confusion matrix, plot precision-recall curve. Đây là thứ thể hiện bạn không chỉ train rồi thôi mà còn biết đánh giá model.

## 3. AI Algorithm Development — phần quan trọng nhất đang thiếu

Đây là gap lớn nhất. "Algorithm development" nghĩa là bạn phải tự implement một thứ gì đó, không chỉ gọi API có sẵn.

**Thứ phù hợp nhất với project AGV của bạn:**

**Distance estimation từ 2D camera** — hiện tại `distance_meters` trong code đang là `None`. Đây là cơ hội lớn. Implement thuật toán ước tính khoảng cách dựa trên bounding box size — vật thể càng gần thì bbox càng lớn. Dùng công thức pinhole camera model: `distance = (real_height * focal_length) / bbox_pixel_height`. Không cần depth camera, chỉ cần biết kích thước thực của vật thể.

**Obstacle risk scoring** — không phải chỉ detect "có vật thể", mà tính risk score dựa trên: confidence × (1/distance) × object_class_weight. Forklift nguy hiểm hơn thùng carton, vật gần nguy hiểm hơn vật xa. Output là một số từ 0-1 để AGV control quyết định dừng hay chậm lại hay đi tiếp.

**Tracking đơn giản** — dùng centroid tracking hoặc IoU tracking giữa các frame liên tiếp. Phát hiện vật thể đang di chuyển hay đứng yên, hướng di chuyển. Người đang đi ngang AGV nguy hiểm hơn người đứng yên xa.

---

## Thứ tự ưu tiên

Làm theo thứ tự này vì tăng dần độ phức tạp và impact:

Trước tiên implement **distance estimation** — fill vào `distance_meters` đang là `None`, có ngay kết quả visible, liên quan trực tiếp đến AGV safety, và thể hiện bạn hiểu camera geometry.

Tiếp theo thêm **preprocessing pipeline** vào `app.py` — vài chục dòng OpenCV, nhưng cần giải thích rõ trong comment tại sao từng bước.

Cuối cùng thêm **risk scoring** — kết hợp output của distance estimation với class weight, output vào JSON response để C# AGV control dùng quyết định hành động.

---

## Kết quả sau khi làm 3 thứ trên

`distance_meters` từ `None` thành số thực → AGV biết vật cách bao xa

Response từ Vision AI sẽ thành:

```json
{
    "object_class": "person",
    "confidence": 0.87,
    "distance_meters": 2.3,
    "risk_score": 0.91,
    "action_recommended": "SLOW_DOWN"
}
```

Đây là thứ một hệ thống AGV thực tế cần, và là thứ thể hiện bạn hiểu bài toán ở mức engineering, không chỉ mức "chạy được YOLO".
