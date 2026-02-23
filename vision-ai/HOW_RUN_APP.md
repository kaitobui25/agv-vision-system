# How to Run `app.py`

## Install and Activate the Python Environment

Follow the setup guide here:  
[How to Set Up](../docs/03_PYTHON_VIRTUAL_ENV.md##how-to-set-up)

Make sure your virtual environment is activated before running the application.

---

## Run the App

```bash
python vision-ai/app.py
```

---

## Run `camera_server.py`

```bash
python camera/camera_server.py
```

Captured images will be automatically saved to:

```
/camera/images/
```

---

## Check Results from `app.py`

### Using a Web Browser

Open:

```
http://localhost:8000/detect/latest
```

The app will automatically read the latest image captured from the camera.
Each time you press **Enter**, it returns a new detection result.

---

### Using Terminal

```bash
# Auto-reads camera/images/latest.jpg
curl http://localhost:8000/detect/latest
```

---

### Using Swagger Web UI

Open:

```
http://localhost:8000/docs
```

You can test the API directly from the interactive interface.

---

### Using Phone

http://[IP_LAPTOP]:8000/docs

---

## For More Details

Refer to the full documentation:

[README](Readme.md)

====================================================================
Vietnamese
====================================================================

# How run app.py

## Cài môi trường và active python env.

Hướng dẫn cài.
[How to Set Up](../docs/03_PYTHON_VIRTUAL_ENV.md##how-to-set-up)

## Run app

```bash
python vision-ai/app.py
```

## Run camera-server.py

```python
python camera/camera_server.py
```

Khi này ảnh sẽ tự động được lưu vào: /camera/images/

## Check kết quả của app.py

### Bằng trình duyệt

http://localhost:8000/detect/latest

Nó sẽ tự động đọc ảnh được chụp từ camera, và mỗi lần nhấn Enter nó sẽ trả về kết quả phân tích.

### Bằng terminal

```bash
# Auto-reads camera/images/latest.jpg
curl http://localhost:8000/detect/latest
```

### Bằng giao diện web Swagger

http://localhost:8000/docs

### Bằng điện thoại

Chụp ảnh bằng camera điện thoại rồi attach vào web dưới. hihi!!!

http://[IP_LAPTOP]:8000/docs

## Chi tiết hơn thì tham khảo Readme

[README](Readme.md)
