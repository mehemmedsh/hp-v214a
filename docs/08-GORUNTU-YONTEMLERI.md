# Görüntü Yöntemleri

HP P21b G4'te görüntüyü iyileştirmek için kullanılan yöntemler:

1. Üst-alt ışık farkı
2. Renk düzeltme (gözle, kamerasız)
3. Parlaklığı aşağı çekme
4. Mavi ışık filtresi
5. Renk boru hattı (hepsinin birlikte nasıl çalıştığı)
6. Sinematik görüntü ve uzun süreli konfor (ton eğrisi ve modlar)

Kod yok. Görev numaraları `04-FAZLAR.md` içinde. 2026-10-30 daraltmasında plan dışı kalan yöntemler `06-CIKARILANLAR.md`'de.

---

## 1. Üst-alt ışık farkı

**Gözlem (kullanıcı):** Ekranın altı daha parlak, üstü daha karanlık.

### Neden oluyor

1. **Bakış açısı (en olası sebep).** TN panelde parlaklık ve gamma dikey açıyla hızlı değişir. Bu yüzden TN paneller bir telafi filmiyle üretilir ve **göz ekranın üst kenarı hizasındayken** düzgün görünecek şekilde ayarlanır. Göz daha aşağıdaysa üst kısım alttan görülür ve kararır. Alt kısım da tasarlanandan daha az eğik bir açıdan görüldüğü için açık ve soluk görünür. Senin tarif ettiğin durum tam olarak bu.
2. **Arka ışık dağılımı.** Kenardan aydınlatmalı panellerde ışık bir kenardan verilir. O kenara yakın bölge fiziksel olarak biraz daha parlak olabilir.

### 2 dakikalık teşhis (T311, T312)

- Ekranı düz %50 gri yap.
- Başını 10–15 cm yavaşça yukarı ve aşağı oynat. Fark belirgin şekilde değişiyorsa sebep **bakış açısı**, hiç değişmiyorsa **arka ışık**.
- Kesinleştirmek için telefonla 1.5–2 m uzaktan, ekranın ortasına dik ve zoomla bir fotoğraf çek. Uzaktan çekilen fotoğrafta fark kayboluyorsa açı, duruyorsa arka ışık.

