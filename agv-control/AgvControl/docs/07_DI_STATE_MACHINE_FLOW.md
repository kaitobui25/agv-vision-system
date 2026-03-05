# Dependency Injection trong .NET 🎮

## Hãy tưởng tượng bạn đang chơi game RPG...

Bạn có một **nhân vật chiến binh** 🗡️. Nhân vật cần một **vũ khí** để chiến đấu.

---

## ❌ Cách làm "tệ" — tự tạo vũ khí trong nhân vật

```csharp
class ChienBinh
{
    private Kiem vuKhi; // Cứng nhắc, chỉ dùng được kiếm!

    public ChienBinh()
    {
        vuKhi = new Kiem(); // Tự tạo ra kiếm bên trong
    }

    public void TanCong()
    {
        vuKhi.Chém();
    }
}
```

**Vấn đề:** Chiến binh bị "dính chặt" với kiếm. Muốn dùng cung tên? Phải sửa toàn bộ class! 😫

---

## ✅ Cách làm "hay" — ai đó đưa vũ khí từ bên ngoài vào

```csharp
// Tạo "hợp đồng" — vũ khí nào cũng phải có khả năng tấn công
interface IVuKhi
{
    void TanCong();
}

// Các loại vũ khí khác nhau
class Kiem : IVuKhi
{
    public void TanCong() => Console.WriteLine("Chém bằng kiếm! ⚔️");
}

class CungTen : IVuKhi
{
    public void TanCong() => Console.WriteLine("Bắn tên! 🏹");
}

// Chiến binh KHÔNG tự tạo vũ khí — nhận từ bên ngoài
class ChienBinh
{
    private IVuKhi _vuKhi;

    // Vũ khí được "tiêm" vào qua constructor
    public ChienBinh(IVuKhi vuKhi)
    {
        _vuKhi = vuKhi; // Nhận vũ khí từ bên ngoài 🎁
    }

    public void TanCong()
    {
        _vuKhi.TanCong();
    }
}
```

---

## 🏭 Trong .NET — có "kho vũ khí" tự động (IoC Container)

```csharp
// Program.cs — Đăng ký vào "kho"
var builder = WebApplication.CreateBuilder(args);

// Nói với .NET: "Khi ai cần IVuKhi, hãy đưa cho họ cái Kiem"
builder.Services.AddScoped<IVuKhi, Kiem>();
builder.Services.AddScoped<ChienBinh>();

var app = builder.Build();
```

```csharp
// .NET tự động "tiêm" Kiem vào ChienBinh — bạn không cần làm gì!
app.MapGet("/chien-dau", (ChienBinh cb) =>
{
    cb.TanCong(); // "Chém bằng kiếm! ⚔️"
    return "Thắng rồi!";
});
```

---

## 3 "vòng đời" của service cần biết

| Loại | Ý nghĩa | Ví dụ |
|---|---|---|
| `AddSingleton` | Tạo **1 lần duy nhất**, dùng mãi mãi | Cấu hình game |
| `AddScoped` | Tạo **1 lần mỗi request** | Giỏ hàng, session |
| `AddTransient` | Tạo **mới mỗi lần dùng** | Gửi email |

---

## 🧠 Tóm lại siêu đơn giản

> **DI = Đừng tự đi lấy đồ, hãy để người khác mang đến cho bạn**

- **Không có DI** → Chiến binh tự rèn kiếm (khó thay đổi 😓)
- **Có DI** → Có người mang vũ khí đến tận tay (linh hoạt, dễ test ✅)

