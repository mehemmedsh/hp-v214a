# Uygulama Fikri (monitörden bağımsız)

> Bu dosya uygulamanın **monitöre özel olmayan** fikridir. Kenarda durur, sık değişmez. Her monitör için bu fikirden bir uyarlama yapılır (şablon: bölüm 10).
>
> - **HP V214a uyarlaması:** bu repo, `docs/` klasörü (güncel ve aktif)
> - **Lenovo T24i-20 uyarlaması:** `Myapplab/Lenovo-monitor` reposu, `docs/` klasörü (arşiv)
>
> Bu sürüm, HP V214a çalışmasında eklenen genel yöntemleri de içerir: renk boru hattı, ek karartma, Kelvin tabanlı mavi ışık filtresi, canlılık, tekdüzelik telafisi, DaVinci LUT'u ve kolorimetre yolu.

---

## 1. Amaç

Bir Windows uygulaması ve ona bağlı bir Android yardımcı uygulaması. Sıradan bir ofis monitöründen yazılımla çıkarılabilecek her şeyi **güvenli** şekilde çıkarır:

- Panelin ve scaler'ın kaldırabildiği en yüksek yenileme hızını bulur ve gerçekten çalıştığını kanıtlar.
- Monitörün ayarlarını menüsüne dokunmadan yönetir.
- Telefonu ölçüm cihazı olarak kullanıp monitörü kalibre eder ve test eder.
- Rengi düzeltir, parlaklığı monitörün alt sınırının da altına güvenle indirir, mavi ışığı azaltır.
- Panelin tekdüzelik sorunlarını (ör. TN'de üst-alt farkı) konumla ve yazılımla dengeler.
- İki ekran kartlı sistemde işi kartlara dağıtır.
- Monitörün belgelenmemiş yeteneklerini sadece güvenli testlerle araştırır.

Uygulama **monitörün fiziksel sınırlarını aşamaz.** Panelin bit derinliği, renk gamı, kontrastı, en yüksek parlaklığı ve tepki süresi yazılımla değişmez. Yapabileceği şey, bu sınırların içindeki en iyi ve en doğru ayarı bulmaktır.

Kullanıcının sistemi (uyarlamalarda değişebilir): iki NVIDIA ekran kartı (şu an RTX 3090 ağır iş + RTX 3080 ekran), EVGA 1000 W PSU, ölçüm ve kumanda için Pixel 10 Pro.

---

## 2. Değişmez kurallar

1. **Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir.** Otomatik Hz, otomatik parlaklık, zamana veya uygulamaya göre geçiş yok. Üç güvenlik istisnası var; üçü de yeni bir değer seçmez, kullanıcının bilinen değerine döner:
   - Onaylanmayan bir değişiklikten sonra 15 saniyede **önceki değere geri dönüş**.
   - Sürücü güncellemesi ayarı sildiyse **kullanıcının daha önce onayladığı ayarın geri yüklenmesi**.
   - Başka bir program GPU renk tablosunu değiştirirse **kullanıcının seçtiği tablonun geri yüklenmesi**.
2. **Firmware'e yazma yok.** Hiçbir sürümde, hiçbir deneysel modda. Sadece okuma denenebilir.
3. **Monitörün orijinal modu hiçbir zaman silinmez veya değiştirilmez.** Yeni modlar yanına eklenir.
4. **Her donanım ve sistem ayarı yazması bir geri alma noktası oluşturur** ve Güvenlik katmanından geçer.
5. **Deneysel işlemler ve overclock testleri telefondan başlatılamaz.**
6. **Her görüntü efekti tek kısayolla kapanır** (panik kısayolu); kalibrasyon kalır.

---

## 3. Uygulamanın kullandığı yollar

| Yol | Ne için | Ne gerektirir |
|---|---|---|
| **EDID override** (Windows kayıt defteri, bağlantı başına) | Yeni Hz modları, deneysel EDID testleri | Yönetici izni; scaler'ın yeni zamanlamayı kabul etmesi |
| **DDC/CI** (VESA MCCS kodları) | Arka ışık, kontrast, RGB kazancı, renk ön ayarı, giriş, ses | Monitörde DDC/CI açık ve kodları destekliyor olmalı |
| **GPU renk tablosu** (Windows gamma tablosu) | Kalibrasyon eğrileri, mavi ışığın GPU kısmı, ek karartma | Derin değerler için `GdiIcmGammaRange` kaydı (yönetici) |
| **Ekran kartı sürücüsü** (NVIDIA) | Dithering, RGB aralığı, canlılık, GPU bilgisi, güç limiti; v3'te renk dönüşümü | NVIDIA kart |
| **Windows ekran ayarları** | Mod listesi ve seçimi, ekranı açma/kapama, uygulama başına GPU tercihi, ICC profili | Windows 10 (2004+) veya 11 |
| **Katman pencereleri** | Tekdüzelik dengesi, ekran bilgisi | Tıklama geçirgen, ekran yakalamadan hariç |
| **Masaüstü yakalama + gölgelendirici** (v3) | Satır bazlı ton telafisi | Windows 10 2004+, DirectX 11 |
| **Telefon kamerası** (RAW) | Kare atlama, kalibrasyon, panel testleri, PWM, yaklaşık parlaklık | Manuel kamera kontrolü olan telefon |
| **Harici araçlar** | FPS (PresentMon), hareket yumuşatma (mpv + RIFE), kolorimetre (ArgyllCMS) | — |

---

## 4. Modüller ve özellikler

Her özelliğin yanında **monitörden ne istediği** ve o yetenek yoksa **yedek yolu** yazar. Uyarlama yapılırken bu iki sütuna bakılır.

### Modül 1 — Çekirdek

| Özellik | Sürüm | Ne yapar | Monitörden ne ister | Yoksa |
|---|---|---|---|---|
| Monitör tanıma | v1 | EDID'den marka, model, seri no, bağlantı tipi ve bağlı kartı gösterir; hedef monitörü doğrular; zayıf bağlantıda uyarır | Okunabilir EDID | — |
| Çoklu monitör altyapısı | v1 | Her monitör kendi profiliyle; hedef monitöre özel testler sadece onda | — | — |
| Monitör profil dosyası | v1 | Monitöre özel bilgiler (spec sınırları, DDC kodları, OSD yetenekleri, titreşimsiz taban, konum sonucu) kodda değil veri dosyasında | — | — |
| Kullanıcı profili | v1 | Anahtar: **monitör + seri no + port + kart.** Onaylı Hz'ler, timing'ler, Hz başına RGB aralığı, presetler, efekt ayarları, Hz başına kalibrasyon | — | — |
| Yedek ve geri alma | v1 | Orijinal EDID, DDC değerleri ve GPU tablosunun yedeği; geri alma noktaları; "fabrika ayarına dön"; kurtarma dosyası; kaldırınca tam temizlik | — | — |
| Yönetici yardımcısı | v1 | Sadece izinli komutlar (EDID override, sürücüyü yeniden başlatma, güç limiti, tablo aralığı kaydı) | — | — |
| Kayıt defteri, dışa aktarma, dil, tepsi | v1 | Türkçe, Azerbaycanca, İngilizce; panik kısayolu | — | — |

### Modül 2 — Yenileme hızı (Hz)

| Özellik | Sürüm | Ne yapar | Monitörden ne ister | Yoksa |
|---|---|---|---|---|
| Hz overclock | v1 | EDID override ile yeni mod; orijinal mod yerinde; timing yöntemleri (monitörün kendi, CVT-RB, sıkı); aralık sınırı güncellenir; HDMI'da CTA-861 bloğu korunur; 15 sn onay | Scaler'ın yeni zamanlamayı kabul etmesi | "Orijinal Hz'de kal" sonucu |
| Hz tavanı hesabı | v1 | Pixel clock ve yatay frekansı her adımda gösterir; spec içi/dışı | Veri sayfasında max pixel clock ve yatay frekans | Bilinmiyorsa sadece test |
| Maksimum Hz bulucu | v1 | Kullanıcı başlatır; adım adım; her adımda onay ve kare atlama testi; spec dışı adımda ayrı onay; üst sınır profilden | — | Bilinmiyorsa orijinal Hz + 20 |
| Kare atlama testi | v1 | Sırayla yanan kutular + uzun pozlamalı fotoğraf | — | — |
| Stabilite testi | v1 | Onaylı Hz'de 20–30 dk hareketli desen | — | — |
| Sürücü güncellemesi koruması | v1 | Silinen override'ı onaylı değerle geri yükler | — | — |
| İçerik Hz'leri (elle) | v2 | 48 (sinema), 50 (PAL VHS), 72 (film, 24×3); test edilerek eklenir | Dikey frekans aralığı | Kabul edilmeyen değer listeye girmez |

### Modül 3 — Görüntü ve renk

| Özellik | Sürüm | Ne yapar | Monitörden ne ister | Yoksa |
|---|---|---|---|---|
| DDC/CI yetenek testi ve paneli | v1 | Hangi kodların çalıştığını bulur; parlaklık, kontrast, RGB, renk ön ayarı, giriş, ses | DDC/CI | Parlaklık ve kontrast OSD'den; renk işleri GPU tablosuyla |
| Renk boru hattı | v1 | GPU tablosunun tek sahibi; sıra: kalibrasyon → mavi ışık → ek karartma → dithering; dış değişikliği tespit edip geri yükler | — | — |
| GPU dithering | v1 | GPU tablosu kullanan her özellikte açık (NVIDIA tam aralık RGB'de varsayılan olarak dithering yapmıyor) | — | — |
| RGB Full/Limited | v2 (HDMI'da v1) | Limited çıkışı tespit edip Full'e sabitler; Hz başına doğrular | — | — |
| Parlaklık ve ek karartma | v1 | Tek kaydırıcı: önce arka ışık, sonra GPU ile ek karartma (taban: beyazın %15'i); PWM varsa titreşimsiz mod | DDC arka ışık; PWM bilgisi | DDC yoksa arka ışık OSD'den, kaydırıcı sadece GPU kısmını yönetir |
| Mavi ışık filtresi | v1 | Kelvin tabanlı (6500K → 1900K) doğal sıcak beyaz; önce monitör RGB kazancı, sonra GPU; kalibrasyona fark; isteğe bağlı yavaş zamanlama (sadece renk) | DDC RGB kazancı | Monitörün düşük mavi ışık ön ayarı ya da tamamen GPU |
| Presetler (elle) | v1 | Gece (mavi ışık + düşük parlaklık), Kod, DaVinci, Film, Oyun | — | — |
| Canlılık | v1 | NVIDIA Digital Vibrance; sadece Film/Oyun presetlerinde | — | — |
| Dijital parlaklık koruması | v1 | Kontrast/RGB ile parlaklık **artırma** engeli; ek karartma sadece azaltma yönünde | — | — |
| Görüntü iyileştirme rehberi | v1 | RTX VSR, DLDSR, DLSS, DaVinci Super Scale | — | — |

### Modül 4 — Tekdüzelik

TN paneller veya kenardan aydınlatmalı panellerde belirgin bir parlaklık farkı varsa açılır.

| Özellik | Sürüm | Ne yapar |
|---|---|---|
| Teşhis, açı ayar deseni, oturma rehberi | v1 | Farkın sebebi açı mı arka ışık mı; konum ayarı (en etkili çözüm) |
| Dikey denge katmanı (elle) | v1 | Parlak bölgeyi görünmez katmanla hafifçe karartır |
| Ölçümle dikey denge | v2 | Telefonla göz konumundan ölçüp eğriyi otomatik hesaplar |
| Satır bazlı ton telafisi | v3 | Masaüstü yakalanıp her satır için ayrı ton eğrisiyle yeniden çizilir; varsayılan kapalı |

### Modül 5 — GPU

| Özellik | Sürüm | Ne yapar |
|---|---|---|
| GPU algılama | v1 | Kartlar, monitörün bağlı olduğu kart, temel sıcaklık |
| GPU yönlendirme paneli | v1 | Uygulama başına kart seçimi |
| Modlar (elle) | v1 | **AI** (ağır kart boş), **Oyun** (ekran kartı güçlüyse oyun doğrudan onda, değilse ağır kartta), **Render** (DaVinci ağır kartta) |
| Ekran bilgi katmanı | v1 | Hz, FPS, kart sıcaklıkları |
| GPU izleme paneli | v2 | Sıcaklık, VRAM, güç, kullanım |
| Güç yönetimi | v2 | Güç limitini sadece **düşürme** ve varsayılana dönme |

Tek kartlı bir sistemde bu modül sadece izleme ve katman olarak çalışır.

### Modül 6 — Telefon yardımcı uygulaması

| Özellik | Sürüm | Ne yapar |
|---|---|---|
| Eşleştirme | v2 | Sadece yerel Wi-Fi; PC ekranındaki kodla |
| Kamera kilidi | v2 | RAW, sabit ISO/enstantane/beyaz dengesi/odak, pozlama ≥ 1/15 s, çoklu kare (CinePixel'den) |
| Otomatik çekim ve aktarım | v2 | PC deseni gösterir, telefon çeker, fotoğraf PC'ye gelir |
| Uzaktan kontrol | v2 | Preset, parlaklık/karartma, mavi ışık, mod, onaylı Hz'ler; overclock testleri ve deneyler **yok** |

### Modül 7 — Kalibrasyon ve testler

Sıra: **konum → kontrast ve siyah → beyaz noktası → gri dengesi ve ton eğrisi → doğrulama → ICC → DaVinci LUT → Hz başına kayıt.**

| Özellik | Sürüm | Ne yapar | Monitörden ne ister | Yoksa |
|---|---|---|---|---|
| Fotoğraf analiz çekirdeği | v2 | DNG renk matrisiyle yaklaşık XYZ, çoklu kare ortancası, pozlama basamaklama, tekrarlanabilirlik | — | — |
| Kontrast ve siyah | v2 | Açık ve koyu basamakların görünür kaldığı nokta | DDC kontrast | OSD talimatı |
| Beyaz noktası | v2 | D65 ya da "doğal beyaz" (telefonla en güvenilir) | DDC RGB kazancı | GPU'da |
| Gri dengesi ve ton eğrisi | v2 | 17 basamak, yumuşatılmış kanal eğrileri, GPU tablosunun kalibrasyon katmanı | — | — |
| ICC profili | v2 | Matris + ton eğrisi + VCGT | — | — |
| DaVinci 3D LUT | v2 | Rec.709 / gamma 2.4 `.cube`, Resolve'un görüntüleyici LUT'u; kalibrasyon iki kez uygulanmaz | — | — |
| Hz başına kalibrasyon | v2 | Her onaylı Hz için ayrı değerler, RGB aralığı dahil; parlaklığa dokunmaz | — | — |
| Parlaklık ve PWM ölçümü | v2 | Yaklaşık nit; titreşimsiz taban | — | — |
| Panel testleri | v2 | Ölü piksel, ışık sızması, homojenlik teşhisi (uzaktan), ghosting | — | — |
| Overdrive ve Hz testi | v2 | Overdrive modlarını karşılaştırır | OSD'de overdrive ayarı | Sadece ghosting ölçümü |
| Monitör karnesi | v2 | Tüm sonuçlar tek raporda, önce/sonra | — | — |

### Modül 8 — Deneysel (v3)

Her deney ayrı uyarı ekranından sonra açılır, kendi geri alma noktasını oluşturur.

| Özellik | Ne yapar | Ne gerektirir |
|---|---|---|
| Gizli FreeSync testi | EDID'e adaptive sync aralığı ekleyip scaler'ın kabul edip etmediğine bakar | NVIDIA'da **DisplayPort** (veya HDMI 2.1 VRR). NVIDIA HDMI üzerinden FreeSync desteklemez |
| 10-bit sinyal testi | EDID'e 10-bit desteği yazıp sinyal dener | DP ya da deep color'lı HDMI ve pixel clock payı; 6-bit panelde görsel fayda yok |
| Hareket yumuşatma (RIFE + mpv) | Yerel videoları monitörün Hz'ine yumuşatır; hesap ağır kartta | — |
| Kartlar arası geçiş | İki girişe iki kart; DDC ile giriş değiştirip ekranı süren kartı değiştirir | İki giriş (ideal: ikisi de dijital), DDC giriş seçimi |
| Kolorimetre modu | Ölçüm kaynağı telefon yerine kolorimetre (ArgyllCMS) | Kolorimetre |
| NVIDIA renk dönüşümü arka ucu | LUT-matris-LUT düzeltmesi ekran çıkışında (belgelenmemiş arayüz) | NVIDIA kart |
| Keşif | Salt okunur yavaş DDC taraması, servis modu görünümü, USB kontrol cihazı kontrolü, firmware **sadece okuma** (en son) | DDC/CI; USB kontrolü için monitörde USB hub |

---

## 5. Ölçüm kuralları (her monitörde)

- Kamera RAW, sabit ISO, enstantane, beyaz dengesi ve odak; karanlık oda; telefon sabit.
- Kamera **kullanıcının göz konumunda**, ekranın ortasındaki yamaya bakar. TN gibi açıya duyarlı panellerde bu zorunludur. Homojenlik teşhisi ise uzaktan ve zoomla yapılır.
- Renk ve parlaklık ölçümlerinde pozlama **en az 1/15 s** (FRC ve PWM titremesi ortalansın diye). Kare atlama ve PWM testleri kendi pozlamasını kullanır.
- Ölçümden önce monitör 30 dakika ısınır.
- Ölçüm başlarken mavi ışık, ek karartma, canlılık, katmanlar ve **dinamik kontrast** kapanır. DDC ile kapatılamıyorsa kullanıcıya OSD'den kapattırılır. Bitince eski hale döner.
- Telefon ölçümleri "yaklaşık" olarak gösterilir. Göreceli doğruluk (gri dengesi) yüksek, mutlak doğruluk (D65) kolorimetreden zayıf.
- Yanıp sönen desenlerden önce ışığa duyarlı epilepsi uyarısı.

---

## 6. Monitör yetenek profili

Her yeni monitör için aşağıdakiler araştırılır. Sonuçlar hem uyarlama dokümanlarına hem de koddaki **monitör profil dosyasına** yazılır.

| Araştırılacak bilgi | Nereden | Neyi etkiler |
|---|---|---|
| Panel tipi (IPS / VA / TN) | Veri sayfası | Kamera konumu, tekdüzelik modülü, beklentiler |
| Arka ışık tipi (kenardan / arkadan) | Veri sayfası, inceleme | Tekdüzelik teşhisi |
| Bit derinliği (8-bit / 6-bit + FRC / 8-bit + FRC) | Veri sayfası | 10-bit testi, ölçüm pozlaması, dithering |
| Girişler ve sürümleri (DP, HDMI, VGA, USB-C) | Veri sayfası | Kablolama, overclock yolu, RGB aralığı önceliği, FreeSync testi, kartlar arası geçiş |
| Max pixel clock, yatay ve dikey frekans aralığı | Veri sayfası, EDID | Hz tavanı, içerik Hz'leri, 10-bit testi |
| DDC/CI desteği ve kodlar | Kılavuz + ilk gün testi | Görüntü modülü ana yoldan mı, GPU yedek yolundan mı çalışacak |
| OSD özellikleri (overdrive, renk ön ayarları, düşük mavi ışık, dinamik kontrast, bilgi ekranı) | Kılavuz + ilk gün testi | Overdrive testi, mavi ışık kısayolu, ölçüm hazırlığı |
| USB hub, hoparlör | Veri sayfası | Keşif'teki USB kontrolü, DDC ses |
| Adaptive sync | Veri sayfası | FreeSync testi |
| PWM / flicker-free | Sertifikalar + ilk gün testi | Titreşimsiz karartma, ölçüm pozlaması |
| Parlaklık, kontrast, gam, görüş açısı, fabrika kalibrasyonu | Veri sayfası | Beklentiler, karne |
| Panel modeli | Panel veritabanları | Overclock beklentisi |

### Karar kuralları

1. **Bağlantı HDMI ve kart NVIDIA** → RGB aralığı kontrolü v1'e alınır.
2. **NVIDIA ve monitörde DP veya adaptive sync yok** → FreeSync testi çıkarılır.
3. **DDC/CI yok** → Görüntü modülü GPU yoluna geçer; arka ışık ve kontrast sadece OSD'den.
4. **Panel 6-bit + FRC** → 10-bit testinin görsel faydası yok; ölçümlerde uzun pozlama; dithering daha önemli.
5. **TN panel** → kamera göz konumunda; tekdüzelik modülü açılır; konum kalibrasyondan önce yapılır.
6. **OSD'de overdrive ayarı yok** → Hz başına sadece ghosting ölçümü.
7. **USB hub yok** → USB kontrolü çıkar.
8. **Max pixel clock ve yatay frekans biliniyor** → bulucunun üst sınırı hesaplanır, spec dışı adımlar işaretlenir ve ayrıca onay ister:
   `Hz tavanı ≈ en küçüğü ( pixel clock max ÷ (yatay toplam × dikey toplam) , yatay frekans max ÷ dikey toplam )`
9. **Monitörün tek dijital girişi var** → kartlar arası geçiş ya VGA + aktif adaptörle (düşük kalite, isteğe bağlı) yapılır ya da çıkar.
10. **Dikey frekans aralığı** → içerik Hz'lerinden hangilerinin spec içinde olduğunu belirler.
11. **Flicker-free değil** → titreşimsiz karartma modu ve PWM testi eklenir.
12. **Kablolama** → monitör en iyi dijital girişinden (DP > HDMI > VGA) ekranı süren karta bağlanır; ağır kart boş kalır. Ekran kartı güçlüyse oyunlar doğrudan onda çalışır. İkinci kablo sadece kartlar arası geçiş için.

---

## 7. Mimari özeti

- **Süreçler:** Ana uygulama (normal kullanıcı) · yönetici yardımcısı (sadece izinli komutlar) · telefon uygulaması (yerel ağ) · harici araçlar.
- **Katmanlar:** Arayüz → Uygulama servisleri → **Güvenlik** → Donanım (EDID, DDC, NVIDIA, GPU tablosu, Windows ekran, sürücü) → Saf mantık (EDID, timing, Kelvin, karartma planı, kalibrasyon çözücü, LUT/ICC, fotoğraf analizi).
- **Renk boru hattı:** İçerik → masaüstü (katmanlar) → GPU tablosu [kalibrasyon → mavi ışık → ek karartma] → dithering → kablo (Full RGB) → monitör RGB kazancı → arka ışık → panel. GPU tablosuna sadece boru hattı yazar.
- **Teknoloji:** C# / .NET 8 + WPF · Kotlin + Camera2 · görüntü işleme (OpenCV sarmalayıcısı) + DNG çözücü (LibRaw) · PresentMon · mpv + VapourSynth + RIFE · ArgyllCMS.
- **Monitöre özel bilgiler kodda değil, veri dosyasında.** Yeni monitör = yeni profil dosyası + uyarlama dokümanları; uygulama kodu değişmez.

---

## 8. Geliştirme yöntemi

**Sürümler:** v1 temel · v2 ölçüm (telefon uygulaması dahil) · v3 deneysel. Her sürüm sonunda kullanıcının gerçek monitörde test ettiği bir kontrol noktası.

| Sınıf | Ne tür iş | Yazan | İnceleyen |
|---|---|---|---|
| **A** | Donanıma veya sistem ayarına yazan, geri dönüşü zor ya da güvenlikle ilgili iş | Opus | Sonnet + güvenlik kontrol listesi |
| **B** | Mantık, algoritma, akış | Sonnet | Opus |
| **C** | Arayüz, metin, basit ayar ekranı | Sonnet | Opus |
| **K** | Faz sonu kontrol noktası | Kullanıcı (gerçek monitör) | Claude app – Opus – max |

- Yazan ve inceleyen her zaman farklı model; inceleme atlanmaz.
- Görev başına yaklaşık 200 satır; önce test; sözleşmeler kodda; 🔧 görevler gerçek monitör testi olmadan bitmez.
- `LESSONS.md` her faz sonunda; kullanım limiti yaklaşınca yeni göreve başlanmaz.

---

## 9. Bilerek çıkarılanlar (her monitörde geçerli)

| Özellik | Neden |
|---|---|
| Firmware değiştirme / "unlock" | Monitörü kalıcı bozabilir; sadece okuma kaldı |
| Saate göre / ortam ışığına göre otomatik parlaklık | Kural 1; göz yorgunluğu ve gereksiz monitör belleği yazması |
| Varlık algılama | Sık değişiklik; Windows zaten boşta ekranı kapatıyor |
| Uygulamaya göre otomatik preset, otomatik içerik Hz'i | Her geçişte ekran değişir/kararır; yerine elle seçim |
| Mavi ışık açılınca arka ışığı artırarak telafi | Kural 1 |
| İki boyutlu homojenlik düzeltme katmanı | Karmaşık, kazancı az; gerekirse sadece dikey hali (Modül 4) |
| CRT simülasyonu, Black Frame Insertion | 60–75 Hz'de titreşir |
| Yüksek çözünürlük sinyali testi | DLDSR'den kötü sonuç |
| Takılı piksel düzeltici, kullanım istatistikleri, telefonla gecikme ölçümü | Faydası az / seçilmedi |
| Scaler kartı veya panel değiştirme | Donanım; maliyeti ve riski kazancına değmez |
| Telefonu HDMI ile monitöre bağlamak | Kullanıcı kullanmayacak |

---

## 10. Yeni monitör uyarlama şablonu

Uyarlama ya tek bir dosya (`uyarlama/<MONITÖR>.md`) ya da tam bir doküman seti (bu repodaki `docs/` gibi) olur. İçermesi gerekenler:

1. **Özet:** en büyük değişiklikler.
2. **Araştırma:** bölüm 6'daki tablonun doldurulmuş hali, önceki monitörle karşılaştırma, kaynaklar.
3. **Hz tavanı hesabı:** kural 8'deki formülle tablo ve gerçekçi beklenti.
4. **Kablolama ve kurtarma yolu.**
5. **Özellik özellik karar tablosu:** ✔ aynen · ✎ değişir · ✖ çıkar · ★ yeni · ↑ öne alındı.
6. **Monitöre özel kurallar.**
7. **Gerçekçi beklentiler.**
8. **Görev listesi:** değişen, taşınan, çıkan ve eklenen görevler (ya da yeniden numaralanmış tam liste).
9. **İlk gün kontrolü:** kodlamadan önce kullanıcının monitörde yapacağı kısa kontroller.
10. **Açık sorular.**
11. **Kaynaklar.**
