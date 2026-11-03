namespace MonitorTuner.Core;

/// <summary>DDC/CI'dan okunan değer (VCP kodu: şu anki ve en büyük değer). Core'da: arayüz katmanı da görür.</summary>
public readonly record struct DdcValue(ushort Current, ushort Maximum);
