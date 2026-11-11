using NUnit.Framework;

/// <summary>
/// Timed&lt;T&gt; 把值和一个到期时间绑在一起，AI 用它表达“躲避方向有效 0.3 秒”这类状态。
/// </summary>
public class TimedTests
{
    [Test]
    public void TryGet_BeforeSet_ReturnsFalse()
    {
        var timed = new Timed<int>();

        Assert.IsFalse(timed.TryGet(0f, out int value));
        Assert.AreEqual(0, value);
    }

    [Test]
    public void TryGet_ReturnsValueUntilDeadline()
    {
        var timed = new Timed<string>();
        timed.Set(2f, 3f, "hello");

        Assert.IsTrue(timed.TryGet(4.9f, out string value));
        Assert.AreEqual("hello", value);

        Assert.IsFalse(timed.TryGet(5f, out value));
        Assert.IsNull(value);
    }

    [Test]
    public void Set_ReplacesValueAndDeadline()
    {
        var timed = new Timed<int>();
        timed.Set(0f, 10f, 1);
        timed.Set(1f, 2f, 2);

        Assert.IsTrue(timed.TryGet(2.9f, out int value));
        Assert.AreEqual(2, value);
        Assert.IsFalse(timed.TryGet(3f, out _));
    }

    [Test]
    public void SetIfDue_WritesOnlyWhenGateIsOpen()
    {
        var timed = new Timed<int>();

        Assert.IsTrue(timed.SetIfDue(0f, 5f, 1));
        Assert.IsFalse(timed.SetIfDue(1f, 5f, 2));

        Assert.IsTrue(timed.TryGet(1f, out int value));
        Assert.AreEqual(1, value);

        Assert.IsTrue(timed.SetIfDue(5f, 5f, 3));
        Assert.IsTrue(timed.TryGet(5f, out value));
        Assert.AreEqual(3, value);
    }

    [Test]
    public void SetIfDue_FactoryOverload_DoesNotComputeWhenBlocked()
    {
        var timed = new Timed<int>();
        int calls = 0;

        Assert.IsTrue(timed.SetIfDue(0f, 5f, () =>
        {
            calls++;
            return 7;
        }));
        Assert.IsFalse(timed.SetIfDue(1f, 5f, () =>
        {
            calls++;
            return 8;
        }));

        Assert.AreEqual(1, calls);
        Assert.IsTrue(timed.TryGet(1f, out int value));
        Assert.AreEqual(7, value);
    }

    [Test]
    public void Set_FactoryOverload_AlwaysWrites()
    {
        var timed = new Timed<int>();
        timed.Set(0f, 100f, () => 1);
        timed.Set(0f, 100f, () => 2);

        Assert.IsTrue(timed.TryGet(0f, out int value));
        Assert.AreEqual(2, value);
    }
}
