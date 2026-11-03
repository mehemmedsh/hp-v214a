# Handoff

Hazır metinler ve kurulum. Düzen: **Opus ve Sonnet Claude Code'da, Qwen PC'de yerel** (2026-11-03'ten beri; Antigravity kullanılmıyor). Claude Code `CLAUDE.md` üzerinden `AGENTS.md`'yi otomatik okur. Kurallar, okuma haritası ve commit biçimi orada olduğu için buradaki metinler kısa. Her metinde `T___` yerine görev numarasını yaz.

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

Claude Code, Claude'un kod yazan ajanıdır. PC'de çalışınca Windows testlerini ve Qwen'i kullanabilir.

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

Bulutta Windows'a özel testler çalışmaz (CI'da çalışır) ve Qwen'e ulaşılamaz; ajan Qwen adımını atlar ve commit gövdesine yazar.

### 0.5 Qwen (yerel model)

Qwen, PC'de Ollama ile çalışan yerel modeldir: kota harcamaz, internet istemez. Rolleri ve durumları: `04-FAZLAR.md` → "Qwen rolleri". Kurulu ve ölçüldü (2026-11-03): Windows 11, Ollama 0.40.14, model `qwen3.8:27b` (17 GB, 4-bit), sadece RTX 3090'da; üretim 47 token/sn; düşünme modu varsayılan açık.

**Ortam değişkenleri** (kullanıcı değişkeni; Ollama yeniden başlayınca geçerli olur):

| Değişken | Değer | Neden |
|---|---|---|
| `CUDA_VISIBLE_DEVICES` | 3090'ın UUID'si | Ollama sadece 3090'ı görür; 3080 masaüstüne kalır |
| `OLLAMA_FLASH_ATTENTION` | `1` | daha az bellek, daha hızlı |
| `OLLAMA_KV_CACHE_TYPE` | `q8_0` | bağlam belleği yarıya iner |
| `OLLAMA_CONTEXT_LENGTH` | `65536` (sığmazsa `32768`) | bağlam: modelin bir seferde okuyabildiği metin miktarı. Aşan girdi sessizce kırpılır; 16k bir ön inceleme paketine yetmez |
| `OLLAMA_NUM_PARALLEL` | `1` | aynı anda tek istek; Ollama belleği bağlam × paralel istek kadar ayırdığı için hesap öngörülebilir kalır |

PowerShell'de (bir kez):

```powershell
[Environment]::SetEnvironmentVariable('OLLAMA_CONTEXT_LENGTH', '65536', 'User')
[Environment]::SetEnvironmentVariable('OLLAMA_NUM_PARALLEL', '1', 'User')
```

**Ollama'yı yeniden başlatma:** görev çubuğunun sağındaki Ollama simgesine sağ tık → Quit Ollama; sonra Başlat menüsünden Ollama'yı aç. Ollama uygulamasının ayarlarındaki bağlam (context length) kaydırıcısını da aynı değere (64k) getir. Komutla kapatmak gerekirse sadece tam adlarla: `Stop-Process -Name 'ollama app','ollama','llama-server' -ErrorAction SilentlyContinue` (`*ollama*` yazarsan kurulum programı da kapanır).

