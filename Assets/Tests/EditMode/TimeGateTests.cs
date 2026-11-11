using NUnit.Framework;

/// <summary>
/// TimeGate 用“未来某个时刻”表达冷却和限流，是 AI 计时逻辑的基础。
/// </summary>
public class TimeGateTests
{
    [Test]
    public void DefaultGate_IsDueImmediately()
    {
        var gate = new TimeGate();

        Assert.IsTrue(gate.IsDue(0f));
        Assert.IsFalse(gate.HasTimeLeft(0f));
    }

    [Test]
    public void Set_KeepsGateClosedUntilDeadline()
    {
        var gate = new TimeGate();
        gate.Set(10f, 2f);

        Assert.IsFalse(gate.IsDue(11.99f));
        Assert.IsTrue(gate.HasTimeLeft(11.99f));

        Assert.IsTrue(gate.IsDue(12f));
        Assert.IsFalse(gate.HasTimeLeft(12f));
    }

    [Test]
    public void Set_WithDefaultSeconds_ExpiresImmediately()
    {
        var gate = new TimeGate();
        gate.Set(5f);

        Assert.IsTrue(gate.IsDue(5f));
    }

    [Test]
    public void Set_ReplacesPreviousDeadline()
    {
        var gate = new TimeGate();
        gate.Set(10f, 100f);
        gate.Set(20f, 1f);

        Assert.IsTrue(gate.IsDue(21f));
    }

    [Test]
    public void TryPass_PassesOnceThenWaitsForInterval()
    {
        var gate = new TimeGate();

        Assert.IsTrue(gate.TryPass(0f, 1f));
        Assert.IsFalse(gate.TryPass(0.5f, 1f));
        Assert.IsTrue(gate.TryPass(1f, 1f));
        Assert.IsFalse(gate.TryPass(1.5f, 1f));
    }

    [Test]
    public void TryPass_WhenBlocked_DoesNotChangeDeadline()
    {
        var gate = new TimeGate();
        gate.Set(10f, 5f);

        Assert.IsFalse(gate.TryPass(11f, 100f));
        Assert.IsTrue(gate.TryPass(15f, 100f));
    }
}
