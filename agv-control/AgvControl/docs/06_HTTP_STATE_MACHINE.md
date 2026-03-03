# State machine

```
==========================================================================
  LUỒNG DI + HttpClient  —  từ lúc app bật đến lúc xe chạy thật
==========================================================================

  THỜI ĐIỂM           TRẠNG THÁI            SỰ KIỆN
  ──────────────────────────────────────────────────────────────────────

  [Program.cs chạy — app chưa Run]
                       +------------------+
                       |   CHỈ LÀ GIẤY TỜ |
                       +------------------+
    ├─ đọc appsettings.json    |  chưa có gì thật
    ├─ Configure<Settings>()   |  HttpClient?    KHÔNG CÓ
    └─ AddHttpClient<          |  VisionClient?  KHÔNG CÓ
         IVisionClient,        |  AgvOrchest...? KHÔNG CÓ
         VisionClient>()       |
                               |  ← chỉ là công thức
                               |    "khi nào cần thì làm"

  ══════════════ app.Run() được gọi ══════════════
                               |
                               ▼
                       +------------------+
                       |   DI KÍCH HOẠT   |  ← KHÔNG CẦN request!
                       +------------------+
  .NET phát hiện:              |
  AgvOrchestrator              |
  là BackgroundService!        |
    │                          |
    └─ "phải khởi động ngay,   |
        không cần chờ khách"   |
         │                     |
         └─ AgvOrch cần        |
            IVisionClient      |
              │                |
              ├─ IHttpClientFactory.CreateClient()
              │    └─► HttpClient THẬT được tạo  ✓
              │
              ├─ lấy IOptions<VisionAiSettings> từ kho
              │    └─► settings THẬT được lấy ra ✓
              │
              └─ lấy ILogger từ kho
                   └─► logger THẬT được lấy ra   ✓

                       +------------------+
                       | CONSTRUCTOR CHẠY |
                       +------------------+
  new VisionClient(            |
    httpClient,   ─────────────┤ 3 thứ thật
    settings,     ─────────────┤ được nhét vào
    logger        ─────────────┘
  )
    │
    ├─ _httpClient.BaseAddress = settings.Value.BaseUrl
    │                            └─► "http://localhost:8000"   ⛽
    └─ _httpClient.Timeout     = settings.Value.TimeoutMs
                                 └─► 2000ms                    ⛽

                       +------------------+
                       |  ĐANG CHẠY THẬT  |
                       +------------------+
  mỗi 100ms:                   |
    AgvOrchestrator             |
      └─ await _visionClient    |
           .GetLatestDetect()   |
              │                 |
              ├─[có data]──────►│ trả VisionResponse về
              │                 │ → tính đường né vật cản
              │                 │
              └─[lỗi/timeout]──►│ trả null
                                │ → bỏ qua, 100ms sau hỏi lại

==========================================================================
  TÓM GỌN  (đã sửa)
  ------------------
  Program.cs chạy  : viết công thức        — chưa có gì thật
  app.Run()        : .NET thấy Background
                     Service → kích hoạt
                     DI NGAY LẬP TỨC      — tạo đồ thật, không chờ
  constructor      : nhét đồ thật vào     — nạp xăng
  vòng lặp         : chạy mãi mãi 100ms
==========================================================================
```

---

