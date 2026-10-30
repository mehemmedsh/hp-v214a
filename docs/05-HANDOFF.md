# Handoff

Hazır metinler ve kurulum. Antigravity ve Claude Code ajanları `AGENTS.md`'yi otomatik okur. Kurallar, okuma haritası ve commit biçimi orada olduğu için buradaki metinler kısa. Her metinde `T___` yerine görev numarasını yaz.

**İlk görev:** T001 → `hp-opus` penceresi, model Opus, bölüm 2'deki metin. Teknoloji kararları zaten onaylı (`02-MIMARI.md`); ajan soru sormadan başlayabilir.

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

`gh auth login` soruları: GitHub.com → HTTPS → Git için de giriş: Yes → "Login with a web browser". Ekrandaki kodu tarayıcıda gir. Bu, ajanların işlerini GitHub'a göndermesini (push) ve CI sonucunu okumasını sağlar.

### 0.2 Klasörler (hatlar)

```powershell
mkdir C:\dev
cd C:\dev
git clone https://github.com/mehemmedsh/hp-v214a hp-opus
git clone https://github.com/mehemmedsh/hp-v214a hp-gemini
git clone https://github.com/mehemmedsh/hp-v214a hp-inceleme
```

Dördüncü hat (`hp-gemini2`) sadece Dalga 4 ve 5'te, alışınca gerekir: aynı komut, sonda `hp-gemini2`.

Her klasör projenin ayrı bir kopyasıdır. Ajan işini GitHub'a gönderir (push), diğer klasörler oradan çeker (pull). Böylece aynı anda çalışan iki ajan birbirinin dosyasını bozmaz.

### 0.3 Antigravity

- **Pencere ve klasör:** File → Open Folder → örneğin `C:\dev\hp-opus`. Her hat ayrı pencerede: File → New Window, sonra Open Folder.
- **Ajan paneli:** Sohbet panelinde yeni görev = yeni konuşma. Bir konuşmada tek görev; bitince yeni konuşma aç (uzayan konuşma kotayı hızlı bitirir).
- **Model ve düşünme seviyesi:** Sohbet kutusunun altındaki model seçicide. Düşünme seviyesi (Low / Medium / High) de orada. Claude modellerinde adında "(Thinking)" olan sürümü seç. Kısa kural: kod işi High, sadece metin işi Medium. Hangi işte hangisi: `04-FAZLAR.md` → "Araçlar, modeller ve kota sırası" ve görev tablosundaki "Yazan → İnceleyen".
- **Terminal onayı (önemli):** Ayarlarda terminal komutlarının otomatik çalışmasını kapat; her komut senin onayını istesin. `git`, `gh`, `dotnet build/test`, `tools/dev` komutlarını onayla. Uygulamayı çalıştıran (`dotnet run`, `.exe`), kayıt defterine yazan (`reg`, `Set-ItemProperty`) ya da program kuran komutları onaylama; önce bölüm 7'deki metinle sor.
- **Plan modu:** Konuşma modu seçeneği varsa görevlerde "Planning"i seç. Ajan önce plan çıkarır, sen okuyup onaylarsın, sonra yazar.
- **Kota:** Sohbet kutusuna `/usage` yaz; Gemini ve Claude havuzları ayrı görünür.
- Bir ayarın adı sende farklıysa ya da bulamıyorsan bölüm 7'deki metinle sor.

### 0.4 Claude Code (yedek)

Claude Code bulut ortamının kurulum betiğine (setup script) şu satırı ekle. Böylece Claude Code oturumları da kodu derler ve testleri çalıştırır:

```bash
apt-get update && apt-get install -y dotnet-sdk-10.0
```

## 1. Görev döngüsü

