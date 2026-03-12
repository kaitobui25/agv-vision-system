Phân Tích Kỹ Lưỡng: 10 Cải Tiến Chính
1. ✅ WSAStartup/WSACleanup — Bắt Buộc Trên Windows
Mức độ quan trọng: ⭐⭐⭐⭐⭐ (Cực kỳ cần thiết)

```cpp
// main_old.cpp — THIẾU
// (không có)

// main.cpp — CÓ
#ifdef _WIN32
    WSADATA wsaData;
    int wsaInit = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (wsaInit != 0) {
        printf("[hardware-sim] ERROR: WSAStartup failed: %d\n", wsaInit);
        return 1;
    }
#endif
```
Tại sao cần thiết:
•	Trên Windows, <winsock2.h> yêu cầu bắt buộc gọi WSAStartup() trước khi dùng bất kỳ socket nào
•	Nếu không gọi → các hàm socket (socket creation, connect, listen) sẽ trả về lỗi, hoặc hoạt động không xác định
•	libmodbus dùng socket phía dưới → nếu WSA chưa initialize → modbus_new_tcp() có thể fail im lặng hoặc crash
•	File main_old.cpp có #include <winsock2.h> nhưng KHÔNG gọi WSAStartup() = Lỗi tai hại trên Windows
Phát hiện lỗi: File old chỉ chạy được trên Linux (hoặc Windows đã may mắn có ai đó khác initialize trước).
---

2. ✅ Socket Accept — Thay Đổi Cơ Bản (Platform-specific logic)
Mức độ quan trọng: ⭐⭐⭐⭐⭐ (Tối quan trọng)
main_old.cpp — Vấn đề Cổ Điển:

```cpp
while (running && client_socket == -1) {
    fd_set rfds;
    FD_ZERO(&rfds);
    FD_SET(server_socket, &rfds);

    struct timeval tv;
    tv.tv_sec = 1;
    tv.tv_usec = 0;

    int rc = select(server_socket + 1, &rfds, nullptr, nullptr, &tv);
    if (rc > 0) {
        client_socket = -1;
        if (modbus_tcp_accept(ctx, &client_socket) == -1) {  // ❌ BUG
            printf("[hardware-sim] ERROR: Accept failed: %s\n",
                   modbus_strerror(errno));
            client_socket = -1;
        }
    }
}
```
Các vấn đề:

modbus_tcp_accept() không xử lý error correctly	Socket bị hỏng, 
không thể accept thêm client	⭐⭐⭐⭐⭐

Dùng select() + thực hiện hành động không nguyên tử	
Race condition (edge case hiếm nhưng nguy hiểm)	⭐⭐⭐

Nếu modbus_tcp_accept() fail → listening socket bị sửa đổi	
Session tiếp theo không thể accept (cạn kiệt port)	⭐⭐⭐⭐

Không có platform-specific error handling (Windows vs Linux khác)	
Hard to debug, errno không đáng tin trên Windows	⭐⭐⭐⭐

main.cpp — Sửa Đúng Cách:

```cpp
#ifdef _WIN32
    sockaddr_in client_addr;
    int addrlen = sizeof(client_addr);
    SOCKET accepted = accept((SOCKET)server_socket, (sockaddr*)&client_addr, &addrlen);
    if (accepted == INVALID_SOCKET) {
        int wsaerr = WSAGetLastError();  // ✅ Dùng WSAGetLastError() thay vì errno
        if (wsaerr == WSAEINTR || wsaerr == WSAEWOULDBLOCK || wsaerr == WSAETIMEDOUT) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            continue;
        }
        printf("[hardware-sim] ERROR: Accept failed (winsock): wsa=%d\n", wsaerr);
        client_socket = -1;
        break;
    }
    client_socket = static_cast<int>(accepted);
#else
    // ... Linux version ...
#endif

// ✅ Sau khi accept thành công, NHẤT ĐỊNH phải gọi
modbus_set_socket(ctx, client_socket);
```
Lợi ích:
•	Dùng accept() trực tiếp thay vì modbus_tcp_accept() → Giữ listening socket nguyên vẹn
•	modbus_set_socket(ctx, client_socket) → Bảo libmodbus sử dụng socket mới
•	Platform-specific error codes (Windows: WSAGetLastError(), Linux: errno)
•	Tránh việc listening socket bị "ăn cắp" bởi libmodbus

3. ✅ Socket Cleanup — Đóng Socket Đúng Cách
Mức độ quan trọng: ⭐⭐⭐⭐ (Rất cần)
main_old.cpp:

```cpp
// Không đóng client_socket ở cuối session
printf("[hardware-sim] Session ended. Waiting for reconnection...\n");
// ❌ Vòng lặp quay lại accept session mới → listening socket bị vứt
```

main.cpp:

```cpp
// ✅ Sau khi client ngắt kết nối
if (client_socket != -1) {
#ifdef _WIN32
    closesocket(client_socket);
#else
    close(client_socket);
#endif
    // Đặt lại context socket về listening socket
    modbus_set_socket(ctx, server_socket);
    client_socket = -1;
}

printf("[hardware-sim] Session ended. Waiting for reconnection...\n");

```

Tại sao quan trọng:
•	Nếu không đóng client_socket → Hệ điều hành giữ cổng mở (TIME_WAIT state) → Session tiếp theo không thể bind() lại cổng đó (Address already in use)
•	Cứ cắm thêm client → cứ lộn thêm một socket zombie → Sau vài client, server "cạn kiệt port"
•	Trên Windows, timeout TIME_WAIT là 4 phút → sửa code + compile + test lại → vẫn lỗi vì port chưa giải phóng

