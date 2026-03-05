# Giải thích VisionClient.cs

## Tổng quan
Câu chuyện: Thuê thám tử đi hỏi thăm Python AI

```
CÁC NHÂN VẬT:
  🏢  appsettings.json  =  tờ giấy ghi địa chỉ
  👔  Program.cs        =  ông sếp
  🕵️  VisionClient      =  thám tử
  🤖  Python AI         =  người cần thẩm vấn
```

---

### BUỔI SÁNG — Sếp chuẩn bị

```
  🏢 Tờ giấy nằm im trên bàn:
  ┌─────────────────────────────────────┐
  │  appsettings.json                   │
  │                                     │
  │  Địa chỉ Python AI: localhost:8000  │
  │  Chờ tối đa       : 2000ms          │
  └─────────────────────────────────────┘

  👔 Sếp (Program.cs) đọc tờ giấy:
  ┌─────────────────────────────────────┐
  │  À, Python AI ở localhost:8000      │
  │  Tao sẽ nhét thông tin này vào      │
  │  phong bì, đặt lên bàn chờ...       │
  └──────────────┬──────────────────────┘
                 │
                 │  đặt phong bì lên bàn
                 ▼
          ┌─────────────┐
          │  📨 PHONG BÌ │   <── IOptions<VisionAiSettings>
          │  localhost   │       (đây là cái tên dài dòng
          │  :8000       │        mà code hay dùng)
          └─────────────┘
```

---

### BUỔI TRƯA — Thám tử được thuê

```
  👔 Sếp gọi thám tử vào:
  
  "Mày là VisionClient.
   Nhiệm vụ: cứ 100ms chạy sang hỏi Python AI xem nó thấy gì.
   Đây — " 
   
        👔 ──[📨 phong bì]──► 🕵️
        
   "Trong phong bì có địa chỉ, tự đọc mà đi."


  🕵️ Thám tử mở phong bì ra xem:

  ┌──────────────────────────────────────────┐
  │  constructor(HttpClient, IOptions, ...)   │
  │  {                                        │
  │      mở phong bì ra: settings.Value       │
  │                                           │
  │      _httpClient.BaseAddress              │
  │          = settings.Value.BaseUrl  ◄───── │─── lấy địa chỉ từ phong bì
  │                                           │
  │      _httpClient.Timeout                  │
  │          = settings.Value.TimeoutMs ◄──── │─── lấy thời gian chờ
  │  }                                        │
  └──────────────────────────────────────────┘

  Bây giờ thám tử đã biết cần đến đâu.
```

---

### CỨ MỖI 100ms — Thám tử đi làm việc

```
  🕵️ chạy sang gõ cửa 🤖:

  🕵️ ──── GET /detect/latest ────────────► 🤖
           "mày thấy vật cản gì không?"


  3 kịch bản có thể xảy ra:
  
  ═══════════════════════════════════════════════════════
  KỊCH BẢN 1: Bình thường
  ═══════════════════════════════════════════════════════
  
  🕵️ ◄──── {"detections": [{"class":"box"}]} ──── 🤖
  
  🕵️ về báo cáo:
  "Thấy 1 cái hộp, cách 2 mét"
  → trả về VisionResponse object
  
  ═══════════════════════════════════════════════════════
  KỊCH BẢN 2: Gõ cửa không ai mở (timeout)
  ═══════════════════════════════════════════════════════
  
  🕵️ gõ cửa...
     đợi...
     đợi...
     2 giây trôi qua...
  
  🕵️ về báo cáo:
  "Không ai mở cửa trong 2 giây"  → log warning
  → trả về NULL  (không ngã xỉu, không crash)
  
  ═══════════════════════════════════════════════════════
  KỊCH BẢN 3: Địa chỉ không tồn tại (Python chưa bật)
  ═══════════════════════════════════════════════════════
  
  🕵️ chạy đến localhost:8000...
  
       ??? ở đây không có nhà nào ???
  
  🕵️ về báo cáo:
  "Địa chỉ không tồn tại"  → log warning
  → trả về NULL  (không hoảng loạn, không crash)
```

---

### TẠI SAO PHẢI DÙN PHONG BÌ (IOptions)?

Đây là câu hỏi hay nhất. Sao không viết thẳng vào code luôn?

```
  ❌ CÁCH XẤU — viết cứng trong code:
  ┌────────────────────────────────────┐
  │  _httpClient.BaseAddress           │
  │    = new Uri("localhost:8000");    │  ← dán keo vào tường
  └────────────────────────────────────┘
  
  Hôm nay Python AI chuyển sang port 9000?
  → phải mở code ra sửa, build lại, deploy lại
  → mất cả tiếng


  ✅ CÁCH TỐT — đọc từ phong bì (appsettings):
  ┌────────────────────────────────────┐
  │  _httpClient.BaseAddress           │
  │    = settings.Value.BaseUrl;       │  ← đọc từ phong bì
  └────────────────────────────────────┘
  
  Hôm nay Python AI chuyển sang port 9000?
  → chỉ sửa file appsettings.json
  → xong, không cần build lại
```

---

### TOÀN BỘ LUỒNG, 1 HÌNH

```
  appsettings.json          Program.cs            VisionClient.cs         Python AI
  ────────────────          ──────────            ───────────────         ─────────
  
  "BaseUrl:8000"            Đọc file          
        │                   Nhét vào phong bì
        └──────────────────►📨                
                            Thuê thám tử          Nhận phong bì
                            Đưa phong bì ─────────►Mở ra, đọc địa chỉ
                                                   
                                                   [mỗi 100ms]
                                                   Gọi GET ──────────────►Chạy YOLO
                                                   Nhận JSON◄──────────────Trả kết quả
                                                   Trả về object
                                                   
                                                   [nếu lỗi]
                                                   Bắt lỗi (try/catch)
                                                   Trả về null
                                                   (không crash!)
```

---

**Câu nhớ đời:**
- `appsettings.json` = tờ giấy ghi địa chỉ
- `IOptions` = phong bì chứa tờ giấy đó
- `VisionClient` = thám tử cầm phong bì đi hỏi thăm
- `null` = về tay không, nhưng vẫn về — không chết giữa đường




