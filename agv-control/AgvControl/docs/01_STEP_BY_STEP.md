# Triển khai hệ thống AGV Control

## NuGet Packages

VS2022 (GUI)
```
Chuột phải vào AgvControl project trong Solution Explorer
Chọn Manage NuGet Packages...
Tab Browse → search NModbus → chọn version 3.0.81 → Install
Lặp lại với Npgsql version 10.0.1
```
Chuột phải vào project và chọn Rebuild , 2 gói đó sẽ được thêm vào AgvControl.csproj

```xml
  <ItemGroup>
    <PackageReference Include="NModbus" Version="3.0.81" />
    <PackageReference Include="Npgsql" Version="10.0.1" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.6.2" />
  </ItemGroup>
```