4. ✅ Modbus Context Socket Management
Mức độ quan trọng: ⭐⭐⭐⭐
main_old.cpp:

```cpp
int client_socket = -1;
if (modbus_tcp_accept(ctx, &client_socket) == -1) { ... }
// ❌ Không rõ modbus_tcp_accept() có sửa ctx->s không
```

main.cpp:
```cpp
// ✅ Rõ ràng quản lý socket trước/sau
modbus_set_socket(ctx, server_socket);  // Trước listen

// ... accept logic ...

modbus_set_socket(ctx, client_socket);  // Sau accept

// ... client session ...

modbus_set_socket(ctx, server_socket);  // Sau disconnect
```
Lợi ích:
•	Ngoài cùng rõ ràng được modbus_tcp_accept() tôi làm gì
•	Không bị "bất ngờ" khi libmodbus sửa context socket
•	Dễ debug nếu có socket drift
---

5. ✅ Error Handling — Phân Loại Lỗi Tạm Thời vs Lỗi Thực
Mức độ quan trọng: ⭐⭐⭐⭐
main_old.cpp:
```cpp
// ❌ KHÔNG phân biệt lỗi tạm thời vs lỗi thực
if (rc == -1) {
    printf("[hardware-sim] ERROR: Accept failed: %s\n",
           modbus_strerror(errno));
    client_socket = -1;
}
// ❌ Nếu lỗi là EWOULDBLOCK → vẫn coi là lỗi nghiêm trọng
```

Mỗi lần accept fail → in lỗi → loop lặp ngay → Nếu accept fail do EINTR (signal) → retry liền mà không delay → CPU 100%

main.cpp:
```cpp
if (wsaerr == WSAEINTR || wsaerr == WSAEWOULDBLOCK || wsaerr == WSAETIMEDOUT) {
    std::this_thread::sleep_for(std::chrono::milliseconds(100));  // ✅ Delay trước retry
    continue;
}


Lợi ích:
•	Tạm thời (EINTR, EWOULDBLOCK) → sleep rồi retry
•	Thực (EIO, EBADF) → thoát vòng lặp accept, báo lỗi
•	Tránh busy-wait loop (CPU 100%)

6. ✅ Platform-Specific Error Codes
Mức độ quan trọng: ⭐⭐⭐⭐
main_old.cpp:
```cpp
if (errno != ETIMEDOUT && errno != EAGAIN) {
    printf("[hardware-sim] Client disconnected: %s\n", modbus_strerror(errno));
    // ❌ Trên Windows, errno là cái của POSIX, nhưng Windows socket error là WSAError
}
```
Ngoài Linux, errno không đáng tin cậy cho socket operations trên Windows.

main.cpp:
```cpp
#ifdef _WIN32
    && errno != WSAETIMEDOUT && errno != WSAEWOULDBLOCK
#endif
```
Đầy đủ định nghĩa cho cả hai nền tảng.

7. ✅ Client Reconnection Loop — Điều Chỉnh Retry
Mức độ quan trọng: ⭐⭐⭐
main_old.cpp:
```cpp
while (running && client_socket == -1) {
    // ... select + modbus_tcp_accept ...
}
```
Nếu accept liên tục fail → loop quay đủ nhanh có thể CPU spike

main.cpp:
```cpp
if (errnum == EINTR || errnum == EAGAIN || errnum == ETIMEDOUT) {
    std::this_thread::sleep_for(std::chrono::milliseconds(100));  // ✅ Throttle
    continue;
}
```
8. ✅ WSACleanup Trên Windows
Mức độ quan trọng: ⭐⭐⭐⭐ (Vệ sinh tài nguyên)
main_old.cpp:
```cpp
// ❌ Không gọi WSACleanup()
// Trên Windows, nếu không gọi WSACleanup() → socket resources không được giải phóng
// Có thể gây rò rỉ tài nguyên, đặc biệt nếu server chạy lâu hoặc restart nhiều lần
```

main.cpp:
```cpp
#ifdef _WIN32
    WSACleanup();  // ✅ Giải phóng tài nguyên Winsock
#endif
```
Tuy lỗi này thường không gây ra vấn đề khi server tắt (OS tự cleanup), nhưng là best practice để tránh resource leak khi reboot hoặc restart service.

9. ✅ Error Recovery Path — Client Accept Failure
Mức độ quan trọng: ⭐⭐⭐
main_old.cpp:
```cpp
if (modbus_tcp_accept(ctx, &client_socket) == -1) {
    printf("[hardware-sim] ERROR: Accept failed: %s\n",
           modbus_strerror(errno));
    client_socket = -1;
}
// ❌ Nếu accept fail → client_socket = -1 → loop quay lại accept
// Nhưng listening socket có thể đã bị sửa đổi bởi modbus_tcp_accept()
// → Session tiếp theo có thể fail ngay lập tức
```

main.cpp:
```cpp
if (accepted == INVALID_SOCKET) {
    // ... xác định lỗi là tạm thời hay thực ...
    if (is_transient_error) {
        continue;  // retry
    } else {
        printf("[hardware-sim] ERROR: Accept failed (winsock): wsa=%d\n", wsaerr);
        client_socket = -1;
        break;  // ✅ Thoát vòng lặp, quay về wait for connection
    }
}
```