Mesafe arttıkça ekranın üstü ile altı arasındaki açı farkı küçülür (20.7", göz ortada):

| Mesafe | Üst-alt açı farkı |
|---|---|
| 50 cm | 28.9° |
| 60 cm | 24.3° |
| 70 cm | 20.9° |
| 80 cm | 18.3° |

### Çözüm sırası (en etkiliden başlayarak)

**1. Konum. Donanımsız ve en etkili çözüm (T311 rehber, T312 açı deseni)**
- Gözler ekranın **üst kenarı hizasında ya da biraz altında** olmalı. Monitörün standı yükselmiyor: sandalyeyi yükselt ya da monitörü alçalt. İleride VESA 100 kol veya duvar montajı en temiz çözüm.
- Mesafeyi 60 cm'den **70–80 cm'e** çıkar. 108 PPI sayesinde yazılar yine net kalır.
- Eğimi 0° ile −5° (öne) arasında dene. Arkaya yatırmak üst kısmı daha da karartır.
- Uygulamadaki **açı ayar deseni** ile ince ayar yap: üst, orta ve alt yamalar eşit görünene kadar.

**2. Kalan fark için (plan dışı)**
- Dikey denge katmanı, ölçümle ayar ve satır bazlı ton telafisi plandan çıktı (`06-CIKARILANLAR.md`). Konum ayarından sonra fark rahatsız ederse yeniden değerlendirilir.

---

## 2. Renk düzeltme

### Hedef

- **Masaüstü:** sRGB (D65 beyaz, yaklaşık 2.2 gamma), 100–120 nit (karanlık odada 80–100 nit).
- **DaVinci:** Bu monitörde renk işi yapılmaz, sadece zaman çizelgesi. Renk referansı Pixel'in ekranı.
- Ölçüm cihazı yok; ayar gözle ve desenlerle yapılır.

### Gerçekçi sınır

TN + 6-bit + FRC + 600:1. Gözle ayar grileri nötre, gammayı hedefe ve beyazı referansa yaklaştırır. Panelin renk gamını genişletmez, açıya bağlı kaymayı da yok etmez. Ayar **tek bir göz konumu** için geçerlidir; bu yüzden önce konum ayarlanır (bölüm 1).

### Adımlar (ayar sihirbazı)

**0. Hazırlık**
- Monitörü 30 dakika ısıt (arka ışık ilk dakikalarda kayar).
- Konumu ayarla (bölüm 1).
- OSD: Color → Custom RGB (yoksa Neutral), keskinlik varsayılan (Level 4), Overscan Off.
- NVIDIA: RGB **Full**. Uygulama: dithering **açık**.
- Mavi ışık, ek karartma ve canlılık **kapalı** (sihirbaz kendisi kapatır, bitince geri açar).
- Oda ışığı her zamanki çalışma ışığın gibi olsun.

**1. Kontrast ve parlaklık (T312)**
- Açık gri basamakların (230–255) ayırt edilebildiği en yüksek kontrast; koyu basamakların (0–25) görünür kaldığı parlaklık.

**2. Keskinlik ve RGB aralığı (T312)**
- Yazı kenarında hale (beyaz çizgi) çıkmayan en yüksek keskinlik.
- Siyaha yakın basamaklar (0–16) ayrı görünüyorsa RGB Full doğru; görünmüyorsa uyarı.

**3. Beyaz noktası, monitörde (T312)**
- Pixel ekranında beyaz bir sayfa, monitörde beyaz desen, yan yana. DDC RGB kazançlarıyla iki beyaz eşlenir. Pixel'in ekranı "Doğal" renk modunda olmalı.
- DDC yoksa GPU'da yapılır.

**4. Gamma ve gri dengesi, GPU'da (T313)**
- Gamma: dama desenleri (ince çizgili alan ile düz gri) uzaktan bakınca eşit parlaklıkta görünene kadar; hedef 2.2.
- Gri dengesi: koyu, orta ve açık gri yamalarda renk kayması (pembe, yeşil, mavi ton) kalmayana kadar kanal başına küçük düzeltme.
- Eğriler yumuşatılır ve renk boru hattının kalibrasyon katmanına yazılır. **Dithering zorunlu**, çünkü NVIDIA tam aralık RGB'de varsayılan olarak dithering yapmıyor.

**5. Doğrulama**
- Gri basamaklarda renk kayması yok; gradyan desende bantlaşma yok.

### Daha iyi renk için

- **Canlılık (T303):** NVIDIA Digital Vibrance ile doygunluk artırılır ve TN'nin soluk görünümü telafi edilir. Bu doğru renk değil, göze hoş renk. Sadece sinema modlarında ve Oyun'da açılır (T321); DaVinci zaman çizelgesi profilinde ve sihirbazda kapalı.

---

## 3. Parlaklığı aşağı çekme

### Tek kaydırıcı, iki kademe (T306)

- **Üst kısım: arka ışık (DDC).** Kaydırıcının 100 → 0 bölümü monitörün kendi parlaklığıdır. Kontrast oranı korunur, renk seviyesi kaybı olmaz. En iyi yol.
- **Alt kısım: ek karartma (GPU).** Arka ışık tabanına gelince kaydırıcı devam eder ve GPU renk tablosu ölçeklenerek ekran daha da karartılır. Gece karanlık odada 0'ın bile parlak geldiği durum için.
  - **Bedeli:** Beyaz düşer ama siyah aynı kalır, yani kontrast azalır. Seviye kaybını dithering gizler.
  - **Taban:** Beyaz en fazla %15'e iner; ekran hiçbir zaman tamamen kararmaz.
- Arayüzde "Arka ışık" ve "Ek karartma (dijital)" bölümleri renkle ayrılır.
- Windows derin karartma tablolarını reddettiği için `GdiIcmGammaRange` kayıt değeri 256 yapılır (tek seferlik yönetici onayı, T302; kaldırınca eski değere döner).

### Titreşimsiz karartma (PWM)

- **HP P21b G4'te PWM yok.** HP flicker-free olarak listeliyor; ilk gün testinde 1/1000 s'de parlaklık 100'den 0'a kadar hiçbir fotoğrafta yatay bant çıkmadı. Bu yüzden bu monitörde tek mod var ve arka ışık 0'a kadar serbest.
- Aşağıdaki iki mod, profilinde PWM olan monitörler için genel fikirde kalıyor. PWM varsa iki mod sunulur:
  - **Titreşimsiz mod:** Arka ışık bu tabanın altına inmez, kalan karartma GPU'da yapılır (kontrast biraz düşer).
  - **En iyi kontrast modu:** Önce arka ışık sonuna kadar iner, sonra GPU devreye girer.
- PWM yoksa tek mod vardır.

### Kurallar

- Parlaklık sadece kullanıcı kaydırıcıyı oynatınca değişir.
- Ayar sihirbazı sırasında ek karartma geçici olarak kapanır, bitince geri gelir.
- Dijital parlaklık koruması değişmez: kontrast veya RGB ile parlaklık **artırma** engellenir. Ek karartma sadece **azaltma** yönünde ve sadece arka ışık tabanının altında çalışır.

---

## 4. Mavi ışık filtresi (T307)

Eski "gece modu"nun yerini alır.

### Yöntem

- **Kelvin tabanlı:** 6500K (kapalı) → 5000 → 4000 → 3400 → 2700 → 1900K. Her sıcaklık için beyaz nokta siyah cisim (Planck) eğrisinden hesaplanır ve RGB çarpanına çevrilir. Böylece renk kirli sarı değil, doğal sıcak beyaz olur.

  Yaklaşık değerler (sRGB ana renkleri varsayılarak, doğrusal ışık):

  | Sıcaklık | R | G | B | Mavi azalması | Beyaz parlaklığı |
  |---|---|---|---|---|---|
  | 6500K | 1.00 | 1.00 | 1.00 | — | %100 |
  | 5000K | 1.00 | 0.84 | 0.63 | %37 | %86 |
  | 4000K | 1.00 | 0.69 | 0.38 | %62 | %74 |
  | 3400K | 1.00 | 0.59 | 0.24 | %76 | %65 |
  | 2700K | 1.00 | 0.44 | 0.10 | %90 | %54 |
  | 1900K | 1.00 | 0.25 | 0.00 | ~%100 | %39 |

- **Karma uygulama:** İlk kısım monitörün RGB kazancıyla yapılır (DDC; seviye kaybı yok). Monitör kazancının sınırına gelince devamı GPU renk tablosunda. DDC yoksa tamamı GPU'da.
- **Kalibrasyonu bozmaz:** Kalibre değerlerin üstüne fark olarak uygulanır. Kapatınca kalibre beyaza dönülür.
- **Hızlı seçim:** Monitörün kendi Low Blue Light ön ayarı da tek tıkla seçilebilir.
- **Zamanlama (isteğe bağlı, varsayılan kapalı):** Akşam belirlenen saatte 30–60 dakikada yavaş geçiş. Sadece renk değişir, arka ışık değişmez. Renk ısındıkça beyaz doğal olarak kararır (tabloda); bunu telafi etmek için arka ışık **artırılmaz**.
- **Otomatik kapanma:** DaVinci zaman çizelgesi profilinde ve ayar sihirbazı sırasında kapalı.
- **Çakışma:** Windows Gece ışığı veya f.lux açıksa uyarı verir; ikisi aynı anda çalışmaz.

### Ne bekleyebilirsin

Akşamları göz rahatlığı için işe yarar. Uyku için tek başına beklenen etkiyi göstermeyebilir: BYU'nun 2021 çalışmasında telefonun Night Shift modu uyku sonuçlarını iyileştirmedi; daha iyi uyku kalitesi sadece yatmadan önce telefonu hiç kullanmayan grupta (ve sadece yaklaşık 7 saat uyuyanlarda) görüldü. Bu yüzden **Gece preseti mavi ışık filtresini ve parlaklığı birlikte düşürür.**

---

## 5. Renk boru hattı (T302)

İçerikten göze doğru sıra:

```
İçerik
  → Windows masaüstü
  → GPU renk tablosu: ton eğrisi (T320) → kalibrasyon → mavi ışık (GPU kısmı) → ek karartma → dithering
  → HDMI (Full RGB)
  → Monitör: RGB kazancı (beyaz noktası + mavi ışık, monitör kısmı) → arka ışık (parlaklık) → panel
```

**Kurallar**
- GPU renk tablosunun tek sahibi uygulamadır. Başka bir program (oyun, Windows Gece ışığı, f.lux, NVIDIA masaüstü renk ayarları) tabloyu değiştirirse bu tespit edilir. Kullanıcının seçtiği tablo geri yüklenir (Kural 1, 2. istisna) ve çakışan program için uyarı verilir.
- Ayar sihirbazı sırasında mavi ışık, ek karartma, ton eğrisi ve canlılık geçici olarak kapanır.
- Uygulama normal kapanırken tabloda sadece kalibrasyon kalır, efektler kaldırılır. Çökerse kurtarma dosyası tabloyu sıfırlar; uygulama yeniden açılınca tabloyu kontrol eder.
- Kaldırmada tablo sıfırlanır ve kayıt değeri eski haline döner.
- **Arka uç:** Windows gamma tablosu.

---

## 6. Sinematik görüntü ve uzun süreli konfor (T320, T321)

Kullanıcı isteği (2026-11-05): "bu monitörden en sinematik görüntü; uzun süre gözü ve beyni yormayan modlar". Araştırma 2026-11-05; ölçüm cihazı olmadığı için değerler kaynaklardan ve panelin bilinen sınırlarından (TN, 600:1, 6-bit + FRC, 250 nit, PWM yok) gelir.

### Sinema: ne değiştirir

1. **Gamma 2.4 (ton eğrisi, T320).** Filmler ITU-R BT.1886'ya göre, loş bir odada gamma 2.4 ile hazırlanır; masaüstünün 2.2'si (sRGB) aydınlık oda içindir. Karanlık çevrede algılanan kontrast düşer, daha yüksek gamma bunu telafi eder (çevre telafisi oranı ortalama : loş : karanlık ≈ 1 : 1,17 : 1,31).
   - Bu panelde siyah gri (600:1). Saf 2.4 siyaha yakın basamakları siyahın içinde kaybettirir. BT.1886'nın kendi formülü siyah seviyesine göre gölgeleri kaldırır; ama 600:1'de orta tonlarda neredeyse 2.2'ye döner (etkin ~2,18) ve sinema hissini vermez. Gölgeleri kaldıran BT.1886'nın kalkık siyahlı ekranlarda gölgeyi soldurduğunu söyleyen kaynak da var.
   - Uygulamanın sinema eğrisi ikisinin arası: 64/255 ve üstünde tam 2.4, altında 2.2'ye yumuşak geçiş. Örnek (8 bit): 128 → 120, 192 → 187, 64 → 56; 13 → 12,6 (ilk 13 basamak bir seviyeden az değişir).
   - Eğri kalibrasyondan **önce**, üç kanala aynı uygulanır: siyah ve beyaz yerinde, gri dengesi bozulmaz, parlaklık artmaz.
   - **Canlı kontrast:** Orta griyi sabit tutan hafif S eğrisi (koyular %15'e kadar koyulaşır, açıklar açılır; 51 → 47, 192 → 196). Doğru renk değil; animasyon ve aksiyon için.
2. **Beyaz 6500K (D65).** Filmler bu beyazla hazırlanır; "sinema modu" sarı demek değildir. Monitörlerin "Movie" ön ayarları genelde sıcak görünür çünkü fabrika ayarları soğuktur; bu monitör kalibrasyonla zaten 6500K. Sadece Sinema · Gece ılık (4500K).
3. **Parlaklık odaya göre.** Karanlık odada düşük parlaklık göz kamaştırmaz ve gri siyahı koyulaştırır: 600:1'de 60 nit beyazın siyahı 0,1 nit. Sinema salonu yaklaşık 48 nit ile çalışır. Sinema modları ek karartmaya inmez (GPU karartması kontrastı düşürür, bölüm 3).
4. **Canlılık ölçülü.** TN'in soluk görünümüne hafif artış (%56–68). Karanlıkta renkler soluk algılandığı için (Hunt etkisi) karanlık oda loş odadan biraz daha canlı.
5. **72 Hz.** 24 fps film 60 Hz'de 3:2 gösterilir (kareler sırayla 3 ve 2 yenileme ekranda kalır); düzgün kaymalarda takılma görülür. 72 Hz'de her kare tam 3 yenileme kalır. 72 Hz bu monitörde spec içi en yüksek mod (`00-DONANIM.md`), NVIDIA'ya eklenip test edilmeli. Modlar Hz'e dokunmaz (Kural 1); öneri modların yanında yazar.
6. **Bias ışığı.** Monitörün arkasına, duvara vuran hafif bir 6500K ışık (ekran beyazının yaklaşık %10'u) karanlık odada siyahı daha derin gösterir, ekranla oda arasındaki parlaklık farkını azaltır. Kaynaklar meraklı ve üretici siteleri; ölçümlü çalışma bulunamadı.

### Uzun süreli kullanım: neyi yorar

1. **Ekranın odadan çok parlak olması.** Yüksek ekran parlaklığında bir saatlik okuma göz kırpmayı azalttı, yorgunluğu artırdı (Benedetto 2014). Akşam için uygun ekran parlaklığı oda ışığıyla birlikte artıyor: 13–62 lx odada yaklaşık 21–75 cd/m² (Zhou 2021). Pratik kural: beyaz sayfa yanındaki duvardan çok parlak görünmesin.
2. **Çok sıcak beyaz uzun okumada daha çok yordu.** 450 lx'te 4500–6500K, karanlık odada (3 lx) 4500K en az yordu; 2800K iki ortamda da en çok yorgunluk verdi (2024, 36 kişi). Konfor modları bu yüzden 4000–5500K; 3400K ve 2700K uyku öncesi kısa kullanım için (Akşam, Gece).
3. **Uyku için asıl etken melanopik ışık** (gözün uyanıklık algılayıcılarını uyaran mavi-yeşil ışık). Parlaklığı düşürmek, renk sıcaklığını değiştirmekten daha güvenilir; renk sıcaklığı tek başına iyi bir ölçü değil (Schöllhorn 2023). Gece modlarında parlaklık da düşer (bölüm 4).
4. **Mola.** Ekrana bakarken göz kırpma yarıya iniyor. 20-20-20 hatırlatması (20 dakikada bir, 20 saniye, 6 metre uzağa bak) belirtileri azalttı; hatırlatma bitince belirtiler geri döndü (Aston 2022, 29 kişi). Uygulamada isteğe bağlı mola hatırlatıcısı var (T324, Ayarlar; film ve oyun modlarında susar).
5. **Titreşim.** PWM yok (ilk gün testi). Panelin kendi FRC'si (6-bit + 2 FRC) kapatılamaz; GPU dithering'i bu yüzden durağan desen, ikinci bir zamansal desen eklenmez (T303). FRC'nin hassas kişileri rahatsız ettiğine dair kanıt çoğunlukla anekdot.
6. **Efektsiz görüntü.** Konfor modlarında ton eğrisi ve canlılık kapalı: saatlerce bakılacak görüntü sade ve doğru kalır.

### Modlar (T321)

Parlaklık tek kaydırıcının değeri; parantezde monitöre yazılan arka ışık (`DimmingPlanner`).

| Grup | Mod | Ne zaman | Parlaklık | Beyaz | Ton | Canlılık |
|---|---|---|---|---|---|---|
| Sinema | Film | Aydınlık oda (kullanıcı beğendi, aynen) | 0,50 (%38) | 6500K | Standart | %60 |
| Sinema | Sinema | Loş oda, bir lamba | 0,42 (%27) | 6500K | Sinema 2.4 | %56 |
| Sinema | Sinema · Karanlık oda | Işıklar kapalı | 0,30 (%12) | 6500K | Sinema 2.4 | %60 |
| Sinema | Sinema · Canlı | Animasyon, aksiyon, belgesel | 0,55 (%44) | 6500K | Canlı kontrast | %68 |
| Sinema | Sinema · Gece | Yatmadan önce film | 0,26 (%8) | 4500K | Sinema 2.4 | %58 |
| Uzun kullanım | Konfor · Gündüz | Gün ışığında saatlerce | 0,52 (%40) | 5500K | Standart | yok |
| Uzun kullanım | Konfor · Akşam | Loş odada uzun okuma ve iş | 0,38 (%23) | 4500K | Standart | yok |
| Uzun kullanım | Konfor · Gece | Karanlık odada uzun kullanım | 0,22 (%2) | 4000K | Standart | yok |
| Günlük | Gündüz | En doğru renk, önerilen | 0,60 (%50) | 6500K | Standart | yok |
| Günlük | Akşam | Uyku öncesi saatler | 0,50 (%38) | 3400K | Standart | yok |
| Günlük | Gece | Yatmadan hemen önce, kısa | 0,30 (%12) | 2700K | Standart | yok |
| Günlük | Kod | Yazı ve kod | 0,55 (%44) | 5000K | Standart | yok |
| Özel | Oyun | Oyun | 0,65 (%56) | 6500K | Standart | %65 |
| Özel | DaVinci zaman çizelgesi | Nötr, efektsiz | 0,60 (%50) | 6500K | Standart | yok |

- Arka ışık yüzdeleri monitörün kendi ölçeğidir; nit karşılığı ölçülmedi (250 nit tavan). Oda farklıysa kullanıcı modu seçtikten sonra parlaklığı elle düzeltir; mod bir kez ayarlar.
- Modlar Hz'e, kalibrasyona ve monitörün renk ön ayarına dokunmaz; panik kısayolu ton eğrisini de kapatır.

---

## Kaynaklar

- [Lorimer: Why do TN monitors look worse upside down?](https://lorimer.id.au/tn-monitors.html): TN telafi filmi, göz konumu ve üst-alt parlaklık farkı
- [novideo_srgb (GitHub)](https://github.com/ledoge/novideo_srgb): NVIDIA'da LUT-matris-LUT, dithering, NVIDIA'nın tam aralık RGB'de varsayılan dithering yapmaması
- [DisplayCAL wiki: 3D LUT creation workflow for Resolve](https://hub.displaycal.net/wiki/3d-lut-creation-workflow-for-resolve/): "3D Color Viewer Lookup Table", Tetrahedral, VCGT'nin iki kez uygulanmaması
- [UnlockGamma.reg (GitHub)](https://github.com/DogukanErmis/NightLight/blob/master/UnlockGamma.reg): `GdiIcmGammaRange` kayıt değeri
- [ShaderGlass](https://cdn.jsdelivr.net/gh/mausimus/shaderglass@master/README.md): masaüstünü yakalayıp gölgelendiriciyle yeniden çizme yaklaşımı
- [EurekAlert: BYU Night Shift çalışması (2021)](https://www.eurekalert.org/news-releases/642949): Night Shift ve uyku
- [HP P21b G4 veri sayfası (9TY24AA, PDF)](https://pcb.inc.hp.com/dc/api/spec-sheet/ww-en/34514035/pdf/9TY24AA.pdf): 6-bit + 2 FRC, flicker-free, 5 ms GtG overdrive ile
- [Poynton: Gamma in studio and CE](https://poynton.ca/notes/misc/Gamma-in-studio-and-CE.html): BT.1886 gamma 2.4, loş çevre (beyazın ~%5'i)
- [Light Illusion: BT.1886 issues](https://lightillusion.com/issues_bt1886.html): kalkık siyahlı ekranda BT.1886'nın gölgeleri kaldırması
- [ITU-R BT.1886 (Vikipedi)](https://en.wikipedia.org/wiki/ITU-R_BT.1886): formül, siyah seviyesine bağlılık
- [Çevreye göre görüntü gamması (KoreaScience)](https://koreascience.kr/article/JAKO201405981344319.pdf): Bartleson; ortalama, loş, karanlık çevre telafisi 1 : 1,17 : 1,31
- [Benedetto ve ark. 2014](https://lead.ube.fr/?p=5702): ekran parlaklığı, oda ışığı, göz kırpma ve yorgunluk
- [Zhou ve ark. 2021, Applied Sciences](https://doaj.org/article/bf903f5a690b493cb988ef10dff8c0a5): akşam için oda ışığına göre uygun ekran parlaklığı
- [Ekran renk modu ve renk sıcaklığı ile yorgunluk (2024)](https://informahealthcare.com/doi/full/10.1080/10447318.2024.2305982): 4500–6500K az, 2800K çok yorgunluk
- [Schöllhorn ve ark. 2023: melanopik ışık ve ekran](https://pubmed.ncbi.nlm.nih.gov/36854795/): uyku gecikmesi, melatonin
- [Aston Üniversitesi: 20-20-20 (AOP, 2022)](https://www.aop.org.uk/ot/news/2022/09/25/aston-researchers-confirm-20-20-20-guidance): hatırlatmayla belirtiler azaldı
- [20-20-20 kuralına uyum (PubMed, 2023)](https://pubmed.ncbi.nlm.nih.gov/37203083/): kuralı uygulayanlar %34
- [MediaLight bias ışığı incelemesi](https://hometheaterhifi.com/reviews/video-accessory/video-accessories-misc/medialight-6500k-bias-lighting-system-review/): 6500K, ekran beyazının ~%10'u
- [doom9: 72 Hz ve 24p](https://forum.doom9.org/archive/index.php/t-158951.html): 3:2 ve 3:3 kadans
- [Blur Busters forumu: FRC ve titreşim](https://forums.blurbusters.com/viewtopic.php?p=28917): FRC'nin PWM'e göre etkisi
