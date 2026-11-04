@echo off
rem MonitorTuner kurtarma dosyasi (T104). Ayrintilar: GORUNTU-GELMEZSE.txt (ayni klasor).
rem GPU renk tablosunu sifirlar, GdiIcmGammaRange degerini eski haline, NVIDIA dithering ve
rem canliligi varsayilana dondurur. Hz, monitor ayarlari (DDC) ve NVIDIA ozel cozunurlukleri degismez.
rem GdiIcmGammaRange degismisse Windows yonetici onayi sorar.
setlocal
set "APP=%~dp0MonitorTuner.App.exe"
if not exist "%APP%" (
  echo MonitorTuner.App.exe bulunamadi: "%APP%"
  echo Bu dosyayi MonitorTuner paketinin klasorunden calistir.
  pause
  exit /b 1
)
start "" /wait "%APP%" --kurtarma
endlocal
