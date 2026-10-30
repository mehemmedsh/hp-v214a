# Görüntü Yöntemleri

HP P21b G4'te görüntüyü iyileştirmek için geliştirilen beş yöntem:

1. Üst-alt ışık farkı
2. Renk düzeltme
3. Parlaklığı aşağı çekme
4. Mavi ışık filtresi
5. Renk boru hattı (hepsinin birlikte nasıl çalıştığı)

Kod yok. Görev numaraları `04-FAZLAR.md` içinde.

---

## 1. Üst-alt ışık farkı

**Gözlem (kullanıcı):** Ekranın altı daha parlak, üstü daha karanlık.

### Neden oluyor

1. **Bakış açısı (en olası sebep).** TN panelde parlaklık ve gamma dikey açıyla hızlı değişir. Bu yüzden TN paneller bir telafi filmiyle üretilir ve **göz ekranın üst kenarı hizasındayken** düzgün görünecek şekilde ayarlanır. Göz daha aşağıdaysa üst kısım alttan görülür ve kararır. Alt kısım da tasarlanandan daha az eğik bir açıdan görüldüğü için açık ve soluk görünür. Senin tarif ettiğin durum tam olarak bu.
2. **Arka ışık dağılımı.** Kenardan aydınlatmalı panellerde ışık bir kenardan verilir. O kenara yakın bölge fiziksel olarak biraz daha parlak olabilir.

### 2 dakikalık teşhis (T401)

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

**1. Konum. Donanımsız ve en etkili çözüm (T401)**
- Gözler ekranın **üst kenarı hizasında ya da biraz altında** olmalı. Monitörün standı yükselmiyor: sandalyeyi yükselt ya da monitörü alçalt. İleride VESA 100 kol veya duvar montajı en temiz çözüm.
- Mesafeyi 60 cm'den **70–80 cm'e** çıkar. 108 PPI sayesinde yazılar yine net kalır.
- Eğimi 0° ile −5° (öne) arasında dene. Arkaya yatırmak üst kısmı daha da karartır.
- Uygulamadaki **açı ayar deseni** ile ince ayar yap: üst, orta ve alt yamalar eşit görünene kadar.

**2. Dikey denge katmanı, elle (v1, T402)**
- Konumdan sonra kalan farkı, ekranın parlak kalan kısmını görünmez bir katmanla hafifçe karartarak dengeler.
- Tıklamaları geçirir, ekran görüntüsüne ve kayda girmez. Ölçümlerde ve tam ekran oyunlarda kendini gizler.
- Güç kaydırıcısı, açı ayar deseni eşit görünene kadar ayarlanır.
- **Sınırları:** Sadece karartabilir, aydınlatamaz; bu yüzden biraz maksimum parlaklık kaybedilir. Tek bir oturma konumu için ayarlıdır. Bakış açısı kaynaklı farkta en çok orta tonları düzeltir, beyazlar altta biraz sönük kalabilir. Arka ışık kaynaklı farkta ise neredeyse tamamen düzeltir.

**3. Ölçümle otomatik ayar (v2, T803)**
- Pixel, senin göz konumundan üst, orta ve alt bölgelerdeki gri yamaları çeker. Katmanın eğrisi buna göre hesaplanır.

**4. Satır bazlı ton telafisi (v3, deneysel, T904)**
- Masaüstü görüntüsü yakalanır ve GPU'da her satır için ayrı ton eğrisiyle yeniden çizilir. Hem karartabilir hem orta tonları açabilir. TN'nin gamma kaymasına en doğru çözüm bu.
- **Bedeli:** bir kare gecikme ve 3080'de ek yük. Korumalı videolar (Netflix vb.) siyah görünür. Tam ekran özel mod oyunlarda çalışmaz.
- Varsayılan olarak kapalı, panik kısayoluyla anında kapanır.

> Daha önce çıkarılan iki boyutlu homojenlik katmanının tek eksenli, basit hali. Bu monitörde gerçek bir sorun olduğu için geri geldi.

---

## 2. Renk düzeltme

### Hedef

