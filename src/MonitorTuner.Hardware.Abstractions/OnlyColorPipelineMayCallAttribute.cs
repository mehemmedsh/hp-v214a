namespace MonitorTuner.Hardware.Abstractions;

/// <summary>
/// İşaretli arayüz üyesini sadece renk boru hattı (<c>MonitorTuner.Services.ColorPipeline</c>) çağırabilir.
/// GPU renk tablosuna yazan üyeler bununla işaretlenir (T002). Mimari testi, bu üyeyi ya da onu uygulayan
/// sınıf metodunu başka bir yerden çağıran kodu yakalar (docs/02-MIMARI.md → CI korumaları).
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class OnlyColorPipelineMayCallAttribute : Attribute
{
}
