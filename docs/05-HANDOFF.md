# Handoff

Hazır metinler ve kurulum. Düzen: **Opus ve Sonnet Claude Code'da** (2026-11-03'ten beri; Antigravity kullanılmıyor, Qwen 2026-11-04'te çıkarıldı). Claude Code `CLAUDE.md` üzerinden `AGENTS.md`'yi otomatik okur. Kurallar, okuma haritası ve commit biçimi orada olduğu için buradaki metinler kısa. Her metinde `T___` yerine görev numarasını yaz.

**Sıradaki iş:** `04-FAZLAR.md`'nin ilk satırında. Antigravity'den geçiyorsan önce 0.2'deki klasör adı değişikliğini ve 0.3'ü yap.

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

### 0.2 Klasörler (hatlar)

```powershell
mkdir C:\dev
cd C:\dev
git clone https://github.com/mehemmedsh/hp-v214a hp-opus
git clone https://github.com/mehemmedsh/hp-v214a hp-sonnet
git clone https://github.com/mehemmedsh/hp-v214a hp-inceleme
```

Dördüncü hat (`hp-sonnet2`) sadece Dalga 4 ve 5'te, alışınca gerekir: aynı komut, sonda `hp-sonnet2`.

Her klasör projenin ayrı bir kopyasıdır. Ajan işini GitHub'a gönderir (push), diğer klasörler oradan çeker (pull). Böylece aynı anda çalışan iki ajan birbirinin dosyasını bozmaz.

**Antigravity'den geçiş (bir kez):** eski `hp-gemini` klasörü artık Sonnet hattı. Önce içinde yarım iş olmadığına bak, sonra adını değiştir:

```powershell
cd C:\dev\hp-gemini
git status
```

"nothing to commit, working tree clean" görürsen:

```powershell
cd C:\dev
Rename-Item hp-gemini hp-sonnet
```

Başka bir şey yazıyorsa adını değiştirme, bölüm 7'deki metinle sor. `hp-gemini2` açtıysan aynı yolla `hp-sonnet2` yap. Antigravity'yi kapatabilirsin; bu proje için artık açılmaz.

### 0.3 Claude Code (PC, ana araç)

Claude Code, Claude'un kod yazan ajanıdır. PC'de çalışınca Windows testlerini de çalıştırabilir.

- **Kurulum:** claude.ai/download'dan Claude masaüstü uygulamasını kur ve Claude hesabınla gir. Uygulamada **Code** bölümünü aç.
- **Klasör = hat:** Yeni oturum açarken klasör olarak örneğin `C:\dev\hp-opus` seç. Her hat kendi klasöründe ayrı oturumdur; aynı anda birkaç oturum açık olabilir.
- **Bir oturum, bir görev:** Yazma, inceleme ve düzeltme ayrı oturumdur. Bitince yeni oturum aç (uzayan oturum kotayı hızlı bitirir).
- **Model ve düşünme seviyesi (effort):** Oturumun başında, ilk mesajdan önce seç: model seçicide ya da sohbet kutusuna `/model` yazarak (Opus veya Sonnet). Effort de orada ya da `/effort` ile. Kısa kural: kod işi High, sadece metin işi Medium. Hangi işte hangisi: `04-FAZLAR.md` → "Araçlar, modeller ve kota sırası" ve görev tablosundaki "Yazan → İnceleyen". Oturum ortasında modeli değiştirme.
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

1. `docs/04-FAZLAR.md`: ilk satır (sıradaki görev) ve "Dalgalar" tablosu (hangi hat). Model: görevin "Yazan → İnceleyen" sütunu.
2. **Yazma:** Claude Code'da görevin hattının klasöründe yeni oturum → modeli ve effort'u seç → bölüm 2.
3. Ajan bitince özetini oku. Commit ve push etmiş olmalı. 🔧 görevse test adımlarını not et.
4. **İnceleme:** `hp-inceleme` klasöründe yeni oturum → inceleyen modeli seç → bölüm 3.
5. **ONAY** ise görev biter; ajan durumu ✅ (🔧 görevde 🔧) yapar. **DÜZELT** ise: yazanın klasöründe yeni oturum, yazan model, bölüm 4. Sonra `hp-inceleme`'de yeniden inceleme (bölüm 4).
6. 🔧 görevde monitör testini yap ve sonucu bildir (bölüm 5).
7. Aynı dalgadaki diğer görevler için 2–6'yı diğer hatta paralel yürüt. Dalganın hepsi onaylanınca sonraki dalgaya geç.

## 2. Yazma

```text
Görev: T___. AGENTS.md'deki okuma haritasına göre gerekenleri oku; docs/04-FAZLAR.md'deki
tanım, bağımlılıklar ve "Bitti sayılır" kriterine göre yap. Sadece bu görev.
Önce git pull --rebase. Bağımlı görevlerin hepsi ✅ veya 🔧 değilse başlama, bana söyle.
Önce testleri yaz, sonra kodu. Uygulamayı ve donanıma yazan kodu çalıştırma.
Bitince: derle, testleri çalıştır; docs/04-FAZLAR.md'de durumu 🟨 yap, commit ("T___: ...") ve push.
Sonunda bana Türkçe ve kısa: ne yaptın, neyi test ettin, inceleyicinin bakması gereken 3 nokta
(aynısı commit mesajında); 🔧 ise monitörde yapacağım testin adımları.
```

## 3. İnceleme