**Chốt lại quy trình bằng Code thực tế thưa sếp:**

1. **Báo thức:** `WebApplication.CreateBuilder(args);` (Nạp JSON)
2. **Cắt dán:** `builder.Configuration.GetSection("VisionAi")` (Lấy data) -> `Configure<VisionAiSettings>` (Tạo ra `IOptions`)
3. **Xin giấy:** `public VisionClient(..., IOptions<VisionAiSettings> settings, ...)` (Nhận data qua DI)
4. **Đọc giấy:** `settings.Value.BaseUrl` (Sử dụng data)

Đấy sếp thấy không, C# .NET thiết kế mọi thứ như một dây chuyền tự động. Thằng nào lo việc thằng nấy, trơn tru từ đầu đến cuối mà không cần các file phải gọi lằng nhằng vào nhau!

## Bức tranh toàn cảnh


### VisionClient.cs là gì?

**Nhiệm vụ duy nhất:** Gọi điện sang Python (Vision AI) hỏi "mày thấy vật cản gì không?" rồi mang kết quả về.

Chỉ vậy thôi. Không hơn.

---

### Bức tranh toàn cảnh

```
  [XE AGV đang chạy]
        |
        |  cứ mỗi 100ms hỏi 1 lần
        v
  +-----------------+          gọi HTTP          +------------------+
  |  C# (mình)      |  ----------------------->  |  Python AI       |
  |  VisionClient   |  GET /detect/latest         |  localhost:8000  |
  |                 |  <-----------------------  |                  |
  +-----------------+    trả về JSON kết quả     +------------------+
        |
        |  nhận được: "thấy 1 cái hộp, cách 2m"
        v
  [A* tính đường tránh]
```

Đơn giản vậy thôi. VisionClient là **"cái điện thoại"** để C# gọi sang Python.

---

### File có 3 phần. Đọc từng phần:

#### PHẦN 1 — Tờ giấy ghi số điện thoại

```
  +------------------------------------------+
  |  appsettings.json  (danh bạ điện thoại)  |
  |------------------------------------------|
  |  "VisionAi": {                           |
  |      "BaseUrl": "http://localhost:8000"  |  <-- địa chỉ Python AI
  |      "TimeoutMs": 2000                   |  <-- chờ tối đa 2 giây
  |  }                                       |
  +------------------------------------------+

         class VisionAiSettings {
             BaseUrl = ...   <-- khi chương trình chạy,
             TimeoutMs = ... <-- .NET tự đọc file JSON
         }     ^              vào đây cho mình dùng
               |
               settings.Value.BaseUrl
               settings.Value.TimeoutMs
```

---

#### PHẦN 2 — Tờ hợp đồng (Interface)

```
  INTERFACE = tờ hợp đồng, ghi rõ "mày phải làm được 2 việc này"
  
  +------------------------------------+
  |  <<interface>> IVisionClient       |
  |------------------------------------|
  |  + GetLatestDetectionsAsync()      |  "hỏi AI xem thấy gì"
  |  + HealthCheckAsync()              |  "kiểm tra AI còn sống không"
  +------------------------------------+
           ^
           | thực hiện
           |
  +------------------------------------+
  |  class VisionClient                |  <-- đây mới là code thật
  |------------------------------------|
  |  thực sự gọi HTTP đến Python       |
  +------------------------------------+

  Tại sao cần hợp đồng?
  Vì AgvOrchestrator chỉ cần biết "có ai làm được 2 việc đó là được"
  Không cần biết bên trong làm thế nào → dễ thay thế, dễ test
```

---

#### PHẦN 3 — Khi gọi điện (hàm GetLatestDetectionsAsync)

```
  Gọi hàm GetLatestDetectionsAsync():
  
  +-------------------------------------------------+
  |  Gọi HTTP GET /detect/latest                    |
  +-------------------------------------------------+
            |
            |  3 tình huống có thể xảy ra:
            |
     --------+----------+------------------+
     |                  |                  |
     v                  v                  v
  [OK - 200]       [Timeout]         [Không kết nối]
  Nhận JSON        Chờ 2 giây         Python chưa bật
  Deserialize      không thấy         
  → trả về data    → trả về null     → trả về null
                   
  
  Tất cả 3 trường hợp → code bên ngoài không bị crash!
  Đây gọi là "graceful degradation" = "thất bại nhẹ nhàng"
  
  Xe AGV: "À Python không trả lời → thôi bỏ qua lần này, 
            100ms sau hỏi lại"
```

---

### Tóm gọn toàn bộ file vào 1 hình

```
  appsettings.json          VisionClient.cs              Python AI
  ─────────────────         ───────────────              ─────────
  
  "BaseUrl":                  [Khởi tạo]
  "localhost:8000"   ──>   đọc địa chỉ từ settings
                           cắm vào HttpClient
  
  
                            [Mỗi 100ms]
                            gọi GET /detect/latest  ──>  chạy YOLO
                                                    <──  trả JSON
                            chuyển JSON → object C#
                            trả về cho AgvOrchestrator
                            
                            [Nếu lỗi bất kỳ]
                            bắt hết (try/catch)
                            log warning
                            trả về null  (không crash!)
```

---

**Một câu để nhớ:** VisionClient là người đưa tin — chạy sang nhà Python hỏi "mày thấy gì không", mang câu trả lời về, nếu Python không nhà thì về báo "không gặp được" chứ không ngã ra đường chết.

## Cụ thể về Options Pattern (Mô hình Tùy chọn)

### 1. Tạo khuôn

```csharp
public class VisionAiSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutMs { get; set; } = 2000;
}
```

### 2. Đổ thông tin vào khuôn
bên Program.cs có.
```bash
builder.Services.Configure<VisionAiSettings>(builder.Configuration.GetSection("VisionAi"));
```

#### Tại sao lại là GetSection?
```json
{
  "AllowedHosts": "*",

  "VisionAi": {
    "BaseUrl": "http://localhost:8000",
    "TimeoutMs": 2000
  }
}

```
Hãy tưởng tượng `appsettings.json` là một **cái tủ hồ sơ**:

"VisionAi" là một NGĂN KÉO (Section)

