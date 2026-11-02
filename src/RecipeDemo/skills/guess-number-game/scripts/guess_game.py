#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""猜数字游戏状态引擎。

设计原则：目标数字只在脚本内部与状态文件中存在，除 reveal 外绝不输出，
因此主模型无需、也无法在上下文里携带真实答案，从根本上避免泄露。

用法:
    python guess_game.py start [--min 1] [--max 100] [--slot default]
    python guess_game.py guess <输入原文> [--slot default]
    python guess_game.py status [--slot default]
    python guess_game.py reveal [--slot default]
    python guess_game.py end [--slot default]

退出码:
    0   正常（提示已输出，游戏继续）
    10  猜中（提示已输出）
    11  输入非法/越界（提示已输出，且不计入猜测次数）
    12  当前没有进行中的对局 / 本轮已结束
    2   命令用法错误
"""

import json
import os
import random
import re
import sys
import tempfile
import unicodedata

STATE_VERSION = 1

MSG_SMALL = "你输入的数字小了，请再输入一个数字"
MSG_BIG = "你输入的数字大了，请再输入一个数字"
MSG_WIN = "你答对了"
MSG_INVALID = "输入无效：请输入一个整数数字（例如 42）。"
MSG_OUT_OF_RANGE = "输入无效：请输入 {lo} 到 {hi} 之间的整数。"
MSG_NO_GAME = "当前没有进行中的对局，请先开始一轮游戏。"
MSG_ROUND_OVER = "本轮已经结束，请先开始新一轮游戏。"
MSG_TOO_MANY = "一次只能猜一个数字，请输入一个数字："


def state_path(slot="default"):
    base = os.path.join(tempfile.gettempdir(), "guess-number-game")
    os.makedirs(base, exist_ok=True)
    safe = "".join(c for c in str(slot) if c.isalnum() or c in "-_") or "default"
    return os.path.join(base, f"{safe}.json")


def load(slot):
    path = state_path(slot)
    if not os.path.exists(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        if data.get("version") != STATE_VERSION:
            return None
        return data
    except (OSError, ValueError):
        return None


def save(state, slot):
    path = state_path(slot)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(state, f, ensure_ascii=False)


def drop(slot):
    path = state_path(slot)
    if os.path.exists(path):
        try:
            os.remove(path)
        except OSError:
            pass


def normalize(raw):
    """全角转半角、去首尾空白，返回字符串。"""
    return unicodedata.normalize("NFKC", str(raw)).strip()


def as_int(raw):
    """严格解析整数，失败返回 None。"""
    text = normalize(raw).replace(",", "").replace("，", "")
    if not text:
        return None
    body = text[1:] if text[0] in "+-" else text
    if not body.isdigit():
        return None
    try:
        return int(text)
    except ValueError:
        return None


NUM_TOKEN = re.compile(r"[+-]?\d+(?:\.\d+)?")


def extract_numbers(raw):
    """从自然语言中抽取数字片段（含小数）。"""
    text = normalize(raw).replace(",", "").replace("，", "")
    return NUM_TOKEN.findall(text)


def parse_guess(raw):
    """返回 (整数, 错误文案)。两者恰有一个为 None。"""
    direct = as_int(raw)
    if direct is not None:
        return direct, None
    tokens = extract_numbers(raw)
    if len(tokens) > 1:
        return None, MSG_TOO_MANY
    if len(tokens) == 1:
        value = as_int(tokens[0])
        if value is not None:
            return value, None
    return None, MSG_INVALID


def parse_opts(argv):
    opts = {"min": 1, "max": 100, "slot": "default", "args": []}
    i = 0
    while i < len(argv):
        item = argv[i]
        if item == "--min" and i + 1 < len(argv):
            opts["min"] = argv[i + 1]
            i += 2
        elif item == "--max" and i + 1 < len(argv):
            opts["max"] = argv[i + 1]
            i += 2
        elif item == "--slot" and i + 1 < len(argv):
            opts["slot"] = argv[i + 1]
            i += 2
        else:
            opts["args"].append(item)
            i += 1
    return opts


def cmd_start(opts):
    lo = as_int(opts["min"])
    hi = as_int(opts["max"])
    if lo is None or hi is None:
        print("用法错误：--min 与 --max 必须是整数。")
        return 2
    if lo > hi:
        lo, hi = hi, lo
    if lo == hi:
        print("用法错误：区间至少需要包含两个不同的整数。")
        return 2

    prev = load(opts["slot"])
    round_no = (prev.get("round", 0) + 1) if prev else 1

    target = random.SystemRandom().randint(lo, hi)
    save(
        {
            "version": STATE_VERSION,
            "target": target,
            "min": lo,
            "max": hi,
            "round": round_no,
            "attempts": 0,
            "status": "playing",
        },
        opts["slot"],
    )
    print(f"第 {round_no} 轮游戏开始，我已想好一个 {lo} 到 {hi} 之间的整数。")
    print("请输入一个数字：")
    return 0


def cmd_guess(opts):
    state = load(opts["slot"])
    if not state:
        print(MSG_NO_GAME)
        return 12
    if state.get("status") != "playing":
        print(MSG_ROUND_OVER)
        return 12

    raw = " ".join(opts["args"])
    guess, error = parse_guess(raw)
    if error is not None:
        print(error)
        return 11

    lo, hi = state["min"], state["max"]
    if guess < lo or guess > hi:
        print(MSG_OUT_OF_RANGE.format(lo=lo, hi=hi))
        return 11

    state["attempts"] = state.get("attempts", 0) + 1
    target = state["target"]

    if guess < target:
        save(state, opts["slot"])
        print(MSG_SMALL)
        return 0
    if guess > target:
        save(state, opts["slot"])
        print(MSG_BIG)
        return 0

    state["status"] = "won"
    save(state, opts["slot"])
    print(MSG_WIN)
    return 10


def cmd_status(opts):
    state = load(opts["slot"])
    if not state:
        print("没有进行中的对局。")
        return 12
    if state.get("status") == "won":
        print(f"第 {state['round']} 轮已结束：共猜 {state['attempts']} 次，已猜中。")
        return 12
    print(
        f"第 {state['round']} 轮进行中：范围 {state['min']}-{state['max']}，"
        f"已猜 {state['attempts']} 次。"
    )
    return 0


def cmd_reveal(opts):
    state = load(opts["slot"])
    if not state:
        print("没有可揭示的数字，当前没有对局。")
        return 12
    if state.get("status") != "won":
        # 仅在本轮已结束时允许揭示，防止提前泄露答案
        print("本轮尚未结束，不能揭示目标数字。")
        return 12
    print(f"本轮的目标数字是 {state['target']}。")
    return 0


def cmd_end(opts):
    state = load(opts["slot"])
    if state:
        target = state.get("target")
        round_no = state.get("round")
        attempts = state.get("attempts", 0)
        won = state.get("status") == "won"
    else:
        target = round_no = attempts = None
        won = False
    drop(opts["slot"])
    if target is None:
        print("游戏已结束。")
    else:
        print(
            f"游戏已结束，第 {round_no} 轮的目标数字是 {target}"
            f"（共猜 {attempts} 次，{'已猜中' if won else '未猜中'}）。"
        )
    return 0


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    command = argv[1].lower()
    opts = parse_opts(argv[2:])
    handlers = {
        "start": cmd_start,
        "new": cmd_start,
        "guess": cmd_guess,
        "status": cmd_status,
        "reveal": cmd_reveal,
        "end": cmd_end,
        "quit": cmd_end,
    }
    handler = handlers.get(command)
    if not handler:
        print(f"未知命令：{command}")
        print(__doc__)
        return 2
    return handler(opts)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
