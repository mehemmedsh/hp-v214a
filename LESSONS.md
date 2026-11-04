# LESSONS

Geliştirme sırasında öğrenilenler. Her faz sonunda güncellenir. Bir hata iki kez yaşandıysa buraya yazılır ve ilgili görevin kontrol listesine eklenir.

Biçim: `Tarih · Görev · Ne oldu · Bundan sonra ne yapılacak`

## Donanım bulguları

HP P21b G4 ünitesinde test edilen gerçek değerler: ilk gün kontrolü (`docs/09-ILK-GUN.md`), maksimum Hz, kabul edilen içerik Hz'leri, DDC'nin desteklediği kodlar, overdrive, RGB aralığı, PWM, üst-alt farkının sebebi, oturma ölçüleri. Ham veriler `donanim/hp-p21b-g4/` içinde.

- 2026-10-30 · — · Monitör Lenovo T24i-20'den HP V214a'ya değişti; ekran kartı RTX 3080 oldu (3090 boş) · Plan bu repoda HP V214a'ya göre yeniden yazıldı
- 2026-10-30 · — · Kullanıcı gözlemi: ekranın altı daha parlak, üstü daha karanlık · İlk gün kontrolünde sebebi (açı / arka ışık) belirle; T401 ve T402 buna göre
- 2026-10-30 · İlk gün · Monitör aslında **HP P21b G4** (EDID adı ve `HPN36A2`, OSD, Windows; üretim 2021/48). Sinyal sınırları V214a ile aynı (50–60 Hz, 30–80 kHz, 170 MHz); TN, 6-bit + FRC, HDMI 1.4 + VGA, 250 nit, flicker-free, OSD'de overdrive yok · Dokümanlar P21b G4'e uyarlandı; repo adı aynı kaldı. Ders: plana başlamadan önce modeli EDID'den doğrula, etiketten ya da hatıradan değil
- 2026-10-30 · #1 · Baş yukarı-aşağı oynayınca üst-alt farkı değişiyor → sebep bakış açısı (TN) · Önce konum (T401), sonra T402
- 2026-10-30 · #2 · Oturma ölçüsü alınmadı; kullanıcı şu an sorun görmüyor · T401'de açı deseniyle bakılacak
- 2026-10-30 · #3–4 · DDC/CI çalışıyor, MCCS 2.2, 48 kod. R/W: 10, 12, 14 (2, 4, 5, 8, 11–15; şu an 5 = Neutral), 16/18/1A (255/255), 0C, 2E, 60 (17 = HDMI, 1 = VGA), 62, 6C/6E/70 (siyah seviyesi, 128/255), 86, 87, 8D, CA, CC, D6, DA. Salt okunur: AE, AC, C0 (2167 saat), C8 (muhtemelen MStar), C9 (fw 1.0). E6–FF üreticiye özel · T004 profiline ve T301'e işlenecek; 14'ün 12–15 değerleri OSD'den seçilip okunarak eşlenecek
- 2026-10-30 · #4 · **AE** gerçek gelen dikey frekansı ×100 veriyor (60 Hz → 6000, 74 Hz → 7400). **AC** yatay frekansı Hz olarak 16 bit taşmalı veriyor (60 Hz → 1964 = 67 500 − 65 536; 74 Hz → 17264 = 82 800 − 65 536) · T301 AC'yi düzeltir; T203/T206 her Hz değişiminden sonra AE ve AC ile otomatik doğrulama yapar
- 2026-10-30 · #5 · NVIDIA çıkış dinamik aralığı baştan Full; Windows: 8-bit, RGB, SDR · T304 sadece doğrulama yapar
- 2026-10-30 · #6–8 · OSD: Image menüsünde overdrive yok (Sharpness Level 4, Image Scaling Fill to Screen, Overscan Off). Color: Warm, Neutral, Cool, Night, Native, Low Blue Light, Reading, HP Enhance+, Custom RGB. Information: fw CTM121, 2167 saat. Alt çubuk her menüde "Current Mode … Hz" gösteriyor · T804 sadece ghosting; T307 Low Blue Light kısayolu
- 2026-10-30 · #9 · 1/1000 s'de parlaklık 100, 75, 50, 35, 25, 10, 0'da yatay bant yok (görülen renkli desen moiré) → PWM yok, HP'nin flicker-free bilgisi doğrulandı · Titreşimsiz mod çıktı (T306); T801 sadece nit ölçümü
- 2026-10-30 · #10 · Gece ışığı kapalı, ICC profili yok, çakışan yazılım yok
- 2026-10-30 · #11 · EDID kaydedildi (`donanim/hp-p21b-g4/edid-original.bin`). Native timing 2200×1125 (88/44/148, 4/5/36, +/+), 148.5 MHz. CTA bloğunda 1080p50 (VIC 31) var. Video Capability QS=1 · T101 test seti; T805'te 50 Hz override gerektirmez
- 2026-10-30 · #12 · 74 Hz CVT-RB (2080×1119, 82.81 kHz, 172.24 MHz) çalışıyor: Windows aktif sinyal 74 Hz, AE = 7400, testufo frameskipping'de 1/10 s'de 8 ardışık kare, atlama yok · Günlük kullanımda gözlem; sorun olursa 72 Hz
- 2026-10-30 · #12 · 75 Hz CVT-RB (2080×1119, 83.9 kHz, 174.5 MHz) → "Input Signal Out of Range"; scaler sınırı katı uyguluyor, kare atlamak yerine görüntü vermiyor. NVIDIA'nın kendi testi 15 sn'de geri döndü · Gerçek sınır 82.8–83.9 kHz (172.2–174.5 MHz) arası
- 2026-10-30 · #12 · 75 Hz sıkı dikey (2080×1100, dikey 3/5/12, 82.5 kHz, 171.6 MHz) NVIDIA testinden geçti. Not: NVIDIA penceresi yatay frekansı 81.40 kHz gösterdi; bu 74 Hz'den kalan eski değer, doğrusu 75 × 1100 = 82.5 kHz · Windows, AE = 7500 ve kare atlama ile doğrulanacak. Ders: aynı Hz'de dikey boşluğu kısaltmak yatay frekansı düşürür; bulucu (T206) önce dikey toplamı küçültmeli
- 2026-10-30 · #12 · 76 Hz çok sıkı dikey (2080×1092; yatay 48/32/80, dikey 3/5/4; 82.99 kHz, 172.62 MHz) NVIDIA testinden geçti, Windows aktif sinyal 76 Hz · AE = 7600 ve kare atlama ile doğrulanacak; kenar, siyah seviyesi, hoparlör ve kararma günlük kullanımda gözlenecek
- 2026-10-30 · #12 · 77 Hz (2000×1092; yatay 48/32/0, dikey 3/5/4; 84.08 kHz, 168.17 MHz) → "Out of Range". Pixel clock çalışan 76 Hz'den düşük olduğu halde reddedildi → **sınır yatay frekansta, 83.0–83.9 kHz arası**; tavan 76 Hz (77 Hz en az boşlukla bile 83.9 kHz) · T206 aralığı 61 → 76; profilde sınır pixel clock değil yatay frekans olarak tutulur
- 2026-10-30 · #12 · NVIDIA özel çözünürlük penceresi Hz değişince kHz alanını güncellemiyor (75 Hz'de 81.40, 76'da 81.90, 77'de 82.99 gösterdi) · Doğru değer her zaman Hz × dikey toplam; uygulama kendi hesabını gösterir (T201)

