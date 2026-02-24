# Tất tần tật về MODELS


## Tổng quan
### 1. "Models" trong kiến trúc phần mềm là gì?

Trong lập trình phần mềm (đặc biệt là theo mô hình MVC, API hay Clean Architecture), **Model** là tầng dùng để **đại diện cho dữ liệu và cấu trúc dữ liệu**.

Hiểu một cách đơn giản:

* **Model trả lời câu hỏi "Cái gì?" (What):** Dữ liệu trông như thế nào? Có những thuộc tính gì? Trạng thái ra sao?
* **Service/Controller trả lời câu hỏi "Làm thế nào?" (How):** Dữ liệu này sẽ được lấy từ đâu, tính toán thế nào, và gửi đi đâu?

Đặc điểm của Model là chúng thường chỉ chứa các thuộc tính (Properties), hằng số (Constants), kiểu liệt kê (Enums) và các logic rất cơ bản để quản lý trạng thái của chính nó. Chúng **không** chứa các logic gọi mạng (HTTP), không gọi database, và không giao tiếp trực tiếp với phần cứng.

---

### 2. Tại sao 4 file này lại được xếp vào thư mục `Models`?

Dưới đây là lý do cụ thể cho từng file dựa trên mã nguồn dự án của bạn:

#### A. `ModbusRegisters.cs` (Mô hình hợp đồng dữ liệu - Data Contract)

* **Nội dung:** Chứa các hằng số (`LeftMotorSpeed = 1000`, `Command = 1002`) và các Enums (`CommandCode`, `StatusCode`, `ErrorCode`).
* **Lý do là Model:** Đây là bản vẽ mô phỏng cấu trúc bộ nhớ (Memory Map) của giao thức Modbus. Nó đóng vai trò là "Ngôn ngữ chung" (Ubiquitous Language) giữa C# và C++. Bằng cách định nghĩa chúng ở tầng Model, các Service khác (như `ModbusClient`) chỉ cần gọi tên hằng số thay vì phải nhớ các con số tĩnh, giúp code dễ đọc và tránh sai sót.

#### B. `AgvState.cs` (Mô hình trạng thái - State Snapshot)

* **Nội dung:** Chứa các properties như `PositionX`, `PositionY`, `HeadingDegrees`, `BatteryLevel`, và `Status`.
* **Lý do là Model:** Nó đại diện cho "ảnh chụp" (snapshot) trạng thái vật lý của chiếc AGV tại một thời điểm nhất định. Nó là một đối tượng chứa dữ liệu thuần túy (Data Holder). Hệ thống sẽ đọc dữ liệu từ C++ Modbus và "đổ" (map) vào Model này, sau đó đưa Model này cho Controller hoặc Database để xử lý tiếp.

#### C. `DetectionResult.cs` (Đối tượng chuyển giao dữ liệu - DTO)

* **Nội dung:** Chứa các class `VisionResponse`, `Detection`, `BoundingBox` cùng với các thẻ `[JsonPropertyName]`.
* **Lý do là Model:** Khi module C# gọi API sang module Python (Vision AI), Python trả về một chuỗi JSON. C# không thể trực tiếp hiểu JSON này. Do đó, `DetectionResult` được tạo ra làm **Data Transfer Object (DTO)** — một khuôn mẫu để C# có thể "hứng" (deserialize) dữ liệu JSON từ Python chuyển thành object trong C#. Nó chỉ mô tả hình thù của dữ liệu Vision AI trả về.

#### D. `GridMap.cs` (Mô hình nghiệp vụ cốt lõi - Domain Model)

* **Nội dung:** Chứa lưới 2D kích thước 40x20 (`CellType[,] _grid`) đại diện cho nhà kho, và một số hàm như `InitStaticWalls`, `SetObstacle`, `WorldToGrid`.
* **Lý do là Model:** Khác với 3 file trên chỉ chứa dữ liệu tĩnh, `GridMap` có chứa một chút logic (hàm). Tuy nhiên, đây là **Domain Logic** (Logic nghiệp vụ lõi) chứ không phải Application Logic. Các hàm của nó chỉ dùng để tự quản lý trạng thái mảng 2D của chính nó (như biến toạ độ thực tế thành toạ độ mảng, check xem ô có trống không). Nó mô phỏng lại "thế giới thực" (nhà kho) để thuật toán A* (sẽ nằm ở tầng Service) có thể lấy ra và tính toán đường đi.

### Tóm lại

Việc bạn (hoặc AI) nhóm các file này vào thư mục `Models` là hoàn toàn tuân thủ nguyên tắc **Separation of Concerns (Tách biệt mối quan tâm)** và **Single Responsibility Principle (SRP)** trong dự án của bạn:

1. **Models (`AgvState`, `GridMap`, v.v.):** Chỉ chứa hình hài dữ liệu.
2. **Services (`ModbusClient`, `PathPlanner`, `VisionClient`):** Chứa não bộ (logic) xử lý các Models đó.
3. **Controllers:** Nhận request từ ngoài, điều phối Services làm việc, và trả Models về cho người dùng.

## Chi tiết từng file
### A. `ModbusRegisters.cs`