1. `docs/04-FAZLAR.md`: ilk satır (sıradaki görev) ve "Dalgalar" tablosu (hangi hat). Model: görevin "Yazan → İnceleyen" sütunu.
2. **Yazma:** Görevin hattının penceresinde yeni konuşma → modeli ve düşünme seviyesini seç → bölüm 2.
3. Ajan bitince özetini oku. Commit ve push etmiş olmalı. 🔧 görevse test adımlarını not et.
4. **İnceleme:** `hp-inceleme` penceresinde yeni konuşma → inceleyen modeli seç → bölüm 3.
5. **ONAY** ise görev biter; ajan durumu ✅ (🔧 görevde 🔧) yapar. **DÜZELT** ise: yazanın penceresinde yeni konuşma, yazan model, bölüm 4. Sonra `hp-inceleme`'de yeniden inceleme (bölüm 4).
6. 🔧 görevde monitör testini yap ve sonucu bildir (bölüm 5).
7. Aynı dalgadaki diğer görevler için 2–6'yı diğer hatta paralel yürüt. Dalganın hepsi onaylanınca sonraki dalgaya geç.

## 2. Yazma

```text
Görev: T___. AGENTS.md'deki okuma haritasına göre gerekenleri oku; docs/04-FAZLAR.md'deki
tanım, bağımlılıklar ve "Bitti sayılır" kriterine göre yap. Sadece bu görev.
Önce git pull --rebase. Bağımlı görevlerin hepsi ✅ veya 🔧 değilse başlama, bana söyle.
Önce testleri yaz, sonra kodu. Uygulamayı ve donanıma yazan kodu çalıştırma.
Bitince: derle, testleri çalıştır, docs/04-FAZLAR.md'de durumu 🟨 yap, commit ("T___: ...") ve push.
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

**Düzeltme** (yazanın penceresi, yazan model):

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
Düzeltmenin yol açtığı yeni bir sorun gördüysen ekle. Derle ve testleri çalıştır.
ONAY ise docs/04-FAZLAR.md'de durumu ✅ yap (🔧 görevse 🔧), commit ve push; değilse DÜZELT listesi ver.

[DÜZELT listesi]
```

## 5. 🔧 Monitör testi

**Test paketini açma:** T001 bu bölümü doldurur (CI'daki Windows paketini indirme ve PC'de yerel derlemeyle açma).

**Monitörde kalan kontroller** (kod beklemez):
- 75 Hz (2080×1100) ve 76 Hz (2080×1092): Windows aktif sinyal, DDC AE = 7500 / 7600 ve testufo kare atlama. **T209'dan önce.**
- DDC 14 kodunun eşlemesi: OSD'de her renk ön ayarını sırayla seç, ControlMyMonitor'da 14'ün değerini oku (12–15 ve tahmin edilen 2, 4, 8, 11). **T004'ten önce.**

**Sonucu bildirme** (`hp-inceleme`, Flash · Medium yeterli):

```text
T___ 🔧 testini monitörde yaptım. Sonuç: [geçti / geçmedi]. Notlarım: [...]
Önce git pull --rebase. Geçtiyse docs/04-FAZLAR.md'de durumu ✅ yap.
Geçmediyse LESSONS.md'ye not düş, durumu 🟨 yap ve yazan modelin düzelteceği maddeleri
DÜZELT listesi biçiminde ver. Commit ve push et. Bana Türkçe ve kısa yaz.
```

## 6. Kota bitince

- **Antigravity'nin Claude havuzu (Opus, Sonnet) bitti:** Sıradaki Opus/Sonnet işini Claude Code'da yap. claude.ai/code → bu repoyla (`mehemmedsh/hp-v214a`) yeni oturum → Opus ya da Sonnet (effort high) → bölüm 2, 3 veya 4'teki metnin aynısı. Claude Code, `CLAUDE.md` üzerinden `AGENTS.md`'yi okur.
- **Gemini havuzu bitti:** Gemini işleri bekler; Opus hattı o arada sürebilir.
- **Görevin ortasında bitti:** Modeli değiştirme. Havuz yenilenince aynı konuşmaya "devam et" yaz.

## 7. Takıldığında yardım (Claude, yeni sohbet)

claude.ai/code'da bu repoyla yeni bir oturum aç ve yapıştır:

```text
hp-v214a projesindeyim; Antigravity'de çalışıyorum ve Antigravity'ye yeni başladım.
AGENTS.md ve docs/05-HANDOFF.md'nin gerekli bölümlerini oku. Kod yazma; ne yapmam gerektiğini
Türkçe, kısa ve adım adım anlat. Gerekirse Antigravity'ye yapıştıracağım metni yaz.
Şu an: Dalga __, görev T___, pencere hp-____, model ____.
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
