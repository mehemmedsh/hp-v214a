T311 incelemesi: DÜZELT (kalibrasyon için 095b5d3 ve 99d44ba düzeltmelerinden yeniden yazıldı)

1. `src/MonitorTuner.App/Resources/Strings/Guide.resx` → `Hz76Details`: dokümanda 76 Hz için polarite yok; "+/+" kaldırılsın. 76 Hz `00-DONANIM.md`'de 🟨 (AE ve kare atlama doğrulaması bekliyor): metne "kullanmadan önce kare atlama testiyle doğrulayın" notu eklensin.
2. `Guide.resx` → `Hz75Details`: "25/50 fps Video" ifadesi kaldırılsın; 75 Hz de 🟨, aynı doğrulama notu eklensin.
3. `Guide.resx` → `FrameSkippingDesc`: "1/8 s" dokümanda yok, sadece 1/10 s kalsın. "8–10 kutucuk" bir ölçüt değil: kural "atlanan kare yok" olsun; 8 ardışık kare 74 Hz'deki ölçümün örneği olarak geçebilir.
4. `Guide.resx` → `RtxVsrDesc`: "Kalite 3 veya 4" kaynaksız; belirli bir seviye yazılmasın, genel ifade kullanılsın.
5. `Guide.resx` → `DldsrDesc`: "DL 2.25x veya DL 1.78x" kaynaksız; genel ifade kullanılsın.
6. `Guide.resx` → `ClearTypeDesc`: "5 adımda" kaynaksız; adım sayısı verilmesin.
7. `Guide.resx` → `Rule202020Desc`: "20 fit (yaklaşık 6 metre)" dokümanda yok; kaldırılsın.
8. `tests/MonitorTuner.Tests/Localization/GuideLocalizationTests.cs`: değerler bütün metinler birleştirilerek aranıyor; `Contains("76")`, `Contains("70")` her zaman geçer. Her Hz anahtarı ayrı kontrol edilsin; kaldırılan değerler için `DoesNotContain` eklensin.
9. `GuideLocalizationTests.cs`: ışığa duyarlılık uyarısının kare atlama ve stabilite testinden önce geldiğini (`GuideView.xaml` sırası) doğrulayan test eklensin.
10. `Guide.resx` → `Hz50Details`: "CTA-861" ve "VIC 31" teknik terimleri kaldırılsın; "EDID'de hazır 50 Hz modu" yeter.
