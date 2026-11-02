namespace RecipeDemo.Tools;

/// <summary>
/// 自动猜测策略（guess_game_auto 的大脑）：纯函数，只依据「当前可行范围」决定下一个猜测数字。
/// 有意与判定引擎（<see cref="GuessGameTools"/>）分离——本类不接触任何状态文件，
/// 结构上无法获知目标数字；它对游戏的所有了解只有「上一次反馈是大了还是小了」。
/// </summary>
public static class GuessGameAutoStrategy
{
    /// <summary>
    /// 二分取中：返回 [low, high] 的中点（偏向小的一侧，防溢出写法）。
    /// low == high 时返回该值（此时它必然就是答案）。
    /// </summary>
    public static int NextGuess(int low, int high) => low + (high - low) / 2;

    /// <summary>
    /// 依据判定反馈收缩可行范围：小了 → 下界抬到 guess+1；大了 → 上界压到 guess-1。
    /// 反馈永远来自判定引擎（guess_game_guess / 判定路径），策略自身不做任何大小判断。
    /// </summary>
    public static (int Low, int High) Adjust(int low, int high, int guess, bool tooSmall) =>
        tooSmall ? (guess + 1, high) : (low, guess - 1);
}
