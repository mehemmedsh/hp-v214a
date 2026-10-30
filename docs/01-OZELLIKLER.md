# Özellikler

HP P21b G4 + RTX 3080 (ekran) + RTX 3090 (ağır iş) için son özellik listesi. 8 modül. Her özelliğin yanında sürümü ve onu yapan görevler (`04-FAZLAR.md`) yazar.

**Değişmez kural:** Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir. Üç güvenlik istisnası var, üçü de yeni bir değer seçmez, kullanıcının bilinen değerine döner (`03-GUVENLIK.md`).

---

## Modül 1 — Çekirdek

### 1.1 Monitör tanıma · v1 · T101, T102
- EDID'den marka, model, seri numarası, bağlantı tipi ve bağlı olduğu kartı gösterir; HP P21b G4 olduğunu doğrular.
- VGA'daysa "HDMI'a geç" uyarısı verir.

### 1.2 Çoklu monitör altyapısı · v1 · T102
- Her bağlı monitör kendi profiliyle yönetilir. HP P21b G4'e özel testler sadece onda açılır.

### 1.3 Monitör profil dosyası · v1 · T004
- Monitöre özel bilgiler kodda değil, veri dosyasında tutulur: spec sınırları (170 MHz, 30–80 kHz, 50–60 Hz), ölçülen gerçek sınır (74 Hz çalışıyor, 83.9 kHz reddediliyor), OSD yetenekleri, çalışan DDC kodları, PWM durumu (yok), konum sonucu.

### 1.4 Profil sistemi · v1 · T102
- Anahtar: **monitör + seri numarası + port + kart.**
- İçerik: onaylı Hz'ler ve timing'leri, Hz başına RGB aralığı, presetler, mavi ışık ve karartma ayarları, dithering, v2'de Hz başına kalibrasyon.

### 1.5 Yedek ve geri alma · v1 · T103, T104
- İlk açılışta orijinal EDID, DDC değerleri ve GPU renk tablosu yedeklenir; her değişiklik bir geri alma noktası oluşturur.
- "Fabrika ayarına dön" tek tık.
- Güvenli mod kurtarma dosyası: EDID override'larını siler, GPU renk tablosunu sıfırlar.
- Kaldırınca tam temizlik.

### 1.6 Yönetici yardımcısı · v1 · T105
- Ana uygulama normal kullanıcı olarak çalışır. Sadece şu işler için küçük bir yönetici süreci devreye girer: EDID override yazma/silme, sürücüyü yeniden başlatma, GPU güç limiti, GPU tablo aralığı kaydı (`GdiIcmGammaRange`).

### 1.7 Kayıt defteri ve dışa aktarma · v1 · T501
- Hangi testin ne zaman geçtiği, hangi ayarın ne zaman değiştiği. Tüm profiller tek dosyada yedeklenir ve geri yüklenir.

### 1.8 Dil · v1 · T001, T502
- Türkçe, Azerbaycanca, İngilizce. Altyapı ilk günden.

### 1.9 Sistem tepsisi ve kısayollar · v1 · T310
- Tepsiden preset, mavi ışık, karartma, katmanlar.
- **Panik kısayolu:** Tüm görüntü efektlerini (mavi ışık, ek karartma, canlılık, katmanlar) tek tuşla kapatır; kalibrasyon kalır.

---

## Modül 2 — Yenileme hızı

### 2.1 Hz overclock · v1 · T201, T202, T203
- HDMI üzerinden EDID override ile yeni mod eklenir. **60 Hz orijinal mod yerinde kalır.**
- EDID'in CTA-861 uzantı bloğu (HDMI ve ses bilgisi) korunur.
- Timing yöntemleri: standart, CVT-RB, sıkı. Bu monitörde sıkılaştırılmış timing ana yöntem.
- Değişiklikten sonra sürücü yeniden başlatılır; 15 saniye içinde onay gelmezse geri dönülür.

### 2.2 Hz tavanı hesabı · v1 · T201
- Her adımda pixel clock ve yatay frekans gösterilir; spec içi yeşil, spec dışı sarı.

