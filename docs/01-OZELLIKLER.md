# Özellikler

HP P21b G4 + RTX 3080 için özellik listesi (2026-10-30'da daraltıldı). **Hedef:** bu panelden çıkabilecek en iyi görüntü, göz konforu ve 76 Hz; gerekli ince ayar var, aşırısı yok. Her özelliğin yanında onu yapan görevler yazar (`04-FAZLAR.md`). Plan dışı kalanlar: `06-CIKARILANLAR.md`.

**Değişmez kural:** Hz ve parlaklık sadece kullanıcı değiştirdiğinde değişir (`03-GUVENLIK.md`).

---

## Modül 1 — Çekirdek

### 1.1 Monitör tanıma · T102
- Model, seri numarası, bağlantı tipi ve bağlı olduğu kart; HP P21b G4 olduğunu doğrular. VGA'daysa "HDMI'a geç" uyarısı verir.

### 1.2 Monitör profil dosyası · T004
- Monitöre özel bilgiler kodda değil veri dosyasında: Hz modları ve timing'leri (76 Hz tavan; 77 Hz reddedildi), spec ve ölçülen sınır, OSD yetenekleri, çalışan DDC kodları, PWM yok.

### 1.3 Ayar deposu · T102
- `%AppData%` altında JSON: temel ayar (sihirbaz sonucu), renk profilleri, mavi ışık ve karartma ayarları. Yedekler ayrı klasörde.

### 1.4 Yedek, geri alma ve kurtarma · T103, T104
- İlk açılışta DDC değerleri ve GPU renk tablosu yedeklenir; her değişiklik bir geri alma noktası oluşturur. "Fabrika ayarına dön" tek tık.
- Kurtarma dosyası GPU tablosunu sıfırlar, dithering ve canlılığı varsayılana alır. Kaldırınca tam temizlik.

### 1.5 Dil · T001, T502
- Türkçe (ana dil), Azerbaycanca, İngilizce. Altyapı ilk günden.

### 1.6 Sistem tepsisi ve kısayollar · T310
- Tepsiden renk profili, mavi ışık, parlaklık ve karartma.
- **Panik kısayolu:** Mavi ışık, ek karartma ve canlılık tek tuşla kapanır; kalibrasyon kalır.

---

## Modül 2 — Yenileme hızı

### 2.1 Hz paneli · T209
- Modlar NVIDIA özel çözünürlüğüyle bir kez eklenir; girilecek değerler profilde ve rehberde (T311). İlk gün: 76 Hz (2080×1092) tavan; 75 Hz (2080×1100) ve 74 Hz (CVT-RB) çalışıyor; 77 Hz reddedildi.
- Onaylı modlar arasında elle geçiş: **76** (masaüstü), **75** (25/50 fps video), **72** (24 fps film; eklenip test edilecek), **60** (orijinal), **50** (EDID'de zaten var). Onay 15 sn'de gelmezse önceki moda döner.
- Gelen Hz monitörün kendisinden (DDC AE) okunarak doğrulanır.
- Sürücü güncellemesi bir modu silerse uyarı ve NVIDIA'ya girilecek değerler.

### 2.2 Kare atlama ve stabilite · T311
- testufo.com kare atlama testi (uzun pozlamalı fotoğraf) ve 20–30 dk hareketli desen için adım adım rehber.

---

## Modül 3 — Görüntü, renk ve göz konforu

Yöntemlerin ayrıntısı: `08-GORUNTU-YONTEMLERI.md`.

### 3.1 DDC/CI katmanı ve paneli · T301, T305
- Panel: parlaklık, kontrast, renk ön ayarı, RGB kazancı (Custom RGB'de), keskinlik, giriş, hoparlör sesi. Bunlar monitörün içinde yapılır; renk seviyesi kaybı olmaz.
- Monitörün 9 renk ön ayarının hepsi uygulamadan seçilir; seçilenin ne yaptığı altında yazar, adı tahmin olanlar işaretli (T318).
- DDC yazmaları monitörün belleğini yormamak için sınırlanır (kaydırıcı bırakılınca tek yazma).
- **Dijital parlaklık koruması:** Kontrast veya RGB ile parlaklık artırma engellenir.

### 3.2 Ayar sihirbazı · T312, T313
- Isınma (30 dk) ve konum hatırlatması. Desenlerle: kontrast ve parlaklık (siyah ve beyaz kırpılmadan), keskinlik (yazı kenarında hale yok), RGB Full kontrolü, TN açı deseni, ölü piksel ve homojenlik.
- **Beyaz:** Pixel'in ekranı yan yana referans; DDC RGB kazancıyla eşlenir.
- **Gözle kalibrasyon:** Dama desenleriyle gamma 2.2'ye getirilir; koyu, orta ve açık grilerdeki renk kayması giderilir. Sonuç GPU tablosunun kalibrasyon katmanına yazılır, dithering açık.
- Sonuç "temel ayar" olarak kaydedilir; renk profilleri bunun üstüne çalışır.

### 3.3 Renk boru hattı · T302
- GPU renk tablosunun tek sahibi. Katman sırası: **kalibrasyon → mavi ışık (GPU kısmı) → ek karartma**, ardından dithering.
- Başka bir program (oyun, Windows Gece ışığı, f.lux, NVIDIA masaüstü renk ayarları) tabloyu değiştirirse tespit eder, kullanıcının tablosunu geri yükler ve uyarır.
- Derin karartma ve çok sıcak renkler için Windows'un tablo aralığı kaydını açar (tek seferlik yönetici onayı, geri alınabilir).

### 3.4 Dithering ve canlılık · T303
- **Dithering:** GPU tablosu değiştiğinde (kalibrasyon, mavi ışık, ek karartma) 256 ton seviyesinin bir kısmı kaybolur ve koyu geçişlerde (gökyüzü, gölge) basamaklar görünür. Dithering kaybolan ara tonları çok ince bir desenle taklit eder; geçişler pürüzsüz kalır. NVIDIA tam aralık RGB'de varsayılan olarak bunu yapmadığı için GPU tablosu kullanılırken açık tutulur.
- **Canlılık (NVIDIA Digital Vibrance):** Renk doygunluğunu artırır, TN'nin soluk görünümünü canlandırır. Doğru renk değil, göze hoş renk; sadece Film ve Oyun profillerinde açılır. Uygulama kapanınca varsayılana döner.

### 3.5 Parlaklık ve ek karartma · T306
- Tek kaydırıcı: önce arka ışık (DDC, 100 → 0; PWM yok, titreşimsiz), sonra GPU ile ek karartma (beyaz en az %15). Karanlık odada 0'ın bile parlak geldiği durum için.

### 3.6 Mavi ışık filtresi · T307
- Kelvin tabanlı (6500K → 1900K), doğal sıcak beyaz. Önce monitörün RGB kazancı, sınıra gelince GPU. Kalibrasyonun üstüne fark olarak uygulanır.
- Monitörün Low Blue Light ön ayarına kısayol. İsteğe bağlı yavaş akşam geçişi (varsayılan kapalı; sadece renk değişir).
- Windows Gece ışığı veya f.lux açıksa uyarı.

### 3.7 Renk profilleri (presetler) · T308
- **Gündüz**, **Akşam**, **Gece** (mavi ışık + düşük parlaklık), **Kod**, **Film** ve **Oyun** (canlılık açık), **DaVinci zaman çizelgesi** (nötr, efektsiz). Kullanıcı yenilerini ekleyebilir.
- Sadece kullanıcı seçince uygulanır; Hz'e dokunmaz.

### 3.8 Rehber · T311
- Oturma ve açı (TN'de en etkili iyileştirme), ortam ışığı, 20-20-20 kuralı, ClearType, RTX Video Super Resolution, DLDSR; Hz kurulumu ve testleri.