- **Masaüstü:** sRGB (D65 beyaz, yaklaşık 2.2 gamma), 100–120 nit (karanlık odada 80–100 nit).
- **DaVinci:** Rec.709 / gamma 2.4, sadece Resolve görüntüleyicisinde (3D LUT ile).
- **"Doğal beyaz" seçeneği:** Beyaz noktasına dokunmaz, sadece gri dengesini ve gammayı düzeltir. Telefonla en güvenilir mod.

### Gerçekçi sınır

TN + 6-bit + FRC + 600:1. Kalibrasyon grileri nötr, gammayı doğru, beyazı hedefe yakın yapar. Panelin renk gamını genişletmez, açıya bağlı kaymayı da yok etmez. Kalibrasyon **tek bir göz konumu** için geçerlidir; bu yüzden önce konum ayarlanır (bölüm 1).

### Adımlar

**0. Hazırlık**
- Monitörü 30 dakika ısıt (arka ışık ilk dakikalarda kayar).
- Konumu ayarla (bölüm 1).
- OSD: Dynamic Contrast **kapalı** (varsa), Color → Custom RGB (yoksa Neutral), keskinlik varsayılan (Level 4), Overscan Off.
- NVIDIA: RGB **Full**. Uygulama: dithering **açık**.
- Mavi ışık, ek karartma, canlılık ve katmanlar **kapalı** (uygulama ölçüm sırasında kendisi kapatır).
- Oda karanlık.

**1. Kontrast ve siyah (T702)**
- Açık gri basamakların (230–255) ayırt edilebildiği en yüksek kontrast ve görünür koyu basamaklar (0–25).

**2. Beyaz noktası, monitörde (T703)**
- DDC RGB kazançlarıyla D65'e: ölç → öner → uygula → tekrar ölç. Sapma eşiğin altına inince durur.
- "Doğal beyaz" modunda bu adım atlanır. DDC yoksa GPU'da yapılır.

**3. Gri dengesi ve ton eğrisi, GPU'da (T704)**
- 17 gri basamak ölçülür (koyu tonlarda pozlama basamaklama ile).
- Her kanal için düzeltme eğrisi çıkarılır: her gri beyazla aynı renkte, parlaklığı hedef gamma eğrisinde olacak şekilde.
- Eğriler yumuşatılır. Çok koyu tonlarda (telefon gürültüsü yüksek) düzeltme kademeli olarak sıfıra iner.
- Sonuç renk boru hattının kalibrasyon katmanına yazılır. **Dithering zorunlu**, çünkü NVIDIA tam aralık RGB'de varsayılan olarak dithering yapmıyor.

**4. Doğrulama**
- 24 yama ölçülür (griler, ana ve ara renkler, ten tonları) ve yaklaşık ΔE2000 karneye yazılır.
- Gradyan desende bantlaşma kontrol edilir.

**5. ICC profili (T705)**
- Matris + ton eğrisi + VCGT. Renk yönetimli uygulamalar (ör. Photoshop, Chromium tabanlı tarayıcılar) bunu kullanır.

**6. DaVinci 3D LUT (T706)**
- Ölçümlerden Rec.709 / gamma 2.4 hedefli bir `.cube` LUT üretilir.
- Resolve'da: Project Settings → Color Management → **3D Color Viewer Lookup Table**, interpolasyon **Tetrahedral**.
- LUT, GPU kalibrasyonu açıkken ölçülen değerlerden üretilir; kalibrasyon iki kez uygulanmaz.

**7. Hz başına kalibrasyon (T707)**
- Her onaylı Hz için ayrı değerler; RGB aralığı Full'e sabit.

### Telefonla ölçümü daha doğru yapan kurallar (T602, T701)

- RAW (DNG); sabit ISO, enstantane, beyaz dengesi ve odak; pozlama **en az 1/15 s** (FRC ve olası PWM titremesi ortalansın diye).
- Ham değerler DNG'deki renk matrisleriyle yaklaşık XYZ'ye çevrilir. Telefonun JPEG işlemesi devreye girmez.
- Her yama 5 kare çekilir, ortanca değer alınır. Sadece yamanın merkez bölgesi kullanılır.
- Koyu tonlar için ek uzun pozlama yapılır. Ham veri doğrusal olduğu için pozlama oranıyla ölçeklenir.
- Sensörün siyah seviyesi DNG'den alınıp çıkarılır.
- Yama ekranın ortasında, kamera **senin göz konumunda** durur.
- **Tekrarlanabilirlik testi:** Beyaz iki kez ölçülür. Aradaki fark ölçüm gürültüsünü gösterir ve karneye yazılır.
- Mutlak doğruluk (D65) telefonun renk matrisine bağlı ve yaklaşıktır. Göreceli doğruluk (gri dengesi) yüksektir.

