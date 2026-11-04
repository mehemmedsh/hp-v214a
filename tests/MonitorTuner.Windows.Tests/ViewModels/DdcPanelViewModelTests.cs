using System.Threading;
using System.Windows.Controls;
using MonitorTuner.App.Localization;
using MonitorTuner.App.ViewModels;
using MonitorTuner.App.Views;
using MonitorTuner.Core.Profiles;
using MonitorTuner.Services;

namespace MonitorTuner.Windows.Tests.ViewModels;

/// <summary>DDC paneli ekranı (T305): satırlar, tek yazma, OSD talimatı, dış değişiklik bildirimi, giriş uyarısı, metinler.</summary>
public class DdcPanelViewModelTests
{
    private sealed class StubPanel : IDdcPanel
    {
        public DdcPanelState State { get; set; } = new(true, [], []);
        public List<(PanelSetting Setting, int Value, bool Warned)> Sets { get; } = [];
        public Queue<PanelSetResult> Results { get; } = new();
        public int Refreshes { get; private set; }

        public DdcPanelState Refresh() { Refreshes++; return State; }

        public PanelSetResult Set(PanelSetting setting, int value, bool inputWarningAccepted = false)
        {
            Sets.Add((setting, value, inputWarningAccepted));
            return Results.Count > 0 ? Results.Dequeue() : PanelSetResult.Written;
        }
    }

    private static PanelSettingState Slider(PanelSetting s, int current, int min = 0, int max = 100, SettingLock lck = SettingLock.None) =>
        new(s, 0x10, current, min, max, max, lck, []);

    private static PanelSettingState Choice(PanelSetting s, int current, params PanelChoice[] choices) =>
        new(s, 0x14, current, 0, 15, 15, SettingLock.None, choices);

    [Fact]
    public void Refresh_BuildsOneRowPerSetting_WithLocalizedTitles()
    {
        var panel = new StubPanel
        {
            State = new(true,
            [
                Slider(PanelSetting.Brightness, 55),
                Choice(PanelSetting.ColorPreset, 5, new PanelChoice(4, "Warm", false), new PanelChoice(5, "Neutral", true)),
            ], []),
        };
        var vm = new DdcPanelViewModel(panel);

        vm.Refresh();

        Assert.True(vm.Available);
        Assert.False(vm.ShowOsdInstructions);
        Assert.Equal(2, vm.Items.Count);
        Assert.True(vm.Items[0].IsSlider);
        Assert.Equal(55, vm.Items[0].Value);
        Assert.Equal("Parlaklık (arka ışık)", vm.Items[0].Title);
        Assert.True(vm.Items[1].IsChoice);
        Assert.Equal("Neutral (nötr)", vm.Items[1].SelectedChoice!.Title);
        Assert.Contains("doğrulanmadı", vm.Items[1].ChoiceList[0].Title); // tahmin eşleme açıkça işaretli
    }

    [Fact]
    public void DraggingWritesNothing_ReleaseWritesOnce()
    {
        var panel = new StubPanel { State = new(true, [Slider(PanelSetting.Brightness, 55)], []) };
        var vm = new DdcPanelViewModel(panel);
        vm.Refresh();
        var item = vm.Items[0];

        item.Value = 60; item.Value = 63; item.Value = 64.4; // sürükleme
        Assert.Empty(panel.Sets);
        Assert.Equal("64", item.ValueText);

        vm.Commit(item);

        Assert.Equal([(PanelSetting.Brightness, 64, false)], panel.Sets);
        Assert.Equal("Ayar yazıldı.", vm.Status);
    }

    [Fact]
    public void Commit_UnchangedResult_DoesNotReplaceStatus()
    {
        var panel = new StubPanel { State = new(true, [Slider(PanelSetting.Brightness, 55)], []) };
        var vm = new DdcPanelViewModel(panel);
        vm.Refresh();
        vm.Commit(vm.Items[0]); // Written
        panel.Results.Enqueue(PanelSetResult.Unchanged);

        vm.Commit(vm.Items[0]); // aynı bırakma olayı ikinci kez

        Assert.Equal("Ayar yazıldı.", vm.Status);
    }

    [Fact]
    public void NoDdc_ShowsOsdInstructions_NoRows()
    {
        var panel = new StubPanel { State = new(false, [], []) };
        var vm = new DdcPanelViewModel(panel);

        vm.Refresh();

        Assert.False(vm.Available);
        Assert.True(vm.ShowOsdInstructions);
        Assert.Empty(vm.Items);
        Assert.Contains("Management → DDC/CI", vm.OsdText);
        Assert.Contains("Custom RGB", vm.OsdText);
    }

