# Kurtarma (T104)

## Görüntü gelmezse (kolaydan zora)

1. **15 saniye bekle.** Yeni Hz'i onaylamazsan uygulama önceki moda kendisi döner. NVIDIA panelindeki test de kendi kendine döner.
2. **HDMI kablosunu başka bir çıkışa tak:** 3090'ın HDMI çıkışı ya da 3080'in bir DP çıkışı (DP→HDMI kabloyla). NVIDIA özel çözünürlüğü çıkışa bağlı olduğu için orada 60 Hz gelir. Sonra sorunlu özel çözünürlüğü sil:
   1. Masaüstüne sağ tık → **NVIDIA Denetim Masası** → **Ekran** → **Çözünürlüğü değiştir**.
   2. Monitörü seç → **Özelleştir...** → listede sorunlu özel çözünürlüğün kutusunu seç → **Kaldır** → **Tamam**.
   3. Orijinal 60 Hz (1920×1080) mod dokunulmadan durur; silme.
   4. Kabloyu 3080'in HDMI çıkışına geri tak. Görüntü 60 Hz'de gelir.
3. **Güvenli mod + kurtarma dosyası.** Güvenli modda Windows temel ekran sürücüsünü kullanır, görüntü gelir.
   - Görüntü varken: **Ayarlar** → **Sistem** → **Kurtarma** → **Gelişmiş başlangıç: Şimdi yeniden başlat** → **Sorun gider** → **Gelişmiş seçenekler** → **Başlangıç Ayarları** → **Yeniden başlat** → **4** (Güvenli mod).
   - Hiç görüntü yoksa önce 2. adımı dene; ekran bu menüleri de başka çıkışta gösterir.
   - Güvenli modda NVIDIA sürücüsü yüklenmez: kurtarma dosyası sadece `GdiIcmGammaRange`'i döndürebilir, dithering ve canlılık için normal modda bir kez daha çalıştır. GPU renk tablosu Windows yeniden başlayınca zaten sıfırlanır.

## Kurtarma dosyası ne yapar

`MonitorTuner-Kurtarma.exe` (CI paketinde `kurtarma` klasörü). Çift tıkla; uygulamanın açık olmasına gerek yok.

- Masaüstündeki her ekranda **GPU renk tablosunu sıfırlar** (birim tablo: tablo yokmuş gibi). Uygulama çöktüyse ekranda kalan koyu ya da sıcak tablo gider.
- **Dithering ve canlılığı NVIDIA varsayılanına** alır (dithering sürücüye bırakılır, canlılık %50).
- **`GdiIcmGammaRange`'i eski hâline** döndürür: uygulama ilk açıldığında yedeklenen değer. Yönetici onayı penceresi çıkarsa **Evet** de. Yedek yoksa uygulama bu değeri hiç değiştirmemiştir, dokunulmaz.
- Hz'e, parlaklığa, monitörün (DDC) ayarlarına, NVIDIA özel çözünürlüklerine ve uygulamanın kayıtlı ayarlarına **dokunmaz**.
- Her satırda `[tamam]` ya da `[YAPILAMADI]` ve ne yapman gerektiği yazar. Tekrar çalıştırmak zararsızdır: zaten varsayılan olan yazılmaz.
