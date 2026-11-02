using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FloatAgent.Abstractions;

namespace RecipeDemo.Tools;

/// <summary>
/// 猜数字游戏状态引擎（完整移植自 skills/guess-number-game/scripts/guess_game.py）。
/// 设计原则：目标数字只在工具内部的进程内存里存在，除 reveal / end 外绝不输出，
/// 模型无需也无法在上下文里携带真实答案，从根本上避免泄露。
/// 对局状态（目标数字、轮次、次数）与自动猜测状态（可行范围、历史）全部保存在进程内存中，
/// 不读写任何文件；进程退出即清空，下一次启动从「无对局」开始。
/// 同一会话同一时刻只维护一个当前对局，不支持并行多局。
/// 所有工具返回值统一为两行：第一行「状态：…」对应原脚本的退出码分支
/// （继续=0 / 猜中=10 / 输入无效=11 / 无对局=12 / 用法错误=2 / 已结束=end 的正常收尾），
/// 第二行起为与脚本 stdout 逐字一致的文案。
/// </summary>
public sealed partial class GuessGameTools
{
    private const string MsgSmall = "你输入的数字小了，请再输入一个数字";
    private const string MsgBig = "你输入的数字大了，请再输入一个数字";
    private const string MsgWin = "你答对了";
    private const string MsgInvalid = "输入无效：请输入一个整数数字（例如 42）。";
    private const string MsgNoGame = "当前没有进行中的对局，请先开始一轮游戏。";
    private const string MsgRoundOver = "本轮已经结束，请先开始新一轮游戏。";
    private const string MsgTooMany = "一次只能猜一个数字，请输入一个数字：";

    /// <summary>状态读写互斥（工具调用可能并发到达）。</summary>
    private static readonly object Gate = new();

    /// <summary>当前对局状态（进程内存，不落盘）；null 表示无对局。</summary>
    private static GameState? CurrentGame;

    /// <summary>当前自动猜测状态（进程内存，不落盘）：可行范围与猜测历史；null 表示无自动对局。</summary>
    private static AutoState? CurrentAuto;

    /// <summary>界面进度广播（可选）：自动猜测把每轮的数字与判定推到“正在输入”气泡的状态行。</summary>
    private readonly ToolProgressService? _progress;

    public GuessGameTools(ToolProgressService? progress = null) => _progress = progress;

    [GeneratedRegex(@"[+-]?\d+(?:\.\d+)?")]
    private static partial Regex NumTokenRegex();

    // ---------------------------------------------------------------- tools

    public string StartGame(
        [Description("范围下界（含），未指定默认 1")]
        int? min,
        [Description("范围上界（含），未指定默认 100")]
        int? max)
    {
        lock (Gate)
        {
            var lo = min ?? 1;
            var hi = max ?? 100;
            if (lo > hi)
            {
                (lo, hi) = (hi, lo);
            }

            if (lo == hi)
            {
                return Result("用法错误", "用法错误：区间至少需要包含两个不同的整数。");
            }

            var roundNo = CurrentGame is null ? 1 : CurrentGame.Round + 1;

            var target = RandomNumberGenerator.GetInt32(lo, hi + 1);
            var state = new GameState
            {
                Target = target,
                Min = lo,
                Max = hi,
                Round = roundNo,
                Attempts = 0,
                Status = "playing",
            };
            CurrentGame = state;
            CurrentAuto = null;

            return Result("继续",
                $"第 {roundNo} 轮游戏开始，我已想好一个 {lo} 到 {hi} 之间的整数。\n请输入一个数字：");
        }
    }

    public string Guess(
        [Description("用户这条消息的原文，工具会自动抽取其中唯一的整数（支持全角数字、千分位与自然语言夹带）")]
        string? input)
    {
        lock (Gate)
        {
            var state = CurrentGame;
            if (state is null)
            {
                return Result("无对局", MsgNoGame);
            }

            if (state.Status != "playing")
            {
                return Result("无对局", MsgRoundOver);
            }

            var (guessValue, error) = ParseGuess(input);
            if (error is not null)
            {
                return Result("输入无效", error);
            }

            var (status, _, message) = JudgeGuess(state, guessValue!.Value);
            return Result(status, message);
        }
    }

