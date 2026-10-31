namespace MonitorTuner.Tests.Contracts;

/// <summary>
/// Kırmızı sözleşme testi (T002): davranışı adıyla tanımlar, gövdesi <c>Assert.Fail</c>. CI yeşil kalsın diye
/// atlanır; görevi yazan <c>[Pending]</c>'i <c>[Fact]</c> yapar ve testi sahte donanımla doldurur.
/// </summary>
public sealed class PendingAttribute : FactAttribute
{
    public PendingAttribute(string task)
    {
        Task = task;
        Skip = $"Kırmızı: {task} yazacak";
    }

    public string Task { get; }
}