Chữ `"VisionAi"` trong file JSON không phải là một giá trị đơn lẻ (như một con số hay một dòng chữ). Nó là một **JSON Object** (chứa dấu ngoặc nhọn `{ }`), bên trong nó lại chứa nhiều thông tin con khác (`BaseUrl`, `TimeoutMs`).

Trong thuật ngữ của .NET, một block chứa nhiều cục thông tin con như vậy được gọi là một **Configuration Section** (Phân vùng cấu hình).
=> Do đó, để lấy *nguyên cả cái ngăn kéo* này ra, sếp bắt buộc phải dùng lệnh **`GetSection("VisionAi")`**.

C# cung cấp vài hàm khác để lấy dữ liệu từ JSON

* **`GetValue<T>("Key")`:** Dùng để lấy một tờ giấy mỏng manh (giá trị đơn lẻ).
Ví dụ, sếp muốn lấy cái `"AllowedHosts"` ở trên cùng, sếp sẽ viết:
`builder.Configuration.GetValue<string>("AllowedHosts");`

* **`Get<T>()`:** Dùng để biến đổi thẳng data thành Object. Thường người ta sẽ kết hợp nó với `GetSection`:
`var config = builder.Configuration.GetSection("VisionAi").Get<VisionAiSettings>();`

#### Tại sao builder.Configuration


Sếp hãy tưởng tượng biến **`builder`** chính là một **Ông Giám Đốc Khởi Nghiệp**.

Khi ông giám đốc này thành lập công ty (chạy app), ổng không tự làm mọi việc mà ổng chia công ty thành **các phòng ban chuyên trách**. 

1. Phòng Tài liệu & Cấu hình: `builder.Configuration`

Nhiệm vụ của phòng này là đi thu thập toàn bộ các thông số cài đặt từ file `appsettings.json`, từ biến môi trường (Environment Variables), từ dòng lệnh... và gom hết vào một cái "tủ hồ sơ".

2. Các "phòng ban" (cái khác) của `builder` làm nhiệm vụ gì?

* **`builder.Services` (Phòng Nhân Sự):** Chuyên môn tuyển dụng và đăng ký nhân viên (Dependency Injection).
Ví dụ: `builder.Services.AddHttpClient(...)` (Tuyển một anh nhân viên tên là VisionClient). Sếp không thể bắt phòng nhân sự đi đọc file cấu hình được.

* **`builder.Environment` (Phòng Ngoại Giao / Môi Trường):**
Chuyên môn kiểm tra xem công ty đang chạy ở môi trường nào (Đang test ở máy sếp, hay đang chạy thật trên server).
Ví dụ: `if (builder.Environment.IsDevelopment()) { ... }`

* **`builder.Logging` (Phòng Giám Sát / Ghi Nhật Ký):**
Chuyên môn ghi chép lại log hệ thống, lỗi lầm, cảnh báo.
Ví dụ: `builder.Logging.AddConsole()` (Cho phép in lỗi ra màn hình đen).

* **`builder.WebHost` (Phòng Hạ Tầng mạng):**
Chuyên môn cấu hình cổng kết nối (Port 5000, 5001) hay server Kestrel.

#### Dấu ()
Dấu `()` bao quanh cái đống đó đơn giản là **cú pháp bắt buộc để truyền đồ vật (tham số) vào cho một hàm (Method) xử lý**.

1. `Configure<VisionAiSettings>` là "Cái Máy Xay"
Đây là tên của một hành động (một hàm). Hành động này mang ý nghĩa: *"Hãy lấy dữ liệu và đổ vào cái khuôn `VisionAiSettings` cho tôi"*.

2. Cặp dấu `( )` là "Cái miệng phễu" của máy xay
Cặp ngoặc tròn này đóng vai trò như **cái miệng phễu** để sếp ném nguyên liệu vào cho máy hoạt động.

**=> Ráp lại toàn bộ quá trình:**
Sếp bê cái ngăn kéo `builder.Configuration.GetSection("VisionAi")` ➡️ Ném vào cái phễu `( )` ➡️ Của cái máy `Configure<VisionAiSettings>`.

#### Tại sao lại là `builder.Services.Configure

Câu hỏi này đi thẳng vào một "đặc sản" của .NET gọi là **Options Pattern** (Mô hình tùy chọn).

Vẫn tiếp tục với câu chuyện **Phòng Nhân Sự (`builder.Services`)** nhé:

 1. Hàm `Configure` làm nhiệm vụ gì đặc biệt?

Khi sếp gọi `builder.Services.Configure<VisionAiSettings>(...)`, sếp đang ra một mệnh lệnh "3 trong 1" cực kỳ phức tạp cho phòng Nhân Sự mà chỉ chữ `Configure` mới làm được:

1. **Tạo khuôn:** "Ê Nhân sự, tạo cho tôi một cái object từ class `VisionAiSettings`."
2. **Đổ data:** "Đọc cái ngăn kéo JSON đưa cho, bóc từng chữ `BaseUrl`, `TimeoutMs` rót vào cái object vừa tạo nhé."
3. **Đóng phong bì (Quan trọng nhất):** "Làm xong thì nhét cái object đó vào một cái phong bì có mác là **`IOptions<VisionAiSettings>`**, dán kín lại rồi cất vào tủ cho tôi!"

 2. Nếu sếp dùng các hàm "Cái Khác"

Các hàm phổ biến nhất của `builder.Services` là `AddSingleton`, `AddScoped`, `AddTransient`.
"""Tôi muốn tất cả mọi nơi đều xài chung 1 cục Data/Connection""",👉 AddSingleton
"""Tôi làm API web, muốn dữ liệu an toàn cho từng User gọi tới""",👉 AddScoped
"""Tôi chỉ cần 1 cái máy tính toán, tính xong vứt luôn cho nhẹ RAM""",👉 AddTransient

#### Tổng kết
Cuối cùng sau khi chạy xong
```
builder.Services.Configure<VisionAiSettings>(builder.Configuration.GetSection("VisionAi"));
```
Thì trong cái tủ hồ sơ của `builder.Services`, nó sẽ có thêm 1 cái khuôn(phong bì) tên là **`IOptions<VisionAiSettings>`** ("Cục Dữ Liệu VisionAi") (đã được điền số liệu từ JSON). Và bất kỳ ai (như class VisionClient) muốn lấy số này, chỉ việc giơ tay xin cái phong bì đó ra là xài được ngay, không cần phải tự đi lục file JSON nữa.