    /// <summary>
    /// 自动猜测：由策略按上次反馈自动决定并提交本次猜测，替代人工输入。
    /// 策略（<see cref="GuessGameAutoStrategy"/>）只依据自身的可行范围与判定反馈工作，
    /// 判定仍走与 guess_game_guess 完全相同的路径——次数统计、文案、胜负判定原样不变。
    /// </summary>
    public string AutoGuess()
    {
        lock (Gate)
        {
            var state = CurrentGame;
            if (state is null)
            {
                return Result("无对局", MsgNoGame);
            }

            if (state.Status != "playing")
            {
                return Result("无对局", MsgRoundOver);
            }

            // 自动对局状态：换轮（轮次号变化）即重置可行范围为本局公布范围
            var auto = CurrentAuto;
            if (auto is null || auto.Round != state.Round)
            {
                auto = new AutoState { Round = state.Round, Low = state.Min, High = state.Max };
            }

            var rangeLow = auto.Low;
            var rangeHigh = auto.High;
            var guess = GuessGameAutoStrategy.NextGuess(rangeLow, rangeHigh);
            var (status, feedback, message) = JudgeGuess(state, guess);
            var label = feedback switch
            {
                Feedback.Small => "小了",
                Feedback.Big => "大了",
                Feedback.Win => "猜中",
                _ => "输入无效",
            };
            auto.History.Add($"{guess}（{label}）");

            if (feedback == Feedback.Win)
            {
                CurrentAuto = null;
                Report($"🎉 猜中了！第 {auto.History.Count} 次自动猜 {guess} 命中" +
                       $"｜历史：{string.Join("→", auto.History)}");
                return Result("猜中",
                    $"自动猜测：{guess} → 判定：猜中\n" +
                    $"{message}\n" +
                    $"本局自动猜测完成，共自动猜 {auto.History.Count} 次，" +
                    $"历史：{string.Join("→", auto.History)}（游戏累计已猜 {state.Attempts} 次，与 guess_game_status 一致）\n" +
                    "按原流程询问用户是否开始下一轮游戏。");
            }

            (auto.Low, auto.High) = GuessGameAutoStrategy.Adjust(
                rangeLow, rangeHigh, guess, tooSmall: feedback == Feedback.Small);
            CurrentAuto = auto;
            Report($"🎯 自动猜第 {auto.History.Count} 次：{guess} → 判定：{label}" +
                   $"（下一轮范围 {auto.Low}-{auto.High}）｜历史：{string.Join("→", auto.History)}");
            return Result("继续",
                $"自动猜测：{guess} → 判定：{label}（在可行范围 {rangeLow}-{rangeHigh} 内取中点）\n" +
                $"{message}\n" +
                $"当前可行范围：{auto.Low}-{auto.High}，本局自动历史：{string.Join("→", auto.History)}，" +
                $"游戏累计已猜 {state.Attempts} 次\n" +
                "继续调用 guess_game_auto 进行下一次自动猜测，直到状态为「猜中」。");
        }
    }

    public string CheckStatus()
    {
        lock (Gate)
        {
            var state = CurrentGame;
            if (state is null)
            {
                return Result("无对局", "没有进行中的对局。");
            }

            return state.Status == "won"
                ? Result("无对局", $"第 {state.Round} 轮已结束：共猜 {state.Attempts} 次，已猜中。")
                : Result("继续", $"第 {state.Round} 轮进行中：范围 {state.Min}-{state.Max}，已猜 {state.Attempts} 次。");
        }
    }

    public string Reveal()
    {
        lock (Gate)
        {
            var state = CurrentGame;
            if (state is null)
            {
                return Result("无对局", "没有可揭示的数字，当前没有对局。");
            }

            // 仅在本轮已结束时允许揭示，防止提前泄露答案
            return state.Status != "won"
                ? Result("无对局", "本轮尚未结束，不能揭示目标数字。")
                : Result("继续", $"本轮的目标数字是 {state.Target}。");
        }
    }

