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
### `PositionX` bị lặp lại,  mang **2 ý nghĩa hoàn toàn khác biệt**.
#### `public const ushort PositionX = 2003;` (Nằm trong file `ModbusRegisters.cs`)
* **Ý nghĩa:** Đây là **Địa chỉ thanh ghi (Register Address)** trên mạng Modbus.
* **Cách hiểu:** Hãy tưởng tượng bộ nhớ của chiếc xe AGV là một cái tủ thuốc có rất nhiều ngăn kéo. **`2003`** chính là số thứ tự được dán bên ngoài cái ngăn kéo chứa thông tin về tọa độ X.
* Nó luôn là một hằng số (`const`) và không bao giờ thay đổi.

#### `public int PositionX { get; set; }` (Nằm trong file `AgvState.cs`)

* **Ý nghĩa:** Đây là **Giá trị thực tế (Actual Value)** của tọa độ X tại một thời điểm.
* **Cách hiểu:** Đây chính là "đồ vật" nằm bên trong ngăn kéo số 2003. Giá trị này liên tục thay đổi khi xe di chuyển (ví dụ: `0`, `500`, `1500` mm).


### B. `AgvState.cs`

#### `{ get; set; }`
`public int PositionX;` (không có get; set;) thì nó là một Field.
`public int PositionX { get; set; }` thì nó là một Property.

##### 1. Bắt buộc để làm việc với các Framework (JSON, Database)

Dự án của bạn là một **ASP.NET Web API**. Khi trình duyệt gọi API (ví dụ `GET /agv/status`), C# sẽ lấy object `AgvState` và chuyển nó thành chuỗi JSON để gửi cho trình duyệt hoặc phần mềm khác.

* Các thư viện chuyển đổi JSON (như `System.Text.Json` mà bạn đang dùng) **chỉ đọc được các Property (`{ get; set; }`)**.

##### 2. Kiểm soát quyền Đọc/Ghi (Tính đóng gói - Encapsulation)

`get` nghĩa là cho phép **đọc** giá trị
`set` là cho phép **ghi/sửa** giá trị

Ví dụ, bạn muốn `PositionX` chỉ được phép sửa ở bên trong class `AgvState`, còn các class khác bên ngoài chỉ được phép đọc thôi (để tránh việc code chỗ khác vô tình làm sai lệch vị trí xe). Bạn chỉ cần viết:

```csharp
public int PositionX { get; private set; } // Ai cũng đọc được (get), nhưng chỉ nội bộ mới sửa được (private set)

```
##### 3. Dễ dàng chèn thêm Logic (Validation) về sau

Muốn thêm quy tắc: **"Xe AGV không bao giờ có tọa độ âm (PositionX luôn >= 0)"**.

```csharp
private int _positionX; // Biến ẩn ngầm bên dưới

public int PositionX 
{ 
    get { return _positionX; } 
    set 
    { 
        if (value < 0) 
        {
            _positionX = 0; // Nếu ai đó cố tình gán số âm, tự động đưa về 0
        }
        else 
        {
            _positionX = value;
        }
    } 
}

```

Nhờ có `set`, bạn đã chặn đứng lỗi ngay tại cửa.

#### HeadingRadians => HeadingDegrees * Math.PI / 180.0;

`HeadingRadians` là một **Computed Property** (Thuộc tính tính toán).

Nó không lưu giá trị của riêng nó. Thay vào đó, mỗi khi bạn **đọc** giá trị của `HeadingRadians`, nó sẽ tự động chạy công thức `HeadingDegrees * Math.PI / 180.0` để tính toán giá trị đó dựa trên `HeadingDegrees` hiện tại.

**Ví dụ:**

* Nếu `HeadingDegrees` = 90
* Khi bạn truy cập `HeadingRadians`, code sẽ tính: `90 * 3.14159 / 180.0 = 1.5708`
* Giá trị `1.5708` này sẽ được trả về ngay lập tức.

#### public ErrorCode Error { get; set; }

Mối quan hệ giữa **Kiểu dữ liệu** và **Thuộc tính**:

1. **`public enum ErrorCode : ushort`**: Đây là đoạn code dùng để **tạo ra một kiểu dữ liệu mới** (tên là `ErrorCode`).
2. **`public ErrorCode Error { get; set; }`**: Đây là đoạn code **sử dụng kiểu dữ liệu** vừa tạo ở trên để khai báo một thuộc tính có tên là `Error`.

Giống như khi bạn viết `public int Age { get; set; }`, thì `ErrorCode` đóng vai trò y hệt như chữ `int` (kiểu dữ liệu).

