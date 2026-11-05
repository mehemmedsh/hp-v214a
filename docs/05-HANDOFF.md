# Handoff

Hazır metinler ve kurulum. Düzen: **tek model, Opus, Claude Code'da; ayrı inceleme yok** (kullanıcı kararı 2026-11-05; Sonnet hattı iptal, Antigravity kullanılmıyor). Claude Code `CLAUDE.md` üzerinden `AGENTS.md`'yi otomatik okur. Kurallar, okuma haritası ve commit biçimi orada olduğu için buradaki metinler kısa. Her metinde `T___` yerine görev numarasını yaz.

**Sıradaki iş:** `04-FAZLAR.md`'nin ilk satırında.

## 0. Bir kerelik kurulum (PC)

### 0.1 Programlar

Başlat menüsüne "PowerShell" yaz ve aç. Satırları sırayla yapıştır; biri bitmeden diğerine geçme:

```powershell
winget install --id Git.Git -e
winget install --id GitHub.cli -e
winget install --id Microsoft.DotNet.SDK.10 -e
```

PowerShell'i kapatıp yeniden aç (yeni kurulan programlar ancak böyle görünür). Sonra:

```powershell
git config --global user.name "Adın Soyadın"
git config --global user.email "GitHub e-posta adresin"
gh auth login
```

`gh auth login` soruları: GitHub.com → HTTPS → Git için de giriş: Yes → "Login with a web browser". Ekrandaki kodu tarayıcıda gir. Bu, ajanların işlerini GitHub'a göndermesini (push) ve CI sonucunu okumasını sağlar. Git, Claude Code'un Windows'ta çalışması için de gerekir.

### 0.2 Klasörler

```powershell
mkdir C:\dev
cd C:\dev
git clone https://github.com/mehemmedsh/hp-v214a hp-opus
git clone https://github.com/mehemmedsh/hp-v214a hp-inceleme
```

`hp-opus` görevlerin yazıldığı klasördür. `hp-inceleme` sadece monitör testi için yerel derlemede (bölüm 5, B yolu) ve yardım oturumlarında (bölüm 7) kullanılır. Her klasör projenin ayrı bir kopyasıdır: ajan işini GitHub'a gönderir (push), diğer klasör oradan çeker (pull). Eski `hp-sonnet` ve `hp-sonnet2` klasörleri artık gerekmez; içinde yarım iş yoksa (`git status` → "nothing to commit, working tree clean") silebilirsin.

### 0.3 Claude Code (PC, ana araç)

Claude Code, Claude'un kod yazan ajanıdır. PC'de çalışınca Windows testlerini de çalıştırabilir.

- **Kurulum:** claude.ai/download'dan Claude masaüstü uygulamasını kur ve Claude hesabınla gir. Uygulamada **Code** bölümünü aç.
- **Klasör = hat:** Yeni oturum açarken klasör olarak örneğin `C:\dev\hp-opus` seç. Her hat kendi klasöründe ayrı oturumdur; aynı anda birkaç oturum açık olabilir.
- **Oturum:** Bir oturumda sırayla birkaç görev yapılabilir; her görev ayrı commit'tir. Oturum çok uzarsa yeni oturum aç (uzayan oturum kotayı hızlı bitirir).
- **Model ve düşünme seviyesi (effort):** Oturumun başında, ilk mesajdan önce seç: model seçicide ya da sohbet kutusuna `/model` yazarak (her işte Opus). Effort de orada ya da `/effort` ile. Kısa kural: kod işi High, sadece metin işi Medium (`04-FAZLAR.md` → "Araçlar, modeller ve kota sırası"). Oturum ortasında modeli değiştirme.
- **İzinler (önemli):** İzin modu varsayılan kalsın: ajan her komut ve dosya değişikliği için onay ister. Otomatik onaylayan modları (auto, bypass) bu projede açma.
  - `git`, `gh`, `dotnet build/test/restore/format` ve `tools/dev` komutlarını onayla; bunlar için "bir daha sorma" seçeneği varsa seçebilirsin.
  - Uygulamayı çalıştıran (`dotnet run`, `.exe`), kayıt defterine yazan (`reg`, `Set-ItemProperty`), ekran, NVIDIA ya da monitör ayarına dokunan ya da program kuran komutları onaylama; önce bölüm 7'deki metinle sor.
