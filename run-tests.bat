@echo off
echo ========================================================
echo   ScanToOrder - Unit Test and Code Coverage Automation
echo ========================================================
echo.

echo [1/3] Don dep rac tu bao cao du lieu (TestResults) cu...
for /d /r . %%d in (TestResults) do @if exist "%%d" rd /s /q "%%d" 2>nul
if exist "CodeCoverageReport" rd /s /q "CodeCoverageReport" 2>nul

echo [2/3] Bat dau chay lai toan bo Unit Tests trong he thong...
dotnet test ScanToOrder_BE_SEP490.sln --collect:"XPlat Code Coverage"

echo.
echo [3/3] Dang sinh bang du lieu HTML (ReportGenerator)...
reportgenerator -reports:"**\TestResults\*\coverage.cobertura.xml" -targetdir:"CodeCoverageReport" -reporttypes:Html

echo.
echo ========================================================
echo   Hoan tat quy trinh! 
echo   Hay mo file: CodeCoverageReport\index.html de xem.
echo ========================================================
pause
