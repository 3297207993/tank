//通用计时工具：只记一个“未来时刻”，冷却、节流、状态有效期都由它表达。
//按单调递增的秒数工作（例如 Time.time），调用方传同一个 now 即可。

using System;

//到点要自动重新排期的场合（冷却/限流）用 TryPass；只是“某状态有效到什么时候”用 Set + HasTimeLeft。
public struct TimeGate
{
    float deadline;

    //已到点：now 不早于 deadline
    public bool IsDue(float now)
    {
        return now >= deadline;
    }

    //还没到点：距离 deadline 还有剩余时间
    public bool HasTimeLeft(float now)
    {
        return now < deadline;
    }

    //把到点时刻设为 now + seconds（传 0 即立即失效）
    public void Set(float now, float seconds = 0.0f)
    {
        deadline = now + seconds;
    }

    //到点则放行，并顺延 interval 秒；未到点返回 false 且不改变状态
    public bool TryPass(float now, float interval)
    {
        if (now < deadline) return false;
        deadline = now + interval;
        return true;
    }
}

//在限定时间内保持一个值：值与到期时间绑在一起，避免分开写导致的不同步
public struct Timed<T>
{
    TimeGate gate;
    T value;

    //还在有效期内就取出保持的值，否则返回 false 并给出 default
    public bool TryGet(float now, out T current)
    {
        if (gate.HasTimeLeft(now))
        {
            current = value;
            return true;
        }
        current = default;
        return false;
    }

    public void Set(float now, float seconds, T newValue)
    {
        value = newValue;
        gate.Set(now, seconds);
    }

    //到点才写入；返回这次是否真的写入，方便调用方据此做后续处理
    public bool SetIfDue(float now, float seconds, T newValue)
    {
        if (!gate.IsDue(now)) return false;
        value = newValue;
        gate.Set(now, seconds);
        return true;
    }

    //回调版：只有真的需要写入时才调用取值回调，未到点不会产生计算开销
    public bool SetIfDue(float now, float seconds, Func<T> createValue)
    {
        if (!gate.IsDue(now)) return false;
        value = createValue();
        gate.Set(now, seconds);
        return true;
    }

    public void Set(float now, float seconds, Func<T> createValue)
    {
        value = createValue();
        gate.Set(now, seconds);
    }
}