#### DateTime LastUpdated 
Hệ quy chiếu thời gian đồng nhất cho toàn bộ hệ thống:

1. **PostgreSQL:** Các bảng đều dùng kiểu dữ liệu `TIMESTAMPTZ` kèm hàm `DEFAULT NOW()`. Đặc điểm của `TIMESTAMPTZ` là nó luôn tự động lưu trữ thời gian ở chuẩn **UTC**.
2. **Python (`db_logger.py`):** Các câu lệnh `INSERT` ghi log đều sử dụng thẳng hàm `NOW()` của SQL. Do đó, Python đang giao việc định đoạt thời gian cho Database (tức là ghi bằng UTC).
3. **C# (`AgvState.cs`):** Việc bạn chủ động dùng `DateTime.UtcNow` đảm bảo khi C# xử lý logic hoặc đẩy dữ liệu xuống DB, mốc thời gian sẽ khớp hoàn toàn với C++ và Python.

=> Dùng **UTC** cho backend/database là quy tắc vàng (Best Practice) để hệ thống không bao giờ bị lỗi lệch múi giờ.

Tính năng **hiển thị tự động** của DBeaver đối với kiểu dữ liệu `TIMESTAMPTZ`.

1. **Bản chất lưu trữ:** Dưới đáy Database, thời gian vẫn đang được lưu ở chuẩn giờ gốc **UTC (GMT+0)**.
2. **Cách DBeaver hoạt động:** Khi bạn xem data, DBeaver sẽ tự động lấy múi giờ trên máy tính của bạn (hiện tại đang là múi giờ GMT+9) để tính toán và hiển thị ra cho bạn dễ đọc.
3. **Ý nghĩa của đuôi `+0900`:** DBeaver gắn thêm đuôi này để thông báo: *"Thời gian bạn đang nhìn thấy trên màn hình đã được cộng thêm 9 tiếng so với giờ UTC gốc"*.

**Tóm lại:** Dữ liệu không bị thay đổi, nó vẫn là UTC. DBeaver chỉ đang "dịch" ra giờ địa phương của bạn (GMT+9) để thân thiện với mắt người nhìn mà thôi. Nếu bạn gửi CSDL này cho một người ở Việt Nam mở lên, DBeaver của họ sẽ tự động hiển thị thành `+0700`.

### C. DetectionResult

#### VisionResponse và Detection

Cần cả `VisionResponse` và `Detection` vì dữ liệu JSON mà Python (Vision AI) trả về có cấu trúc **phân tầng (cha - con)**. C# cần các class tương ứng để "mô phỏng" lại chính xác cấu trúc này.

Hãy nhìn vào cục JSON thực tế mà Python gửi sang:

```json
{
    "detections": [ 
        {
            "object_class": "person",
            "confidence": 0.8723,
            "distance_meters": 2.5
        }
    ],
    "processing_time_ms": 45,
    "total_objects": 1
}

```

* **`VisionResponse` (Lớp Cha):** Dùng để "hứng" toàn bộ cục JSON to nhất ở ngoài cùng. Nó chứa thời gian xử lý (`processing_time_ms`), tổng số vật thể (`total_objects`) và **một danh sách** các vật thể (`List<Detection>`).
* **`Detection` (Lớp Con):** Dùng để "hứng" thông tin chi tiết của **từng vật thể cụ thể** nằm bên trong danh sách `detections` (như class là gì, độ tin cậy bao nhiêu, cách bao xa).

#### BoundingBox

1. Để khớp với cấu trúc JSON lồng nhau (Nested JSON)

Python (Vision AI) gửi sang C# một cục JSON có chứa object con bên trong:

```json
"bbox": { 
    "x1": 0.12, "y1": 0.34, "x2": 0.56, "y2": 0.78 
}

```

Để C# có thể "hứng" được object con này một cách tự động, bạn bắt buộc phải tạo một class riêng tên là `BoundingBox` chứa 4 thuộc tính (X1, Y1, X2, Y2).
2. Để Tái sử dụng code (Reusability)

Nếu bạn để ý trong class `Detection`, bạn đang dùng `BoundingBox` tới **2 lần**:

* Một lần cho `Bbox` (tọa độ chuẩn hóa 0-1 để lưu vào Database).
* Một lần cho `BboxPixels` (tọa độ pixel thực tế để debug/hiển thị).

Nhờ tạo riêng class `BoundingBox`, bạn chỉ cần khai báo 2 dòng gọn gàng, thay vì phải khai báo tới 8 biến cồng kềnh ngay trong class `Detection` (kiểu như `BboxX1`, `BboxY1`, `BboxPixelX1`,...).


#### Tại sao ta vẫn tạo Class để hứng dữ liệu đến C#

