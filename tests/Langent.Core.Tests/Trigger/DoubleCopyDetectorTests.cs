using Langent.Core.Trigger;
using Xunit;

namespace Langent.Core.Tests.Trigger;

public sealed class DoubleCopyDetectorTests
{
    private static DoubleCopyDetector Create() => new(windowMs: 400, burstMergeMs: 80);

    [Fact]
    public void Two_ctrl_copies_150ms_apart_trigger()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));
        Assert.True(d.OnClipboardUpdate(150, ctrlDown: true));
    }

    [Fact]
    public void Two_ctrl_copies_450ms_apart_do_not_trigger()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));
        Assert.False(d.OnClipboardUpdate(450, ctrlDown: true));
    }

    [Fact]
    public void Office_burst_of_three_updates_within_20ms_does_not_trigger()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));
        Assert.False(d.OnClipboardUpdate(10, ctrlDown: true));
        Assert.False(d.OnClipboardUpdate(20, ctrlDown: true));
    }

    [Fact]
    public void Burst_plus_second_copy_after_200ms_triggers()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));
        Assert.False(d.OnClipboardUpdate(10, ctrlDown: true));
        Assert.False(d.OnClipboardUpdate(20, ctrlDown: true));
        Assert.True(d.OnClipboardUpdate(220, ctrlDown: true));
    }

    [Fact]
    public void Second_copy_without_ctrl_does_not_trigger()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));
        Assert.False(d.OnClipboardUpdate(150, ctrlDown: false));
    }

    [Fact]
    public void First_copy_without_ctrl_does_not_trigger()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: false));
        Assert.False(d.OnClipboardUpdate(150, ctrlDown: true));
    }

    [Fact]
    public void Third_operation_right_after_a_trigger_does_not_re_trigger()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));
        Assert.True(d.OnClipboardUpdate(150, ctrlDown: true));

        // A single further copy operation must not fire again.
        Assert.False(d.OnClipboardUpdate(250, ctrlDown: true));
    }

    [Fact]
    public void Update_inside_suppression_window_is_ignored()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));

        d.Suppress(untilTsMs: 1000);

        // Without suppression 0 -> 100 (both ctrl, <=400) would trigger;
        // being ignored proves suppression leaves state untouched.
        Assert.False(d.OnClipboardUpdate(100, ctrlDown: true));
    }

    [Fact]
    public void Disabled_detector_never_triggers()
    {
        var d = Create();
        d.Enabled = false;
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));
        Assert.False(d.OnClipboardUpdate(150, ctrlDown: true));
    }

    [Fact]
    public void A_fresh_pair_after_a_trigger_can_trigger_again()
    {
        var d = Create();
        Assert.False(d.OnClipboardUpdate(0, ctrlDown: true));
        Assert.True(d.OnClipboardUpdate(150, ctrlDown: true));

        // New double-copy after the reset.
        Assert.False(d.OnClipboardUpdate(300, ctrlDown: true));
        Assert.True(d.OnClipboardUpdate(420, ctrlDown: true));
    }
}
