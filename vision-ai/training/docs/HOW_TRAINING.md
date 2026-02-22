# How to train AI model

Xác định những object muốn detect
ví dụ:
truck
fan
bolling-pin

## Chụp ảnh

### Tiêu chí ảnh

Chụp nhiều góc độ, theo góc nhìn của camera
Các khung giờ khác nhau, độ sáng khác nhau, xa gần khác.
Vật này che vật khác, có nhiều vật trong 1 khung hình
Bối cảnh sau vật hỗn loạn phức tạp, giống với thực tế trong xưởng.

### Setting camera điện thoại

Tỉ lệ khung hình 16x9 1920x1080

> YOLO được thiết kế xử lý ảnh vuông — 640x640, 1280x1280. Khi nhận ảnh không vuông, nó letterbox (thêm dải đen) để thành vuông trước khi inference. Với training data, ảnh 16:9 sẽ được Roboflow xử lý tự động — nhưng nếu bạn có thể chọn thì 4:3 (1280x960) hoặc 1:1 tốt hơn 16:9 cho YOLO.

## Gán tên class cho từng object

Dùng roboflow.com để thực hiện labeling

### Up ảnh và tạo Dataset

1. New project > Object Detection
2. Upload image > save and continue
3. Lúc này image se nằm ở Unassigned
   ![ERD](/docs/images/img_001.JPG)

4. Chọn Label Myself
5. Chọn Box Prompting
   ![ERD](/docs/images/img_002.JPG)

6. Đăt tên cho class
7. Nhấn mũi tên qua phải rồi Vẽ tiếp cho các vật còn lại
8. Làm y chang vậy cho khoảng 10 tấm hơn gì đó.
9. Trở lại thư mục Annotate, sẽ thấy những tấm đã được annotate
    > trong ảnh là 1 tấm đã được annotate, 9 tấm chưa
    > ![ERD](/docs/images/img_003.JPG)
10. Click vào đống ảnh đó và chọn vào mục Annotated
    ![ERD](/docs/images/img_004.JPG)
11. Chọn Add...image to Dataset
12. Trong thư mục Annotate lại chọn vào đống ảnh còn lại trong Annotating
    ![ERD](/docs/images/img_005.JPG)
13. Chọn ... để di chuyển 9 tấm ảnh dó vào lại cột Unassigned
    ![ERD](/docs/images/img_006.JPG)
    đã move
    ![ERD](/docs/images/img_007.JPG)
14. Chọn vào 9 tấm đó để Auto Labeling
    ![ERD](/docs/images/img_008.JPG)
15. Chọn instant để giúp tự động phát hiện class, instant này được tạo ra từ bước 11 khi add image to dataset (maybe :) )
    Chọn xong thì check xem đã đủ 3 class theo mong muốn chưa
    ![ERD](/docs/images/img_009.JPG)
16. Nhấn Auto General
17. Nó sẽ hiện ra 4 tấm mà dựa theo cái instant đã chọn , nó phán đoán các vật trong ảnh.
    Cứ click next để xem nó chọn có đúng không.
    ![ERD](/docs/images/img_010.JPG)
18. Nếu thấy ok rồi thì click vào Auto Label with this Model
19. Nó sẽ chạy tự động để vẽ vật cho mình hết 9 tấm còn lại.
    ![ERD](/docs/images/img_011.JPG)
20. Sau khi chạy xong, nhiệm vụ của mình là check lại xem nó vẽ đúng không
    ![ERD](/docs/images/img_012.JPG)
21. Click vào từng tấm ảnh và nhấn Approve nếu đồng ý những gì nó vẽ.
22. Add Approved to Dataset
    ![ERD](/docs/images/img_013.JPG)
23. Kết quả thành công 10 ảnh trong Dataset
    ![ERD](/docs/images/img_014.JPG)

## Version

Giờ đã có Dataset, tiếp tục lấy ra thư viện phù hợp với model để training, (ở đây là yolo)

1. Chọn Versions
2. Đặt tên version
3. Chọn phân bổ image cho Train/Valid/Test
   Train (ảnh để học)
   Validation (ảnh để kiểm tra trong lúc học)
   Test (ảnh để thi thật)
   Chọn Rebalance để phân phối cho 3 mục đich trên
   ![ERD](/docs/images/img_015.JPG)
4. Resize chọn Fit black edges in 640x640
   ![ERD](/docs/images/img_016.JPG)
5. Add thêm 1 vài augmentation để nó tự động tạo ra thêm nhiều biến thể dự trên 10 tấm ảnh đã add vào.
   a. Flip > Horizontal
   b. Brightness -25 +25
   c. Blur 1px
6. Chọn số lượng ảnh muốn nhân lên. x3,x10,x20
7. Create
8. Download Dataset
9. YoLov11
10. Show download code > Continue
    ![ERD](/docs/images/img_017.JPG)
11. Copy đoạn mã python để đem vào google colap để training
    ![ERD](/docs/images/img_018.JPG)

## Training

Training bằng colab.research.google.com

1. New notebook in drive
2. Chuyển sao T4 Gpu > nhấn connect
   ![ERD](/docs/images/img_019.JPG)
3. install ultralytics và roboflow trước

```python
!pip install ultralytics roboflow
```

4. Restart runtime
5. Chạy code roboflow để load data
   ![ERD](/docs/images/img_020.JPG)
   sau khi chạy xong thì....
   ![ERD](/docs/images/img_021.JPG)
6. Kiểm tra xem có file yaml chưa. Có rồi thì yên tâm đi train

```python
!ls {dataset.location}
```

7. Bắt đầu train

```python
from ultralytics import YOLO
model = YOLO("yolo11s.pt")  # pretrained, fine-tune tiếp
model.train(
    data=f"{dataset.location}/data.yaml",
    epochs=50,
    imgsz=640,
    batch=16,
    patience=10,    # early stopping
    project="agv_warehouse",
    name="v1"
)
```

Nếu ko có lỗi thì...
![ERD](/docs/images/img_022.JPG)

8. Sau khi train xong thì file best.pt được lưu vào đây
   ![ERD](/docs/images/img_023.JPG)

9. Tải về
   ![ERD](/docs/images/img_024.JPG)

## Chạy thử

1. Copy file best.pt vừa tải về vào folder vision-ai
2. Trong file app.py đổi tên model yolov11s -> best.pt
   ![ERD](/docs/images/img_025.JPG)

Thành công!!