### 3. Xin cái khuôn đó ra để xài

```C#
public VisionClient(IOptions<VisionAiSettings> settings)
{        
  _httpClient.BaseAddress = new Uri(settings.Value.BaseUrl);
  _httpClient.Timeout = TimeSpan.FromMilliseconds(settings.Value.TimeoutMs);
}
```

### 4. Tóm lại

1. Tạo data
```bash
  "VisionAi": {
    "BaseUrl": "http://localhost:8000",
    "TimeoutMs": 2000
  }
```

2. Tạo khuôn
```C#
public class VisionAiSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutMs { get; set; } = 2000;
}
```

3. Đổ data vào khuôn
```C#
builder.Services.Configure<VisionAiSettings>(builder.Configuration.GetSection("VisionAi"));
```

4. Xin cái khuôn đó ra để xài
```C#
public VisionClient(IOptions<VisionAiSettings> settings)
{
    _httpClient.BaseAddress = new Uri(settings.Value.BaseUrl);
    _httpClient.Timeout = TimeSpan.FromMilliseconds(settings.Value.TimeoutMs);
}
```


## Tại sao cần interface IVisionClient

Chào sếp! Sếp hiểu ví dụ về "Tờ hợp đồng" là đã nắm được 90% bản chất của Interface rồi đấy. Để em giải thích sâu hơn tại sao cái tờ hợp đồng `IVisionClient` này lại mang lại 2 "phép thuật" là **Dễ thay thế** và **Dễ test** nhé.

Trong lập trình, đây chính là chữ **D (Dependency Inversion)** trong nguyên lý SOLID mà sếp đã ghi chú ở file `VisionClient.cs`.

---

### 1. Tại sao lại "Dễ thay thế" (Easy to Replace)?

Hãy tưởng tượng sếp viết class `AgvOrchestrator` (bộ não điều phối xe) và **KHÔNG** dùng Interface. Code sẽ trông như thế này:

```csharp
public class AgvOrchestrator 
{
    // Bắt chết (Hardcode) phải dùng đúng anh nhân viên VisionClient này
    private VisionClient _vision = new VisionClient(); 

    public void Run() 
    {
        var data = _vision.GetLatestDetectionsAsync();
        // ... điều khiển xe ...
    }
}

```

**Vấn đề:** Giả sử nửa năm sau, công ty sếp nâng cấp hệ thống. Thay vì gọi HTTP sang Python (FastAPI), sếp muốn đọc dữ liệu trực tiếp từ cổng USB, hoặc gọi qua một giao thức siêu nhanh khác (gRPC).
Lúc này, sếp bắt buộc phải **mở file `AgvOrchestrator.cs` ra, xóa code cũ đi và viết lại code mới**. Việc sửa đi sửa lại bộ não cốt lõi của xe rất dễ gây ra bug ngầm làm xe đâm vào tường.

**Giải pháp với Interface (`IVisionClient`):**

```csharp
public class AgvOrchestrator 
{
    // Tôi không quan tâm anh là ai, miễn anh ký hợp đồng IVisionClient
    private readonly IVisionClient _vision; 

    public AgvOrchestrator(IVisionClient vision) 
    {
        _vision = vision;
    }
}

```

Bây giờ sếp có thể tạo ra N anh nhân viên khác nhau:

1. `class VisionClientHttp : IVisionClient` (Gọi API Python - đang dùng)
2. `class VisionClientUsb : IVisionClient` (Đọc thẳng từ cáp USB)
3. `class VisionClientGrpc : IVisionClient` (Dùng gRPC cho mượt)

Khi muốn đổi công nghệ, sếp **không cần sửa một dòng code nào** trong `AgvOrchestrator`. Sếp chỉ việc ra phòng hành chính (`Program.cs`) chỉ định lại: *"Từ mai, cử anh `VisionClientUsb` mang hợp đồng `IVisionClient` đi làm việc với Orchestrator nhé"*.
=> **Rất an toàn và linh hoạt!**

---

### 2. Tại sao lại "Dễ test" (Easy to Test)?

Trong thực tế, khi sếp muốn kiểm tra (viết Unit Test) xem thuật toán né vật cản (PathPlanner) của `AgvOrchestrator` có hoạt động đúng không, sếp sẽ gặp một rào cản cực lớn:

* Nếu dùng `VisionClient` thật, sếp **bắt buộc phải bật server Python lên**, phải bật camera lên, rồi đưa tay ra trước camera để tạo vật cản. Việc test tự động bằng code trở nên bất thi!

**Giải pháp với Interface (Mocking/Faking):**

Nhờ có tờ hợp đồng `IVisionClient`, trong lúc chạy Test, sếp có thể thuê một "diễn viên đóng thế" (Mock) giả vờ làm AI:

```csharp
// Một class đóng thế, chỉ dùng để test, không bao giờ dùng thật
public class FakeVisionClient : IVisionClient 
{
 //... nội dung ...
}

```
Lúc này, sếp nhét anh `FakeVisionClient` này vào cho `AgvOrchestrator` chạy thử.
`AgvOrchestrator` không hề biết đây là đồ giả (vì anh này vẫn tuân thủ đúng hợp đồng `IVisionClient`). Nhờ vậy, sếp có thể tự động test hàng ngàn kịch bản (có vật cản, không có vật cản, vật cản bên trái, vật cản bên phải...) trong chớp mắt mà **không cần bật Python, không cần bật Camera**.

---

### Tóm tắt lại bằng hình tượng:

* **AgvOrchestrator (Giám đốc):** Chỉ cần biết đưa ra yêu cầu *"Báo cáo cho tôi xem phía trước có gì"*.
* **IVisionClient (Tờ hợp đồng/Bản mô tả công việc):** Ghi rõ yêu cầu của Giám đốc.
* **VisionClient (Nhân viên thật):** Chạy tụt quần gửi request HTTP sang Python để lấy data.
* **FakeVisionClient (Nhân viên giả/Diễn viên):** Báo cáo láo (data giả) để Giám đốc diễn tập phòng cháy chữa cháy (Testing).