- **Plan modu:** Yazma görevlerinde plan modunu aç (mod seçicide ya da Shift+Tab). Ajan önce plan çıkarır, sen okuyup onaylarsın, sonra yazar.
- **Kota:** Sohbet kutusuna `/usage` yaz; ne kadar kaldığını ve ne zaman yenileneceğini gösterir.
- Bir ayarın adı sende farklıysa ya da bulamıyorsan bölüm 7'deki metinle sor.

### 0.4 Claude Code bulut (yedek)

PC'ye ulaşamadığında claude.ai/code'da bu repoyla (`mehemmedsh/hp-v214a`) oturum açılabilir. Bulut ortamının kurulum betiğine (setup script) şu satırı ekle; böylece bulut oturumları da kodu derler ve testleri çalıştırır:

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0
```

Bulutta Windows'a özel testler çalışmaz (CI'da çalışır).

## 1. Görev döngüsü

1. `docs/04-FAZLAR.md`: ilk satır (sıradaki görev).
2. **Yazma:** Claude Code'da `hp-opus` klasöründe oturum → Opus ve effort'u seç → bölüm 2.
3. Ajan bitince özetini oku. Commit ve push etmiş olmalı; testler geçtiyse görev ✅ (🔧 görevde 🔧). 🔧 görevse test adımlarını not et.
4. 🔧 görevde monitör testini yap ve sonucu bildir (bölüm 5). Test geçmezse düzeltme (bölüm 4).

## 2. Yazma

```text
Görev: T___. AGENTS.md'deki okuma haritasına göre gerekenleri oku; docs/04-FAZLAR.md'deki
tanım, bağımlılıklar ve "Bitti sayılır" kriterine göre yap.
Önce git pull --rebase. Bağımlı görevlerin hepsi ✅ veya 🔧 değilse başlama, bana söyle.
Önce testleri yaz, sonra kodu. Uygulamayı ve donanıma yazan kodu çalıştırma.
Bitince: docs/05-HANDOFF.md → 3'teki kendi kontrolünü yap; derle, testleri çalıştır;
geçerse docs/04-FAZLAR.md'de durumu ✅ yap (🔧 görevse 🔧), commit ("T___: ...") ve push.
Sonunda bana Türkçe ve kısa: ne yaptın, neyi test ettin, dikkat edilmesi gereken 3 nokta
(aynısı commit mesajında); 🔧 ise monitörde yapacağım testin adımları.
```

## 3. Kendi kontrolü (ayrı inceleme yerine)

2026-11-05'ten beri ayrı inceleme oturumu yok. Yazan, commit'ten önce kendi farkına şu gözle bakar:

1. "Bitti sayılır" kriteri gerçekten sağlanıyor mu?
2. Testler gerçek davranışı mı test ediyor, yoksa sadece geçmek için mi yazılmış?
3. Donanıma veya sistem ayarına yazan bir yol Güvenlik katmanını atlıyor mu?
4. GPU renk tablosuna renk boru hattı dışında yazan bir yer var mı?
5. `03-GUVENLIK.md`'deki değişmez kurallardan biri ihlal ediliyor mu
   (otomatik Hz/parlaklık, firmware veya EDID yazma, 60 Hz'e dokunma, karartma tabanı, panik kısayolu)?
6. Sözleşme (T002) değişmiş mi; değiştiyse `02-MIMARI.md` güncellenmiş mi?

## 4. Düzeltme

🔧 testi geçmediğinde ya da bir hata bulunduğunda (`hp-opus`, Opus):

```text
T___ için aşağıdaki DÜZELT listesi var. Sadece bu maddeleri düzelt.
Önce git pull --rebase. Dokümanları baştan okuma; maddelerin geçtiği dosyaları ve gerekirse
AGENTS.md'deki haritadan ilgili bölümü aç. Derle, testleri çalıştır, docs/05-HANDOFF.md → 3'e bak.
Commit mesajı "T___ düzeltme: ..." ile başlar; push et.
Sonunda her madde için tek satır: ne yaptın.