## DEPENDENCY INJECTION - STATE MACHINE FLOW
==========================================


  ┌─────────────────────────────────────────────────────────────────┐
  │                    🏁  STARTUP / BOOT                           │
  └───────────────────────────┬─────────────────────────────────────┘
                              │
                              ▼
  ╔═════════════════════════════════════════════════════════════════╗
  ║              📦  IoC CONTAINER (Kho vũ khí)                    ║
  ║                                                                 ║
  ║   builder.Services.AddScoped<IVuKhi, Kiem>()                   ║
  ║   builder.Services.AddScoped<ChienBinh>()                      ║
  ║                                                                 ║
  ║   +------------------+    +------------------+                 ║
  ║   |  IVuKhi -------> |    |  ChienBinh       |                 ║
  ║   |    └── Kiem      |    |  └── (chờ inject)|                 ║
  ║   +------------------+    +------------------+                 ║
  ╚═══════════════════════════════╤═════════════════════════════════╝
                                  │  app.Build()
                                  ▼
  +~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~+
  ~                   🌐  HTTP REQUEST ĐẾN                         ~
  ~                   GET /chien-dau                                ~
  +~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~+
                                  │
                                  ▼
  ╔═════════════════════════════════════════════════════════════════╗
  ║            🔍  CONTAINER RESOLVES DEPENDENCIES                  ║
  ╚═══════════╤════════════════════════════════╤════════════════════╝
              │                                │
              ▼                                ▼
  +-----------+----------+       +-------------+-----------+
  |   Tìm: IVuKhi        |       |   Tìm: ChienBinh        |
  |   Đăng ký: Kiem      |       |   Constructor cần:      |
  |                      |       |   └── IVuKhi ✅ (có rồi)|
  +----------+-----------+       +-------------+-----------+
             │                                 │
             │   new Kiem()                    │
             ▼                                 ▼
  +==========+===========+       +=============+===========+
  ‖   INSTANCE: Kiem     ‖       ‖  INSTANCE: ChienBinh   ‖
  ‖   ──────────────     ‖  ──►  ‖  _vuKhi = [Kiem] 💉    ‖
  ‖   void TanCong()     ‖       ‖  void TanCong()         ‖
  +======================+       +========================= +
                                              │
                                              │  inject xong ✅
                                              ▼
  .-  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -.
  '            🎮  EXECUTE: cb.TanCong()                    '
  '                                                          '
  '   ChienBinh                                              '
  '       │                                                  '
  '       └──► _vuKhi.TanCong()                             '
  '                  │                                       '
  '                  └──► Kiem.TanCong()                    '
  '                            │                             '
  '                            └──► "Chém bằng kiếm! ⚔️"   '
  '-  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -'
                                              │
                                              ▼
  ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
  +                  📤  RESPONSE: "Thắng rồi!"                  +
  ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
                                              │
                              ┌───────────────┘
                              │  Scope kết thúc
                              ▼
  ############################################################
  #              🗑️  SCOPE DISPOSED (AddScoped)              #
  #                                                          #
  #   Kiem instance       ──────────────────► ❌ Destroyed   #
  #   ChienBinh instance  ──────────────────► ❌ Destroyed   #
  #                                                          #
  #   (Singleton sẽ KHÔNG bị destroy ở bước này)            #
  ############################################################


==================================================================
             VÒNG ĐỜI SO SÁNH (LIFETIME STATES)
==================================================================

  SINGLETON
  |
  ●══════════════════════════════════════════════════════════► (sống mãi)
  App Start                                               App Stop


  SCOPED
  |
  ●═════════●        ●═════════●        ●═════════●
  Req#1 Start  End   Req#2 Start  End   Req#3 Start  End


  TRANSIENT
  |
  ●══●  ●══●  ●══●  ●══●  ●══●  ●══●  ●══●  ●══●  ●══●
  Mỗi lần gọi = 1 instance mới, dùng xong bỏ ngay


==================================================================
             ĐỔI VŨ KHÍ — KHÔNG SỬA ChienBinh
==================================================================

  Trước:                         Sau:
  +-----------------------+      +-----------------------+
  | AddScoped<            |      | AddScoped<            |
  |   IVuKhi,             |  ──► |   IVuKhi,             |
  |   Kiem>()             |      |   CungTen>()          |
  +-----------------------+      +-----------------------+
         │                              │
         ▼                              ▼
  [ChienBinh + Kiem ⚔️]         [ChienBinh + CungTen 🏹]

         Không đụng vào ChienBinh dù 1 dòng code! ✅

Để giải thích dễ hiểu khái niệm "Container Resolves Dependencies" (Container giải quyết các phụ thuộc) trong lập trình, mình sẽ dùng hình ảnh ẩn dụ về một **Nhà kho vũ khí** và một **Sự kiện xuất quân**. Đây là flow của Dependency Injection (DI) theo kiểu State Machine (máy trạng thái).

Dưới đây là sơ đồ và giải thích chi tiết:

```text
==================================================================
        DEPENDENCY INJECTION - STATE MACHINE FLOW
==================================================================


  ┌─────────────────────────────────────────────────────────────────┐
  │                    🏁  STARTUP / BOOT                           │
  └───────────────────────────┬─────────────────────────────────────┘
                              │
                              ▼
  ╔═════════════════════════════════════════════════════════════════╗
  ║              📦  IoC CONTAINER (Kho vũ khí)                     ║
  ║                                                                 ║
  ║   builder.Services.AddScoped<IVuKhi, Kiem>()                    ║
  ║   builder.Services.AddScoped<ChienBinh>()                       ║
  ║                                                                 ║
  ║   +------------------+    +------------------+                  ║
  ║   |  IVuKhi -------> |    |  ChienBinh       |                  ║
  ║   |    └── Kiem      |    |  └── (chờ inject)|                  ║
  ║   +------------------+    +------------------+                  ║
  ╚═══════════════════════════════╤═════════════════════════════════╝
                                  │  app.Build()
                                  ▼
  +~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~+
  ~                   🌐  HTTP REQUEST ĐẾN                          ~
  ~                   GET /chien-dau                                ~
  +~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~+
                                  │
                                  ▼
  ╔═════════════════════════════════════════════════════════════════╗
  ║            🔍  CONTAINER RESOLVES DEPENDENCIES                  ║
  ╚═══════════╤════════════════════════════════╤════════════════════╝
              │                                │
              ▼                                ▼
  +-----------+----------+       +-------------+-----------+
  |   Tìm: IVuKhi        |       |   Tìm: ChienBinh        |
  |   Đăng ký: Kiem      |       |   Constructor cần:      |
  |                      |       |   └── IVuKhi ✅ (có rồi)|
  +----------+-----------+       +-------------+-----------+
             │                                 │
             │   new Kiem()                    │
             ▼                                 ▼
  +==========+===========+       +=============+===========+
  ‖   INSTANCE: Kiem     ‖       ‖  INSTANCE: ChienBinh    ‖
  ‖   ──────────────     ‖  ──►  ‖  _vuKhi = [Kiem] 💉     ‖
  ‖   void TanCong()     ‖       ‖  void TanCong()         ‖
  +======================+       +=========================+
                                               │
                                               │  inject xong ✅
                                               ▼
  .-  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -.
  '            🎮  EXECUTE: cb.TanCong()                  '
  '                                                       '
  '   ChienBinh                                           '
  '       │                                               '
  '       └──► _vuKhi.TanCong()                           '
  '                 │                                     '
  '                 └──► Kiem.TanCong()                   '
  '                            │                          '
  '                            └──► "Chém bằng kiếm! ⚔️"   '
  '-  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -'
                                               │
                                               ▼
  ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
  +                  📤  RESPONSE: "Thắng rồi!"                  +
  ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
                                               │
                               ┌───────────────┘
                               │  Scope kết thúc
                               ▼
  ############################################################
  #              🗑️  SCOPE DISPOSED (AddScoped)              #
  #                                                          #
  #   Kiem instance        ──────────────────► ❌ Destroyed  #
  #   ChienBinh instance   ──────────────────► ❌ Destroyed  #
  #                                                          #
  #   (Singleton sẽ KHÔNG bị destroy ở bước này)             #
  ############################################################

```

---

## Giải thích chi tiết qua 3 Giai đoạn (States)

### 1. Giai đoạn STARTUP (Khởi tạo kho vũ khí - IoC Container)

Khi ứng dụng vừa bật lên (trong file `Program.cs`), bạn chưa làm gì cả ngoài việc **đăng ký (Register)**.
Bạn đang nói với **IoC Container** (có thể coi như ông thủ kho) rằng:

* "Này ông thủ kho, nếu ai đó cần `IVuKhi`, hãy đưa cho họ thanh `Kiem`."
* "Tôi cũng có class `ChienBinh`, nhớ mặt nó để tí gọi."

Lúc này, **chưa có thanh kiếm hay chiến binh nào được tạo ra (không có lệnh `new` nào chạy)**. Tất cả chỉ là *bản thiết kế* nằm trong kho.

### 2. Giai đoạn CONTAINER RESOLVES DEPENDENCIES (Thủ kho cấp phát vũ khí)