Nhờ có tờ hợp đồng ở giữa, Giám đốc không bao giờ bị phụ thuộc vào một cá nhân nhân viên nào cả! Đổi người, hay cho người giả vào diễn tập đều trơn tru mượt mà sếp ạ.

### Cụ thể code khi đổi cách kết nối sang VisionClientUsb

tôi sẽ mở file `Program.cs` ra, tìm đến phần 
```C#
// Comment dòng cũ:
// builder.Services.AddHttpClient<IVisionClient, VisionClient>();

// Thêm dòng mới:
builder.Services.AddSingleton<IVisionClient, VisionClientUsb>();

// ... các cấu hình khác giữ nguyên ...
```

tạo file `VisionClientUsb.cs` trong thư mục `Services`
```C#
public class VisionClientUsb : IVisionClient
{
    //....nội dung....
}
```

Note: không cần tạo lại tờ hợp đồng IVisionClient


### Vi diệu của đa hình 

1. Tờ hợp đồng (Interface) CHỈ QUY ĐỊNH "KẾT QUẢ", KHÔNG QUY ĐỊNH "CÁCH LÀM"

Tờ hợp đồng `IVisionClient` mà sếp viết ra nó chỉ nói thế này:

* *"Bất kể anh là ai, dùng công nghệ gì, khi tôi gọi hàm `GetLatestDetectionsAsync()`, anh **bắt buộc phải trả về cho tôi 1 cục data kiểu `VisionResponse**`."*
* *"Khi tôi gọi hàm `HealthCheckAsync()`, anh **bắt buộc phải trả về chữ `true` hoặc `false**`."*

2. Mỗi anh nhân viên (Class) sẽ TỰ VIẾT CÁCH LÀM riêng của mình
Cùng mang tên một hàm, nhưng ruột bên trong khác nhau một trời một vực:

**Anh thứ 1: `VisionClientHttp` (Nhân viên chạy đôn chạy đáo ngoài đường)**

```csharp
public Task<VisionResponse?> GetLatestDetectionsAsync()
{
    // Cách làm: 
    // 1. Kết nối Wifi / LAN
    // 2. Gửi request đến http://localhost:8000
    // 3. Đọc chuỗi JSON trả về
    // 4. Dịch JSON thành object VisionResponse rồi trả kết quả cho sếp
}

```

**Anh thứ 2: `VisionClientUsb` (Nhân viên ngồi nhà cắm cáp)**

```csharp
public Task<VisionResponse?> GetLatestDetectionsAsync()
{
    // Cách làm:
    // 1. Mở cổng COM3 của máy tính
    // 2. Đọc tín hiệu điện tử từ cáp USB
    // 3. Phân tích tín hiệu thành hình ảnh
    // 4. Nhét vào object VisionResponse rồi trả kết quả cho sếp
}

```

3. Tại sao bộ não `AgvOrchestrator` lại thích điều này?

Ông giám đốc `AgvOrchestrator` là một người siêu lười quan tâm đến tiểu tiết.

Ông ấy không thèm biết (và cũng không cần biết) anh nhân viên đang cắm cáp USB hay đang bắt Wifi. Ông ấy chỉ cầm tờ hợp đồng `IVisionClient` lên, gọi tên nhiệm vụ `GetLatestDetectionsAsync()`, và ngửa tay ra đợi: *"Đưa cục `VisionResponse` đây cho tao để tao còn tính toán bẻ lái né vật cản!"*.

## Tại sao cần ? và async

```C#
public Task<VisionResponse?> GetLatestDetectionsAsync()
```
1. Tại sao lại có dấu `?` (Nullable) trong `VisionResponse?`

Dấu `?` mang ý nghĩa: **"Hàm này có thể trả về một object `VisionResponse` đàng hoàng, NHƯNG cũng có quyền trả về `null` (không có gì cả)".**

**Tại sao lại cần nó ở đây?**
Vì việc gọi dữ liệu qua mạng (HTTP) sang server Python là một hành động đầy rủi ro.

* Lỡ server Python bị sập thì sao?
* Lỡ đứt cáp mạng thì sao?
* Lỡ Python xử lý quá lâu dẫn đến Timeout thì sao?

2. Tại sao lại cần `Task` và `Async`?

`Task` và đuôi `Async` là đại diện cho **Lập trình Bất đồng bộ (Asynchronous Programming)**.

**Chuyện gì xảy ra nếu KHÔNG dùng Async (chạy Đồng bộ - Synchronous)?**
Khi C# gọi API sang Python, nó mất khoảng 45-60ms để YOLO phân tích ảnh xong. 
Nếu chạy đồng bộ, cái luồng (thread) điều khiển xe AGV sẽ bị **đóng băng (block)** hoàn toàn trong 60ms đó để chờ đợi.
Trong 60ms bị đóng băng đó, C# không thể đọc tín hiệu Modbus, không thể kiểm tra pin, không thể gửi lệnh phanh. Con xe sẽ chạy mù hoàn toàn!

## Hai kiểu dữ liệu HttpClient và ILogger<T>

Hai kiểu dữ liệu `HttpClient` và `ILogger<T>` này nó là do chính Microsoft đúc sẵn và tặng kèm khi sếp cài đặt .NET.

### 1. `HttpClient` (Chiếc xe máy giao hàng)

* **Lấy ở đâu ra?** Nó nằm trong gói thư viện chuẩn của .NET có tên là `System.Net.Http`.
* **Công dụng:** Đây là công cụ chuyên dụng để gọi mạng (gửi HTTP Request đi và nhận Response về).
* **Ai phát cho anh nhân viên `VisionClient`?**
Sếp nhớ dòng code này ở file `Program.cs` chứ?
```csharp
builder.Services.AddHttpClient<IVisionClient, VisionClient>();
```
Lệnh `AddHttpClient` này mang ý nghĩa: *"Phòng nhân sự hễ tuyển anh VisionClient vào làm, thì nhớ **phát kèm cho anh ấy một chiếc xe máy `HttpClient**` để anh ấy chạy ra đường mạng nhé!"*.
Nhờ lệnh này, lúc anh `VisionClient` khởi tạo, hệ thống (DI) tự động dúi vào tay anh ấy cái `HttpClient` đã được bơm đầy xăng (đã config sẵn `BaseUrl` và `Timeout`).
Lúc này, tất cả chỉ nằm trên giấy tờ (bản thiết kế).