## Geliştirme dersleri

- 2026-10-30 · Plan · Bulut oturumunda deneme: .NET SDK yoktu, Microsoft'un indirme sunucusu engelli; Ubuntu paketi `dotnet-sdk-10.0` kuruldu. WPF projesi `EnableWindowsTargeting` ile Linux'ta derlendi, `net10.0` testleri çalıştı. Sadece `App` adlı projede `App` sınıfı ad alanıyla çakıştı (CS0426) · Kurulum `CLAUDE.md`'de; projeler `MonitorTuner.*` adını alır
- 2026-10-30 · Plan · Antigravity `CLAUDE.md`'yi okumuyor, `AGENTS.md`'yi okuyor · Kurallar `AGENTS.md`'de tek kaynak; `CLAUDE.md` onu içe aktarıyor
- 2026-10-30 · Plan · Kapsam daraltılırken dithering, canlılık ve GPU boru hattı bir an çıkarıldı; kullanıcının birinci önceliği görüntü kalitesi olduğu için geri alındı · Bir özelliği çıkarmadan önce birinci önceliğe (görüntü kalitesi, göz konforu) etkisini yaz
- 2026-11-03 · Plan · Qwen kurulumunda dört tuzak yaşandı:
  - `ollama run` penceresine çok satırlı yapıştırma her satırı ayrı mesaj olarak gönderdi.
  - PowerShell 5.1'de boruyla verilen Türkçe metin bozuldu.
  - `*ollama*` ile `Stop-Process` kurulum programını da kapattı.
  - 16k bağlamı aşan girdi sessizce kırpıldı.
  - Qwen 2026-11-04'te çıkarıldı (betik, Ollama adımları ve rolleri silindi); bu tuzaklar artık geçerli değil, kayıt olarak duruyor.
- 2026-11-03 · Plan · Betik yazılırken ajanın aracı `\u00e7` gibi kaçışları gerçek harfe çevirdi. BOM'suz .ps1'de bu harfler PowerShell 5.1'de bozulur, ’ gibi tırnaklar dizeyi erken kapatır · `.ps1` dosyaları ASCII kalır; Türkçe harf `TR '{00e7}'` biçiminde yazılır, Türkçe metinler ayrı UTF-8 dosyalarda tutulur
- 2026-11-04 · T305 · `Loc.Get` anahtarı tek noktalıdır (`Özellik.Ad`); `Setting.Brightness` gibi ikinci nokta ArgumentException atar · .resx anahtarında alt çizgi kullan (`DdcPanel.Setting_Brightness`)
- 2026-11-04 · T305 · Aynı klasörde iki ajan oturumu (T305 ve T306) çalışırken `02-MIMARI.md` ve HEAD ben okumadan değişti; ortak dosyaya yazmadan önce yeniden oku, commit'e sadece kendi dosyalarını ekle (`git add` yol yol)
