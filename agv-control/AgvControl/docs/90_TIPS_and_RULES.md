# Các tips và rules khi code C# C++

## 1. Comment bằng summary

### `/// <summary>` làm được gì mà comment thường không làm được?

**1. IntelliSense hiển thị khi hover / gõ code**

Khi bạn gõ `ModbusRegisters.` ở file khác, IDE sẽ hiện tooltip:

> *Modbus register addresses for C# (agv-control) ↔ C++ (hardware-sim).*
> *Matches docs/04_MODBUS_REGISTER_MAP.md exactly.*

Nếu dùng `//` thường → IDE không hiện gì cả.

**2. Tự động sinh tài liệu API**

Chạy `dotnet build` với option `/doc` → compiler tạo file `.xml` chứa toàn bộ documentation, có thể dùng để sinh trang web API docs (giống Swagger cho REST API).

**3. Có nhiều tag hữu ích**

```csharp
/// <summary>Mô tả ngắn gọn.</summary>
/// <param name="speed">Tốc độ motor, đơn vị RPM.</param>
/// <returns>True nếu thành công.</returns>
/// <exception cref="ArgumentException">Khi speed vượt giới hạn.</exception>
/// <remarks>Ghi chú bổ sung dài hơn.</remarks>
```

### Tóm lại

| | `//` comment | `/// <summary>` |
|---|---|---|
| Dev đọc source | ✅ | ✅ |
| IDE hiện tooltip | ❌ | ✅ |
| Sinh docs tự động | ❌ | ✅ |
| Compiler hiểu | ❌ | ✅ |

**Quy tắc đơn giản:** Dùng `/// <summary>` cho mọi thứ `public` (class, method, property, enum...) để người khác dùng code của bạn biết nó làm gì mà không cần mở source.

## 2. Dấu `.` trong `AgvControl.Models` 

```
AgvControl.Models
    ↑          ↑
  Gốc      Con (sub-namespace)
```

Nó tương đương với cấu trúc thư mục:

```
AgvControl/          → namespace AgvControl
  └── Models/        → namespace AgvControl.Models
        └── ModbusRegisters.cs
        └── GridMap.cs
```

Quy tắc đơn giản

> **Namespace = Tên project + Đường dẫn thư mục**, nối bằng dấu `.`

Đây là convention chuẩn của .NET: file nằm ở thư mục nào → namespace phản ánh đường dẫn đó.

## 3. Tiêu chuẩn đặt tên (Naming Convention) của từng ngôn ngữ.
>Mỗi ngôn ngữ có một "văn hóa" riêng:

### 1. Thế giới Python (camera_server.py, app.py)

* **Quy chuẩn:** Python tuân theo một bộ quy tắc toàn cầu tên là **PEP 8**.
* **Kiểu đặt tên:** Quy định các biến (variables) và hàm (functions) phải được viết theo kiểu **`snake_case`**.
* **Cách viết:** Toàn bộ là chữ thường, các từ cách nhau bằng dấu gạch dưới `_`.
* **Ví dụ:** `actual_width`, `processing_time_ms`, `object_class`. Việc viết `ActualWidth` trong Python sẽ bị các lập trình viên Python đánh giá là code không chuẩn, giống như nói sai ngữ pháp vậy.

### 2. Thế giới C# (AgvControl)

* **Quy chuẩn:** C# tuân theo bộ quy tắc thiết kế chính thức của **Microsoft**.
* **Kiểu đặt tên:** Quy định các Class, Public Properties, và Hằng số (Constants) phải được viết theo kiểu **`PascalCase`**.
* **Cách viết:** Viết liền nhau, chữ cái đầu tiên của **mỗi từ** phải được viết hoa.
* **Ví dụ:** `WheelBaseMm`, `LeftMotorSpeed`, `CommandCode`.

---

### Bằng chứng cho sự "cố ý" này

Nếu bạn nhìn vào file `DetectionResult.cs` trong project C#, bạn sẽ thấy cách tôi xử lý sự giao thoa giữa 2 "văn hóa" này một cách rất bài bản:

```csharp
[JsonPropertyName("object_class")]
public string ObjectClass { get; set; } = string.Empty;

```

* Bức thư (JSON) từ Python gửi sang dùng `snake_case`: `"object_class"`.
* Nhưng khi nhận vào nhà C#, tôi lập tức "thay áo" cho nó thành `PascalCase` chuẩn C#: `ObjectClass`.

**Tóm lại:** Hệ thống được thiết kế bằng nhiều ngôn ngữ (Polyglot architecture) thì việc giữ đúng bản sắc của từng ngôn ngữ là rất quan trọng để code dễ đọc, dễ bảo trì cho các kỹ sư chuyên trách sau này.