Bạn không bắt buộc phải luôn tạo class.

Trong C#, nếu lười tạo class hoặc dữ liệu gửi đến có cấu trúc không cố định, bạn hoàn toàn có thể dùng các cách sau để "hứng" dữ liệu:

1. Dùng kiểu `dynamic` hoặc `JsonNode` / `JsonDocument`

Hứng nguyên cục JSON dưới dạng đối tượng động (dynamic object) và tự truy xuất thủ công.

```csharp
// Không cần class, truy xuất thẳng bằng key chữ
var confidence = jsonObject["detections"][0]["confidence"].GetValue<double>();

```

2. Dùng `Dictionary<string, object>`

Hứng dữ liệu dạng từ điển (Key-Value), ví dụ: key là `"object_class"`, value là `"person"`.

---

Vậy tại sao trong dự án AGV này ta vẫn cất công tạo Class?

Dù có ngoại lệ, việc tạo class (Strongly-typed) vẫn là **tiêu chuẩn bắt buộc (Best Practice)** trong các dự án thực tế vì:

1. **Có IntelliSense (Gợi ý code):** Gõ `Detection.` là nó tự xổ ra chữ `Confidence` cho bạn chọn. Nếu dùng `dynamic`, bạn phải tự gõ chuỗi `"confidence"`, gõ sai chính tả (ví dụ `"confidenc"`) thì lúc chạy xe AGV mới báo lỗi và... đâm vào tường.
2. **An toàn (Type-Safety):** Nếu Python gửi chữ `"abc"` vào chỗ đáng lẽ là số `2.5`, C# sẽ báo lỗi ngay lập tức lúc nhận.
3. **Clean Code:** Đọc vào file `DetectionResult.cs` là biết ngay Python sẽ gửi sang những dữ liệu gì mà không cần phải chạy thử hay mò mẫm xem log.