Khi có một request tới (ví dụ User bấm nút "Chiến đấu" trên web), hệ thống cần một `ChienBinh` để xử lý.

Đây chính là lúc **Resolve** diễn ra:

1. Hệ thống bảo thủ kho: "Cho tôi một `ChienBinh`."
2. Thủ kho nhìn vào bản thiết kế của `ChienBinh` và thấy: "À, chiến binh này cần một `IVuKhi` trong hàm tạo (constructor)."
3. Thủ kho lật sổ ra xem `IVuKhi` đã được đăng ký là gì. Thấy chữ `Kiem`.
4. **Hành động Resolve:** Thủ kho lập tức chế tạo (khởi tạo bằng `new`) một thanh `Kiem` thật sự (Instance).
5. Sau đó, thủ kho tạo ra `ChienBinh` và **tiêm (Inject)** thanh `Kiem` đó vào tay chiến binh.

Quá trình "thủ kho tự động tìm xem ai cần gì, rồi tự tạo ra đúng món đó và ghép lại với nhau" chính là ý nghĩa của cụm từ **"Container Resolves Dependencies"**.

### 3. Giai đoạn SCOPE DISPOSED (Dọn dẹp)

Sau khi chiến binh đánh xong và server trả kết quả về cho user, cái "Phạm vi" (Scope) của request đó kết thúc.
Vì ở bước 1 chúng ta đăng ký bằng `AddScoped`, Container (thủ kho) sẽ tự động tiêu hủy (`Destroy/Dispose`) cả `Kiem` và `ChienBinh` đó để giải phóng RAM. Lần sau có request mới, thủ kho sẽ chế tạo cặp mới.

---

### Tóm tắt lại

**"Container Resolves Dependencies"** nghĩa là:
Bạn không cần phải code thủ công kiểu: `var kiem = new Kiem(); var cb = new ChienBinh(kiem);`.
Container sẽ tự động đọc sơ đồ, tự động tìm kiếm, tự động `new` và tự động nhét các object vào đúng chỗ của nó. Việc của bạn chỉ là: *Khai báo tôi cần gì, Container sẽ lo phần còn lại*.


## Tóm tắt lại hành trình chuẩn 100% theo lời của bạn:

1. **Chuẩn bị:** Bảo với server: *"Ai cần `IVuKhi` thì phát `Kiem` nhé, và nhớ đăng ký cả `ChienBinh` nữa"*.
2. **Khởi động:** App chạy lệnh `app.Build()`. Kho vũ khí chốt sổ.
3. **Có lệnh gọi:** Người dùng gọi API `/chien-dau`. Server thấy API này đòi một anh `ChienBinh`.
4. **Kiểm tra nhu cầu:** Server lôi bản thiết kế `ChienBinh` ra, thấy hàm khởi tạo `public ChienBinh(IVuKhi vuKhi)` đòi một món vũ khí.
5. **Chế tạo (Resolve):** Server tra sổ, biết `IVuKhi` thực chất là `Kiem`. Nó tự động rèn một cây kiếm thật (`new Kiem()`).
6. **Lắp ráp (Inject):** Server rèn xong `ChienBinh`, tiện tay truyền luôn cây `Kiem` thật vừa rèn vào làm tham số `vuKhi`. Bên trong `ChienBinh` tự lấy cây kiếm thật đó cất vào vỏ (`_vuKhi = vuKhi`).
7. **Chiến đấu:** Lắp ráp xong xuôi, server giao `ChienBinh` (đã cầm kiếm) ra cho API chạy. Chiến binh gọi lệnh `cb.TanCong()`, mang cây kiếm đã được tiêm vào ra chém!
8. **Dọn dẹp:** Chém xong, trả kết quả cho người dùng, server đem cả `ChienBinh` lẫn `Kiem` đi vứt (Dispose) để giải phóng RAM.



## Container resolves dependencies (Giải thích chi tiết)

### 1. Ý nghĩa cốt lõi

"Resolve" có nghĩa là **tìm kiếm, tạo ra và cung cấp**.

Thay vì bạn phải tự mình khởi tạo các đối tượng bằng từ khóa `new` (như `var client = new VisionClient(...)`), bạn sẽ yêu cầu một thực thể trung gian gọi là **DI Container** (Dependency Injection Container) làm việc đó giúp bạn.