### 2. `ILogger<T>` (Cuốn sổ ghi chép nhật ký)

* **Lấy ở đâu ra?** Nó nằm trong thư viện `Microsoft.Extensions.Logging`. Chữ `<T>` là viết tắt của Type (Kiểu/Tên class), ở đây là `ILogger<VisionClient>`.
* **Công dụng:** Dùng để ghi lại các dòng thông báo (Log) ra màn hình console đen ngòm, hoặc ghi ra file để sếp theo dõi (ví dụ: *"Đang gọi API...", "Lỗi mất mạng rồi sếp ơi..."*).
* **Ai phát cho anh nhân viên?**
Cái này còn vi diệu hơn `HttpClient`. Sếp không hề thấy dòng nào đăng ký `ILogger` trong `Program.cs` cả!
Đó là vì ngay ở dòng đầu tiên `var builder = WebApplication.CreateBuilder(args);`, ông giám đốc đã **mặc định mua sẵn hàng ngàn cuốn sổ nhật ký** cho công ty rồi.
Bất kỳ anh nhân viên nào (class nào) trong C# khi đi làm, chỉ cần thò tay ra xin ở hàm tạo: *"Cho tôi 1 cuốn sổ ghi tên tôi nhé `ILogger<VisionClient>`"*, là hệ thống sẽ tự động in tên anh ta lên bìa sổ và phát cho anh ta xài luôn.

Chính xác **100%**!

Trong đoạn code:
`public VisionClient(HttpClient httpClient, IOptions<VisionAiSettings> settings, ILogger<VisionClient> logger)`

Thì cái biến **`httpClient`** (viết thường) nằm trong ngoặc chính là **vật phẩm thực tế (Instance)** mà ông Thủ kho (DI Container) đã nhào nặn ra và dúi vào tay anh thám tử `VisionClient`.

Tuy nhiên, riêng với thằng `HttpClient` này, trong C# .NET có một phép thuật "VIP" hơn việc dùng lệnh `new` bình thường. Tiện đây em giải thích luôn độ xịn trong code của sếp:

## Sự vi diệu của `AddHttpClient`

Nếu sếp tự viết code chạy bằng tay kiểu: `var httpClient = new HttpClient();`, thì mỗi lần gọi, máy tính lại tạo ra một kết nối mạng mới. Nếu xe AGV quét 100ms/lần, sếp sẽ mở hàng ngàn kết nối. Dù sếp có vứt cái điện thoại đi, nhà mạng (Hệ điều hành) vẫn sẽ "giam" cái cổng kết nối đó một thời gian (gọi là lỗi cạn kiệt cổng mạng - *Socket Exhaustion*).

Nhưng hãy nhìn vào file `Program.cs` của sếp, sếp đã đăng ký bằng lệnh này:
`builder.Services.AddHttpClient<IVisionClient, VisionClient>();`

Khi sếp dùng lệnh này, ông Thủ kho sẽ **KHÔNG** làm lệnh `new HttpClient()` ngu ngốc nữa. Thay vào đó, ổng làm như sau:

1. Thủ kho gọi một **Nhà máy chuyên lắp ráp điện thoại** (gọi là `IHttpClientFactory`).
2. Nhà máy này cực kỳ thông minh, nó biết cách tái sử dụng các "đường dây mạng" (Handler) bên dưới để không bao giờ bị nghẽn mạng dù sếp có gọi Python AI 10 lần/giây.
3. Sau khi nhà máy cấu hình xong xuôi một cái `HttpClient` xịn xò và an toàn, nó mới giao cho Thủ kho.
4. Cuối cùng, Thủ kho cầm cái `HttpClient` xịn đó, nhét thẳng vào vị trí biến `httpClient` trong Constructor của `VisionClient`.

**Chốt lại:** Tư duy của sếp là hoàn toàn chính xác. Cái `httpClient` mà `VisionClient` nhận được chính là sản phẩm do Thủ kho mang tới. Việc Thủ kho dùng lệnh `new` hay dùng "Nhà máy" để tạo ra nó là việc nội bộ của Thủ kho, anh thám tử `VisionClient` cứ ngửa tay nhận đồ rồi xài (`_httpClient = httpClient`) là xong!

## _httpClient = httpClient
Tại sao người ta dâng tận tay cái `httpClient` rồi mà không xài luôn, lại còn phải vẽ trò cất vào cái kho riêng `_httpClient = httpClient`?

Lý do bắt nguồn từ một luật thép trong lập trình gọi là **"Phạm vi sống của biến" (Variable Scope)**.

Bi kịch "Nhận hàng ở cửa nhưng quên mang vào nhà"

Cái biến `httpClient` nằm trong ngoặc tròn của hàm tạo (Constructor) nó chỉ là một **Biến cục bộ (Local variable)**. Vòng đời của nó siêu ngắn, **chỉ sống đúng trong lúc cái hàm tạo đó chạy**.

Giả sử sếp **KHÔNG** gán `_httpClient = httpClient`, code sẽ thế này:

```csharp
public class VisionClient 
{
    // HÀM TẠO (Lúc mới tuyển dụng)
    public VisionClient(HttpClient httpClient) 
    {
        // 1. Thủ kho giao cho anh thám tử cái điện thoại (httpClient).
        // 2. Anh thám tử nhận lấy, cầm trên tay gật gù: "Điện thoại xịn đấy".
    } 
    // <--- ĐẾN DẤU NGOẶC NÀY, HÀM TẠO KẾT THÚC!
    // Hệ điều hành thu hồi bộ nhớ, cái biến 'httpClient' bốc hơi luôn tại cửa!


    // HÀM THỰC THI (Lúc đi làm việc thật)
    public async Task GetLatestDetectionsAsync()
    {
        // 100ms sau, sếp bảo: "Lấy điện thoại ra gọi Python đi!"
        // Anh thám tử sờ tay vào túi: 
        var response = await httpClient.GetAsync(...); // ❌ LỖI ĐỎ LÒM (Lỗi biên dịch)!!!
        
        // C# gào lên: "httpClient là cái quái gì? Tôi không biết nó! Anh cất nó ở đâu rồi?"
    }
}

```

