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


### D. GridMap
#### Tại sao CellType lập lại ở dưới lại có CellType[,] _grid = new CellType[Width, Height]

1. **`CellType` (enum):** Quy định **nội dung** bên trong 1 ô. Nó nói rằng một ô chỉ được phép mang 1 trong 3 giá trị: `Empty`, `StaticWall`, hoặc `DynamicObstacle`.
2. **`[Width, Height]`**: Đây là cú pháp tạo **mảng 2 chiều** trong C# (bạn cứ tưởng tượng nó như một cái bàn cờ cờ vua hoặc bảng Excel).

Dòng code `new CellType[Width, Height];` mang ý nghĩa:
*"Hãy tạo ra một mảng 2 chiều (bàn cờ) có chiều rộng 40 (`Width`) và chiều cao 20 (`Height`). 
Tổng cộng có 800 ô. Và **mỗi ô** trong 800 ô này sẽ chứa dữ liệu là kiểu `CellType`."*

Ví dụ thực tế khi chạy:

* Ô `_grid[0, 0]` có thể mang giá trị `CellType.StaticWall`
* Ô `_grid[5, 5]` có thể mang giá trị `CellType.Empty`

Giống với cách bạn khai báo một mảng số nguyên `new int[40, 20]`, nhưng thay vì mỗi ô chứa một số `int` bất kỳ, thì mỗi ô ở đây chỉ được chứa các trạng thái đã định nghĩa sẵn trong `CellType`.

#### Tại sao [,]

Ký hiệu `[,]` là cú pháp đặc trưng của C# dùng để khai báo **Mảng 2 chiều (2D Array)** có cấu trúc hình chữ nhật hoàn hảo (ma trận).

Dấu phẩy `,` ở giữa dùng để ngăn cách các chiều (dimensions) với nhau:

* **`[]`** (Không có dấu phẩy): Mảng 1 chiều (như 1 hàng ngang).
* **`[,]`** (Có 1 dấu phẩy): Mảng 2 chiều (có hàng và cột, như cái bàn cờ).
* **`[,,]`** (Có 2 dấu phẩy): Mảng 3 chiều (như một khối rubik).

**Tại sao C# lại đẻ ra cái này mà không dùng `[][]` như nhiều ngôn ngữ khác?**