#### System.Text.Json.Serialization
Được dùng để cung cấp các công cụ giúp **chuyển đổi qua lại giữa Object (C#) và chuỗi JSON**.

Trong file `DetectionResult.cs` của bạn, nó được gọi vào để sử dụng thẻ **`[JsonPropertyName]`**.

Thẻ này dùng để "phiên dịch" tên biến từ Python sang C#. Ví dụ:
Nhờ có thư viện này, khi Python gửi chuỗi JSON có key là `"object_class"` (viết thường, gạch dưới), C# sẽ biết tự động gán giá trị đó vào biến `ObjectClass` (viết hoa).

Lấy dữ liệu từ chuỗi JSON đắp vào biến `Detections` cho bạn, thay vì bạn phải tự viết code gán thủ công kiểu: `Detections = json["detections"]`.

#### Tại sao chi có mõi  Detections  là có = new();

Sự khác biệt giữa **Kiểu tham chiếu** (Reference Type) và **Kiểu giá trị** (Value Type) trong C#.

1. **`List<Detection>` (Kiểu tham chiếu):** Nếu không có `= new();`, giá trị mặc định của nó sẽ là `null`. Nếu bạn vô tình gọi `Detections.Count` hoặc `Detections.Add()` khi nó đang `null`, chương trình sẽ báo lỗi **NullReferenceException** và sập ngay lập tức. Viết `= new();` (viết tắt của `= new List<Detection>();`) để đảm bảo nó luôn là một danh sách rỗng, an toàn khi sử dụng.
2. **`int ProcessingTimeMs` (Kiểu giá trị):** Các kiểu số (int, double, bool...) trong C# **không bao giờ bị `null**`. Giá trị mặc định của chúng tự động là `0`. Do đó, bạn không cần phải khởi tạo `= 0;` làm gì cho thừa.

#### null thì cũng là rỗng , mà rrỗng thì cũng là null, phân biệt làm chi cho khổ thân vậy.

Hãy tưởng tượng thế này cho dễ hiểu:

* **Rỗng (Empty):** Bạn **có một cái ví**, nhưng trong ví không có đồng nào.
* **Null:** Bạn **không có cái ví nào cả**.

**Tại sao máy tính phải phân biệt**

Bởi vì khi bạn ra lệnh cho máy tính: *"Hãy đếm xem có bao nhiêu tiền trong ví?"* (gọi lệnh `Detections.Count`)

1. Nếu ví **rỗng**: Máy tính mở ví ra đếm và trả lời *"Có 0 đồng"*. Mọi thứ êm đẹp.
2. Nếu **null**: Máy tính ngơ ngác vì *đào đâu ra cái ví mà đòi đếm?* Lúc này nó sẽ hoảng loạn và **đánh sập toàn bộ chương trình (lỗi NullReferenceException)**.

=> Việc viết `= new();` chính là thao tác **"Mua sẵn một cái ví rỗng để đó"**. Nhờ vậy, khi chương trình cần đếm hoặc bỏ đồ vào, nó luôn có sẵn ví để dùng mà không bị sập (crash) ngầm!


#### Khi nào dùng summary khi nào không.
Việc dùng `/// <summary>` (chú thích code) không phải là bắt buộc cho tất cả các biến, mà chỉ dùng ở những chỗ **dễ gây nhầm lẫn** hoặc **cần giải thích thêm logic**.

Trong class `Detection` :

1. **Những biến không cần `<summary>`:** `ObjectClass` hay `Confidence` có tên gọi quá rõ ràng (tên vật thể, độ tin cậy). 

2. **Những biến cần `<summary>` (`Bbox`, `BboxPixels`, `DistanceMeters`):**
* Có tận 2 biến liên quan đến Bounding Box nên phải ghi chú để phân biệt: `Bbox` dùng tọa độ chuẩn hóa (0-1) để lưu Database
`BboxPixels` dùng tọa độ pixel thực tế để hiển thị lên màn hình. 
Nếu không ghi, người khác (hoặc chính bạn sau 1 tháng nữa) sẽ không biết nên xài cái nào.

* Biến `DistanceMeters` cũng cần ghi chú rõ ràng là nó có thể bị `null`, được ước tính bằng mô hình pinhole, và khi xài phải cộng thêm `300mm` camera offset.

**Tóm lại:** Lập trình viên chỉ viết `<summary>` cho những thuộc tính mang "luật ngầm" (business logic) hoặc có sự rườm rà cần phân định rõ, giúp code tự giải thích được chính nó qua IntelliSense (khi rê chuột vào biến).


#### Tại sao lại có dấu ? trong public BoundingBox? Bbox 
Dấu `?` trong `BoundingBox?` có nghĩa là **Nullable** (Cho phép biến này được quyền mang giá trị `null`).

**Tại sao lại cần nó ở đây?**

Đây là cách viết code phòng thủ (Defensive Programming) khi giao tiếp với hệ thống khác (Python).

Giả sử vì một lý do nào đó (model AI bị lỗi, không trích xuất được tọa độ), cục JSON từ Python gửi sang bị thiếu mất key `"bbox"`, hoặc gửi sang là `"bbox": null`.

* **Nếu không có `?`:** C# đinh ninh là lúc nào cũng phải có dữ liệu BoundingBox. Khi nhận thiếu, nó có thể văng lỗi (crash) lúc đọc JSON.
* **Có `?`:** C# hiểu rằng "À, cái hộp tọa độ này có thể không tồn tại". Nếu Python không gửi, C# chỉ đơn giản gán `Bbox = null` và xe AGV vẫn chạy tiếp bình thường, không bị sập phần mềm.


Dưới đây là 2 lý do bạn nên dùng `= new();` thay vì `?` cho List:

1. **Khỏi phải viết code check Null:** Nếu dùng `List<Detection>?`, đi đâu bạn cũng phải viết thêm dòng `if (Detections != null)` trước khi đếm (`.Count`) hoặc duyệt (`foreach`). Nếu quên, chương trình sẽ sập ngầm.
2. **`foreach` cực kỳ thích danh sách rỗng (`new()`):** Nếu AI không nhận diện được vật nào (danh sách rỗng), vòng lặp `foreach` sẽ tự động bỏ qua mà không bị lỗi. Nhưng nếu đưa cho nó một danh sách `null`, vòng lặp `foreach` sẽ báo lỗi sập chương trình ngay lập tức.


Nếu bạn viết `public BoundingBox Bbox { get; set; } = new();`:

1. Máy tính sẽ tự động tạo ra một cái hộp với tọa độ mặc định là **(0, 0, 0, 0)**.
2. Lúc này, hệ thống AGV sẽ hiểu lầm là: *"À, có một vật cản nằm ngay sát góc trên cùng bên trái màn hình với chiều dài/rộng bằng 0"*, dẫn đến tính toán sai đường đi.

Ngược lại, dùng **`?` (cho phép `null`)** phản ánh chính xác sự thật: *"Không có dữ liệu tọa độ nào cả"*.

**Quy tắc:**

* Với **Danh sách (List)**: Rỗng (`new()`) là tốt, vì nó nghĩa là "túi không có đồ".
* Với **Đối tượng cụ thể (như BoundingBox)**: Rỗng (`new()`) sẽ biến thành thông số `0`, làm sai lệch logic toán học. Nên để `null` là chuẩn nhất.