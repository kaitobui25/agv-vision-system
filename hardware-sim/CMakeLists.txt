# CMakeList.txt : CMake project for hardware-sim
cmake_minimum_required (VERSION 3.8)

# Enable Hot Reload for MSVC compilers if supported.
if (POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()

project ("hardware-sim")

# Add source to this project's executable.
add_executable (hardware-sim "src/main.cpp")

if (CMAKE_VERSION VERSION_GREATER 3.12)
  set_property(TARGET hardware-sim PROPERTY CXX_STANDARD 20)
endif()

# ---------------------------------------------------------------------------
# libmodbus — installed via vcpkg manifest mode (see vcpkg.json)
# ---------------------------------------------------------------------------
find_package(libmodbus CONFIG REQUIRED)
target_link_libraries(hardware-sim PRIVATE modbus)

# Windows socket library — required for Modbus TCP
if (WIN32)
  target_link_libraries(hardware-sim PRIVATE ws2_32)
endif()
