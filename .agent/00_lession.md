# Bài Học Kinh Nghiệm (Lessons Learned)

> File này ghi lại các bài học rút ra trong quá trình phát triển.
> **Luôn tham khảo trước khi code** để tránh lặp lại sai lầm.

---

## #1 — Windows: `localhost` vs `127.0.0.1` (IPv6 Trap)

**Ngày:** 2026-03-05

**Vấn đề:**
Gọi HTTP từ C# đến Python server mất ~2.3s, trong khi YOLO inference chỉ ~250ms. Chênh lệch ~2s không giải thích được.

**Nguyên nhân gốc:**
Dùng `http://localhost:8000` trong `appsettings.json`. Trên Windows, `localhost` được resolve theo thứ tự:
1. Windows thử `::1` (IPv6) trước
2. Python server (`0.0.0.0:8000`) chỉ nghe IPv4 → IPv6 bị refuse
3. Windows **chờ timeout ~2 giây** rồi mới fallback sang IPv4 `127.0.0.1`
4. Kết nối thành công, nhưng đã mất 2s vô ích

**Cách fix:**
Dùng `127.0.0.1` thay vì `localhost` ở mọi nơi kết nối local:
- `appsettings.json` → `"BaseUrl": "http://127.0.0.1:8000"`
- `db_logger.py` → `'host': '127.0.0.1'`

**Quy tắc chung:**
- Trên Windows, **LUÔN dùng `127.0.0.1`** thay vì `localhost` cho kết nối local
- Hoặc đảm bảo server listen trên cả IPv4 lẫn IPv6
- Nếu thấy latency chênh lệch ~2s so với thực tế → nghi ngờ DNS/IPv6 fallback ngay

**Sai lầm của agent:**
- Đổ lỗi cho cold start, DB logging, HttpClient warmup mà không kiểm tra network layer cơ bản
- Không nghĩ đến sự khác biệt giữa IPv4/IPv6 resolution trên Windows
- **Bài học: Khi debug latency, kiểm tra từ tầng thấp nhất (network) trước, rồi mới lên application layer**

---

## #2 — Debug Latency: Đi Từ Tầng Thấp Lên

**Ngày:** 2026-03-05

**Quy trình debug latency đúng cách (theo thứ tự ưu tiên):**

1. **Network layer** — DNS resolution, IPv4/IPv6, TCP handshake, proxy
2. **Connection layer** — connection pooling, keepalive, TLS negotiation
3. **Protocol layer** — HTTP version, request/response serialization
4. **Application layer** — business logic, DB queries, file I/O

**ĐỪNG** nhảy thẳng vào application layer và đoán mò. Đo lường trước, kết luận sau.

---

## #3 — Bounds Check Phải Nhất Quán Trên Mọi Hàm Public

**Ngày:** 2026-03-06

**Vấn đề:**
`GridMap.cs` có 3 hàm public truy cập `_grid[x, y]`: `GetCell`, `SetObstacle`, `IsWalkable`. Hai hàm sau có bounds check đầy đủ, nhưng `GetCell` lại bỏ quên → `IndexOutOfRangeException` khi A* dò ô sát mép bản đồ.

**Tại sao nguy hiểm:**
- Tạo **ảo giác an toàn**: Dev thấy `SetObstacle` và `IsWalkable` đã có guard → tự tin gọi `GetCell` mà không kiểm tra
- Trong hệ thống AGV thực tế, exception = mất kiểm soát xe giữa nhà máy
- Bug chỉ xuất hiện ở edge case (sát mép bản đồ) → khó phát hiện khi test

**Cách fix — Pattern "Implicit Boundary Wall":**
- `GetCell(x, y)` → trả về `CellType.StaticWall` nếu ngoài biên (coi như tường cứng)
- `SetObstacle(x, y)` → `return false` nếu ngoài biên (bỏ qua im lặng)
- `IsWalkable(x, y)` → `return false` nếu ngoài biên (không đi được)

**Quy tắc chung:**
- Khi nhiều hàm public cùng truy cập một data structure → **TẤT CẢ** phải có guard nhất quán, không được bỏ sót
- Trong pathfinding/robotics: ngoài bản đồ = tường cứng, **KHÔNG** throw exception
- Sau khi viết guard cho hàm đầu tiên, **grep tất cả hàm** truy cập cùng data structure và kiểm tra lại
---

## #4 — Agent Phải Tự Tư Duy: Chỗ Nào Cần Unit Test Thì Chỉ Ra

**Ngày:** 2026-03-06

**Sai lầm của agent:**
Khi viết/sửa code trong project AGV, agent chỉ tập trung vào logic rồi dừng lại. Không bao giờ chủ động chỉ ra: "Chỗ này nên có unit test vì lý do X". Phải đợi user hỏi mới phân tích — đó là tư duy bị động, không xứng pro engineer.

**Bài học cốt lõi:**
Agent không phải code monkey gõ theo lệnh. Agent là **pro engineer** — phải có tư duy toàn diện. Khi viết bất kỳ đoạn code nào, phải tự hỏi:
- Code này có edge case nào nguy hiểm không?
- Nếu ai đó sửa code này trong tương lai, họ có thể phá logic không?
- Chỗ này có đáng để bảo vệ bằng unit test không?

Nếu câu trả lời là "có" → **PHẢI chủ động đề xuất**, không chờ user nhắc.

**Áp dụng cụ thể cho project AGV — những chỗ PHẢI có unit test:**
- Pathfinding logic (A*, bounds check, neighbor expansion)
- Coordinate conversion (WorldToGrid — sai = xe đi nhầm ô)
- Dynamic obstacle lifecycle (set → clear → verify)
- Modbus command builder (sai register = xe mất kiểm soát)

**Quy tắc cho agent:**
- Mỗi khi viết/sửa code → tự đánh giá: "Chỗ này cần test không?" → Nếu cần thì **nói ra**
- Không chỉ fix bug mới nghĩ đến test — viết code MỚI cũng phải nghĩ
- Tư duy chủ động (proactive) là tiêu chuẩn tối thiểu, không phải bonus
