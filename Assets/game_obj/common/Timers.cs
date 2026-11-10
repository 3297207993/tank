//通用计时工具：只记一个“未来时刻”，冷却、节流、状态有效期都由它表达。
//按单调递增的秒数工作（例如 Time.time），调用方传同一个 now 即可。

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
    public void Set(float now, float seconds)
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
