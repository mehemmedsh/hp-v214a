# İnceleme Notları

Planın son kontrolünün kaydı. Bu sürüm üç değişiklikten sonra yazıldı: monitör HP V214a oldu, ekran kartı RTX 3080 oldu, renk, parlaklık, mavi ışık ve üst-alt farkı için yeni yöntemler eklendi.

Önceki geçmiş: Lenovo T24i-20 planı ve onun üç turluk incelemesi `Myapplab/Lenovo-monitor` reposunda.

---

## 1. Tur: Özelliklerin yeniden kontrolü

| # | Bulunan sorun | Düzeltme |
|---|---|---|
| 1.1 | Ekran kartı 3070 yerine 3080 oldu. 3080 güçlü bir kart; oyunları 3090'da render edip 3080'den göstermek gereksiz kopya gecikmesi ekliyordu. | Oyun modu artık oyunu **doğrudan 3080'de** çalıştırıyor. 3090 aynı anda LLM için boş kalabiliyor. |
| 1.2 | 3090 + 3080 + i3-10100 tam yükte ≈ 800 W; 3090 + 3070'e göre güç payı azaldı. | 1000 W yeterli. Güç yönetimi iki karta genişletildi (T807). İki PSU kuralı eklendi: bir kartın bütün güç girişleri aynı PSU'dan. |
| 1.3 | GPU renk tablosunu dört özellik birden kullanmak istiyordu (kalibrasyon, mavi ışık, ek karartma, DDC yoksa beyaz noktası). Her biri ayrı yazarsa birbirini ezer. | Tek sahip: **renk boru hattı** (T302). Eski uyarlamadaki koşullu "GPU LUT yedek yolu" koşulsuz v1 çekirdeği oldu. |
| 1.4 | Oyunlar, Windows Gece ışığı, f.lux ve NVIDIA masaüstü renk ayarları GPU tablosunu değiştirebiliyor. | Boru hattı dış değişikliği tespit edip kullanıcının tablosunu geri yüklüyor. Kural 1'e 3. istisna olarak yazıldı (yeni değer seçmez). |
| 1.5 | Windows derin karartma ve çok sıcak renk tablolarını reddediyor. | `GdiIcmGammaRange` kaydı yönetici yardımcısının izinli komutlarına eklendi; eski değer yedeklenir, kaldırınca geri döner. |
| 1.6 | NVIDIA tam aralık RGB'de varsayılan olarak dithering yapmıyor; GPU tablosu değiştirilince bantlaşma çıkar. | GPU tablosu kullanan her özellikte dithering açık tutuluyor (T303, T704). |
| 1.7 | Ek karartma, "dijital parlaklık koruması" kuralıyla çelişiyordu. | Koruma sadece **artırma** yönünde. Ek karartma sadece azaltma yönünde, sadece arka ışık tabanının altında ve arayüzde "dijital" etiketiyle. |
| 1.8 | Uygulama çökerse koyu bir tablo ekranda kalabilir. | Karartma tabanı (beyazın %15'i), kurtarma dosyası tabloyu da sıfırlıyor, açılışta tablo kontrolü, panik kısayolu (yeni Kural 6). |
| 1.9 | Eski gece modu sadece monitörün RGB kazancıyla sınırlıydı; sıcak tonlara inemiyordu. | Kelvin tabanlı **mavi ışık filtresi** (T307): siyah cisim eğrisi, önce monitör sonra GPU. |
| 1.10 | Mavi ışık filtresi beyazı doğal olarak karartıyor (3400K'de ~%65). Telafi için arka ışığı artırmak Kural 1'i bozar. | Telafi eklenmedi; kayıp arayüzde gösteriliyor. |
| 1.11 | V214a flicker-free değil; arka ışığı kısmak PWM titremesi yaratabilir. | PWM testi ve **titreşimsiz karartma modu**: arka ışık titreşimsiz tabanın altına inmez, kalanı GPU yapar. |
| 1.12 | TN'de kalibrasyon sadece tek bir göz konumu için geçerli. Önceki plan kamerayı "ekran merkezine dik" koyuyordu. | Kamera **göz konumunda**. Konum ayarı (T401) kalibrasyondan önce. |
| 1.13 | Kullanıcı gerçek bir üst-alt ışık farkı gördü. Daha önce çıkarılan homojenlik katmanı bu sorunu çözebilirdi. | İki boyutlu katman yerine sadece **dikey** hali geri geldi: elle (v1), ölçümlü (v2), gölgelendiricili (v3). Asıl çözüm konum. |
| 1.14 | Gölgelendiricili telafi korumalı videoları siyah gösterir, tam ekran özel mod oyunlarda çalışmaz. | v3'te, varsayılan olarak kapalı; açılırken uyarı; panik kısayolu. |
| 1.15 | Katmanlar ekran görüntüsüne ve kayda girebilirdi. | Katmanlar ekran yakalamadan hariç tutuluyor. |
| 1.16 | DaVinci LUT'u, sistem genelindeki GPU kalibrasyonunu ikinci kez uygulayabilirdi. | LUT, kalibrasyon açıkken ölçülen değerlerden üretiliyor (T706). |
| 1.17 | Telefonla mutlak D65 hedefi yaklaşık; kullanıcı daha iyi renk istiyor. | "Doğal beyaz" seçeneği (telefonla en güvenilir), ölçüm doğruluğu kuralları (DNG matrisi, çoklu kare, pozlama basamaklama, tekrarlanabilirlik) ve isteğe bağlı kolorimetre modu (T905). |
| 1.18 | Eski T703 (kontrast + gamma) tek görev için büyüktü ve sıra yanlıştı: gri dengesi beyaz noktasından önce gelemez. | Sıra: kontrast ve siyah (T702) → beyaz noktası (T703) → gri dengesi ve ton eğrisi (T704). |
| 1.19 | Canlılık "doğru renk" hedefiyle çelişiyor. | Sadece Film ve Oyun presetlerinde; ölçümde ve DaVinci'de kapalı; kapanışta varsayılana döner. |
| 1.20 | Monitöre özel bilgiler (spec sınırları, DDC kodları, PWM tabanı) kod içine dağılabilirdi. | Monitör profil dosyası (T004). Yeni monitör = yeni profil dosyası. |
| 1.21 | Önceki uyarlama "3070'in HDMI'ı" diyordu; kurtarma yolu da 3070'e göreydi. | Tüm dokümanlar 3080'e göre güncellendi; kurtarma: 3090'ın HDMI'ı veya 3080'in DP çıkışı (DP→HDMI). |

---

## 2. Tur: Faz tablosu kontrolü (script ile)

- 66 görev, tekrar eden numara yok. ✔
- Hiçbir görev kendinden sonra gelen bir göreve bağımlı değil. ✔
- Her görev numarası kendi fazıyla uyumlu (T3xx → Faz 3). ✔
- A sınıfının hepsini Opus, B ve C sınıfının hepsini Sonnet yazıyor; K görevleri kullanıcıda. ✔
- Her görevde yazan ve inceleyen farklı model. ✔
- Özet tablosu sayımla birebir: 22 Opus, 41 Sonnet, 3 kontrol noktası. ✔
- Diğer dokümanlarda geçen her görev numarası faz tablosunda var. ✔

---

## 3. Tur: Son tutarlılık kontrolü

- Otomatik Hz veya otomatik parlaklık değişimi yapan bir görev yok. Mavi ışık zamanlaması sadece rengi değiştirir ve varsayılan olarak kapalı. ✔
- Firmware'e yazma hiçbir görevde yok; sadece okuma (T908, en son görev). ✔
- Donanıma veya sistem ayarına yazan her katman A sınıfı ve Opus tarafından yazılıyor: EDID (T101, T202), sürücü (T203), DDC (T301), renk boru hattı (T302), NVIDIA ayarları (T303, T304, T309), ICC (T705), içerik Hz'leri (T805), güç limiti (T807), deneyler (T901, T903, T906–T908). B görevleri bu katmanları kullanır, kendisi doğrudan yazmaz. ✔
- GPU renk tablosuna boru hattı dışında yazan görev yok. ✔
- Çıkarılan özellikler sadece "çıkarıldı" listelerinde ve genel fikirdeki "ne gerektirir" tablosunda geçiyor. ✔
- Bu repoda uygulama kodu yok, sadece plan. ✔

---

## 4. Eski → yeni görev eşlemesi

Lenovo reposundaki görev numaralarından bu repodakilere:

| Eski | Yeni | Not |
|---|---|---|
| T001–T003 | T001–T003 | — |
| — | T004 | Yeni: monitör profil dosyası |
| T101–T105 | T101–T105 | HP ve 3080'e göre güncellendi |
| T201–T208 | T201–T208 | Hz tavanı, 61 → 75, CTA-861 |
| T301 | T301 | + DDC yetenek testi |
| — | T302 | Yeni: renk boru hattı (eski koşullu Y2) |
| T305 | T303 | Dithering |
| T709 | T304 | RGB aralığı, v2'den v1'e |
| T302 | T305 | DDC paneli (+ ses) |
| — | T306 | Yeni: parlaklık ve ek karartma (koruma buraya taşındı) |
| T304 | T307 | Gece modu → mavi ışık filtresi |
| T303 | T308 | Presetler (+ Oyun) |
| — | T309 | Yeni: canlılık |
| T306 | T310 | Tepsi (+ panik kısayolu) |
| T307 | T311 | Rehber |
| Y1 (HP uyarlaması) | T401 | Açı ayar deseni + teşhis |
| — | T402 | Yeni: dikey denge katmanı |
| T401–T404 | T403–T406 | GPU (3090 + 3080) |
| T501–T503 | T501–T503 | — |
| T601–T604 | T601–T604 | Kamera göz konumunda |
| T701 | T701 | + DNG matrisi, tekrarlanabilirlik |
| T703 | T702 + T704 | Bölündü: kontrast/siyah ve gri/gamma |
| T702 | T703 | Beyaz noktası |
| T704 | T705 | ICC |
| — | T706 | Yeni: DaVinci 3D LUT |
| T705 | T707 | Hz başına kalibrasyon |
| T706 | T801 | Parlaklık + PWM |
| T707 | T802 | Panel testleri |
| — | T803 | Yeni: ölçümle dikey denge |
| T708 | T804 | Ghosting / overdrive |
| T710 | T805 | İçerik Hz'leri |
| T711, T712 | T806, T807 | İzleme, güç (iki kart) |
| T713, T714 | T808, T809 | Karne, v2 kontrol noktası |
| T801 | ✖ | FreeSync testi çıkarıldı |
| T802 | T901 | 10-bit (50 Hz) |
| T803 | T902 | RIFE |
| T804 | T903 | Kartlar arası geçiş (VGA) |
| — | T904 | Yeni: satır bazlı ton telafisi |
| — | T905 | Yeni: kolorimetre modu |
| — | T906 | Yeni: NVIDIA renk dönüşümü arka ucu |
| T805, T806 | T907, T908 | Keşif (USB kontrolü çıkarıldı), firmware okuma |
| T807 | T909 | v3 kontrol noktası |

**Toplam:** 55 eski görevden 1'i çıktı, 1'i ikiye bölündü, 11 yeni görev eklendi → 66.

---

## 5. Tur: İlk gün kontrolü ve monitör düzeltmesi (2026-10-30)

İlk gün kontrolünde EDID, DDC, OSD ve Windows'un dördü de monitörün **HP P21b G4** olduğunu gösterdi; plan HP V214a için yazılmıştı. Sinyal sınırları (50–60 Hz, 30–80 kHz, 170 MHz), panel tipi (TN, 6-bit + FRC), girişler (HDMI 1.4 + VGA) ve boyut aynı olduğu için planın yapısı değişmedi. Değişenler:

| # | Bulgu | Düzeltme |
|---|---|---|
| 5.1 | Monitör HP P21b G4 | `00-DONANIM.md` yeniden yazıldı; diğer dokümanlarda model adı düzeltildi; repo adı aynı kaldı. Ham veriler `donanim/hp-p21b-g4/` |
| 5.2 | Flicker-free; ilk gün testinde PWM yok | Titreşimsiz mod ve PWM testi bu monitörde çıktı (T306, T801); genel fikirde profil koşuluyla kaldı |
| 5.3 | OSD'de overdrive yok | T804 sadece ghosting |
| 5.4 | 74 Hz CVT-RB (82.81 kHz, 172.24 MHz) kare atlamadan çalışıyor; 75 Hz CVT-RB (83.9 kHz) reddedildi; 75 Hz 2080×1100 (82.5 kHz) NVIDIA testinden geçti | Hz tablosu gerçek sonuçlarla güncellendi. T206: her Hz'de önce dikey toplamı küçük timing; T201 dört timing yöntemi |
| 5.4b | Aynı gün: 76 Hz (2080×1092, 83.0 kHz) NVIDIA testinden geçti, Windows 76 Hz; 77 Hz (2000×1092, 84.1 kHz, 168.2 MHz) reddedildi | Sınırın pixel clock değil yatay frekans olduğu anlaşıldı; tavan 76 Hz. T206 aralığı 61 → 76 |
| 5.5 | DDC AE gerçek Hz'i, AC 16 bit taşmalı yatay frekansı veriyor | T301 AE/AC okumasını ve taşma düzeltmesini yapar, overclock akışına bağlar (T301 artık T206'ya da bağımlı) |
| 5.6 | DDC'de kanal başına siyah seviyesi (6C/6E/70) var | `00-DONANIM.md` ve T004 profiline yazıldı; T702'de kullanılabilir |
| 5.7 | EDID'de 1080p50 zaten var | T805'te 50 Hz override gerektirmez |
| 5.8 | NVIDIA baştan Full RGB gönderiyor; Video Capability QS=1 | T304 değişmedi, sadece doğrulama yapacak |
| 5.9 | Üst-alt farkının sebebi bakış açısı | T401 / T402 planlandığı gibi; oturma ölçüsü T401'e kaldı |

Kontrol: görev sayısı (66) ve sınıflar değişmedi. T203'e DDC bağımlılığı eklemek Faz 2'yi Faz 3'e bağlayacağı için yapılmadı; bağlantı ters yönde T301'e verildi, böylece "hiçbir görev kendinden sonra gelen bir göreve bağımlı değil" kuralı korunuyor.

---

## 6. Tur: Kullanım (usage) incelemesi (2026-10-30)

Amaç: uygulamanın kalitesini ve geliştirme akışını bozmadan Claude kullanımını azaltmak. En büyük kalem, her oturumun başta okuduğu dokümanlardı: 7 dosya, yaklaşık 72 KB. Bu, 66 görevin yazma ve inceleme oturumlarında (yaklaşık 130 oturum) tekrar ediyor ve oturumdaki her adımda yeniden işleniyordu.

| # | Bulgu | Değişiklik |
|---|---|---|
| 6.1 | Her yazma oturumu README, 00, 02, 03, 04, 08 ve LESSONS'ın tamamını okuyordu; görevlerin çoğu bunların küçük bir kısmını kullanıyor | `CLAUDE.md` (otomatik okunur): kurallar özeti ve görev bazlı okuma haritası. Örnek görevlerde (T001, T206, T306, T402, T404) baştaki okuma 72 KB'tan 26–31 KB'a indi (yaklaşık %55–65). 02 ve 03 her kod görevinde tam okunmaya devam ediyor |
| 6.2 | Görev ayrıntısının bir kısmı sadece `01-OZELLIKLER.md`'deydi ve eski okuma listesinde yoktu | Harita, görev numarasının geçtiği 01 ve 08 bölümlerini de okutuyor (küçük maliyet, kalite artışı) |
| 6.3 | Yazanın "inceleyiciye 3 nokta" notu inceleme oturumuna aktarılmıyordu | Notlar commit mesajında; inceleme oradan başlıyor. Şablonlar kısaldı (`05-HANDOFF.md`) |
| 6.4 | İnceleme DÜZELT derse ne olacağı tanımlı değildi | Düzeltme şablonu: yazan model, sadece DÜZELT maddeleri; yeniden inceleme sadece düzeltme commit'leri |
| 6.5 | Bulut oturumunda .NET SDK yok ve Microsoft'un indirme sunucusu engelli; kod derlenmeden ve test edilmeden CI'a gidecekti | Ubuntu paketi `dotnet-sdk-10.0` kuruluyor (denendi); ortam kurulum betiğine eklenmesi önerildi (`05-HANDOFF.md`, bölüm 0). Saf mantık platformdan bağımsız projelerde; WPF `EnableWindowsTargeting` ile Linux'ta derleniyor (denendi) |
| 6.6 | .NET 8'in desteği 10 Kasım 2026'da bitiyor | Öneri .NET 10 (LTS, Kasım 2028'e kadar); T001'de onaylanır |
| 6.7 | Derleme, test ve CI çıktısı uzun | Sessiz komutlar ve `tools/dev` betikleri; CI'da sadece hatalı adımın logu |
| 6.8 | T303 ve T304 küçük ve aynı modülde; T304 artık sadece doğrulama | Toplam ~200 satırı aşmıyorsa tek yazma ve tek inceleme oturumunda |
| 6.9 | `05-HANDOFF.md` sadece 75 Hz doğrulamasını yazıyordu | 76 Hz eklendi; DDC 14 eşlemesi T004 öncesi kontrol olarak eklendi |

**Değiştirilmeyenler:** Görev sayısı (66), sınıflar, yazan/inceleyen dağılımı ve effort seviyeleri. B incelemeleri Opus high'ta kalıyor; burada tasarruf kaliteyi düşürür. C incelemelerinin effort'unu düşürmek kullanıcının kararına bırakıldı.

## 7. Tur: Kapsam daraltması ve Antigravity düzeni (2026-10-30)

Kullanıcının kararları: en önemli şey görüntü kalitesi ve göz konforu; en yüksek hız 76 Hz; DaVinci'de renk işi bu monitörde yok (sadece zaman çizelgesi, renk referansı Pixel'in ekranı); aşırı ince ayar yok; iş hızlı bitsin ama kalite düşmesin. Kodlama Antigravity'de (Gemini, Opus, Sonnet); Antigravity'nin Claude kotası bitince Claude Code.

| # | Değişiklik |
|---|---|
| 7.1 | 66 görev → 21. Çıkanlar ve nedenleri `06-CIKARILANLAR.md`'de |
| 7.2 | Hz: EDID override motoru yerine NVIDIA özel çözünürlüğü (kullanıcı ekler) + Hz paneli (T209). EDID yazılmadığı için Kural 1'in sürücü güncellemesi istisnası kalktı |
| 7.3 | Renk: kamerayla kalibrasyon yerine gözle ayar sihirbazı (T312, T313); beyaz referansı Pixel'in ekranı. ICC ve DaVinci LUT çıktı |
| 7.4 | Görüntü kalitesi için kalanlar: GPU renk boru hattı, dithering, canlılık (Film/Oyun), ek karartma, mavi ışık, renk profilleri |
| 7.5 | Yönetici yardımcısı süreci yerine tek seferlik yönetici onayı (`GdiIcmGammaRange`, T302) |
| 7.6 | Projeler: Overlay, Elevated ve Patterns çıktı; Hardware.Abstractions ve Composition eklendi (katman ihlali CI'da derleme referansından yakalanır) |
| 7.7 | Kurallar `AGENTS.md`'ye taşındı (Antigravity onu okuyor); `CLAUDE.md` onu içe aktarıyor. Ajanın PC'de uygulamayı ve donanıma yazan komutları çalıştırmaması kuralı eklendi |
| 7.8 | Modeller: A sınıfını Opus yazar, Gemini inceler; B'yi Gemini yazar, Opus inceler; C'yi Gemini (metinde Flash) yazar, Sonnet inceler. Paralel çalışma 4 klasörle (hat), 11 dalga |

**Eski → yeni görev eşlemesi**

| Eski | Yeni |
|---|---|
| T001–T004 | Aynı (T003 artık Opus yazıyor) |
| T101, T105 | Çıktı |
| T102, T103 | Aynı, sadeleşti |
| T104 | T104 (EDID kısmı çıktı; GPU tablosu, dithering ve canlılık sıfırlama) |
| T201–T208, T805 | T209 (Hz paneli) + T311 (rehber: kurulum, kare atlama, stabilite) |
| T301, T302, T305, T306, T307, T310 | Aynı |
| T303 + T309 | T303 (dithering + canlılık) |
| T304 | T312 (RGB Full deseni) |
| T308 | T308 (renk profilleri) |
| T311 + T401 | T311 (rehber) + T312 (açı deseni) |
| T402–T406, T501 | Çıktı |
| T502, T503 | Aynı |
| T601–T604 | Çıktı |
| T701–T707 | T312 + T313 (gözle ayar) |
| T801–T809 | Çıktı (T805 → T209) |
| T901–T909 | Çıktı |

## 8. Tur: Antigravity'den çıkış, Opus + Sonnet + Qwen düzeni (2026-11-03)

Kullanıcının kararı: Antigravity (Gemini, Flash) sistemden çıkar; iş Claude Code'da Opus ve Sonnet ile, yerel Qwen yardımıyla sürer.

| # | Değişiklik |
|---|---|
| 8.1 | Ana araç PC'deki Claude Code (masaüstü uygulaması, Windows); bulut oturumu yedek. Kurallar yine `AGENTS.md`'de, `CLAUDE.md` içe aktarıyor |
| 8.2 | Modeller: A'yı Opus yazar, Sonnet inceler; B'yi Sonnet yazar, Opus inceler (T003: Opus → Sonnet); C'yi Sonnet yazar, Opus inceler (metinde Medium). Biten T001, T002, T311'in kaydı değişmedi |
| 8.3 | Hatlar: `hp-gemini` → `hp-sonnet`, `hp-gemini2` → `hp-sonnet2`; dalgalar aynı |
| 8.4 | Flash'ın işleri (sonuç bildirme, kalibrasyon değerlendirmesi, araç soruları) Sonnet · Medium'a geçti; Qwen rolleri aynı |
| 8.5 | Kota: Qwen → Sonnet → Opus. Opus'un sınırı dolarsa iş Sonnet'e kaydırılmaz; Sonnet hattı sürer |
| 8.6 | Güvenlik: Antigravity'nin "terminal otomatik çalışmasın" kuralı yerine Claude Code'da varsayılan izin modu; auto ve bypass PC'de kullanılmaz |

## 9. Tur: Qwen'in çıkarılması (2026-11-04)

Kullanıcının kararı: yerel Qwen sistemden çıkar. Düzen Opus + Sonnet. (8. turdaki Qwen satırları tarihî kayıt olarak durur.)

| # | Değişiklik |
|---|---|
| 9.1 | `tools/dev/qwen-review.ps1` ve `tools/dev/qwen/` silindi; `.gitignore`'dan `.qwen/` çıktı |
| 9.2 | `AGENTS.md` → "Yerel model (Qwen)" bölümü, `04-FAZLAR.md` → "Qwen rolleri", Ön inceleme sütunu ve kota sırasının ilk basamağı, `05-HANDOFF.md` → 0.5 ve 9 çıktı |
| 9.3 | Görev akışı: yazma son adımı artık derleme ve test; commit gövdesinde Qwen satırı yok. Hata ve log açıklamalar doğrudan Sonnet · Medium'a gider |
