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
