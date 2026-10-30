# Görüntü Yöntemleri

HP P21b G4'te görüntüyü iyileştirmek için kullanılan beş yöntem:

1. Üst-alt ışık farkı
2. Renk düzeltme (gözle, kamerasız)
3. Parlaklığı aşağı çekme
4. Mavi ışık filtresi
5. Renk boru hattı (hepsinin birlikte nasıl çalıştığı)

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

- **Canlılık (T303):** NVIDIA Digital Vibrance ile doygunluk artırılır ve TN'nin soluk görünümü telafi edilir. Bu doğru renk değil, göze hoş renk. Sadece Film ve Oyun profillerinde açılır; DaVinci zaman çizelgesi profilinde ve sihirbazda kapalı.

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
  → GPU renk tablosu: kalibrasyon → mavi ışık (GPU kısmı) → ek karartma → dithering
  → HDMI (Full RGB)
  → Monitör: RGB kazancı (beyaz noktası + mavi ışık, monitör kısmı) → arka ışık (parlaklık) → panel
```

**Kurallar**
- GPU renk tablosunun tek sahibi uygulamadır. Başka bir program (oyun, Windows Gece ışığı, f.lux, NVIDIA masaüstü renk ayarları) tabloyu değiştirirse bu tespit edilir. Kullanıcının seçtiği tablo geri yüklenir (Kural 1, 2. istisna) ve çakışan program için uyarı verilir.
- Ayar sihirbazı sırasında mavi ışık, ek karartma ve canlılık geçici olarak kapanır.
- Uygulama normal kapanırken tabloda sadece kalibrasyon kalır, efektler kaldırılır. Çökerse kurtarma dosyası tabloyu sıfırlar; uygulama yeniden açılınca tabloyu kontrol eder.
- Kaldırmada tablo sıfırlanır ve kayıt değeri eski haline döner.
- **Arka uç:** Windows gamma tablosu.

---

## Kaynaklar

- [Lorimer: Why do TN monitors look worse upside down?](https://lorimer.id.au/tn-monitors.html): TN telafi filmi, göz konumu ve üst-alt parlaklık farkı
- [novideo_srgb (GitHub)](https://github.com/ledoge/novideo_srgb): NVIDIA'da LUT-matris-LUT, dithering, NVIDIA'nın tam aralık RGB'de varsayılan dithering yapmaması
- [DisplayCAL wiki: 3D LUT creation workflow for Resolve](https://hub.displaycal.net/wiki/3d-lut-creation-workflow-for-resolve/): "3D Color Viewer Lookup Table", Tetrahedral, VCGT'nin iki kez uygulanmaması
- [UnlockGamma.reg (GitHub)](https://github.com/DogukanErmis/NightLight/blob/master/UnlockGamma.reg): `GdiIcmGammaRange` kayıt değeri
- [ShaderGlass](https://cdn.jsdelivr.net/gh/mausimus/shaderglass@master/README.md): masaüstünü yakalayıp gölgelendiriciyle yeniden çizme yaklaşımı
- [EurekAlert: BYU Night Shift çalışması (2021)](https://www.eurekalert.org/news-releases/642949): Night Shift ve uyku
- [HP P21b G4 veri sayfası (9TY24AA, PDF)](https://pcb.inc.hp.com/dc/api/spec-sheet/ww-en/34514035/pdf/9TY24AA.pdf): 6-bit + 2 FRC, flicker-free, 5 ms GtG overdrive ile