"Container resolves dependencies" nghĩa là Container sẽ tự động đi tìm tất cả các "món đồ" (dependencies) mà một class cần, lắp ráp chúng lại và đưa cho bạn một object hoàn chỉnh để sử dụng.

### 2. Ví dụ thực tế từ project của bạn

Hãy nhìn vào class `VisionClient` và cách nó được đăng ký trong `Program.cs`:

**Bước 1: Đăng ký (Register)**
Trong `Program.cs`, bạn nói với Container rằng: "Nếu ai đó cần `IVisionClient`, hãy tạo cho họ một đối tượng `VisionClient`".

```csharp
builder.Services.AddHttpClient<IVisionClient, VisionClient>();

```

**Bước 2: Giải quyết (Resolve)**
Khi hệ thống cần tạo ra class `AgvOrchestrator`, nó thấy class này yêu cầu một `IVisionClient` trong constructor:

```csharp
public AgvOrchestrator(IVisionClient visionClient) { ... }

```

Lúc này, **Container thực hiện việc "Resolve"**:

1. Nó kiểm tra trong "danh bạ" đã đăng ký xem `IVisionClient` là cái gì.
2. Nó thấy đó là `VisionClient`.
3. Nó tự động `new VisionClient(...)`, truyền mọi tham số cần thiết vào.
4. Cuối cùng, nó đưa object đó vào cho `AgvOrchestrator` sử dụng.

### 3. Tại sao cần Container thực hiện việc này?

* **Tự động hóa hoàn toàn:** Bạn không cần quan tâm class `A` cần class `B`, class `B` lại cần class `C`. Container sẽ tự đi sâu xuống từng tầng để khởi tạo mọi thứ theo đúng thứ tự.
* **Quản lý vòng đời (Lifetime):** Container quyết định một đối tượng sẽ sống bao lâu: tạo mới mỗi lần dùng (`Transient`), dùng chung trong một request (`Scoped`), hay dùng duy nhất một đối tượng cho toàn bộ ứng dụng (`Singleton`).
* **Giảm sự phụ thuộc chặt chẽ (Loose Coupling):** Class của bạn chỉ yêu cầu "Tờ hợp đồng" (Interface), còn Container sẽ quyết định đưa "Nhân viên" (Implementation) nào vào. Điều này giúp bạn dễ dàng thay đổi công nghệ (ví dụ đổi từ gọi HTTP sang gọi USB) mà không phải sửa code ở nhiều nơi.

**Tóm lại:** Container đóng vai trò như một **"Quản gia thông minh"**. Bạn chỉ cần khai báo danh sách những thứ mình cần, và khi bạn bắt đầu làm việc, Quản gia (Container) đã chuẩn bị sẵn sàng mọi công cụ (Resolve dependencies) trên bàn cho bạn.


## **DI Container** 
Ông thủ kho là một **object đặc biệt trong .NET**, chịu trách nhiệm 3 việc:

---

**1. Lưu bản đăng ký (Registry)**

Khi bạn viết trong `Program.cs`:
```csharp
builder.Services.AddHttpClient<IVisionClient, VisionClient>();
```

`builder.Services` chính là **DI Container**. Bạn đang nói với nó: *"Ghi vào sổ: ai cần `IVisionClient` thì tạo `VisionClient` cho họ."*

---

**2. Tạo object khi cần (Resolve)**

Khi có request đến, Controller cần `IVisionClient` — Container tự động `new VisionClient(...)` và nhét vào. Bạn không gọi `new` ở đâu cả.

---

**3. Quản lý vòng đời (Lifetime)**

Container quyết định object đó sống bao lâu:

| Đăng ký | Sống bao lâu |
|---|---|
| `AddSingleton` | Cả đời app |
| `AddScoped` | 1 HTTP request |
| `AddTransient` | Mỗi lần được inject |

---

**Tóm gọn:** DI Container = cái **"danh bạ + nhà máy + quản lý vòng đời"** tích hợp trong một. Thay vì bạn phải `new` thủ công mọi thứ, Container làm hết — bạn chỉ cần khai báo *"tôi cần cái này"* trong constructor.