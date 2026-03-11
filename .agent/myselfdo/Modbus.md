# Trình tự xây dựng Modbus

## C++ Hardware Simulator (Slave/Server)

1. Tạo project c++ bằng vs2022
2. Xoá file .cpp mặc định.
3. Tạo folder src và tạo file main.cpp, nhét vào đó
4. Sửa file cmakelist để nó link với main.cpp

```bash
add_executable (hardware-sim "src/main.cpp")
```

```bash
#include <iostream>
#include <modbus.h>

int main() {
	std::cout << "Hardware Sim (Modbus Server) is starting..." << std::endl;
	return 0;
}
```

5. Cài thư viện libmodbus
tạo file vcpkg.json
```bash
{
  "name": "hardware-sim",
  "version": "0.1.0",
  "dependencies": [
    "libmodbus"
  ]
}
```

6. Cài đặt vcpkg

```bash
vcpkg install libmodbus
```
Cài đặt trên windows cực kì vất vả. tốn cả buổi tối chỉ để cài nó mà cũng ko xong.
Cuối cùng phải qua vs2022 hỏi copilot để cài.

## C# Modbus Client (Master/Client)