* `[,]` (C# gọi là **Rectangular Array**): Đảm bảo 100% tạo ra một hình chữ nhật chuẩn (ví dụ 40 cột, mỗi cột đúng 20 ô). Nó cấp phát một khối bộ nhớ liền mạch, chạy rất nhanh và hoàn hảo để làm bản đồ tọa độ `(x, y)` cho xe AGV.
* `[][]` (C# gọi là **Jagged Array** - Mảng lởm chởm): Là "mảng chứa các mảng". Hàng 1 có thể có 5 ô, hàng 2 có 10 ô, hàng 3 có 2 ô... không phù hợp để làm bản đồ nhà kho vuông vức.

#### Giới thiệu file GirdMap

File `GridMap.cs` là mô hình đại diện cho bản đồ nhà kho 2D, đóng vai trò như một "bàn cờ" để thuật toán A* tính toán đường đi cho xe AGV.

Dưới đây là các thành phần chính cấu tạo nên nó:

**1. `CellType` (Kiểu liệt kê trạng thái ô)**
Định nghĩa 3 loại địa hình có thể có trên bản đồ:

* `Empty`: Ô trống, xe đi được.
* `StaticWall`: Tường hoặc kệ hàng cố định (không bao giờ đổi).
* `DynamicObstacle`: Vật cản động do AI vừa phát hiện ra.

**2. Các hằng số kích thước (Constants)**
Quy định mảng có 40 cột (`Width = 40`) và 20 hàng (`Height = 20`). Mỗi ô đại diện cho `500mm` ngoài đời thực (`CellSizeMm = 500`).

**3. Mảng lưu trữ lõi (`_grid`)**
`private readonly CellType[,] _grid`: Đây là mảng 2 chiều chứa dữ liệu thực sự của 800 ô trên bản đồ, được giấu kín (`private`) để bảo vệ an toàn.

**4. Nhóm hàm quản lý Tường & Vật cản**

* `InitStaticWalls()`: Vẽ biên giới nhà kho và các kệ hàng. Chỉ gọi 1 lần khi phần mềm mới chạy.
* `ClearDynamicObstacles()`: Quét sạch các vật cản AI cũ để chuẩn bị cập nhật tầm nhìn mới. (Không xóa tường cố định).
* `SetObstacle(x, y)`: Đặt vật cản mới do AI phát hiện vào tọa độ. Hàm này tự động chặn nếu tọa độ bị lọt ra ngoài mảng hoặc đè lên tường tĩnh.

**5. Nhóm hàm Tiện ích & Tính toán**

* `WorldToGrid()`: "Dịch" tọa độ thực tế từ milimet (ví dụ: x=1500mm, y=2000mm) thành tọa độ của mảng (ô số mấy).
* `IsWalkable()`: Hàm hỏi nhanh xem AGV có được phép đi vào tọa độ (x,y) hay không (trả về `true` nếu là `Empty`).

#### private readonly CellType[,] _grid = new CellType[Width, Height];

* **`CellType[,] _grid`**: Dọn ra một góc nhà, dán cái nhãn: *"Chỗ này tôi chuẩn bị đặt một cái tủ 2 chiều chỉ để đựng CellType"*. (Lúc này nhà vẫn trống trơn, chưa có cái tủ nào cả).
* **`= new CellType[Width, Height]`**: Đây là hành động **gọi thợ mộc đến đóng luôn một cái tủ khổng lồ** có 40 cột, 20 hàng (tổng 800 ngăn kéo)!
Ngay khi cái tủ 800 ngăn kéo vừa đóng xong (chạy xong lệnh `new`), nó đã tự động nhét sẵn giá trị mặc định là `Empty` vào **kín mít cả 800 ngăn*  rồi!

Sau này chạy phần mềm, sếp muốn xây tường ở đâu, sếp chỉ việc kéo đúng cái ngăn kéo ở tọa độ đó ra, vứt chữ `Empty` đi và thay bằng chữ `StaticWall` (`_grid[x,y] = CellType.StaticWall`).

Readony : Chữ readonly khóa chặt cái "vỏ". Sếp đã gán new CellType[Width, Height] ở đó (hoặc gán trong hàm tạo - constructor), thì xuống các hàm khác sếp KHÔNG THỂ vứt mảng này đi để đẻ ra mảng mới.
(Ví dụ: Cố tình viết _grid = new CellType[10, 10]; là C# nó gõ đầu ngay).
Readonly KHÔNG khóa cái "ruột" bên trong mảng! Nghĩa là ở bất kỳ hàm nào trong class đó, sếp vẫn có thể lôi từng ô ra sửa đổi bét nhè.
(Ví dụ: Viết _grid[0, 0] = CellType.StaticWall; thì mượt mà trơn tru, chả ai cấm).

#### public CellType GetCell(int x, int y) => _grid[x, y];
So sánh nhanh cho sếp thấy độ "phũ" của C# nhé:

**1. `public CellType GetCell(int x, int y) => _grid[x, y];**`
* **Giải thích:** Tủ `_grid` đang chứa đồ kiểu `CellType`. Sếp thò tay vào ngăn `[x, y]` lấy ra một món, và dõng dạc tuyên bố cho cả thế giới biết: *"Trả về cho tôi món đồ kiểu `CellType`!"*. Trùng khớp 100%

**2. `public int GetCell(int x, int y) => _grid[x, y];**`

* **Kết quả:** **BÙM! Lỗi đỏ lòm (Lỗi biên dịch).**
* **Giải thích:** Tủ sếp đang chứa `CellType` (ví dụ như quả táo). Sếp lôi quả táo ra nhưng lại bắt cái hàm này trả về một số `int` (bắt gọi quả táo là củ hành). Thằng C# nó nguyên tắc lắm, nó gào lên ngay: *"Sếp ơi em không thể tự động biến `CellType` thành số `int` được!"*.

**💡 Cách cứu vãn số 2 **
Nếu sếp vẫn khăng khăng muốn lấy số `int` (để xem mã số bí mật của Enum là 0, 1 hay 2), sếp phải "ép" nó bằng vũ lực, gọi là Ép kiểu (Casting):
`public int GetCell(int x, int y) => (int)_grid[x, y];`


#### public int[,] ToArray()

Nếu dọn ra một hàm thế này:
`public CellType[,] ToArray() { return _grid; }`

Thì câu chuyện ngoài đời nó sẽ diễn ra như sau:

1. **`private _grid`**: Sếp giấu cái tủ `_grid` trong phòng ngủ khóa trái cửa. Không ai tự ý xông vào được.
2. **`public ToArray()`**: Sếp mở một cái "cửa sổ giao dịch" cho người ngoài tới xin thông tin bản đồ.
3. **`return _grid;`**: Khi người ta xin thông tin, thay vì đưa bản photo, sếp lại... **thò tay qua cửa sổ, đưa luôn cái chìa khóa phòng ngủ** cho họ!

Lúc này, thằng ở file khác nó sẽ làm trò này:

```csharp
var gridNgoaiLai = banDo.ToArray();         // Nó lấy được chìa khóa từ tay sếp!
gridNgoaiLai[0, 0] = CellType.StaticWall;   // Nó mở tủ nhà sếp ra xây ngay bức tường!

```

Đấy! Cái mác `private` lúc này trở nên **VÔ DỤNG**. Vì `private` chỉ cấm người ta *tự phá cửa vào nhà* (gọi tên biến trực tiếp), chứ nó **KHÔNG CẤM** sếp *tự tay dâng hiến chìa khóa* (địa chỉ vùng nhớ) cho người ngoài thông qua lệnh `return` của một hàm `public`!

**Chốt hạ:** Vì mảng là kiểu tham chiếu, lệnh `return _grid` chính là hành động tuồn chìa khóa gốc ra ngoài. Thế nên sếp bắt buộc phải "chạy bằng cơm", dùng vòng lặp `for` để tạo mảng `new` (bản photo) rồi mới dám ném ra ngoài sếp ạ! 😎 Thuyết phục chưa sếp ơi? Lên kèo tiếp đi nào!

#### Hình tượng hoá _grid 8x5

Dạ vâng thưa sếp, em xin múa phím vẽ ngay cái "sơ đồ chiến thuật" 8x5 (Width = 8, Height = 5) cho sếp dễ thị tẩm nhé!

Trong lập trình (và cả màn hình máy tính), **Gốc tọa độ (0,0) luôn nằm ở góc trên cùng bên trái**. Nó không nằm ở giữa hay ở dưới đáy như trục tọa độ Toán học ngày xưa sếp học đâu nha!

Sếp nhìn cái sa bàn này là hiểu ngay:

```text
       ÂM Y (-1, -2...) 
             |
             |  (GỐC 0,0) ====== CHIỀU DƯƠNG X (Width: 0 đến 7) ======>
             |      X=0   X=1   X=2   X=3   X=4   X=5   X=6   X=7
  ÂM X      - - +-------------------------------------------------+
(-1, -2...)     | [0,0] [1,0] [2,0] [3,0] [4,0] [5,0] [6,0] [7,0] | Y=0
                | [0,1] [1,1] [2,1] [3,1] [4,1] [5,1] [6,1] [7,1] | Y=1
             C  | [0,2] [1,2] [2,2] [3,2] [4,2] [5,2] [6,2] [7,2] | Y=2
             H  | [0,3] [1,3] [2,3] [3,3] [4,3] [5,3] [6,3] [7,3] | Y=3
             I  | [0,4] [1,4] [2,4] [3,4] [4,4] [5,4] [6,4] [7,4] | Y=4
             Ề  +-------------------------------------------------+
             U 
             
             D
             Ư
             Ơ
             N
             G 
             
             Y (Height: 0 đến 4)
             |
             v

```

**🔍 Giải mã sa bàn cho sếp:**

1. **Điểm gốc `[0,0]`:** Nằm chễm chệ ở góc Trái - Trên cùng.


#### Mô tả tường

 Bỏ qua hệ trục tọa độ, em in ngay cho sếp cái "bản vẽ thi công" nhà xưởng 40x20, chuẩn xác đến từng milimet theo đúng đoạn code sếp vừa đưa.

```text
████████████████████████████████████████  <-- Tường trên (Top)
█......................................█
█......................................█
█.........█............................█  <-- Kệ 1 bắt đầu (y=3)
█.........█............................█
█.........█............................█
█.........█............................█
█.........█............................█
█.........█............................█  <-- Kệ 1 kết thúc (y=8)
█......................................█
█........................█.............█  <-- Kệ 2 bắt đầu (y=10)
█........................█.............█
█........................█.............█
█........................█.............█
█........................█.............█
█........................█.............█
█........................█.............█  <-- Kệ 2 kết thúc (y=16)
█......................................█
█......................................█
████████████████████████████████████████  <-- Tường dưới (Bottom)
^         ^              ^             ^
|         |              |             |
Tường   Cột x=10       Cột x=25      Tường

```

#### IMPORTANT: Always performs bounds check to prevent IndexOutOfRangeException.

Sếp cứ tưởng tượng cái mảng `_grid[40, 20]` của sếp là miếng đất **đã có sổ đỏ** chính chủ, kích thước ranh giới rõ ràng.

**Bounds check (kiểm tra ranh giới)** chính là việc sếp thuê một anh bảo vệ đứng canh cửa trước khi cho phép ai đó đặt đồ (`SetObstacle`) hay lấy đồ (`GetCell`) trong miếng đất này.

**Tại sao bắt buộc phải có anh bảo vệ này?**

1. **Đề phòng thằng camera/AI bị "ngáo":** Lỡ hệ thống thị giác AI nhận diện nhầm do chói nắng, nó báo về trung tâm có một chướng ngại vật nằm ở tọa độ trên trời `x = 999` hoặc dưới âm phủ `y = -5`.
2. **Chống "sập tiệm" (Crash):** Nếu sếp không kiểm tra mà nhắm mắt nhét luôn cục chướng ngại vật đó vào `_grid[999, -5]`, C# sẽ lập tức quăng cái lỗi `IndexOutOfRangeException` (Lỗi vượt ranh giới) và **bắn sập toàn bộ phần mềm điều khiển!** Con xe AGV của sếp đang chạy sẽ lập tức đứng hình, lăn đùng ra chết lâm sàng.

Nhờ có lệnh kiểm tra `if (x < 0 || x >= Width || y < 0 || y >= Height)`, anh bảo vệ sẽ thẳng tay "đá đít" mấy cái tọa độ ảo tưởng đó vào thùng rác, giúp xe AGV của sếp vẫn băng băng tiến bước bình an vô sự! 😎

#### <param name="x">Grid X coordinate (0 to Width-1).</param>

 Cái dòng `<param name="x">` này **KHÔNG PHẢI** là dòng khai báo biến cho máy tính chạy. Biến `x` và `y` sếp đã khai báo rành rành ở trong ngoặc `(int x, int y)` rồi, máy tính nó tự biết.

**Vậy sinh ra cái trò `<param>` này làm chi cho rảnh?**

Cái này gọi là **XML Comment** (Chú thích làm màu). Công dụng duy nhất của nó là **nịnh nọt lập trình viên**!

Khi sếp viết cái dòng này, thì nửa năm sau sếp (hoặc thằng đệ của sếp) ở một file khác gõ chữ `SetObstacle(`, cái Visual Studio nó sẽ lập tức nhảy ra một cái bảng nhắc bài vàng vàng (Tooltip) ghi rõ:
*"Ê, điền chữ x vào đây nhé, x là tọa độ từ 0 đến Width-1 nha đại ca!"*

**Chốt hạ:** Viết cái này không làm code chạy nhanh hơn 1 mili-giây nào, nhưng nó giúp sếp sau này không bị "lú" khi xài lại hàm của chính mình. 


#### WorldToGrid()
"Dịch" tọa độ thực tế từ milimet (ví dụ: x=1500mm, y=2000mm) thành tọa độ của mảng (ô số mấy).

1. **Ngoài đời thực (xMm, yMm):** Con xe AGV của sếp nó chạy bằng bánh xe, đo bằng thước dây. Nó báo về trạm: *"Sếp ơi, em đang ở tọa độ X là 2300 mm"*.
2. **Trong code của sếp (gridX, gridY):** Cái bản đồ `_grid` lại là một cái bàn cờ chia theo từng ô (0, 1, 2, 3...).

Làm sao để biết 2300mm nằm ở ô số mấy trên bàn cờ? Chính là nhờ 2 dòng này:

```csharp
int gx = (int)(xMm / CellSizeMm);

```
Giả sử sếp quy định 1 ô dài 500mm (`CellSizeMm = 500`). Máy tính sẽ lấy `2300 / 500 = 4.6`. Ép sang kiểu `(int)` nó sẽ chém mất phần lẻ, còn lại **4**. Tức là: *"À, 2300mm ngoài đời rơi đúng vào cái ô số 4 trên mảng!"*.




