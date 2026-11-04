using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;
using MonitorTuner.Hardware.Abstractions;

namespace MonitorTuner.Hardware;

/// <summary>
/// <c>HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM\GdiIcmGammaRange</c> (T302). Okuma normal yetkiyle.
/// Uygulama yönetici olarak çalışmadığı için yazma, Windows'un <c>reg.exe</c>'si yönetici onayıyla (UAC) çalıştırılarak
/// yapılır: tek kayıt, tek komut. Kullanıcı onayı vermezse false. Eski değeri yazmadan önce Güvenlik yedekler.
/// </summary>
public sealed class WindowsGdiIcmGammaRange : IGdiIcmGammaRange
{
    private const string KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM";
    private const string ValueName = "GdiIcmGammaRange";
    private const int UserCancelled = 1223; // ERROR_CANCELLED: UAC penceresinde "Hayır"
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    public int? Read()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = root.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is int value ? value : null;
    }

    public bool Write(int? value, RestorePoint restorePoint)
    {
        ArgumentNullException.ThrowIfNull(restorePoint);
        if (restorePoint.Request.Target != WriteTarget.GdiIcmGammaRange)
            throw new ArgumentException("Geri alma noktası GdiIcmGammaRange yazması için değil.", nameof(restorePoint));
        ArgumentOutOfRangeException.ThrowIfNegative(value ?? 0, nameof(value));

        var arguments = value is { } v
            ? $"add \"HKLM\\{KeyPath}\" /v {ValueName} /t REG_DWORD /d {v} /f /reg:64"
            : $"delete \"HKLM\\{KeyPath}\" /v {ValueName} /f /reg:64";
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "reg.exe"), arguments)
        {
            UseShellExecute = true, // "runas" sadece kabuk üzerinden yönetici onayı ister
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var process = Process.Start(start);
            if (process is null || !process.WaitForExit(Timeout)) return false;
            return process.ExitCode == 0 && Read() == value;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == UserCancelled)
        {
            return false;
        }
    }
}
