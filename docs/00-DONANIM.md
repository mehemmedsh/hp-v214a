# Donanım

## Monitör: HP V214a

| Özellik | Değer | Uygulamaya etkisi |
|---|---|---|
| Panel | 20.7" **TN**, WLED arka ışık, mat | Renk ve gamma dikey açıyla değişir (üst-alt farkı) |
| Piksel yoğunluğu | 108 PPI (0.2385 mm) | Keskin yazı; 70–80 cm'den rahat okunur |
| Çözünürlük / Hz | 1920×1080 @ 60 Hz | — |
| **Sinyal sınırları** | **Yatay 30–80 kHz, dikey 50–60 Hz, max pixel clock 170 MHz** | Hz tavanı hesaplanabilir |
| Tepki süresi | 5 ms (on/off) | — |
| Parlaklık | 200 nit | — |
| Kontrast | 600:1 (en az 450:1), dinamik 5M:1 | Siyahlar gri; ölçümde Dynamic Contrast kapalı |
| Renk | **6-bit + Hi-FRC**, 16.7 milyon, %72 NTSC, varsayılan Neutral 6500K | Dithering önemli; ölçümde uzun pozlama |
| Görüş açısı | 90° yatay / 65° dikey | Kalibrasyon göz konumunda |
| Girişler | HDMI 1.4 (HDCP), VGA, 3.5 mm ses girişi | DisplayPort yok; overclock HDMI üzerinden |
| USB | Yok | — |
| Hoparlör | 2 × 1 W | DDC ses kontrolü (destekliyorsa) |
| Adaptive sync | Yok | FreeSync testi yok |
| OSD menüleri | Brightness, Contrast, Color Control, Input Control, Image Control, Power Control, Menu Control, Management, Language, Information | — |
| Düşük mavi ışık | Var (TÜV Low Blue Light) | Mavi ışık filtresine kısayol |
| Flicker-free sertifikası | Listede yok | PWM testi, titreşimsiz karartma |
| Yazılım | Yok | — |
| Güç | Dahili, tipik 23 W, en fazla 28 W | — |
| Stand | Sadece eğim (−5° / +11°), VESA 100 | Yükseklik yok; göz hizası sandalye, kol veya montajla |

- **DDC/CI:** Veri sayfası belirtmiyor. Aynı OSD yapısını kullanan HP VH240a'nın kılavuzunda "Management" menüsü DDC/CI'ı açıp kapatıyor; V214a'da da büyük ihtimalle aynı. İlk gün doğrulanır (`09-ILK-GUN.md`).
- **Overdrive ayarı:** Bilinmiyor, ilk gün OSD'den bakılır.
- **Panel modeli:** Bulunamadı.

## Hz tavanı

Her hücre: **pixel clock (MHz) / yatay frekans (kHz).** Sınırlar: 170 MHz ve 80 kHz.

| Hz | Standart timing (2200×1125) | CVT-RB (2080×~1115) | Sıkı timing (2000×1100) |
|---|---|---|---|
| 60 | 148.5 / 67.5 ✔ | 138.5 / 66.7 ✔ | 132.0 / 66.0 ✔ |
| 65 | 160.9 / 73.1 ✔ | 150.5 / 72.4 ✔ | 143.0 / 71.5 ✔ |
| 68 | 168.3 / 76.5 ✔ | 157.5 / 75.8 ✔ | 149.6 / 74.8 ✔ |
| 70 | 173.2 / 78.8 ✖ | 162.2 / 78.1 ✔ | 154.0 / 77.0 ✔ |
| 71 | 175.7 / 79.9 ✖ | 164.8 / 79.3 ✔ | 156.2 / 78.1 ✔ |
| 72 | 178.2 / 81.0 ✖ | 167.2 / 80.4 ✖ | 158.4 / 79.2 ✔ |
| 75 | 185.6 / 84.4 ✖ | 174.5 / 83.9 ✖ | 165.0 / 82.5 ✖ |
| 80 | 198.0 / 90.0 ✖ | 186.5 / 89.8 ✖ | 176.0 / 88.0 ✖ |

- Standart timing 68 Hz'e, CVT-RB 71 Hz'e, sıkı timing 72 Hz'e kadar spec içinde kalıyor. Bu yüzden **sıkılaştırılmış timing ana yöntem.**
- **75 Hz her timing'de spec dışı.** 1080 aktif satır × 75 Hz bile 81 kHz ediyor. Sadece scaler spec'in üstünü tolere ederse çalışır.
- **80 Hz** hem pixel clock'u hem yatay frekansı aşıyor; pratikte yok.
- Spec, HP'nin garanti ettiği aralık. Birçok scaler biraz üstüne çıkabiliyor, bazıları spec içinde bile kare atlıyor. Son sözü test söyler.

## Gerçekçi beklentiler

