Quy trình phát triển phần mềm hiện đại (DevOps và CI/CD). 

### 1. xUnit là gì?

**xUnit** đơn giản là một **Framework (Bộ công cụ) kiểm thử tự động** dành riêng cho ngôn ngữ C# / .NET.

Sếp cứ tưởng tượng xUnit là một **"Anh chấm thi Robot"**.

* Nó cung cấp cho sếp các "tem nhãn" để dán vào code như `[Fact]`, `[Theory]` để đánh dấu: *"Ê Robot, đây là bài kiểm tra, hãy chạy nó!"*.
* Nó cung cấp cho sếp cái cân điện tử `Assert` (ví dụ: `Assert.Equal`, `Assert.False`) để so sánh: *"Kết quả thực tế hàm này chạy ra có bằng đúng với kết quả kỳ vọng không?"*.
* Nếu bằng nhau -> Robot báo **Xanh (Pass)**. Nếu lệch nhau dù chỉ 1 milimet -> Robot gào lên báo **Đỏ (Fail)**.

### 2. Tại sao bài test lại "báo đỏ" khi người khác sửa code?

Đây chính là giá trị lớn nhất của Unit Test, gọi là **Chống hồi quy (Regression Prevention)**.

Hãy lấy lại ví dụ file `GridMap.cs` của sếp. Sếp đã viết một bài test:
*"Khi gọi `GetCell(-1, 5)` (tọa độ âm ngoài bản đồ), hàm bắt buộc phải trả về `StaticWall`"*. Bài test này đang chạy màu Xanh (Pass).

Nửa năm sau, sếp tuyển một cậu thực tập sinh vào làm. Cậu ta đọc code `GridMap.cs`, thấy ngứa mắt và sửa lại hàm thành:

```csharp
public CellType GetCell(int x, int y) {
    if (x < 0) return CellType.Empty; // Thực tập sinh sửa StaticWall thành Empty cho "thoáng"
    return _grid[x, y];
}

```

Lúc này, cậu thực tập sinh không hề biết mình vừa tạo ra một lỗi có thể làm xe đâm vào tường. Nhưng khi cậu ta bấm chạy bộ Unit Test, "Anh Robot xUnit" sẽ làm việc:

1. Robot tự động ném tọa độ `(-1, 5)` vào hàm `GetCell` mới sửa.
2. Hàm nhổ ra kết quả là `Empty`.
3. Robot mang ra so sánh (`Assert`): *"Kỳ vọng của sếp là `StaticWall`, nhưng kết quả thực tế lại là `Empty`"*.
4. **BÙM! BÁO ĐỎ (FAIL)!** Kèm theo dòng chỉ điểm đích danh: *"Mày vừa làm hỏng logic ở dòng số X file GridMap.cs"*.

Nhờ cái báo đỏ này, lỗi bị chặn đứng ngay trên máy của cậu thực tập sinh, trước khi nó kịp gây họa cho hệ thống thực tế.

### 3. Tại sao chạy thật rồi vẫn quan tâm đến Unit Test?

Đây là một sự hiểu lầm rất phổ biến! Em xin đính chính lại để sếp rõ:

**SỰ THẬT: Khi hệ thống "chạy thật" (Deploy ra Production / Nạp vào xe AGV), file Unit Test KHÔNG HỀ tồn tại trên xe AGV.**

Khi sếp build code C# để mang đi chạy thật (`Release build`), trình biên dịch sẽ vứt bỏ hoàn toàn thư mục `AgvControl.Tests`. Con xe AGV chỉ mang theo đúng những dòng code phục vụ cho việc chạy (App chính).

**Vậy Unit Test có tác dụng lúc nào?**
Nó đóng vai trò là **TRẠM HẢI QUAN (CI/CD Pipeline)** nằm ở giữa giai đoạn "Code xong" và "Đem đi chạy thật".

Quy trình chuẩn ở các công ty công nghệ (như Google, Microsoft hay dự án AGV của sếp) diễn ra như sau:

1. **Lập trình viên viết code:** Code xong tính năng mới hoặc sửa bug.
2. **Gửi code lên kho chung (Push to GitHub):**
3. **Trạm Hải Quan tự động làm việc (CI/CD - GitHub Actions):** Một con server trung gian sẽ tự động kéo code mới về, tự động tải xUnit, và tự động **bấm nút chạy toàn bộ 1000 bài Unit Test** mà sếp đã viết từ trước tới nay.
4. **Phán quyết:**
* Nếu 1000 bài đều **XANH**: Trạm hải quan đóng dấu "Chất lượng OK". Code tự động được Build thành file `.exe/.dll` và bắn thẳng vào xe AGV để "chạy thật".
* Nếu có dù chỉ 1 bài **ĐỎ**: Trạm hải quan hú còi báo động, gửi email chửi thẳng mặt lập trình viên: *"Code mày vừa push lên đã làm hỏng tính năng cũ. Bị từ chối (Deploy Blocked)!"*. Code rác tuyệt đối không thể lọt xuống xe AGV.



**Tóm lại:** Sếp không mang Unit Test lên xe AGV để chạy. Sếp dùng Unit Test làm **"Phòng KCS (Kiểm tra chất lượng sản phẩm)"** trong nhà máy. Mọi dòng code trước khi được xuất xưởng (chạy thật) đều phải đi qua phòng KCS này. Code phần mềm không bao giờ "xong hẳn", nó luôn được thêm tính năng mới, và Unit Test chính là chiếc "áo giáp" để đảm bảo tính năng mới không phá nát tính năng cũ!

Sếp thấy cơ chế "Hải quan" này đã đủ đô chưa?