# Handoff

Yeni bir sohbete veya Claude Code oturumuna yapıştırılacak hazır metinler.

## 0. Kodlamadan önce

`docs/09-ILK-GUN.md`'deki kontrolleri monitörde yap ve sonuçları `LESSONS.md` → "Donanım bulguları" bölümüne yaz. T001–T003 bu kontrolden önce de yapılabilir; **T004 ve sonrası bu sonuçlara ihtiyaç duyar.**

## 1. İlk görev (T001)

**Nerede:** Claude Code, `mehemmedsh/hp-v214a` · **Model:** Sonnet 5.5 · **Sonra inceleme:** Opus 5.5 (high), ayrı oturum

```text
mehemmedsh/hp-v214a reposunda çalışıyoruz. Önce şu dosyaları oku:
README.md, docs/00-DONANIM.md, docs/02-MIMARI.md, docs/03-GUVENLIK.md,
docs/04-FAZLAR.md, docs/08-GORUNTU-YONTEMLERI.md, LESSONS.md.

Görev: T001 (Faz 0). docs/04-FAZLAR.md'deki tanıma ve "Bitti sayılır" kriterine göre yap.
Kurallar:
- Sadece T001. Başka göreve geçme.
- Önce test, sonra kod. Yaklaşık 200 satır.
- 02-MIMARI.md'deki teknoloji kararlarını uygula; değiştirmen gereken bir şey varsa önce sor.
- Bitince: commit, 04-FAZLAR.md'de T001'i ✅ yap, "Sıradaki görev" satırını T002 yap.
- Sonunda bana: ne yaptın, neyi test ettin, inceleyicinin özellikle bakması gereken 3 nokta.
```

## 2. Herhangi bir görev için şablon

**Nerede:** Claude Code · **Model:** görev tablosundaki "Yazan"

```text
mehemmedsh/hp-v214a reposunda çalışıyoruz. Önce README.md, docs/00-DONANIM.md,
docs/02-MIMARI.md, docs/03-GUVENLIK.md, docs/04-FAZLAR.md, docs/08-GORUNTU-YONTEMLERI.md
ve LESSONS.md dosyalarını oku.

Görev: T___ . 04-FAZLAR.md'deki tanım, bağımlılıklar ve "Bitti sayılır" kriterine göre yap.
Bağımlı görevlerin hepsi ✅ değilse başlama, bana söyle.
Kurallar:
- Sadece bu görev. Önce test, sonra kod. Yaklaşık 200 satır.
- Donanıma veya sistem ayarına yazan her işlem Güvenlik katmanından geçer.
  03-GUVENLIK.md'deki değişmez kurallar pazarlıksız.
- GPU renk tablosuna sadece renk boru hattı yazar.
- 🔧 işaretliyse gerçek monitörde benim yapacağım testin adımlarını yaz; ben onaylamadan ✅ yapma.
- Bitince: commit, durum güncellemesi, gerekirse LESSONS.md.
- Sonunda bana: ne yaptın, neyi test ettin, inceleyicinin bakması gereken 3 nokta.
```

## 3. İnceleme şablonu

**Nerede:** Claude Code, yeni oturum · **Model:** görev tablosundaki "İnceleyen"

```text
mehemmedsh/hp-v214a reposunda T___ görevinin incelemesini yap. Kodu sen yazmadın.
Önce docs/02-MIMARI.md, docs/03-GUVENLIK.md ve 04-FAZLAR.md'deki T___ satırını oku,
sonra son commit'teki değişikliklere bak.

Kontrol et:
1. "Bitti sayılır" kriteri gerçekten sağlanıyor mu?
2. Testler gerçek davranışı mı test ediyor, yoksa sadece geçmek için mi yazılmış?
3. Donanıma veya sistem ayarına yazan bir yol Güvenlik katmanını atlıyor mu?
4. GPU renk tablosuna renk boru hattı dışında yazan bir yer var mı?
5. 03-GUVENLIK.md'deki değişmez kurallardan biri ihlal ediliyor mu
   (otomatik Hz/parlaklık, firmware yazma, 60 Hz'e dokunma, karartma tabanı, panik kısayolu)?
6. Sözleşme (T002) değişmiş mi; değiştiyse 02-MIMARI.md güncellenmiş mi?

Sonuç: ONAY veya DÜZELT listesi. Düzeltmeleri kendin yapma, listele.
```

## 4. Faz sonu değerlendirmesi

**Nerede:** Claude app · **Model:** Opus 5.5 · **Effort:** max

```text
hp-v214a projesinde Faz __ bitti. Bu fazın görev listesi, LESSONS.md ve gerçek
monitör test sonuçlarım aşağıda. Değerlendir:
- Planla gerçek sonuç arasında fark var mı?
- Sonraki fazın görevlerinde değişmesi gereken bir şey var mı?
- LESSONS.md'ye eklenmesi gereken ders var mı?

[görev listesi + test sonuçları + LESSONS.md buraya]
```