### Daha iyi renk için ek yollar

- **Canlılık (v1, T309):** NVIDIA Digital Vibrance ile doygunluk artırılır ve TN'nin soluk görünümü telafi edilir. Bu doğru renk değil, göze hoş renk. Sadece Film ve Oyun presetlerinde açılır; DaVinci'de ve ölçümde kapalı.
- **Kolorimetre modu (v3, isteğe bağlı, T905):** Bir kolorimetre alınırsa (ikinci el i1Display Pro, Calibrite veya Spyder gibi) ölçüm kaynağı telefon yerine ArgyllCMS üzerinden kolorimetre olur. Boru hattı aynı kalır, mutlak doğruluk çok artar. Kolorimetre alınırsa bu görev v2'ye çekilir.
- **NVIDIA renk dönüşümü arka ucu (v3, deneysel, T906):** novideo_srgb'nin kullandığı (belgelenmemiş) NVIDIA arayüzüyle LUT-matris-LUT düzeltmesi ekran çıkışında yapılır. Ana renklerin ton hatalarını da düzeltebilir ve dithering kontrolü sunar. Windows gamma tablosuyla aynı anda kullanılmaz, biri seçilir. Fare imleci dönüşüme girmez.

---

## 3. Parlaklığı aşağı çekme

### Tek kaydırıcı, iki kademe (T306)

- **Üst kısım: arka ışık (DDC).** Kaydırıcının 100 → 0 bölümü monitörün kendi parlaklığıdır. Kontrast oranı korunur, renk seviyesi kaybı olmaz. En iyi yol.
- **Alt kısım: ek karartma (GPU).** Arka ışık tabanına gelince kaydırıcı devam eder ve GPU renk tablosu ölçeklenerek ekran daha da karartılır. Gece karanlık odada 0'ın bile parlak geldiği durum için.
  - **Bedeli:** Beyaz düşer ama siyah aynı kalır, yani kontrast azalır. Seviye kaybını dithering gizler.
  - **Taban:** Beyaz en fazla %15'e iner; ekran hiçbir zaman tamamen kararmaz.
- Arayüzde "Arka ışık" ve "Ek karartma (dijital)" bölümleri renkle ayrılır. v2'de her konumun yaklaşık nit değeri yazar (T801).
- Windows derin karartma tablolarını reddettiği için `GdiIcmGammaRange` kayıt değeri 256 yapılır (yönetici yardımcısı; kaldırınca eski değere döner).

### Titreşimsiz karartma (PWM)

- **HP P21b G4'te PWM yok.** HP flicker-free olarak listeliyor; ilk gün testinde 1/1000 s'de parlaklık 100'den 0'a kadar hiçbir fotoğrafta yatay bant çıkmadı. Bu yüzden bu monitörde tek mod var ve arka ışık 0'a kadar serbest.
- Aşağıdaki iki mod, profilinde PWM olan monitörler için genel fikirde kalıyor. PWM varsa iki mod sunulur:
  - **Titreşimsiz mod:** Arka ışık bu tabanın altına inmez, kalan karartma GPU'da yapılır (kontrast biraz düşer).
  - **En iyi kontrast modu:** Önce arka ışık sonuna kadar iner, sonra GPU devreye girer.
- PWM yoksa tek mod vardır.

### Kurallar

