# Donanım

> **Not:** Plan HP V214a için yazılmıştı. İlk gün kontrolünde (2026-10-30) EDID, DDC ve OSD monitörün **HP P21b G4** olduğunu gösterdi. Sinyal sınırları iki modelde aynı olduğu için plan büyük ölçüde geçerli kaldı; bu dosya P21b G4'e ve ünitede ölçülen değerlere göre yeniden yazıldı. Repo adı `hp-v214a` olarak kaldı. Ham veriler: `donanim/hp-p21b-g4/`.

## Monitör: HP P21b G4

| Özellik | Değer | Uygulamaya etkisi |
|---|---|---|
| Model | HP P21b G4 (EDID: `HPN36A2`), üretim 2021/48 | Profil anahtarı |
| Panel | 20.7" **TN**, LED arka ışık, mat | Renk ve gamma dikey açıyla değişir (üst-alt farkı) |
| Piksel yoğunluğu | 108 PPI | Keskin yazı; 70–80 cm'den rahat okunur |
| Çözünürlük / Hz | 1920×1080 @ 60 Hz (EDID'de 50 Hz de var) | Overclock ile **74 Hz doğrulandı** (aşağıda) |
| **Sinyal sınırları (EDID)** | **Yatay 30–80 kHz, dikey 50–60 Hz, max pixel clock 170 MHz** | Scaler gerçekte biraz üstünü kabul ediyor |
| Tepki süresi | 5 ms GtG (overdrive ile) | OSD'de overdrive ayarı **yok** |
| Parlaklık | 250 nit | — |
| Kontrast | 600:1 | Siyahlar gri |
| Renk | **6-bit + FRC**, %72 NTSC, varsayılan Neutral | Dithering önemli; ölçümde uzun pozlama |
| Görüş açısı | 90° yatay / 65° dikey | Kalibrasyon göz konumunda |
| Girişler | HDMI 1.4, VGA | DisplayPort yok; overclock HDMI üzerinden |
| USB | Yok | — |
| Hoparlör | Var (DDC ses kodu 62 çalışıyor) | — |
| Adaptive sync | Yok (Windows: VRR desteklenmiyor) | FreeSync testi yok |
| **Flicker-free** | **Evet** (HP veri sayfası; ilk gün testi doğruladı) | PWM yok; titreşimsiz mod gereksiz |
| Stand | Sadece eğim (−5° / +20°), VESA 100 | Yükseklik yok; göz hizası sandalye, kol veya montajla |
| Firmware | CTM121 (DDC C9: 256) | Sadece okunur |
| Arka ışık süresi | 2167 saat (30.10.2026) | — |

### OSD

| Menü | İçerik |
|---|---|
| Brightness+ | (fotoğrafı yok) |
| Color | Warm, **Neutral** (varsayılan), Cool, Night, Native, Low Blue Light, Reading, HP Enhance+, Custom RGB |
| Image | Sharpness (Level 4), Image Scaling (Fill to Screen), Overscan (**Off** olmalı). **Overdrive yok** |
| Input, Power, Menu, Management | Management → DDC/CI açık |
| Information | Current/Optimal Mode, Color Setting, Backlight Hours, Firmware, Serial |
| Alt çubuk | Her menüde "Current Mode: 1920x1080 xxHz" ve giriş; **gelen Hz'i gösteriyor** |

### DDC/CI (MCCS 2.2)

Ham döküm: `donanim/hp-p21b-g4/ddc-vcp-60hz.txt`.

| Kod | Ad | R/W | 60 Hz değeri | Not |
|---|---|---|---|---|
| 10 | Parlaklık | R/W | 55 (0–100) | Arka ışık |
| 12 | Kontrast | R/W | 72 (0–100) | — |
| 14 | Renk ön ayarı | R/W | 5 | Değerler: 2, 4, 5, 8, 11, 12, 13, 14, 15. Tahmin: 2 Native, 4 Warm, 5 Neutral (doğrulandı), 8 Cool, 11 Custom RGB; 12–15 Night / Low Blue Light / Reading / HP Enhance+ (sıra doğrulanacak) |
| 16 / 18 / 1A | RGB kazancı | R/W | 255 / 255 / 255 (max 255) | Sadece azaltılabilir |
| 0C | Renk sıcaklığı isteği | R/W | 40 (max 126), 0B artış = 50 | Çalışıp çalışmadığı T301'de test edilir |
| 6C / 6E / 70 | Kanal başına siyah seviyesi | R/W | 128 (max 255) | Kalibrasyonda (T702) koyu tonlar için |
| 2E | Gray Scale Expansion | R/W | 2 (0–3) | Anlamı bilinmiyor; dokunulmaz |
| 60 | Giriş | R/W | 17 | 17 = HDMI, 1 = VGA |
| 62 | Ses | R/W | 50 | Hoparlör |
| 86, 87, 8D | Ölçekleme, keskinlik (1–7), sessize alma | R/W | 2, 4, 2 | — |
| **AE** | Dikey frekans | R | 6000 | **Gerçek gelen Hz × 100.** 74 Hz'de 7400 okundu |
| **AC** | Yatay frekans | R | 1964 | **Hz cinsinden, 16 bit taşmalı:** 60 Hz'de 1964 + 65536 = 67 500; 74 Hz'de 17264 + 65536 = 82 800 |
| C0 | Kullanım süresi | R | 2167 | Saat; OSD ile aynı |
| C8 | Scaler | R | 22021 (0x5605) | Muhtemelen MStar |
| C9 / DF | Firmware / VCP sürümü | R | 256 / 514 | 1.0 / MCCS 2.2 |
| D6, CA, CC, DA | Güç, OSD, dil, tarama | R/W | — | Uygulama kullanmaz |
| 04, 05, 08 | Fabrika sıfırlamaları | W | — | **Asla yazılmaz** |
| E6–FF | Üreticiye özel | — | — | **Dokunulmaz** (sadece T907'de salt okuma) |

### EDID

Dosya: `donanim/hp-p21b-g4/edid-original.bin` (256 bayt, checksum'lar doğru).

- **Native timing:** 1920×1080, yatay 88 / 44 / 148 (toplam 2200), dikey 4 / 5 / 36 (toplam 1125), +/+, 148.5 MHz.
- **Aralık sınırı:** dikey 50–60 Hz, yatay 30–80 kHz, 170 MHz.
- **CTA-861:** VIC 16 (1080p60), **31 (1080p50)**, 4, 19, 18, 17, 3, 2, 1. Temel ses, hoparlör bloğu, HDMI VSDB (max TMDS 170 MHz). Video Capability bloğu **QS=1**: monitör RGB aralığını kaynaktan alır.
- Gamma 2.2, boyut 46 × 26 cm.

## Hz tavanı

Her hücre: **pixel clock (MHz) / yatay frekans (kHz).** EDID sınırları 170 MHz ve 80 kHz; ölçülen gerçek sınır parantez içinde.

| Hz | Standart (2200×1125) | CVT-RB (2080 × 1111–1120) | Sıkı dikey (2080×1100) | Sıkı (2000×1100) |
|---|---|---|---|---|
| 60 | 148.5 / 67.5 ✔ | 138.7 / 66.7 ✔ | 137.3 / 66.0 ✔ | 132.0 / 66.0 ✔ |
| 65 | 160.9 / 73.1 ✔ | 150.6 / 72.4 ✔ | 148.7 / 71.5 ✔ | 143.0 / 71.5 ✔ |
| 70 | 173.2 / 78.8 ✖ | 162.5 / 78.1 ✔ | 160.2 / 77.0 ✔ | 154.0 / 77.0 ✔ |
| 72 | 178.2 / 81.0 ✖ | 167.3 / 80.4 ✖ | 164.7 / 79.2 ✔ | 158.4 / 79.2 ✔ |
| 74 | 183.2 / 83.2 ✖ | **172.2 / 82.8 ✅ çalıştı** | 169.3 / 81.4 | 162.8 / 81.4 |
| 75 | 185.6 / 84.4 ✖ | **174.6 / 83.9 ⛔ reddedildi** | **171.6 / 82.5 🟨 NVIDIA testi geçti** | 165.0 / 82.5 |
| 76 | 188.1 / 85.5 ✖ | 177.0 / 85.1 ✖ | 173.9 / 83.6 | 167.2 / 83.6 |

✔ spec içi · ✖ spec dışı · ✅ / ⛔ / 🟨 gerçek test sonucu. CVT-RB'de dikey toplam Hz'e göre değişir (en az 460 µs dikey boşluk): 60 Hz'de 1111, 74–75 Hz'de 1119.

**Ünitede ölçülenler (2026-10-30, RTX 3080 HDMI):**
- **74 Hz, CVT-RB (2080×1119, 82.81 kHz, 172.24 MHz):** çalışıyor. Windows aktif sinyal 74 Hz, DDC AE = 7400, testufo frameskipping'de 1/10 s'de 8 ardışık kare, atlama yok.
- **75 Hz, CVT-RB (83.9 kHz, 174.5 MHz):** monitör "Input Signal Out of Range, H = 83.9 kHz V = 75 Hz" gösterdi. Reddetme net; kare atlamıyor, görüntü vermiyor.
- **75 Hz, sıkı dikey (2080×1100, 82.5 kHz, 171.6 MHz):** NVIDIA testi "Test successful". Windows, AE = 7500 ve kare atlama doğrulaması bekleniyor.

**Sonuç:**
- Scaler spec'in biraz üstünü kabul ediyor. Sınır yatay frekansta **82.8 ile 83.9 kHz arasında** (pixel clock'ta 172.2 ile 174.5 MHz arasında); hangisinin belirleyici olduğu henüz ayrılmadı.
- Dikey 60 Hz sınırını uygulamıyor.
- **Dikey boşluğu kısaltmak** yatay frekansı düşürür: aynı Hz'de daha az satır gönderilir. 75 Hz bu yüzden 1119 yerine 1100 toplam satırla sınırın altına iniyor.
- Tamamen spec içinde kalmak isteyen için en yüksek değer **72 Hz** (2080×1100 veya 2000×1100).
- 76 Hz ve üstü her timing'de ölçülen sınırın üstünde; bulucu denemez.

## Gerçekçi beklentiler

- **Overclock:** 74 Hz doğrulandı; 75 Hz büyük ihtimalle. Daha fazlası yok.
- **Renk:** Kalibrasyonla beyaz nötr, griler renksiz olur. Ama TN'de bakış açısı değişince gamma ve renk kayar, panel 6-bit + FRC. Genel kullanım ve sRGB içerik için iyi, renk kritik iş için zayıf. DaVinci'de son kontrolü telefonda yapmak önemli.
- **Parlaklık:** 250 nit. PWM olmadığı için arka ışık 0'a kadar titreşimsiz kısılabilir; daha aşağısı için ek karartma var (`08-GORUNTU-YONTEMLERI.md`).
- **Kontrast:** 600:1, siyahlar gri görünür.
- **Üst-alt ışık farkı:** İlk gün testinde sebep **bakış açısı** çıktı. Büyük kısmı konumla düzelir, kalanı dikey denge katmanıyla.
- **Artılar:** Tam DDC/CI (siyah seviyesi ve gerçek Hz okuma dahil), flicker-free, 108 PPI ile keskin yazı, hoparlör.

## Bilgisayar

| Parça | Rolü | Not |
|---|---|---|
| **RTX 3080** | **Ekran kartı.** Masaüstü, tarayıcı, video, oyunlar | Monitör bu kartın **HDMI** çıkışına bağlı |
| **RTX 3090** (24 GB) | **Ağır iş kartı.** Yerel LLM'ler, DaVinci render, RIFE | Monitöre bağlı değil, tamamen boş kalır |
| Intel i3-10100, ASUS TUF Gaming Z490-Plus | — | — |
| EVGA SuperNOVA 1000 G3 (+ 650 W ikinci PSU) | Güç | Aşağıya bak |
| Windows 11 | — | — |

**Güç hesabı:** 3090 (~350 W) + 3080 (~320 W) + i3-10100 (65 W) + diğerleri (~60 W) ≈ **800 W** tam yükte. 1000 W yeterli, ama 3090 + 3070 kurulumuna göre pay azaldı. İki kart uzun süre birlikte tam yükte çalışacaksa güç yönetimiyle limitleri biraz düşürmek payı geri getirir (T807).
- Her 8-pin girişe **ayrı PCIe kablosu** kullan.
- İki PSU kullanılıyorsa **bir kartın bütün güç girişleri (ve varsa riser'ı) aynı PSU'dan** beslenmeli.
- RTX 3070 şu an takılı değil. Üç kart birlikte takılırsa 1000 W yetmez (GPU'lar tek başına ~890 W).

## Telefon

| Cihaz | Rolü |
|---|---|
| Pixel 10 Pro | Ölçüm cihazı (RAW fotoğraf) ve uzaktan kumanda. Monitöre bağlanmaz. |

## Kablolama

- **HDMI:** Monitör ↔ **RTX 3080'in HDMI çıkışı.** Tek kablo. Sıradan bir High Speed HDMI kablosu yeterli (74–75 Hz'de 172 MHz, HDMI 1.4 için sorun değil).
- **VGA:** Boş. v3'te isteğe bağlı: 3090'ın DP çıkışı → **aktif** DP→VGA adaptörü → monitörün VGA girişi (T903).
- **NVIDIA ayarı:** Çıkış dinamik aralığı **Full** (ilk gün baştan Full'dü). Uygulama v1'de bunu her Hz'de kontrol eder (T304).

### Görüntü gelmezse (kurtarma)

1. 15 saniye bekle; uygulama kendisi geri döner. (NVIDIA'nın kendi testi de aynı şekilde geri dönüyor; 75 Hz CVT-RB testinde görüldü.)
2. HDMI kablosunu **3090'ın HDMI çıkışına** ya da 3080'in bir DP çıkışına (DP→HDMI kabloyla) tak. Windows override'ı bağlantı başına tuttuğu için başka çıkışta görüntü gelir. Sonra "Fabrika ayarına dön".
3. Güvenli mod + kurtarma dosyası. Kurtarma dosyası EDID override'larını siler ve GPU renk tablosunu da sıfırlar.

## Oturma konumu (TN)

- Gözler ekranın **üst kenarı hizasında ya da biraz altında.**
- Mesafe **70–80 cm.**
- Eğim 0° ile −5° (öne) arası.
- İlk gün: ölçü alınmadı, kullanıcı şu an sorun görmüyor. T401'de açı deseniyle bakılır.
- Ayrıntı ve teşhis: `08-GORUNTU-YONTEMLERI.md`, bölüm 1.

## Kaynaklar

- [HP P21b G4 veri sayfası (9TY24AA, PDF)](https://pcb.inc.hp.com/dc/api/spec-sheet/ww-en/34514035/pdf/9TY24AA.pdf): TN, 5 ms GtG overdrive ile, flicker-free, 6-bit + 2 FRC, eğim
- [HP P21b G4 QuickSpecs (atcomp, PDF)](https://katalog.atcomp.cz/katalog/HP-9TY24AA/c06631379.pdf): görüş açıları, girişler
- [HPShop: P21b G4](https://www.hpshop.co.za/products/hp-p-series-p21b-g4-20-7-inch-1920-x-1080p-fhd-16-9-60hz-5ms-tn-led-monitor-9ty24aa-319826): TN, 60 Hz, 5 ms
- [Monitor Nerds: NVIDIA ve AMD'de HDMI sorunları](https://monitornerds.com/hdmi-1080p-1440p/): HDMI'da Limited RGB ve "Output dynamic range"
- [Monitor Tests forumu: NVIDIA'da HDMI üzerinden FreeSync](https://www.monitortests.com/forum/Thread-Gsync-or-Freesync-over-HDMI-on-Nvidia-Cards-with-CRU): NVIDIA HDMI'da FreeSync desteklemiyor
- [Lorimer: TN monitörler](https://lorimer.id.au/tn-monitors.html): göz konumu ve TN telafi filmi
- Ünite verileri: `donanim/hp-p21b-g4/` (EDID, DDC dökümü)