[DÜZELT listesi]
```

## 5. 🔧 Monitör testi

**Test paketini açma** (iki yoldan birini seç):

- **A. CI paketi (önerilen, kurulum gerekmez):** CI (GitHub'ın her push'ta kodu derleyip test eden sunucusu) her push'ta Windows paketi üretir ve 7 gün saklar.
  1. Tarayıcıda github.com/mehemmedsh/hp-v214a → **Actions** → listede test edeceğin commit'in yeşil ✓ olan **CI** çalışmasını aç.
  2. Sayfanın altındaki **Artifacts** bölümünden `MonitorTuner-win-x64`'ü indir.
  3. İnen zip'e sağ tık → **Tümünü ayıkla**. Klasördeki `MonitorTuner.App.exe`'yi çift tıkla.
  4. "Windows bilgisayarınızı korudu" çıkarsa: **Ek bilgi** → **Yine de çalıştır** (uygulama imzasız olduğu için çıkar).
  - Komutla aynısı: `gh run list -w CI -L 3` ile çalışmanın numarasını bul, sonra `gh run download <numara> -n MonitorTuner-win-x64 -D C:\dev\paket`.
  - 7 gün geçtiyse paket silinir: Actions'ta o çalışmayı açıp **Re-run all jobs** de ya da B yolunu kullan.
- **B. Yerel derleme (.NET 10 SDK gerekir):** PowerShell'de `cd C:\dev\hp-inceleme`, `git pull`, sonra `dotnet run --project src/MonitorTuner.App -c Release`.

Uygulamayı sadece sen açarsın; ajanlar açmaz.

**Monitörde kalan kontroller** (kod beklemez):
- 75 Hz (2080×1100) ve 76 Hz (2080×1092): Windows aktif sinyal, DDC AE = 7500 / 7600 ve testufo kare atlama. **T209'dan önce.**
- DDC 14 kodunun eşlemesi: OSD'de her renk ön ayarını sırayla seç, ControlMyMonitor'da 14'ün değerini oku (12–15 ve tahmin edilen 2, 4, 8, 11). **T004'ten önce.**

**T312 + T315 ayar sihirbazı** (tek test; sayfa: ana pencerede **Ayar sihirbazı** sekmesi):
1. Monitörü en az 30 dk açık tut (sihirbaz ilk adımda geri sayımı gösterir). OSD'de renk ön ayarı **Custom RGB**, NVIDIA'da çıkış **RGB Full**. Oturma konumu rehberdeki gibi.
2. Tepsiden mavi ışığı aç, parlaklığı ek karartma bölümüne indir. **Sihirbazı başlat**: ekran karartmasız ve sıcak tonsuz olmalı; tepsideki profil, parlaklık ve mavi ışık ögeleri kapalı görünür; **Monitör ayarları** sekmesi seçilemez.
3. Her adımda **Deseni aç** düğmeleri: desen tam ekran, ana pencerenin ekranında açılır; sağ/sol ok ile kareler değişir, kendiliğinden değişmez, yanıp sönmez; Esc kapatır. **İleri** ve **Geri** monitöre hiçbir şey yazmaz.
4. Kontrast ve parlaklık: kaydırıcıyı bırakınca bir kez yazılır; siyahta 1–25, beyazda 230–254 yamalarının çoğu ayrı görünsün. OSD'de değerler sihirbazdakiyle aynı olmalı.
5. Keskinlik: yazı kenarında beyaz hale çıkmayan en yüksek değer. RGB Full: 1–16 yamaları ayrı görünüyor.
6. Açı: üç gri şerit eşit; ölü piksel ve homojenlik: siyah, beyaz, R, G, B ve griler temiz.
7. Beyaz eşleme: Pixel "Doğal" modda beyaz sayfa, yan yana; R/G/B kazançlarıyla iki beyaz eşlenir.
8. Gamma: desende soldaki çizgili alan **titreşmeden ince çizgiler** olarak görünmeli (griye karışık ya da dalgalı değil). 2–3 m geriden en eşit görünen karenin altındaki sayıyı oku, listeden aynı satırı seç: ekran hemen değişir. Deseni yeniden aç: artık 2,2 karesi (gri 186) eşit görünmeli.
9. Gri dengesi: koyu, orta, açık gri renksiz görünene kadar kaydırıcıları küçük adımlarla değiştir. Sonra Windows'ta siyah→beyaz bir gradyan aç (ör. tarayıcıda "gradient test"): **bant (basamak) yok**, griler nötr.
10. **Bitir ve kaydet:** mavi ışık geri gelir; arka ışığı sihirbazda değiştirdiysen ek karartma geri **gelmez** (ekran koyulaşmaz), parlaklık kaydırıcısı yeni arka ışık değerinde durur. Uygulamayı kapatıp aç: temel ayar ve kalibrasyon aynı.
11. Sihirbazı yeniden başlat, birkaç değer ve gamma değiştir, **İptal**: OSD'deki değerler ve ekranın tonu eskiye döner, ek karartma ve mavi ışık eskisi gibi geri gelir.
12. Sihirbaz açıkken ana pencereyi kapatıp tepsiden yeniden aç: sihirbaz kaldığı adımda durur.

**Sonucu bildirme** (`hp-inceleme`, Opus · Medium):

```text
T___ 🔧 testini monitörde yaptım. Sonuç: [geçti / geçmedi]. Notlarım: [...]
Önce git pull --rebase. Geçtiyse docs/04-FAZLAR.md'de durumu ✅ yap.
Geçmediyse LESSONS.md'ye not düş, durumu 🟨 yap ve yazan modelin düzelteceği maddeleri
DÜZELT listesi biçiminde ver (düzeltme: bölüm 4). Commit ve push et. Bana Türkçe ve kısa yaz.
```

## 6. Kota bitince

- **Kullanım sınırı doldu:** `/usage` ne zaman yenileneceğini gösterir; yenilenene kadar bekle.
- **Görevin ortasında doldu:** Modeli değiştirme. Yenilenince aynı oturuma "devam et" yaz.
- **PC'ye ulaşamıyorsun:** bölüm 0.4 (bulut oturumu), bölüm 2 veya 4'teki metnin aynısıyla.

## 7. Takıldığında yardım (yeni oturum)

Hata mesajı, CI logu, komut çıktısı ya da soru Claude Code'un ekranı, menüsü, ayarı hakkındaysa → **Opus · Medium**. Bir karar gerekiyorsa (kural, plan, geri alınamayan iş) ya da cevaplar birbirini tutmuyorsa → **Opus · High**.

Claude Code'da `hp-inceleme` klasöründe yeni bir oturum aç ve yapıştır:

```text
Bu bir yardım oturumu: kod yazma, dosya değiştirme, komut çalıştırma.
hp-v214a projesindeyim; Claude Code'a yeni geçtim. AGENTS.md ve docs/05-HANDOFF.md'nin
gerekli bölümlerini oku. Ne yapmam gerektiğini Türkçe, kısa ve adım adım anlat.
Gerekirse görev oturumuna yapıştıracağım metni yaz.
Şu an: görev T___, klasör hp-____.
Sorun / anlamadığım şey: [...]
Ekranda gördüğüm mesaj (varsa, aynen): [...]
```

## 8. Son kontrol noktası (T503)

**Nerede:** Claude app, yeni sohbet · **Model:** Opus

```text
hp-v214a (HP P21b G4 için Windows uygulaması) projesinde son kontrol noktasındayım (T503).
Görev listesi, LESSONS.md ve monitör test sonuçlarım aşağıda. Değerlendir:
- Hedeflerim (en iyi görüntü kalitesi, göz konforu, 76 Hz, aşırı ince ayar yok) karşılandı mı?
- Planla gerçek sonuç arasında fark var mı?
- 06-CIKARILANLAR.md'deki plan dışı fikirlerden geri alınmaya değer olan var mı?
- LESSONS.md'ye eklenmesi gereken ders var mı?

[görev listesi + test sonuçları + LESSONS.md buraya]
```