```text
T___ görevinin incelemesini yap. Kodu sen yazmadın. Önce git pull --rebase.
git log --oneline --grep "^T___" ile görevin commit'lerini bul. Commit mesajındaki
"inceleyiciye 3 nokta" ile başla, sonra bu commit'lerin farkına bak.
AGENTS.md'deki okuma haritasına göre gerekenleri oku.

Kontrol et:
1. "Bitti sayılır" kriteri gerçekten sağlanıyor mu?
2. Testler gerçek davranışı mı test ediyor, yoksa sadece geçmek için mi yazılmış?
3. Donanıma veya sistem ayarına yazan bir yol Güvenlik katmanını atlıyor mu?
4. GPU renk tablosuna renk boru hattı dışında yazan bir yer var mı?
5. 03-GUVENLIK.md'deki değişmez kurallardan biri ihlal ediliyor mu
   (otomatik Hz/parlaklık, firmware veya EDID yazma, 60 Hz'e dokunma, karartma tabanı, panik kısayolu)?
6. Sözleşme (T002) değişmiş mi; değiştiyse 02-MIMARI.md güncellenmiş mi?

Derlemeyi ve testleri kendin de çalıştır (sessiz komutlarla). Kodu değiştirme.
ONAY ise: docs/04-FAZLAR.md'de durumu ✅ yap (🔧 görevse 🔧), commit ("T___ inceleme: ONAY") ve push.
DÜZELT ise: numaralı liste ver (her madde: dosya, sorun, beklenen düzeltme); hiçbir dosyayı değiştirme.
Bana Türkçe ve kısa yaz.
```

## 4. Düzeltme ve yeniden inceleme

**Düzeltme** (yazanın klasörü, yazan model):

```text
T___ incelemesi aşağıdaki DÜZELT listesini verdi. Sadece bu maddeleri düzelt.
Önce git pull --rebase. Dokümanları baştan okuma; maddelerin geçtiği dosyaları ve gerekirse
AGENTS.md'deki haritadan ilgili bölümü aç. Derle, testleri çalıştır.
Commit mesajı "T___ düzeltme: ..." ile başlar; push et.
Sonunda her madde için tek satır: ne yaptın.

[DÜZELT listesi]
```

**Yeniden inceleme** (`hp-inceleme`, inceleyen model):

```text
T___ düzeltmesini incele. Önce git pull --rebase. Sadece git log --oneline --grep "^T___ düzeltme"
commit'lerine ve aşağıdaki maddelere bak. Her madde için: düzeldi / düzelmedi.
Düzeltmenin yol açtığı yeni bir sorun gördüysen ekle.
Derle ve testleri çalıştır.
ONAY ise docs/04-FAZLAR.md'de durumu ✅ yap (🔧 görevse 🔧), commit ve push; değilse DÜZELT listesi ver.

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
- 75 Hz (2080×1100) ve 76 Hz (2080×1092): testufo kare atlama. (AE = 7500 / 7600 okundu, 2026-11-04.) **T209'dan önce.**
- ~~DDC 14 kodunun eşlemesi~~ yapıldı (2026-11-04, T004 ✅).

**Sonucu bildirme** (`hp-inceleme`, Sonnet · Medium yeterli):

```text
T___ 🔧 testini monitörde yaptım. Sonuç: [geçti / geçmedi]. Notlarım: [...]
Önce git pull --rebase. Geçtiyse docs/04-FAZLAR.md'de durumu ✅ yap.
Geçmediyse LESSONS.md'ye not düş, durumu 🟨 yap ve yazan modelin düzelteceği maddeleri
DÜZELT listesi biçiminde ver. Commit ve push et. Bana Türkçe ve kısa yaz.
```

## 6. Kota bitince

- **Kullanım sınırı doldu:** `/usage` ne zaman yenileneceğini gösterir; yenilenene kadar bekle.
- **Sadece Opus'un sınırı doldu:** o arada Sonnet hattındaki işlere geç (Sonnet yazma, Sonnet düzeltme, Sonnet incelemesi; hangisi: `04-FAZLAR.md` → Dalgalar ve görev tabloları). Opus işini Sonnet'e verme.
- **Görevin ortasında doldu:** Modeli değiştirme. Yenilenince aynı oturuma "devam et" yaz.
- **PC'ye ulaşamıyorsun:** bölüm 0.4 (bulut oturumu), bölüm 2, 3 veya 4'teki metnin aynısıyla.

## 7. Takıldığında yardım (yeni oturum)

Hata mesajı, CI logu, komut çıktısı ya da soru Claude Code'un ekranı, menüsü, ayarı hakkındaysa → **Sonnet · Medium**. Bir karar gerekiyorsa (kural, plan, geri alınamayan iş) ya da cevaplar birbirini tutmuyorsa → **Opus · High**.

Claude Code'da `hp-inceleme` klasöründe yeni bir oturum aç ve yapıştır:

```text
Bu bir yardım oturumu: kod yazma, dosya değiştirme, komut çalıştırma.
hp-v214a projesindeyim; Claude Code'a yeni geçtim. AGENTS.md ve docs/05-HANDOFF.md'nin
gerekli bölümlerini oku. Ne yapmam gerektiğini Türkçe, kısa ve adım adım anlat.
Gerekirse görev oturumuna yapıştıracağım metni yaz.
Şu an: Dalga __, görev T___, klasör hp-____, model ____.
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