## IOptions<VisionAiSettings> settings

```csharp
public VisionClient(HttpClient httpClient,
                    IOptions<VisionAiSettings> settings, // <--- 1. Nhận phong bì
                    ILogger<VisionClient> logger)
{
    // ...
    
    // 2. Mở phong bì (settings.Value) và lấy dòng địa chỉ (BaseUrl)
    _httpClient.BaseAddress = new Uri(settings.Value.BaseUrl); 
    
    // 3. Lấy tiếp thời gian chờ (TimeoutMs)
    _httpClient.Timeout = TimeSpan.FromMilliseconds(settings.Value.TimeoutMs);
}

```

**Tóm tắt lại luồng đi của dữ liệu này (gọi là Options Pattern):**

1. **Từ File JSON:** Trong `appsettings.json` ghi là `"BaseUrl": "http://localhost:8000"`.
2. **Đóng phong bì:** Khởi động app (`Program.cs`), DI đọc file JSON, nhét số liệu vào 1 object `VisionAiSettings`, rồi gói object đó vào cái vỏ `IOptions<>`.
3. **Phát phong bì:** Truyền vào constructor của `VisionClient` dưới tên biến `settings`.
4. **Mở phong bì:** Code của bạn lôi giá trị ra xài bằng lệnh `settings.Value.BaseUrl` (lúc này nó mang giá trị đúng bằng `"http://localhost:8000"`).

## TimeSpan.FromMilliseconds

`TimeSpan.FromMilliseconds` là cách tạo ra một **khoảng thời gian** từ số milliseconds.

```csharp
TimeSpan.FromMilliseconds(2000)  // = 2 giây
TimeSpan.FromMilliseconds(100)   // = 0.1 giây
```

```csharp
_httpClient.Timeout = TimeSpan.FromMilliseconds(settings.Value.TimeoutMs);
// → _httpClient.Timeout = TimeSpan.FromMilliseconds(2000)
// → Nếu Vision AI không trả lời trong 2 giây → timeout
```

**Tại sao không viết thẳng `2000` mà phải dùng `TimeSpan`?**

Vì `HttpClient.Timeout` yêu cầu kiểu `TimeSpan`, không nhận số nguyên thô. Microsoft thiết kế vậy để tránh nhầm lẫn — `2000` là 2000 giây hay 2000 milliseconds?

Các cách tạo `TimeSpan` thường dùng:

```csharp
TimeSpan.FromMilliseconds(100)  // 100ms
TimeSpan.FromSeconds(5)         // 5 giây
TimeSpan.FromMinutes(1)         // 1 phút
```

## var json = await response.Content.ReadAsStringAsync()

**Chuẩn không cần chỉnh!** Bạn đã hiểu đúng hoàn toàn bản chất cốt lõi của lập trình bất đồng bộ (Asynchronous Programming) trong C# rồi đấy!

### 1. Hành động giao việc

Khi dòng code chạy đến `response.Content.ReadAsStringAsync()`, ứng dụng thực chất đang giao việc thao tác I/O (Input/Output - đọc dữ liệu từ card mạng/bộ nhớ) cho phần cứng hoặc hệ điều hành (chính là "con robot"). Việc đọc một file lớn hoặc đọc dữ liệu qua mạng thường rất chậm so với tốc độ xử lý của CPU.

### 2. Sức mạnh của chữ `await`

Nếu không có chữ `await` (chạy đồng bộ - Synchronous), "thằng chủ thớt" (ở đây gọi là *Worker Thread*) sẽ **đứng chôn chân (block)** ngay tại dòng code đó. Nó không làm gì cả, chỉ khoanh tay đứng nhìn con robot nhặt từng byte dữ liệu cho đến khi xong. Trong lúc nó đứng chơi, nếu có request khác bay vào (ví dụ ai đó gọi API `GET /agv/status`), server sẽ phải luống cuống đi gọi một nhân viên (Thread) khác ra tiếp khách, gây tốn tài nguyên RAM và CPU.

Nhưng nhờ có chữ **`await`**, kịch bản thay đổi hoàn toàn:

* "Thằng chủ thớt" vỗ vai con robot: *"Mày cứ ở đây đọc dữ liệu cho xong đi nhé, tao ra ngoài sảnh tiếp khách (nhận Request khác) đây, không rảnh đứng đợi mày đâu!"*.
* Lúc này, Thread đó được giải phóng (trả về Thread Pool) và **hoàn toàn rảnh rỗi để đi phục vụ các HTTP Request khác**. Server của bạn hoạt động cực kỳ mượt mà, không bị nghẽn (non-blocking).

### 3. Sự quay lại (Callback)

Khi "con robot" đọc xong toàn bộ chuỗi JSON, nó sẽ bấm chuông báo hiệu: *"Tôi làm xong rồi!"*.
Lúc này, hệ thống sẽ tự động gọi một "thằng chủ thớt" đang rảnh rỗi bước vào (không nhất thiết phải là cái anh lúc nãy, ai rảnh thì vào), cầm lấy cái biến `json` đó và chạy tiếp các dòng code bên dưới.

### Liên hệ thực tế vào dự án AGV của bạn:

Trong class `VisionClient`, xe AGV gọi điện sang server Python AI để hỏi xem có vật cản không. YOLOv11 xử lý mất khoảng 45-60ms.

* Nếu không có `await`, luồng điều khiển của C# sẽ bị "đóng băng" hoàn toàn trong 60ms. Trong 60ms đó, nếu xe cần kiểm tra pin, đọc tọa độ bánh xe, hay nhận lệnh phanh khẩn cấp, nó sẽ **bị mù và điếc** hoàn toàn!
* Nhờ có `await`, trong 60ms đợi Python nặn ra kết quả JSON, CPU của C# vẫn thảnh thơi đi làm hàng tá công việc khác để giữ cho xe AGV luôn trong trạng thái kiểm soát an toàn.