- **Overclock:** İyi ihtimal 70–72 Hz, şanslıysan 75 Hz, orta ihtimal 65–68 Hz.
- **Renk:** Kalibrasyonla beyaz nötr, griler renksiz olur. Ama TN'de bakış açısı değişince gamma ve renk kayar, panel 6-bit + FRC. Genel kullanım ve sRGB içerik için iyi, renk kritik iş için zayıf. DaVinci'de son kontrolü telefonda yapmak önemli.
- **Parlaklık:** 200 nit. Karanlık odada 80–120 nit yeterli; daha aşağısı için ek karartma var (`08-GORUNTU-YONTEMLERI.md`).
- **Kontrast:** 600:1, siyahlar gri görünür.
- **Üst-alt ışık farkı:** Büyük kısmı konumla düzelir, kalanı dikey denge katmanıyla.
- **Artılar:** 108 PPI ile keskin yazı, hızlı TN panel, belgeli sinyal sınırları, dahili hoparlör.

## Bilgisayar

| Parça | Rolü | Not |
|---|---|---|
| **RTX 3080** | **Ekran kartı.** Masaüstü, tarayıcı, video, oyunlar | Monitör bu kartın **HDMI** çıkışına bağlı |
| **RTX 3090** (24 GB) | **Ağır iş kartı.** Yerel LLM'ler, DaVinci render, RIFE | Monitöre bağlı değil, tamamen boş kalır |
| Intel i3-10100, ASUS TUF Gaming Z490-Plus | — | — |
| EVGA SuperNOVA 1000 G3 (+ 650 W ikinci PSU) | Güç | Aşağıya bak |
| Windows 10 (2004+) veya 11 | — | — |

**Güç hesabı:** 3090 (~350 W) + 3080 (~320 W) + i3-10100 (65 W) + diğerleri (~60 W) ≈ **800 W** tam yükte. 1000 W yeterli, ama 3090 + 3070 kurulumuna göre pay azaldı. İki kart uzun süre birlikte tam yükte çalışacaksa güç yönetimiyle limitleri biraz düşürmek payı geri getirir (T807).
- Her 8-pin girişe **ayrı PCIe kablosu** kullan.
- İki PSU kullanılıyorsa **bir kartın bütün güç girişleri (ve varsa riser'ı) aynı PSU'dan** beslenmeli.
- RTX 3070 şu an takılı değil. Üç kart birlikte takılırsa 1000 W yetmez (GPU'lar tek başına ~890 W).

## Telefon

| Cihaz | Rolü |
|---|---|
| Pixel 10 Pro | Ölçüm cihazı (RAW fotoğraf) ve uzaktan kumanda. Monitöre bağlanmaz. |

## Kablolama

- **HDMI:** Monitör ↔ **RTX 3080'in HDMI çıkışı.** Tek kablo. Sıradan bir High Speed HDMI kablosu yeterli.
- **VGA:** Boş. v3'te isteğe bağlı: 3090'ın DP çıkışı → **aktif** DP→VGA adaptörü → monitörün VGA girişi (T903).
- **NVIDIA ayarı:** Çıkış dinamik aralığı **Full.** Uygulama v1'de bunu kendisi ayarlar ve her Hz'de kontrol eder (T304).

### Görüntü gelmezse (kurtarma)

1. 15 saniye bekle; uygulama kendisi geri döner.
2. HDMI kablosunu **3090'ın HDMI çıkışına** ya da 3080'in bir DP çıkışına (DP→HDMI kabloyla) tak. Windows override'ı bağlantı başına tuttuğu için başka çıkışta görüntü gelir. Sonra "Fabrika ayarına dön".
3. Güvenli mod + kurtarma dosyası. Kurtarma dosyası EDID override'larını siler ve GPU renk tablosunu da sıfırlar.

## Oturma konumu (TN)

- Gözler ekranın **üst kenarı hizasında ya da biraz altında.**
- Mesafe **70–80 cm.**
- Eğim 0° ile −5° (öne) arası.
- Ayrıntı ve teşhis: `08-GORUNTU-YONTEMLERI.md`, bölüm 1.

## Kaynaklar

- [HP V214a veri sayfası (PDF)](https://shop.office.se/pdf/0DE9A23C-CEBF-41E2-87F3-46FAF3B4BDA4.pdf): panel, 6-bit Hi-FRC, sinyal sınırları, girişler, OSD, sertifikalar
- [HP V214a ürün veri sayfası (ManualMachine)](https://manualmachine.com/hp/v214a/8089813-user-manual): girişler, hoparlör, USB yok
- [HP V214a özellik listesi (Takeplus)](https://takeplus.net/product/hp-v214a-20-7-fhd-led-monitor/): renk sıcaklığı, düşük mavi ışık
- [HP VH240a kılavuzu, OSD sayfası (ManualsLib)](https://www.manualslib.com/manual/1330100/Hp-Vh240a.html?page=19): Management menüsünün DDC/CI'ı açıp kapatması
- [Monitor Nerds: NVIDIA ve AMD'de HDMI sorunları](https://monitornerds.com/hdmi-1080p-1440p/): HDMI'da Limited RGB ve "Output dynamic range"
- [Monitor Tests forumu: NVIDIA'da HDMI üzerinden FreeSync](https://www.monitortests.com/forum/Thread-Gsync-or-Freesync-over-HDMI-on-Nvidia-Cards-with-CRU): NVIDIA HDMI'da FreeSync desteklemiyor
- [Evetech: G-SYNC Compatible açıklaması](https://evezone.evetech.co.za/ez/g-sync-compatible-meaning-freesync-monitors): DisplayPort gereksinimi
- [Lorimer: TN monitörler](https://lorimer.id.au/tn-monitors.html): göz konumu ve TN telafi filmi