    public string EndGame()
    {
        lock (Gate)
        {
            var state = CurrentGame;
            if (state is null)
            {
                return Result("已结束", "游戏已结束。");
            }

            var (target, roundNo, attempts, won) =
                (state.Target, state.Round, state.Attempts, state.Status == "won");
            CurrentGame = null;
            CurrentAuto = null;
            return Result("已结束",
                $"游戏已结束，第 {roundNo} 轮的目标数字是 {target}（共猜 {attempts} 次，{(won ? "已猜中" : "未猜中")}）。");
        }
    }

    // ------------------------------------------------------------- internals

    /// <summary>判定反馈（供自动策略收缩范围用）。</summary>
    private enum Feedback { Invalid, Small, Big, Win }

    /// <summary>
    /// 共用判定路径：guess_game_guess 与 guess_game_auto 都经由本方法判定，
    /// 越界不计次、命中置 won、文案与计数行为完全一致——保证自动模式不改变任何游戏规则。
    /// 返回（状态标签, 反馈, 文案）。
    /// </summary>
    private (string Status, Feedback Feedback, string Message) JudgeGuess(GameState state, int guess)
    {
        if (guess < state.Min || guess > state.Max)
        {
            return ("输入无效", Feedback.Invalid, $"输入无效：请输入 {state.Min} 到 {state.Max} 之间的整数。");
        }

        state.Attempts++;
        if (guess < state.Target)
        {
            CurrentGame = state;
            return ("继续", Feedback.Small, MsgSmall);
        }

        if (guess > state.Target)
        {
            CurrentGame = state;
            return ("继续", Feedback.Big, MsgBig);
        }

        state.Status = "won";
        CurrentGame = state;
        return ("猜中", Feedback.Win, MsgWin);
    }

    private static string Result(string status, string message) => $"状态：{status}\n{message}";

    /// <summary>向聊天界面广播一条进度（无订阅者时空操作）。</summary>
    private void Report(string message) => _progress?.Report(message);

    /// <summary>全角转半角（NFKC）、去首尾空白，并去除千分位分隔符。</summary>
    private static string Normalize(string? raw) =>
        (raw ?? string.Empty).Normalize(NormalizationForm.FormKC).Trim()
            .Replace(",", string.Empty).Replace("，", string.Empty);

    /// <summary>严格解析整数，失败返回 null。溢出时按符号取极值（与脚本的大整数语义一致，必然越界）。</summary>
    private static int? AsInt(string? raw)
    {
        var text = Normalize(raw);
        if (text.Length == 0)
        {
            return null;
        }

        var body = text[0] is '+' or '-' ? text[1..] : text;
        if (body.Length == 0 || !body.All(char.IsAsciiDigit))
        {
            return null;
        }

        try
        {
            return int.Parse(text, CultureInfo.InvariantCulture);
        }
        catch (OverflowException)
        {
            return text[0] == '-' ? int.MinValue : int.MaxValue;
        }
    }

    /// <summary>返回 (整数, 错误文案)。两者恰有一个为 null。</summary>
    private static (int? Value, string? Error) ParseGuess(string? raw)
    {
        var direct = AsInt(raw);
        if (direct is not null)
        {
            return (direct, null);
        }

        var text = Normalize(raw);
        var tokens = NumTokenRegex().Matches(text).Select(static m => m.Value).ToList();
        if (tokens.Count > 1)
        {
            return (null, MsgTooMany);
        }

        if (tokens.Count == 1)
        {
            var value = AsInt(tokens[0]);
            if (value is not null)
            {
                return (value, null);
            }
        }

        return (null, MsgInvalid);
    }

    /// <summary>自动对局状态（进程内存）：只含轮次号、可行范围与猜测历史——没有目标数字，泄露面为零。</summary>
    private sealed class AutoState
    {
        public int Round { get; set; }

        public int Low { get; set; }

        public int High { get; set; }

        public List<string> History { get; set; } = [];
    }

    /// <summary>对局状态（进程内存）：目标数字只存在于这里，不会出现在任何返回值或文件中。</summary>
    private sealed class GameState
    {
        public int Target { get; set; }

        public int Min { get; set; }

        public int Max { get; set; }

        public int Round { get; set; }

        public int Attempts { get; set; }

        public string Status { get; set; } = "playing";
    }
}
