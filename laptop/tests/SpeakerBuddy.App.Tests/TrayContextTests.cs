namespace SpeakerBuddy.App.Tests;

public class TrayContextTests
{
    [Fact]
    public void The_tooltip_says_waiting_without_a_timer()
    {
        Assert.Equal($"Speaker Buddy {AppVersion.Current}: waiting for the timer", TrayContext.Tooltip(null, null));
    }

    [Fact]
    public void The_tooltip_names_the_port_and_the_firmware_once_known()
    {
        Assert.Equal($"Speaker Buddy {AppVersion.Current}: connected on COM7", TrayContext.Tooltip("COM7", null));
        Assert.Equal($"Speaker Buddy {AppVersion.Current}: connected on COM7, timer v0.1.0",
            TrayContext.Tooltip("COM7", "v0.1.0"));
    }

    [Fact]
    public void The_tooltip_stays_within_the_127_characters_windows_accepts()
    {
        var text = TrayContext.Tooltip(new string('P', 100), new string('f', 40));

        Assert.Equal(127, text.Length);
    }

    [Theory]
    [InlineData("speaker-buddy.ico")]
    [InlineData("speaker-buddy-waiting.ico")]
    public void Both_tray_icons_are_embedded_in_the_notification_area_size(string name)
    {
        using var icon = TrayContext.LoadIcon(name);

        Assert.Equal(SystemInformation.SmallIconSize, icon.Size);
    }

    [Fact]
    public void A_missing_icon_fails_loudly()
    {
        Assert.Throws<InvalidOperationException>(() => TrayContext.LoadIcon("missing.ico"));
    }
}