### 2.3 Maksimum Hz bulucu · v1 · T206
- Kullanıcı başlatır. 61 → **76** Hz arası adım adım dener; her adımda görüntü onayı ve kare atlama testi.
- Her Hz'de önce dikey toplamı küçük timing (2080×1100) denenir: aynı Hz'de yatay frekansı düşürür. Bu monitörde 75 Hz CVT-RB'de (83.9 kHz) reddedildi, 2080×1100'de (82.5 kHz) geçti; 76 Hz 2080×1092'de (83.0 kHz) geçti; 77 Hz (84.1 kHz) reddedildi. Sınır yatay frekansta, 83.0–83.9 kHz arası.
- 72 Hz üstü adımlar spec dışı uyarısı ve ayrı onay ister. Sonuç onaysız kalıcı olmaz.
- DDC katmanı hazır olunca (T301) her adımda gelen Hz, monitörün kendisinden (AE, AC) okunarak doğrulanır.

### 2.4 Kare atlama testi · v1 · T204, T205
- Sırayla yanan kutular + uzun pozlamalı fotoğraf. Boşluk varsa "kare atlıyor, sahte Hz".
- OSD'nin alt çubuğu ve Information ekranı gelen Hz'i gösteriyor; DDC'deki AE kodu da aynı değeri okunabilir şekilde veriyor (74 Hz'de 7400).

### 2.5 Stabilite testi · v1 · T207
- Onaylı Hz'de 20–30 dakika hareketli desen.

### 2.6 Sürücü güncellemesi koruması · v1 · T208
- NVIDIA güncellemesi override'ı sildiyse kullanıcının onayladığı değerler geri yüklenir.

### 2.7 İçerik Hz'leri (elle) · v2 · T805
- **50 Hz** spec içinde ve EDID'de zaten var (1080p50); override gerekmez. PAL VHS için en güçlü aday. 72 Hz spec içi sıkı timing'le eklenebilir. 48 Hz spec altında, büyük ihtimalle kabul edilmez.
- Test edilerek eklenir; tepsiden veya kısayolla elle seçilir.

---

## Modül 3 — Görüntü ve renk

Yöntemlerin ayrıntısı: `08-GORUNTU-YONTEMLERI.md`.

### 3.1 DDC/CI yetenek testi ve paneli · v1 · T301, T305
- Önce DDC/CI çalışıyor mu ve hangi kodlar destekleniyor, test edilir ve profile yazılır.
- Panel: parlaklık, kontrast, renk ön ayarı, RGB kazancı (büyük ihtimalle Custom RGB modunda), giriş, **hoparlör sesi**.
- DDC yoksa: parlaklık ve kontrast için OSD talimatı; renk işleri GPU renk tablosuyla.
- DDC yazmaları monitörün belleğini yormamak için sınırlanır.

### 3.2 Renk boru hattı · v1 · T302
- GPU renk tablosunun tek sahibi. Katman sırası: **kalibrasyon → mavi ışık → ek karartma**, ardından dithering.
- Başka bir program (oyun, Windows Gece ışığı, f.lux, NVIDIA masaüstü renk ayarları) tabloyu değiştirirse tespit eder, kullanıcının tablosunu geri yükler ve uyarır.
- Derin karartma ve sıcak renkler için Windows tablo aralığı kaydını açar (geri alınabilir).

### 3.3 GPU dithering · v1 · T303
- Bağlantı başına aç/kapat. NVIDIA tam aralık RGB'de varsayılan olarak dithering yapmadığı için GPU renk tablosu kullanan her özellikte açık tutulur.

### 3.4 RGB Full / Limited · v1 · T304
- NVIDIA, HDMI'da 1080p60'ı TV formatı sayıp Limited RGB gönderebiliyor; özel Hz'lerde Full gelebiliyor. Full'e sabitlenir ve her onaylı Hz'de doğrulanır.

