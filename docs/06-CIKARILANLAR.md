# Çıkarılan Özellikler

Konuşulup bilerek plana alınmayan özellikler. Tekrar gündeme gelirse önce buradaki neden okunur.

## Her monitörde geçerli

| Özellik | Neden çıkarıldı |
|---|---|
| Firmware değiştirme / "unlock" | Monitörü kalıcı bozabilir. Sadece okuma (v3, Keşif) kaldı. |
| Saate göre otomatik parlaklık | Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişmeli (göz yorgunluğu, gereksiz monitör belleği yazması). |
| Ortam ışığıyla otomatik parlaklık | Aynı neden; ayrıca telefonun sürekli yanında olması gerekir. |
| Varlık algılama (uzaklaşınca karartma) | Sık değişiklik yapar; Windows zaten boşta kalınca ekranı kapatıyor. |
| Uygulamaya göre otomatik preset | Her uygulama geçişinde mod değişir. Yerine elle seçilen presetler ve modlar var. |
| Otomatik içerik Hz'i (film açılınca 72 Hz) | Her geçişte ekran kararır. Yerine elle seçilen içerik Hz'leri (v2) var. |
| Mavi ışık açılınca arka ışığı artırarak telafi etme | Kural 1: parlaklık kendiliğinden değişmez. |
| İki boyutlu homojenlik düzeltme katmanı | Karmaşık, kazancı az, diğer ayarlarla çakışır. **Not:** Bu monitörde (HP P21b G4) gerçek bir üst-alt farkı olduğu için sadece **dikey** hali geri geldi (Modül 4). |
| CRT simülasyonu | 60–75 Hz'de titreşir. |
| Yüksek çözünürlük sinyali testi | Çalışsa bile DLDSR'den kötü sonuç verir. |
| Takılı piksel düzeltici | Çoğu zaman işe yaramaz. |
| Black Frame Insertion | 120 Hz altında çok titreşir. |
| Kullanım istatistikleri | Pratik faydası az. |
| Telefonla gecikme ölçümü | Önerildi, seçilmedi. |
| Pixel'i HDMI ile monitöre bağlama | Kullanıcı kullanmayacak. |
| Scaler kartı veya panel değiştirme | Donanım; maliyeti ve riski kazancına değmez. |

## HP P21b G4'e özel

| Özellik | Neden çıkarıldı |
|---|---|
| Gizli FreeSync testi | NVIDIA HDMI üzerinden FreeSync desteklemiyor; monitörün HDMI'ı 1.4, dikey aralığı 50–60 Hz. |
| USB kontrol cihazı kontrolü | Monitörde USB yok. |
| 80 Hz hedefi | Hem pixel clock'u (170 MHz) hem yatay frekansı (80 kHz) aşıyor; ölçülen gerçek sınır 82.8–83.9 kHz. Bulucunun üst sınırı 75 Hz. |
| Titreşimsiz karartma modu ve PWM testi | Monitör flicker-free; ilk gün testinde 1/1000 s'de parlaklık 0'da bile PWM bandı yok. Profilde PWM olan monitörler için genel fikirde kalıyor. |
| Overdrive karşılaştırması | OSD'de overdrive ayarı yok; T804 sadece ghosting ölçer. |
