# Giải thích tư duy thiết kế, triển khai...

## 1. Tại sao chia thành 4 thư mục Models, Services, Controllers, Data?
Hãy tưởng tượng hệ thống AGV như **một nhà máy sản xuất**:

```
Request đến  →  Controllers  →  Services  →  Models / Data
(ai nhận?)      (ai xử lý?)     (dữ liệu gì?)  (lưu ở đâu?)
```

### Mỗi thư mục có 1 trách nhiệm duy nhất:

| Thư mục | Vai trò | Trong nhà máy AGV |
|---|---|---|
| **Models** | "Cái gì?" — Định nghĩa dữ liệu | Bản vẽ kỹ thuật: register nào, trạng thái gì, lỗi gì |
| **Services** | "Làm gì?" — Logic xử lý | Công nhân: đọc Modbus, gọi Vision AI, tính đường đi |
| **Controllers** | "Ai gọi?" — Nhận request HTTP | Lễ tân: nhận yêu cầu từ bên ngoài, chuyển cho đúng bộ phận |
| **Data** | "Lưu ở đâu?" — Kết nối database | Kho lưu trữ: ghi log vào PostgreSQL |

### Luồng dữ liệu cụ thể trong AGV:

```
1. Controller nhận HTTP request "di chuyển đến (5000, 3000)"
       ↓
2. Service xử lý:
   - Đọc AgvState (Model) từ Modbus
   - Gọi Vision AI → nhận DetectionResult (Model)
   - Cập nhật GridMap (Model)
   - Tính đường đi A*
   - Ghi lệnh motor qua ModbusRegisters (Model)
       ↓
3. Data ghi log vào PostgreSQL
```

### Tại sao không gộp chung 1 file?

**Nguyên tắc Single Responsibility (SRP):** Mỗi phần chỉ có 1 lý do để thay đổi.

| Khi cần thay đổi... | Chỉ sửa... | Không ảnh hưởng... |
|---|---|---|
| Thêm register mới | `Models/` | Services, Controllers, Data |
| Đổi thuật toán pathfinding | `Services/` | Models, Controllers, Data |
| Thêm API endpoint mới | `Controllers/` | Models, Services, Data |
| Đổi database schema | `Data/` | Models, Services, Controllers |

Nếu gộp hết vào 1 file → sửa 1 chỗ có thể vô tình break chỗ khác.

### Đây là pattern gì?

Đây là **Layered Architecture** — kiến trúc phân tầng, convention chuẩn của ASP.NET Core:

```
Controllers/   →  Presentation Layer  (giao tiếp bên ngoài)
Services/      →  Business Layer      (logic nghiệp vụ)
Models/        →  Domain Layer        (cấu trúc dữ liệu)
Data/          →  Data Access Layer   (truy cập database)
```

Hầu hết mọi project .NET đều theo cấu trúc này vì nó đơn giản, dễ hiểu, và dễ bảo trì.


