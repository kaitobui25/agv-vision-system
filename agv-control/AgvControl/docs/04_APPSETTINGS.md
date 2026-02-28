# Giải thích file appsettings.json

 [appsettings.json] chính là nơi **khai báo thông tin kết nối** của C# (agv-control) với các thành phần bên ngoài.

Nhưng cần phân biệt rõ:

## [appsettings.json]chỉ là **"danh bạ điện thoại"**

Nó chỉ **lưu địa chỉ và thông số**, chứ **không thực hiện kết nối**.

```
appsettings.json = "Gọi Vision AI ở đâu? Port mấy? Chờ bao lâu?"
Services/         = Code thực sự mở kết nối và giao tiếp
```

## Luồng hoạt động

```
appsettings.json          Program.cs (DI)              Services/
─────────────────    →    ──────────────────    →    ────────────────
Khai báo config          Đọc config, inject          Dùng config để
(địa chỉ, timeout)      vào từng Service            kết nối thực sự
```

1. [appsettings.json] → khai báo: "Vision AI ở `localhost:8000`"
2. [Program.cs] → đọc config, tạo `VisionClient` với URL đó
3. `VisionClient.cs` → thực sự gọi `GET http://localhost:8000/detect/latest`
