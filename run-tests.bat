@echo off
chcp 65001 >nul
echo ========================================================
echo   ScanToOrder - Unit Test và Code Coverage Automation
echo ========================================================
echo.

echo [1/3] Dọn dẹp rác từ báo cáo dữ liệu (TestResults) cũ...
for /d /r . %%d in (TestResults) do @if exist "%%d" rd /s /q "%%d" 2>nul
if exist "CodeCoverageReport" rd /s /q "CodeCoverageReport" 2>nul

echo [2/3] Bắt đầu chạy lại toàn bộ Unit Tests trong hệ thống...
dotnet test ScanToOrder_BE_SEP490.sln --collect:"XPlat Code Coverage"

echo.
echo [3/3] Đang sinh bảng dữ liệu HTML (ReportGenerator)...
reportgenerator -reports:"*\TestResults\*\coverage.cobertura.xml" -targetdir:"CodeCoverageReport" -reporttypes:Html

echo.
echo ========================================================
echo   Hoàn tất quy trình! 
echo   Hãy mở file: CodeCoverageReport\index.html trên web.
echo ========================================================
pause
