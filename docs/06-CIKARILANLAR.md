# Çıkarılan Özellikler

Konuşulup bilerek plana alınmayan özellikler. Tekrar gündeme gelirse önce buradaki neden okunur.

## Her monitörde geçerli

| Özellik | Neden çıkarıldı |
|---|---|
| Firmware değiştirme / "unlock" | Monitörü kalıcı bozabilir. Sadece okuma da 2026-10-30'da plandan çıktı. |
| Saate göre otomatik parlaklık | Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişmeli (göz yorgunluğu, gereksiz monitör belleği yazması). |
| Ortam ışığıyla otomatik parlaklık | Aynı neden; ayrıca telefonun sürekli yanında olması gerekir. |
| Varlık algılama (uzaklaşınca karartma) | Sık değişiklik yapar; Windows zaten boşta kalınca ekranı kapatıyor. |
| Uygulamaya göre otomatik preset | Her uygulama geçişinde mod değişir. Yerine elle seçilen presetler ve modlar var. |
| Otomatik içerik Hz'i (film açılınca 72 Hz) | Her geçişte ekran kararır. Yerine Hz panelinde elle seçilen modlar var (T209). |
| Mavi ışık açılınca arka ışığı artırarak telafi etme | Kural 1: parlaklık kendiliğinden değişmez. |
| İki boyutlu homojenlik düzeltme katmanı | Karmaşık, kazancı az, diğer ayarlarla çakışır. **Not:** Bu monitörde (HP P21b G4) gerçek bir üst-alt farkı olduğu için sadece **dikey** hali geri gelmişti; o da 2026-10-30'da çıktı (aşağıda). |
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
| 80 Hz hedefi | Hem pixel clock'u (170 MHz) hem yatay frekansı (80 kHz) aşıyor; ölçülen gerçek sınır 83.0–83.9 kHz (yatay frekans). Tavan 76 Hz. |
| 77 Hz ve üstü | En az boşlukla bile (77 × 1090) 83.9 kHz; ilk gün denemesinde (2000×1092, 84.1 kHz) "Out of Range". |
| Titreşimsiz karartma modu ve PWM testi | Monitör flicker-free; ilk gün testinde 1/1000 s'de parlaklık 0'da bile PWM bandı yok. Profilde PWM olan monitörler için genel fikirde kalıyor. |
| Overdrive karşılaştırması | OSD'de overdrive ayarı yok. |

## 2026-10-30 kapsam daraltması

Kullanıcının hedefi netleşti: bu panelden çıkabilecek en iyi görüntü, göz konforu ve 76 Hz; DaVinci'de renk işi bu monitörde yapılmayacak; aşırı ince ayar yok; hızlı bitsin, kalite düşmesin. Plan 66 görevden 21'e indi. Eski → yeni eşleme: `07-INCELEME.md`, 7. tur.

| Özellik (eski görevler) | Neden çıkarıldı |
|---|---|
| Kendi EDID override motoru, timing hesaplayıcı, maksimum Hz bulucu, sürücü yeniden başlatma, sürücü güncellemesi geri yüklemesi, yönetici yardımcısı (T101, T105, T201–T203, T206, T208) | 76 Hz tavanı ilk gün bulundu ve NVIDIA özel çözünürlüğüyle çalışıyor. Hz paneli (T209) geçiş ve doğrulama yapar; mod eklemek bir kerelik elle iş ve daha az riskli |
| DirectX test desenleri motoru, kare atlama fotoğraf analizi, stabilite testi kodu (T204, T205, T207) | testufo.com aynı testleri yapıyor; adımlar rehberde (T311) |
| Dikey denge katmanı, ölçümle dikey denge, satır bazlı ton telafisi (T402, T803, T904) | Kullanıcı şu an üst-alt farkı görmüyor; önce konum (rehber + açı deseni). Gerekirse yeniden değerlendirilir |
| GPU algılama, yönlendirme, AI/Oyun/Render modları, ekran bilgi katmanı, GPU izleme, güç yönetimi (T403–T406, T806, T807) | Monitörle ilgili değil; Windows Grafik ayarları ve NVIDIA uygulaması bunu yapıyor |
| Pixel yardımcı uygulaması ve kamerayla ölçüm (T601–T604, T701, T801–T804) | Renk referansı Pixel'in ekranı; ayar gözle ve desenlerle (T312, T313) |
| Kamerayla kalibrasyon, ICC profili, DaVinci 3D LUT, Hz başına kalibrasyon (T702–T707) | DaVinci'de renk işi bu monitörde yapılmayacak; gözle kalibrasyon (T313) yeterli. Panel geniş gamut değil |
| Monitör karnesi, kayıt defteri ve dışa aktarma (T808, T501) | Aşırı ince ayar; ayarlar zaten tek JSON dosyası |
| Deneysel faz: 10-bit, RIFE, kartlar arası VGA geçişi, kolorimetre, NVIDIA renk dönüşümü, DDC keşfi, firmware okuma (T901–T909) | Panelin potansiyeli belli; risk/kazanç oranı düşük. Kolorimetre alınırsa kolorimetre modu yeniden değerlendirilir |

**Geri alınanlar:** Dithering, canlılık, ek karartma ve GPU renk boru hattı daraltmanın ilk taslağında çıkmıştı. Görüntü kalitesi ve göz konforu birinci öncelik olduğu için geri alındı (T302, T303, T306, T313).