### 3.5 Parlaklık ve ek karartma · v1 · T306 (v2'de nit ölçümü: T801)
- Tek kaydırıcı: önce arka ışık (DDC), arka ışık tabanından sonra GPU ile ek karartma (taban: beyazın %15'i).
- **Titreşimsiz mod:** Sadece profilde PWM varsa açılır. HP P21b G4 flicker-free (ilk gün testi), bu yüzden bu monitörde yok; arka ışık 0'a kadar serbest.
- **Dijital parlaklık koruması:** Kontrast veya RGB ile parlaklık artırma engellenir; ek karartma sadece azaltma yönünde.

### 3.6 Mavi ışık filtresi · v1 · T307
- Kelvin tabanlı (6500K → 1900K), siyah cisim eğrisinden hesaplanan doğal sıcak beyaz.
- Önce monitörün RGB kazancı, devamı GPU'da. Kalibrasyonun üstüne fark olarak uygulanır.
- Monitörün Low Blue Light ön ayarına kısayol.
- İsteğe bağlı yavaş zamanlama (varsayılan kapalı); sadece renk değişir, arka ışık değişmez.
- Render modunda, DaVinci presetinde ve ölçümde kapalı. Windows Gece ışığı ve f.lux ile çakışma uyarısı.

### 3.7 Presetler (elle) · v1 · T308
- **Gece** (mavi ışık + düşük parlaklık), **Kod**, **DaVinci** (kalibre, efektsiz), **Film**, **Oyun.** Kullanıcı yenilerini ekleyebilir.
- Sadece kullanıcı seçince uygulanır.

### 3.8 Canlılık · v1 · T309
- NVIDIA Digital Vibrance ile doygunluk artırılır; TN'nin soluk görünümünü telafi eder.
- Sadece Film ve Oyun presetlerinde; DaVinci'de ve ölçümde kapalı; uygulama kapanınca varsayılana döner.

### 3.9 Görüntü iyileştirme rehberi · v1 · T311
- RTX Video Super Resolution, DLDSR, DLSS, DaVinci Super Scale için açıklama ve kısayollar.

---

## Modül 4 — Üst-alt ışık farkı (TN)

### 4.1 Açı ayar deseni, teşhis ve oturma rehberi · v1 · T401
- Teşhis: sebep bakış açısı mı, arka ışık mı.
- Desen: üst, orta ve alt yamalar eşit görünene kadar konum ayarı (göz yüksekliği, mesafe, eğim).

### 4.2 Dikey denge katmanı (elle) · v1 · T402
- Kalan farkı, parlak bölgeyi görünmez bir katmanla hafifçe karartarak dengeler.
- Tıklamaları geçirir, kayda girmez; ölçümlerde ve tam ekran oyunlarda gizlenir.

### 4.3 Ölçümle dikey denge · v2 · T803
- Pixel göz konumundan üst, orta ve alt gri yamaları çeker; katmanın eğrisi otomatik hesaplanır.

### 4.4 Satır bazlı ton telafisi · v3 · T904
- Masaüstü yakalanıp her satır için ayrı ton eğrisiyle yeniden çizilir; orta tonları da düzeltir.
- Varsayılan olarak kapalı. Korumalı videolar siyah görünür, tam ekran özel mod oyunlarda çalışmaz, bir kare gecikme ekler.

---

## Modül 5 — GPU (RTX 3090 + RTX 3080)

### 5.1 GPU algılama · v1 · T403
- 3090 ve 3080'i (3070 takılırsa üçüncü kartı) ve monitörün hangi karta bağlı olduğunu bulur; temel sıcaklık okur. Kablo başka karta takılırsa profili taşır.

### 5.2 GPU yönlendirme paneli · v1 · T404
- Uygulama başına kart seçimi (ör. DaVinci ve LLM araçları 3090, tarayıcı 3080).

### 5.3 Modlar (elle) · v1 · T405
- **AI modu:** 3090 tamamen boş, her şey 3080'de.
- **Oyun modu:** Oyun **doğrudan 3080'de** çalışır (kartlar arası kopya gecikmesi yok), onaylı overclock Hz açılır. 3090 aynı anda LLM için boş kalabilir.
- **Render modu:** DaVinci 3090'da (Studio'da iki kart birden), mavi ışık ve karartma kapalı, kalibre profil açık.

### 5.4 Ekran bilgi katmanı · v1 · T406
- Ekran köşesinde Hz, FPS ve iki kartın sıcaklığı. Kısayolla açılıp kapanır, testlerde gizlenir.

### 5.5 GPU izleme paneli · v2 · T806
- Sıcaklık, VRAM, güç, kullanım; tepside ve telefonda.

### 5.6 Güç yönetimi · v2 · T807
- 3090 ve 3080'in güç limitini sadece **düşürme** ve varsayılana dönme.

---

## Modül 6 — Pixel yardımcı uygulaması

### 6.1 Eşleştirme · v2 · T601
- Sadece yerel Wi-Fi; PC ekranındaki kodla eşleşme.

### 6.2 Kamera kilidi · v2 · T602
- RAW, sabit ISO, enstantane, beyaz dengesi ve odak; pozlama en az 1/15 s; her yama için çoklu kare.
- Kamera **kullanıcının göz konumunda** durur (TN).