- Parlaklık sadece kullanıcı kaydırıcıyı oynatınca değişir.
- Ölçüm ve kalibrasyon sırasında ek karartma geçici olarak kapanır, bitince geri gelir.
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
- **Hızlı seçim:** Monitörün kendi Low Blue Light ön ayarı da tek tıkla seçilebilir. v2'de gerçek renk sıcaklığı telefonla ölçülüp Kelvin olarak gösterilir.
- **Zamanlama (isteğe bağlı, varsayılan kapalı):** Akşam belirlenen saatte 30–60 dakikada yavaş geçiş. Sadece renk değişir, arka ışık değişmez. Renk ısındıkça beyaz doğal olarak kararır (tabloda); bunu telafi etmek için arka ışık **artırılmaz**.
- **Otomatik kapanma:** Render modu, DaVinci preseti, ölçüm ve kalibrasyon sırasında kapalı.
- **Çakışma:** Windows Gece ışığı veya f.lux açıksa uyarı verir; ikisi aynı anda çalışmaz.

### Ne bekleyebilirsin

Akşamları göz rahatlığı için işe yarar. Uyku için tek başına beklenen etkiyi göstermeyebilir: BYU'nun 2021 çalışmasında telefonun Night Shift modu uyku sonuçlarını iyileştirmedi; daha iyi uyku kalitesi sadece yatmadan önce telefonu hiç kullanmayan grupta (ve sadece yaklaşık 7 saat uyuyanlarda) görüldü. Bu yüzden **Gece preseti mavi ışık filtresini ve parlaklığı birlikte düşürür.**

---

## 5. Renk boru hattı (T302)

İçerikten göze doğru sıra:

```
İçerik
  → Windows masaüstü (katmanlar: dikey denge, ekran bilgisi)
  → GPU renk tablosu: kalibrasyon → mavi ışık (GPU kısmı) → ek karartma → dithering
  → HDMI (Full RGB)
  → Monitör: RGB kazancı (beyaz noktası + mavi ışık, monitör kısmı) → arka ışık (parlaklık) → panel
```

**Kurallar**
- GPU renk tablosunun tek sahibi uygulamadır. Başka bir program (oyun, Windows Gece ışığı, f.lux, NVIDIA masaüstü renk ayarları) tabloyu değiştirirse bu tespit edilir. Kullanıcının seçtiği tablo geri yüklenir (Kural 1, 3. istisna) ve çakışan program için uyarı verilir.
- Ölçüm ve kalibrasyon sırasında mavi ışık, ek karartma, canlılık, dikey denge ve ekran bilgisi katmanı geçici olarak kapanır.
- Uygulama normal kapanırken tabloda sadece kalibrasyon kalır, efektler kaldırılır. Çökerse kurtarma dosyası tabloyu sıfırlar; uygulama yeniden açılınca tabloyu kontrol eder.
- Kaldırmada tablo sıfırlanır ve kayıt değeri eski haline döner.
- **Arka uç:** v1–v2'de Windows gamma tablosu. v3'te isteğe bağlı NVIDIA renk dönüşümü (matris + daha iyi dithering).

---

## Kaynaklar

- [Lorimer: Why do TN monitors look worse upside down?](https://lorimer.id.au/tn-monitors.html): TN telafi filmi, göz konumu ve üst-alt parlaklık farkı
- [novideo_srgb (GitHub)](https://github.com/ledoge/novideo_srgb): NVIDIA'da LUT-matris-LUT, dithering, NVIDIA'nın tam aralık RGB'de varsayılan dithering yapmaması
- [DisplayCAL wiki: 3D LUT creation workflow for Resolve](https://hub.displaycal.net/wiki/3d-lut-creation-workflow-for-resolve/): "3D Color Viewer Lookup Table", Tetrahedral, VCGT'nin iki kez uygulanmaması
- [UnlockGamma.reg (GitHub)](https://github.com/DogukanErmis/NightLight/blob/master/UnlockGamma.reg): `GdiIcmGammaRange` kayıt değeri
- [ShaderGlass](https://cdn.jsdelivr.net/gh/mausimus/shaderglass@master/README.md): masaüstünü yakalayıp gölgelendiriciyle yeniden çizme yaklaşımı
- [EurekAlert: BYU Night Shift çalışması (2021)](https://www.eurekalert.org/news-releases/642949): Night Shift ve uyku
- [HP P21b G4 veri sayfası (9TY24AA, PDF)](https://pcb.inc.hp.com/dc/api/spec-sheet/ww-en/34514035/pdf/9TY24AA.pdf): 6-bit + 2 FRC, flicker-free, 5 ms GtG overdrive ile