    [Fact]
    public void ExternalChange_IsAnnounced_WithSettingNames()
    {
        var panel = new StubPanel { State = new(true, [Slider(PanelSetting.Volume, 10)], [PanelSetting.Volume, PanelSetting.Brightness]) };
        var vm = new DdcPanelViewModel(panel);

        vm.Refresh();

        Assert.Contains("Hoparlör sesi", vm.Status);
        Assert.Contains("Parlaklık", vm.Status);
        Assert.Contains("ezilmedi", vm.Status);
    }

    [Fact]
    public void Commit_ExternallyChanged_ShowsMessage_AndReloadsValues()
    {
        var panel = new StubPanel { State = new(true, [Slider(PanelSetting.Brightness, 30)], []) };
        panel.Results.Enqueue(PanelSetResult.ExternallyChanged);
        var vm = new DdcPanelViewModel(panel);
        vm.Refresh();

        vm.Commit(vm.Items[0]);

        Assert.Contains("OSD", vm.Status);
        Assert.Equal(30, vm.Items[0].Value);
        Assert.True(panel.Refreshes >= 2);
    }

    [Fact]
    public void Input_NeedsWarning_AsksUser_AndWritesOnlyWhenAccepted()
    {
        var input = Choice(PanelSetting.Input, 17, new PanelChoice(1, "Vga", true), new PanelChoice(17, "Hdmi", true));
        var panel = new StubPanel { State = new(true, [input], []) };
        panel.Results.Enqueue(PanelSetResult.NeedsInputWarning);
        var vm = new DdcPanelViewModel(panel) { ConfirmInputSwitch = (_, _) => false };
        vm.Refresh();

        vm.Items[0].SelectedChoice = vm.Items[0].ChoiceList[0]; // VGA seçildi, kullanıcı hayır dedi

        Assert.Equal([(PanelSetting.Input, 1, false)], panel.Sets);

        panel.Sets.Clear();
        panel.Results.Enqueue(PanelSetResult.NeedsInputWarning);
        vm.ConfirmInputSwitch = (_, _) => true;
        vm.Items[0].SelectedChoice = vm.Items[0].ChoiceList[0];

        Assert.Equal([(PanelSetting.Input, 1, false), (PanelSetting.Input, 1, true)], panel.Sets);
    }

    [Fact]
    public void LockedRow_ShowsReason_AndIsNotEditable()
    {
        var panel = new StubPanel { State = new(true, [Slider(PanelSetting.BlueGain, 255, 153, 255, SettingLock.BlueLightActive)], []) };
        var vm = new DdcPanelViewModel(panel);

        vm.Refresh();

        Assert.False(vm.Items[0].IsEditable);
        Assert.True(vm.Items[0].HasLock);
        Assert.Contains("filtreyi kapat", vm.Items[0].LockText);
    }

    [Fact]
    public void AllTexts_ExistInTurkishResources()
    {
        var keys = Enum.GetNames<PanelSetting>().Select(n => "DdcPanel.Setting_" + n)
            .Concat(Enum.GetNames<SettingLock>().Where(n => n != nameof(SettingLock.None)).Select(n => "DdcPanel.Lock_" + n))
            .Concat(Enum.GetNames<PanelSetResult>().Select(n => "DdcPanel.Result_" + n))
            .Concat(Enum.GetNames<OsdColorPreset>().Select(n => "DdcPanel.Choice_" + n))
            .Concat(["DdcPanel.Choice_Hdmi", "DdcPanel.Choice_Vga", "DdcPanel.Choice_Unverified", "DdcPanel.ExternalChange",
                     "DdcPanel.InputWarning_Title", "DdcPanel.InputWarning_Text", "DdcPanel.TabTitle", "DdcPanel.PageTitle",
                     "DdcPanel.Intro", "DdcPanel.BrightnessHint", "DdcPanel.Refresh", "DdcPanel.Osd_Title", "DdcPanel.Osd_Why",
                     "DdcPanel.Osd_Brightness", "DdcPanel.Osd_Color", "DdcPanel.Osd_Image", "DdcPanel.Osd_Input", "DdcPanel.Osd_Volume"]);

        var missing = keys.Where(k => Loc.Get(k).StartsWith("[[", StringComparison.Ordinal)).ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void View_CanBeInstantiated_OnStaThread_AndBindsViewModel()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var panel = new StubPanel { State = new(true, [Slider(PanelSetting.Brightness, 55)], []) };
                var view = new DdcPanelView();
                Assert.IsAssignableFrom<UserControl>(view);

                view.DataContext = new DdcPanelViewModel(panel);

                Assert.True(panel.Refreshes >= 1); // DataContext verilince monitörden okunur
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null) throw new InvalidOperationException("STA thread hatası", error);
    }
}