### 6.3 Otomatik çekim ve aktarım · v2 · T603
- PC deseni gösterir, telefon çeker, fotoğraf Wi-Fi ile PC'ye gelir.

### 6.4 Uzaktan kontrol · v2 · T604
- Preset, parlaklık/karartma, mavi ışık, mod, onaylı Hz'ler. Overclock testleri ve deneyler telefondan başlatılamaz.

---

## Modül 7 — Kalibrasyon ve testler

### 7.1 Fotoğraf analiz çekirdeği · v2 · T701
- DNG çözme, DNG renk matrisiyle yaklaşık XYZ, çoklu kare ortancası, koyu tonlarda pozlama basamaklama, sensör siyah seviyesi, tekrarlanabilirlik ölçümü.

### 7.2 Kontrast ve siyah seviyesi · v2 · T702

### 7.3 Beyaz noktası · v2 · T703
- DDC RGB kazancıyla D65 ya da "doğal beyaz". DDC yoksa GPU'da.

### 7.4 Gri dengesi ve ton eğrisi · v2 · T704
- 17 gri basamak, yumuşatılmış kanal eğrileri, GPU renk tablosunun kalibrasyon katmanı, dithering zorunlu.

### 7.5 ICC profili · v2 · T705

### 7.6 DaVinci 3D LUT · v2 · T706
- Rec.709 / gamma 2.4 hedefli `.cube`; Resolve'da "3D Color Viewer Lookup Table".

### 7.7 Hz başına kalibrasyon · v2 · T707
- Her onaylı Hz için ayrı değerler, RGB aralığı dahil. Mavi ışık ve karartma fark olarak üstüne eklenir. Parlaklığa dokunmaz.

### 7.8 Parlaklık ölçümü · v2 · T801
- Yaklaşık nit (arka ışık ve ek karartma kademeleri). PWM testi sadece profilde PWM şüphesi olan monitörlerde; P21b G4'te ilk gün yok çıktı.

### 7.9 Panel testleri · v2 · T802
- Ölü piksel, ışık sızması, homojenlik teşhisi (uzaktan, zoomla), ghosting.

### 7.10 Ghosting testi · v2 · T804
- P21b G4'ün OSD'sinde overdrive ayarı yok; her onaylı Hz'de sadece ghosting ölçülür.

### 7.11 Monitör karnesi · v2 · T808
- Maksimum Hz ve spec durumu, kare atlama, stabilite, RGB aralığı, yaklaşık renk doğruluğu (ΔE), tekrarlanabilirlik, parlaklık, üst-alt farkı (önce/sonra), ghosting. Önce/sonra karşılaştırmalı.

---

## Modül 8 — Deneysel (v3)

Her deney ayrı bir uyarı ekranından sonra açılır ve kendi geri alma noktasını oluşturur.

### 8.1 10-bit sinyal testi · T901
- Merak testi, ihtimal neredeyse sıfır: panel 6-bit; 1080p60'ta 10-bit 173–186 MHz istiyor. Sadece **50 Hz CVT-RB**'de (≈144 MHz) denenir.

### 8.2 Hareket yumuşatma (RIFE + mpv) · T902
- Hesap 3090'da, görüntü 3080'den. Yerel videolar onaylı Hz'e yumuşatılır.

### 8.3 Kartlar arası geçiş · T903
- 3090 → aktif DP→VGA adaptörü → monitörün VGA girişi. Analog görüntü daha yumuşak. DDC giriş seçimi desteklenmezse monitördeki "Next Active Input" tuşu kullanılır. İsteğe bağlı.

### 8.4 Kolorimetre modu · T905
- Kolorimetre alınırsa ölçüm ArgyllCMS üzerinden yapılır; boru hattı aynı kalır. Alınırsa v2'ye çekilir.

### 8.5 NVIDIA renk dönüşümü arka ucu · T906
- LUT-matris-LUT düzeltmesi ekran çıkışında (novideo_srgb yöntemi, belgelenmemiş arayüz). Windows gamma tablosuyla aynı anda kullanılmaz.

### 8.6 Keşif · T907, T908
- Yavaş, salt okunur DDC taraması; bilinmeyen koda yazma sadece yedek ve onayla; servis modu görünümü.
- Firmware **sadece okuma**, en son görev (T908). Yazma yok.

(Satır bazlı ton telafisi de v3'tür: Modül 4.4, T904.)
