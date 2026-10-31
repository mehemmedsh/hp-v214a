namespace MonitorTuner.Tests.Contracts;

/// <summary>Monitör profili sözleşmesi (02-MIMARI.md). Kırmızı testleri görevi yazan doldurur.</summary>
public class MonitorProfileReaderTests
{
    /// <summary>HP P21b G4 profili: 50, 60, 72, 74, 75, 76 modları; ölçülen tavan 76, 77 reddedildi.</summary>
    [Pending("T004")]
    public void Read_HpProfile_HasModes50To76_AndMeasuredMax76() => Assert.Fail("T004");

    /// <summary>Yazılabilir kodlarda 04, 05, 08 veya E6–FF olan profil reddedilir.</summary>
    [Pending("T004")]
    public void Read_RejectsProfile_ListingForbiddenDdcCode() => Assert.Fail("T004");
}
