using System.Windows.Markup;

namespace MonitorTuner.App.Localization;

/// <summary>
/// XAML'de çeviri dosyasından metin: <c>Text="{loc:Tr Common.AppTitle}"</c>.
/// XAML'e elle metin yazılmaz; LocalizationFileTests bunu yakalar.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