**Kontrol ve ölçüm** (PowerShell, `cd C:\dev\hp-inceleme`, `git pull`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Check
```

Betik bağlantıyı, modeli ve Türkçe karakterlerin bozulmadan gidip geldiğini dener; sonra `ollama ps` ve `nvidia-smi` çıktısını gösterir.

- Bakılacaklar: `UTF-8: TAMAM`; `ollama ps`'te CONTEXT 65536 ve PROCESSOR `100% GPU`; 3090'da kullanılan bellek 23 GB'ın altında.
- `100% GPU` değilse ya da bellek doluysa `OLLAMA_CONTEXT_LENGTH`'i `32768` yap, Ollama'yı yeniden başlat, tekrar dene.
- Betik farkı bağlama göre parçalara böler; ama 32k'da büyük işler 2–3 parçaya ayrılır ve Qwen'in düşünmesine az yer kalır.

## 1. Görev döngüsü

1. `docs/04-FAZLAR.md`: ilk satır (sıradaki görev) ve "Dalgalar" tablosu (hangi hat). Model: görevin "Yazan → İnceleyen" sütunu.
2. **Yazma:** Claude Code'da görevin hattının klasöründe yeni oturum → modeli ve effort'u seç → bölüm 2.
3. Ajan bitince özetini oku. Commit ve push etmiş olmalı. Qwen ön incelemesi ✅ ise ajan onu commit'ten önce yapmış olur; özetinde "Qwen: ..." satırını görürsün (bölüm 9.1). 🔧 görevse test adımlarını not et.
4. **İnceleme:** `hp-inceleme` klasöründe yeni oturum → inceleyen modeli seç → bölüm 3.
5. **ONAY** ise görev biter; ajan durumu ✅ (🔧 görevde 🔧) yapar. **DÜZELT** ise: yazanın klasöründe yeni oturum, yazan model, bölüm 4 (ajan sonunda Qwen düzeltme kontrolünü yapar, bölüm 9.2). Sonra `hp-inceleme`'de yeniden inceleme (bölüm 4).
6. 🔧 görevde monitör testini yap ve sonucu bildir (bölüm 5).
7. Aynı dalgadaki diğer görevler için 2–6'yı diğer hatta paralel yürüt. Dalganın hepsi onaylanınca sonraki dalgaya geç.

## 2. Yazma

```text
Görev: T___. AGENTS.md'deki okuma haritasına göre gerekenleri oku; docs/04-FAZLAR.md'deki
tanım, bağımlılıklar ve "Bitti sayılır" kriterine göre yap. Sadece bu görev.
Önce git pull --rebase. Bağımlı görevlerin hepsi ✅ veya 🔧 değilse başlama, bana söyle.
Önce testleri yaz, sonra kodu. Uygulamayı ve donanıma yazan kodu çalıştırma.
Bitince: derle, testleri çalıştır; Qwen ön incelemesi ✅ ise AGENTS.md → "Yerel model (Qwen)"
kuralına göre yap; docs/04-FAZLAR.md'de durumu 🟨 yap, commit ("T___: ...") ve push.
Sonunda bana Türkçe ve kısa: ne yaptın, neyi test ettin, inceleyicinin bakması gereken 3 nokta
(aynısı commit mesajında), Qwen satırı; 🔧 ise monitörde yapacağım testin adımları.
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

Commit gövdesinin sonundaki Qwen satırını kendi incelemeni bitirdikten sonra oku; reddedilen
bir bulguya katılmıyorsan DÜZELT listesine ekle.
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
Qwen düzeltme kontrolü ✅ ise AGENTS.md → "Yerel model (Qwen)" kuralına göre yap
(listeyi .qwen/duzelt-T___.md dosyasına yaz).
Commit mesajı "T___ düzeltme: ..." ile başlar; push et.
Sonunda her madde için tek satır: ne yaptın; Qwen satırı.

[DÜZELT listesi]
```

**Yeniden inceleme** (`hp-inceleme`, inceleyen model):

```text
T___ düzeltmesini incele. Önce git pull --rebase. Sadece git log --oneline --grep "^T___ düzeltme"
commit'lerine ve aşağıdaki maddelere bak. Her madde için: düzeldi / düzelmedi.
Düzeltmenin yol açtığı yeni bir sorun gördüysen ekle. Commit gövdesindeki Qwen satırını en son oku.
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
- 75 Hz (2080×1100) ve 76 Hz (2080×1092): Windows aktif sinyal, DDC AE = 7500 / 7600 ve testufo kare atlama. **T209'dan önce.**
- DDC 14 kodunun eşlemesi: OSD'de her renk ön ayarını sırayla seç, ControlMyMonitor'da 14'ün değerini oku (12–15 ve tahmin edilen 2, 4, 8, 11). **T004'ten önce.**

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

Önce ucuz yol: hata mesajı, CI logu, komut çıktısı → Qwen (bölüm 9.3). Qwen çözmezse ya da soru Claude Code'un ekranı, menüsü, ayarı hakkındaysa → **Sonnet · Medium**. Bir karar gerekiyorsa (kural, plan, geri alınamayan iş) ya da cevaplar birbirini tutmuyorsa → **Opus · High**.

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

## 9. Qwen adımları

Hangi rolün açık olduğu: `04-FAZLAR.md` → "Qwen rolleri". Komutlar `C:\dev\hp-...` klasörlerinden birinde, PowerShell'de çalışır (ajan PC'deki Claude Code'da kendisi çalıştırır). Her çalıştırma `.qwen\` klasörüne üç dosya yazar: sonuç (`...md`), Qwen'e giden paket (`...-paket.md`) ve Qwen'in düşünmesi (`...-dusunme.md`). Bu klasör GitHub'a gitmez.

### 9.1 Ön inceleme (yazan ajan yapar)

Ajan bunu `AGENTS.md` sayesinde kendisi yapar; komut `tools/dev` betiği olduğu için onaylaman yeterli. Birkaç dakika sürer; ekranda 20 saniyede bir "... sn | dusunme ... | cevap ..." satırı akar.

Ajan çalıştıramazsa (zaman aşımı, hata) yazanın klasöründe sen çalıştır:

```powershell
git add -A
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Task T___
```

Sonra yazanın aynı oturumuna yapıştır:

```text
Qwen ön incelemesi bitti: .qwen/T___-review-son.md. AGENTS.md → "Yerel model (Qwen)" kuralına
göre her bulguyu doğrula, doğrusunu düzelt; sonra durumu 🟨 yap, commit ve push.
```

Çıktıda her bulgunun altında betiğin notu var. "UYDURMA OLABILIR" yazıyorsa Qwen'in alıntısı farkta ya da dokümanda yok; o bulgu büyük ihtimalle yanlış.

### 9.2 Düzeltme kontrolü (düzelten ajan yapar)

Ajan DÜZELT listesini `.qwen/duzelt-T___.md`'ye yazar ve kontrolü kendisi çalıştırır. Elle:

```powershell
git add -A
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Task T___ -Mode fix
```

Listeyi elle kaydedeceksen: Not Defteri → yapıştır → Farklı kaydet → `C:\dev\hp-...\.qwen\duzelt-T___.md`, kodlama UTF-8. Zaten commit edilmiş düzeltmeyi kontrol etmek için sona `-Commits <kısa-hash>` ekle (birden çoksa virgülle).

### 9.3 Yardım: hata açıklama (Qwen)

**Qwen** (hata mesajı, derleme hatası, CI logu, git veya PowerShell çıktısı): Ollama uygulamasını aç → yeni sohbet → model `qwen3.8:27b`. Gerekirse `AGENTS.md`'yi sohbete sürükleyip bırak. Yapıştır:

```text
hp-v214a projesindeyim (C# / .NET 10 / WPF, Windows 11); Claude Code'da çalışıyorum.
Aşağıdaki hatayı ya da çıktıyı Türkçe, kısa ve adım adım açıkla: ne demek, muhtemel sebebi,
ne yapmalıyım. Kod yazma.
Sadece şu komutları önerebilirsin: git (status, log, diff, pull --rebase, add, commit, push),
gh, dotnet restore/build/test/format, tools/dev betikleri. Kayıt defterine, NVIDIA'ya, sürücüye
ya da monitöre dokunan; dosya silen; git reset --hard, push --force, clean gibi geri alınamayan
komut önerme. Claude Code'un menüleri hakkında bilgin eski olabilir: menü adı tahmin etme,
"Sonnet'e sor" de. Emin değilsen "emin değilim" yaz.
Şu an: görev T___, klasör hp-____.
Hata / çıktı (aynen):
[...]
```

Uzun CI logunda son 80 satır yeter: `gh run view <numara> --log-failed | Select-Object -Last 80`.

Qwen çözemezse ya da soru Claude Code'un ekranı, menüsü, ayarı hakkındaysa: bölüm 7.

### 9.4 Kalibrasyon

Kalibrasyon, bir rolü açmadan önce Qwen'i sonucu bilinen eski bir işte sınamaktır. Hepsi `C:\dev\hp-inceleme`'de, PowerShell'de yapılır; önce `git pull`. Her koşu birkaç dakika sürer.

**0. Ayar ve kontrol:** bölüm 0.5 (bağlam 64k, `-Check`).

**1. Ön inceleme: T311'in ilk commit'i (d7b066d), iki koşu.**
- Bu commit incelemede DÜZELT almıştı; düzeltmeleri 095b5d3 ve 99d44ba'da.
- Cevap anahtarı bu düzeltmelerden çıkarıldı (`tools/dev/qwen/kalibrasyon/T311-anahtar.txt`): 4 kesin sorun, 5 kaynaksız ayrıntı, 3 yoruma bağlı madde.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Task T311 -Commits d7b066d -Key tools/dev/qwen/kalibrasyon/T311-anahtar.txt -Name kal1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Task T311 -Commits d7b066d -Key tools/dev/qwen/kalibrasyon/T311-anahtar.txt -Name kal2
```

Betik sonuçta anahtarla eşleşmeyi sayar ve TAMAM / KALDI yazar. Eşleştirme kaba bir arama olduğu için şüpheli eşleşmeye bak.

**Geçer** (iki koşuda da):
- kesin en az 3/4
- kaynaksız en az 2/5
- uydurma bulgu (alıntısı bulunamayan ya da dokümanla çelişen) en fazla 2
- toplam bulgu en fazla 15
- koşu 15 dakikayı geçmez

**2. Negatif kontrol: T001 (5af407e, onaylanmış iş), bir koşu.** Onaylı işte gerçek sorun az olmalı.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Task T001 -Commits 5af407e -Name kal3
```

**Geçer:** "yüksek" önemde yanlış bulgu en fazla 1, uydurma en fazla 2. Bulgunun doğru mu yanlış mı olduğuna Sonnet bakar (aşağıdaki metin).

**3. Düzeltme kontrolü: T311 düzeltmeleri, iki koşu.** 10 maddelik liste düzeltmelerden yeniden yazıldı (`tools/dev/qwen/kalibrasyon/T311-duzelt.md`).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Task T311 -Mode fix -List tools/dev/qwen/kalibrasyon/T311-duzelt.md -Commits 095b5d3,99d44ba -Name kal4
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Task T311 -Mode fix -List tools/dev/qwen/kalibrasyon/T311-duzelt.md -Commits 095b5d3 -Name kal5
```

Beklenen:
- `kal4`'te M1–M10 hepsi "düzeldi".
- `kal5`'te M10 "düzelmedi" (CTA-861/VIC düzeltmesi 99d44ba'da); diğerleri "düzeldi".

**Geçer:** düzelmeyen maddeye "düzeldi" denmez ve 20 kararın en az 18'i doğru.

Eski T311 inceleme konuşmasında (Antigravity) asıl DÜZELT listesi duruyorsa onu da dene: `.qwen\duzelt-T311.md` olarak kaydet ve `-List .qwen/duzelt-T311.md` ile çalıştır.

**4. Hata açıklama: iki eski hata.** Ollama uygulamasında, 9.3'teki metinle, ayrı sohbetlerde sor:
- **Hata:** `error CS0426: The type name 'App' does not exist in the type 'App'` (proje adı `App` iken).
  - Doğru cevap: sınıf adı ad alanıyla (namespace) çakışıyor; proje ya da ad alanı adını değiştir (bu projede `MonitorTuner.App`).
- **Soru:** "PowerShell 5.1'de `git diff | ollama run qwen3.8:27b` yapınca Türkçe karakterler bozuk geliyor."
  - Doğru cevap: boruya giden metnin kodlaması (`$OutputEncoding`) UTF-8 değil; UTF-8 yap ya da farkı dosyaya yazıp dosyadan ver.

**Geçer:** ikisi de doğru ve tehlikeli komut önermiyor.

**Sonucu bildirme** (`hp-inceleme`, Sonnet · Medium, yeni oturum):

```text
Qwen kalibrasyonunu yaptım. Sonuçlar .qwen/ klasöründe: kal1.md … kal5.md.
docs/05-HANDOFF.md → 9.4'teki ölçütlere göre değerlendir. Kod yazma. Önce git pull --rebase.
1. kal1, kal2 ve kal3'te "anahtarla eslesmeyen" ya da "UYDURMA OLABILIR" bulguların her biri için
   farka ve dokümana bakıp yaz: gerçek sorun / yanlış / emin değilim (tek satır gerekçe).
2. Her rol için geçti / geçmedi yaz.
3. docs/04-FAZLAR.md → "Qwen rolleri" tablosunda geçen rolü ✅, geçmeyeni ⛔ yap; tablonun altına
   tek satır sonuç ekle (tarih, kesin x/4, kaynaksız y/5, uydurma z, süre). Commit ("Plan: Qwen
   kalibrasyonu") ve push.
Hata açıklama sonucum: [geçti / geçmedi, notlar]
Bana Türkçe ve kısa yaz.
```

### 9.5 Sorun giderme

- **"Ollama'ya ulasilamadi":** Ollama uygulaması açık mı (görev çubuğunda simgesi var mı)? `-Check` ile dene.
- **Koşu çok uzun ya da "cikti siniri doldu":** Qwen çok uzun düşünmüş. Bağlamı 64k yap ya da komuta `-Think medium` ekle (model destekliyorsa; `-Check` "Dusunme secenekleri"ni gösterir).
- **"Baglam yetmiyor":** dokümanlar tek başına bağlama sığmıyor; `OLLAMA_CONTEXT_LENGTH` değerini büyüt (0.5).
- **Denemek için:** `-DryRun` modeli çağırmaz; paketi `.qwen\` klasörüne yazar ve boyutunu gösterir.
- **Bilinen tuzaklar:**
  - `ollama run` penceresine çok satırlı metin yapıştırınca her satır ayrı mesaj gider. Fark için betiği, soru için Ollama uygulamasını kullan.
  - PowerShell 5.1'de bir programa boruyla (`|`) Türkçe metin verirken `$OutputEncoding` UTF-8 değilse karakterler bozulur. Betik bu yüzden git çıktısını UTF-8 okur ve isteği bayt olarak gönderir.
  - `Get-Process *ollama* | Stop-Process` kurulum programını (OllamaSetup) da kapatır; tam adları kullan (0.5).
  - Bağlamı aşan girdi sessizce kırpılır. Betik girdiyi tahmin eder; sığmazsa farkı parçalara böler.
  - Bulut oturumunda (Claude Code, Linux) Qwen yok; ajan adımı atlar ve commit gövdesine yazar.
- **Betiği düzenleyen ajan için:** `tools/dev/*.ps1` ASCII kalır, çünkü Windows PowerShell 5.1 BOM'suz dosyayı ANSI okur ve Türkçe harfler bozulur (’ gibi tırnaklar dizeyi erken kapatır). Betikte Türkçe harf `TR 'hari{00e7}'` biçiminde yazılır; Qwen'e giden metinler `tools/dev/qwen/` altındaki UTF-8 dosyalardadır.