## var response = await _httpClient.GetAsync("/detect/latest");


Lệnh `GetAsync("/detect/latest")` là một hành động **chủ động đi đòi nợ**!

Hình ảnh "con robot" ở dòng code này thực chất là một **anh Shipper** do Hệ điều hành / Card mạng quản lý. Hành trình thực tế diễn ra như sau:

1. Khi dòng code chạy, anh Shipper (robot) này nổ máy xe, phóng thẳng sang "nhà" của thằng Python AI ở địa chỉ `http://localhost:8000/detect/latest`.
2. Tới nơi, anh Shipper gõ cửa và đòi: *"Ê Python, tao được sếp C# phái sang. Mày vừa chụp được cái ảnh nào, phân tích YOLO nhanh lên rồi đưa kết quả cho tao mang về!"*.
3. Lúc này, **anh Shipper (robot) sẽ đứng chờ tại cửa nhà Python** trong khoảng 45-60ms để đợi thằng Python chạy xong model YOLOv11s và xuất ra file JSON.
4. Ngay khi thằng Python ném ra cục JSON `{"detections": [{"class":"box"}]}`, anh Shipper chộp lấy, phóng xe về báo cáo.
5. Về đến nơi, anh Shipper bấm chuông báo thức: *"Xong việc rồi!"*. Lúc này, một "thằng chủ thớt" bất kỳ trong Thread Pool sẽ bước ra, cầm lấy cục JSON (biến `response`) và chạy tiếp các dòng code bên dưới.


## var result = JsonSerializer.Deserialize<VisionResponse>(json, _jsonOptions);

Câu lệnh này là **"trái tim" của việc giao tiếp giữa C# và Python** trong dự án của sếp.

Để dễ hiểu, sếp cứ tưởng tượng hành động này giống như việc **"Dịch thuật và Đổ khuôn"**.

Hãy mổ xẻ từng thành phần trong câu lệnh:
`var result = JsonSerializer.Deserialize<VisionResponse>(json, _jsonOptions);`

### 1. Vấn đề: C# và Python nói hai ngôn ngữ khác nhau

* Sau khi anh Shipper (`GetAsync`) mang dữ liệu từ Python về, cái biến `json` lúc này chỉ là một **chuỗi văn bản (String) vô tri vô giác** trông như thế này:
`'{"detections": [{"object_class": "box"}], "processing_time_ms": 45}'`
* C# là một ngôn ngữ "kiểu tĩnh" (strongly-typed). Nó **không thể** hiểu và không cho phép sếp viết code kiểu: `in ra cái json["processing_time_ms"]`. Nó nhìn chuỗi text trên như một đám giun dế.

### 2. Giải pháp: Máy dịch thuật `JsonSerializer.Deserialize`

* **`JsonSerializer.Deserialize`**: Đây là cái "Máy dịch thuật" được Microsoft chế tạo sẵn. Nhiệm vụ của nó là đọc chuỗi văn bản giun dế kia và nặn ra thành một **Đồ vật thật (Object)** trong C#.
* **`<VisionResponse>`**: Đây là **"Cái Khuôn"** mà sếp đã định nghĩa bên file `Models/DetectionResult.cs`. Sếp đang ra lệnh cho cái máy: *"Ê máy, hãy lấy đống đất sét `json` kia, ép nó vào cái khuôn `VisionResponse` cho tao!"*.

### 3. Tham số phụ trợ: `_jsonOptions` (Bí kíp dịch thuật)

Python có thói quen viết chữ thường cách nhau bằng dấu gạch dưới (`snake_case`: `processing_time_ms`), còn C# lại thích viết hoa chữ cái đầu (`PascalCase`: `ProcessingTimeMs`).
Nếu máy dịch cứng nhắc, nó sẽ bảo: *"Tôi không tìm thấy chữ `ProcessingTimeMs` nào trong cục JSON cả"*.

* Việc sếp truyền `_jsonOptions` (có chứa `PropertyNameCaseInsensitive = true`) cộng với các thẻ `[JsonPropertyName("...")]` ở file Model chính là đưa cho cái máy một quyển bí kíp: *"Cứ phiên dịch thoáng ra nhé, thấy `processing_time_ms` của Python thì tự động nhét nó vào biến `ProcessingTimeMs` của C# cho tao"*.

### 4. Kết quả: `var result`

Sau khi máy "đổ khuôn" thành công, biến `result` ra đời. Nó không còn là chuỗi text mờ nhạt nữa, mà đã trở thành một **Object C# chính hiệu**.

Lúc này, ông sếp `AgvOrchestrator` có thể thoải mái và tự tin gọi:

* `result.TotalObjects` (Để biết có bao nhiêu vật cản)
* `result.ProcessingTimeMs` (Để biết AI chạy mất bao lâu)
* `result.Detections[0].DistanceMeters` (Để lấy khoảng cách bẻ lái)

## result?.TotalObjects, result?.ProcessingTimeMs

`result?` là **null-conditional operator** — tức là:

> "Nếu `result` là `null` thì **đừng có chạy tiếp**, trả về `null` luôn. Nếu không null thì mới lấy property."

---

```csharp
// Không có ?
result.TotalObjects    // ❌ NullReferenceException nếu result = null

// Có ?
result?.TotalObjects   // ✅ Trả về null nếu result = null, không crash
```

---

Tại sao `result` có thể null? Nhìn lại flow:

```csharp
var json = await response.Content.ReadAsStringAsync();
var result = JsonSerializer.Deserialize<VisionResponse>(json, _jsonOptions);
```

Nếu Python trả về JSON lỗi, không đúng format → `Deserialize` trả về `null` → `result = null` → nếu không có `?` thì crash ngay.

---

Nên `result?` là cách phòng thủ:

```csharp
_logger.LogInformation("Detected {Count} objects in {Time}ms",
    result?.TotalObjects,     // null nếu result null → logger in "null"
    result?.ProcessingTimeMs  // null nếu result null → logger in "null"
);
// Không crash, chỉ log null thôi
```





