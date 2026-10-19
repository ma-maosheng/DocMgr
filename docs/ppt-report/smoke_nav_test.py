# -*- coding: utf-8 -*-
"""DocMgr 侧栏全菜单导航冒泡测试。

登录资料管理员账号，逐一点开侧栏全部叶子菜单，检测：
- 菜单按钮是否存在
- 打开后是否弹出「未处理异常」类对话框
- 进程是否仍存活

用法：python docs/ppt-report/smoke_nav_test.py
"""
from __future__ import annotations

import json
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

from pywinauto import Application, Desktop
from pywinauto.keyboard import send_keys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

ROOT = Path(__file__).resolve().parents[2]
EXE = ROOT / "bin" / "Debug" / "net8.0-windows" / "DocMgr.exe"
OUT = Path(__file__).resolve().parent / "smoke-results"
OUT.mkdir(parents=True, exist_ok=True)

LOGIN_USER = "wxl"
LOGIN_PASS = "wxl"

MAIN = "河北省第三测绘院测绘资料管理系统"
LOGIN_BTN = "登 录"
TIP_OK = "知道了"

# (checklist_item, path_headers..., target, overview_index)
# checklist_item: 对应 docs/使用冒烟清单.md 条目编号；空串表示扩展覆盖
NAV_PAGES = [
    ("", ["历史存档"], "地形图图件", -1),
    ("", ["历史存档"], "航摄胶片、像片", -1),
    ("", ["历史存档"], "其他资料", -1),
    ("10", ["历史存档", "资料台账与资料处置"], "台账", -1),
    ("10", ["历史存档", "资料台账与资料处置"], "处置", -1),
    ("", ["年度资料"], "项目信息", -1),
    ("2", ["年度资料", "建档与立档"], "建档申请", -1),
    ("2", ["年度资料", "建档与立档"], "建档审批", -1),
    ("3", ["年度资料", "建档与立档"], "资料立档", -1),
    ("", ["年度资料", "建档与立档"], "存量硬盘直办", -1),
    ("", ["年度资料", "建档与立档"], "存档文本直办", -1),
    ("", ["年度资料", "建档与立档"], "立档台账", -1),
    ("", ["年度资料", "迁档"], "模拟介质资料迁档", -1),
    ("", ["年度资料", "迁档"], "电子介质资料迁档", -1),
    ("", ["年度资料", "迁档"], "迁档台账", -1),
    ("", ["年度资料", "检索"], "资料检索(综合模式)", -1),
    ("", ["年度资料", "检索"], "电子介质资料检索", -1),
    ("", ["年度资料", "检索"], "模拟介质资料检索", -1),
    ("", ["年度资料", "检索"], "检索池", -1),
    ("4", ["年度资料", "实物流转"], "借出申请", -1),
    ("4", ["年度资料", "实物流转"], "借出审批", -1),
    ("4", ["年度资料", "实物流转"], "归还申请", -1),
    ("4", ["年度资料", "实物流转"], "归还审批", -1),
    ("", ["年度资料", "实物流转"], "流转台账", -1),
    ("7", ["年度资料", "出入网"], "入网申请", -1),
    ("7", ["年度资料", "出入网"], "入网审批", -1),
    ("8", ["年度资料", "出入网"], "出网申请", -1),
    ("8", ["年度资料", "出入网"], "出网审批", -1),
    ("", ["年度资料", "出入网"], "在网数据台账", -1),
    ("9", ["年度资料", "出入网"], "在网数据处置", -1),
    ("", ["年度资料", "出入网"], "跨域流转台账", -1),
    ("", ["年度资料", "盘库与离库"], "模拟资料盘库", -1),
    ("", ["年度资料", "盘库与离库"], "电子资料盘库", -1),
    ("5", ["年度资料", "盘库与离库"], "模拟资料离库处置", -1),
    ("5", ["年度资料", "盘库与离库"], "电子资料离库处置", -1),
    ("", ["库房与介质"], "库管资料总览", -1),
    ("11", ["库房与介质", "档案柜"], "档案柜登记", -1),
    ("11", ["库房与介质", "档案柜"], "开柜查看", -1),
    ("6", ["库房与介质", "硬盘"], "概览", 0),
    ("6", ["库房与介质", "硬盘"], "初始登记", -1),
    ("6", ["库房与介质", "硬盘"], "出库申请", -1),
    ("6", ["库房与介质", "硬盘"], "出库办理", -1),
    ("6", ["库房与介质", "硬盘"], "归还申请", 0),
    ("6", ["库房与介质", "硬盘"], "归还办理", -1),
    ("6", ["库房与介质", "硬盘"], "盘库登记", -1),
    ("6", ["库房与介质", "硬盘"], "离库处置", -1),
    ("6", ["库房与介质", "硬盘"], "硬盘台账", -1),
    ("", ["库房与介质", "光盘"], "概览", 0),
    ("", ["库房与介质", "光盘"], "光盘台账", -1),
    ("", ["系统设置"], "个人设置", -1),
    ("", ["系统设置"], "用户管理", -1),
    ("", ["系统设置"], "部门设置", -1),
    ("", ["系统设置"], "角色概览", -1),
    ("", ["系统设置"], "审核审批", -1),
    ("", ["系统设置"], "服务器路径设置", -1),
    ("", ["系统设置"], "逾期设置", -1),
    ("", ["系统设置"], "数据库操作日志", -1),
    ("", ["系统设置"], "在线用户与登录记录", -1),
    ("1c", ["系统设置"], "待办事项总览", -1),
    ("", ["系统设置"], "⚠️ 高级数据管理", -1),
    ("", ["帮助"], "操作手册", -1),
]

EXCEPTION_MARKERS = (
    "未处理异常",
    "Unhandled Exception",
    "System.Windows.Markup",
    "System.InvalidOperationException",
    "System.NullReferenceException",
    "绑定错误",
)


def find_buttons(win, title: str):
    out = []
    for ctrl in win.descendants(control_type="Button"):
        try:
            if ctrl.window_text() == title:
                out.append(ctrl)
        except Exception:
            continue
    return out


def has_button(win, title: str) -> bool:
    return bool(find_buttons(win, title))


def click_title(win, title: str, index: int = 0) -> bool:
    matches = find_buttons(win, title)
    if not matches:
        return False
    if index >= len(matches):
        index = len(matches) - 1
    ctrl = matches[index]
    try:
        ctrl.scroll_into_view()
    except Exception:
        pass
    ctrl.click_input()
    return True


def ensure_expanded(win, header: str) -> None:
    """Expand nav accordion; never toggle-click (would collapse)."""
    for ctrl in win.descendants(control_type="Group"):
        try:
            if ctrl.window_text() == header:
                try:
                    ctrl.expand()
                except Exception:
                    pass
                time.sleep(0.2)
                return
        except Exception:
            continue
    # fallback: click header button only if children not already visible later
    for ctrl in win.descendants(control_type="Button"):
        try:
            if ctrl.window_text() == header:
                try:
                    ctrl.scroll_into_view()
                except Exception:
                    pass
                ctrl.click_input()
                time.sleep(0.25)
                return
        except Exception:
            continue


def dismiss_overlay(win) -> None:
    for _ in range(8):
        if click_title(win, TIP_OK):
            time.sleep(0.25)
            continue
        clicked = False
        for w in Desktop(backend="uia").windows():
            if click_title(w, TIP_OK) or click_title(w, "确定") or click_title(w, "是"):
                clicked = True
                time.sleep(0.2)
        if not clicked:
            break


def process_alive() -> bool:
    try:
        r = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq DocMgr.exe", "/NH"],
            capture_output=True,
            text=True,
            encoding="gbk",
            errors="ignore",
        )
        return "DocMgr.exe" in (r.stdout or "")
    except Exception:
        return False


def detect_exception_dialog() -> str | None:
    for w in Desktop(backend="uia").windows():
        try:
            title = (w.window_text() or "").strip()
            if not title:
                continue
            for marker in EXCEPTION_MARKERS:
                if marker in title:
                    return title
            # MessageBox / custom error shell often has body text
            for ctrl in w.descendants(control_type="Text"):
                try:
                    text = (ctrl.window_text() or "").strip()
                except Exception:
                    continue
                for marker in EXCEPTION_MARKERS:
                    if marker in text:
                        return f"{title}: {text[:120]}"
        except Exception:
            continue
    return None


def wait_login_window(timeout: float = 120.0):
    deadline = time.time() + timeout
    while time.time() < deadline:
        for w in Desktop(backend="uia").windows():
            if has_button(w, LOGIN_BTN):
                return w
        time.sleep(0.4)
    raise TimeoutError("login window not found")


def wait_main_window(timeout: float = 90.0):
    deadline = time.time() + timeout
    while time.time() < deadline:
        for w in Desktop(backend="uia").windows():
            t = w.window_text() or ""
            if MAIN in t and w.rectangle().width() > 1000 and not has_button(w, LOGIN_BTN):
                return w
        time.sleep(0.4)
    raise TimeoutError("main window not found")


def do_login(win):
    edits = []
    try:
        edits = list(win.descendants(control_type="Edit"))
    except Exception:
        pass
    if edits:
        edits[0].set_focus()
        edits[0].type_keys("^a{BACKSPACE}" + LOGIN_USER, with_spaces=True, pause=0.02)
    pwd = None
    for ctrl in win.descendants():
        try:
            if (ctrl.element_info.automation_id or "") == "TxtPassword":
                pwd = ctrl
                break
        except Exception:
            continue
    if pwd is not None:
        pwd.set_focus()
        time.sleep(0.1)
        send_keys("^a{BACKSPACE}")
        send_keys(LOGIN_PASS, with_spaces=True, pause=0.03)
    else:
        send_keys("{TAB}")
        time.sleep(0.1)
        send_keys("^a{BACKSPACE}")
        send_keys(LOGIN_PASS, with_spaces=True, pause=0.03)
    time.sleep(0.25)
    if not click_title(win, LOGIN_BTN):
        send_keys("{ENTER}")
    time.sleep(2.0)
    dismiss_overlay(win)
    main = wait_main_window(90)
    try:
        main.maximize()
    except Exception:
        pass
    time.sleep(0.5)
    dismiss_overlay(main)
    return main


def open_path(win, path_headers: list[str]) -> None:
    for header in path_headers:
        ensure_expanded(win, header)
        time.sleep(0.15)


def click_nav_target(win, path: list[str], target: str, index: int = 0) -> bool:
    open_path(win, path)
    matches = find_buttons(win, target)
    if not matches:
        # children may be off-screen / still collapsed: re-expand leaf header
        if path:
            ensure_expanded(win, path[-1])
            # last resort: one toggle click on leaf header if still missing
            if not find_buttons(win, target):
                for ctrl in win.descendants(control_type="Button"):
                    try:
                        if ctrl.window_text() == path[-1]:
                            try:
                                ctrl.scroll_into_view()
                            except Exception:
                                pass
                            ctrl.click_input()
                            time.sleep(0.3)
                            break
                    except Exception:
                        continue
        matches = find_buttons(win, target)
    if not matches:
        return False
    if index >= len(matches):
        index = len(matches) - 1
    ctrl = matches[index]
    try:
        ctrl.scroll_into_view()
    except Exception:
        pass
    ctrl.click_input()
    return True


def main() -> int:
    print("EXE", EXE, flush=True)
    if not EXE.exists():
        print("FAIL: DocMgr.exe not found, build Debug first", flush=True)
        return 2

    subprocess.run(["taskkill", "/IM", "DocMgr.exe", "/F"], capture_output=True)
    time.sleep(1.5)
    Application(backend="uia").start(str(EXE))
    time.sleep(4.0)

    login_win = wait_login_window(120)
    print("login window ok", flush=True)
    main_win = do_login(login_win)
    print("main window ok", flush=True)
    dismiss_overlay(main_win)

    results = []
    passed = 0
    failed = 0

    # #1 登录后主窗可达
    login_result = {
        "item": "1",
        "path": "登录 → 主窗口",
        "target": "主窗口",
        "status": "PASS",
        "detail": "",
    }
    results.append(login_result)
    passed += 1
    print("PASS  #1 登录 → 主窗口", flush=True)

    for item, path, target, oidx in NAV_PAGES:
        label = " / ".join(path + [target])
        print(f"OPEN  {label}", flush=True)
        row = {
            "item": item,
            "path": " / ".join(path),
            "target": target,
            "status": "PASS",
            "detail": "",
        }
        try:
            if not process_alive():
                row["status"] = "FAIL"
                row["detail"] = "DocMgr.exe 已退出"
                failed += 1
                results.append(row)
                print(f"FAIL  {label} :: process dead", flush=True)
                break

            dismiss_overlay(main_win)
            idx = 0 if oidx < 0 else oidx
            if not click_nav_target(main_win, path, target, idx):
                row["status"] = "FAIL"
                row["detail"] = "菜单按钮未找到"
                failed += 1
                results.append(row)
                print(f"FAIL  {label} :: NOT FOUND", flush=True)
                continue

            time.sleep(1.1)
            dismiss_overlay(main_win)

            if not process_alive():
                row["status"] = "FAIL"
                row["detail"] = "打开页面后进程退出"
                failed += 1
                results.append(row)
                print(f"FAIL  {label} :: crash", flush=True)
                break

            exc = detect_exception_dialog()
            if exc:
                row["status"] = "FAIL"
                row["detail"] = exc
                failed += 1
                results.append(row)
                print(f"FAIL  {label} :: {exc}", flush=True)
                # try dismiss and continue
                dismiss_overlay(main_win)
                send_keys("{ESC}")
                continue

            passed += 1
            results.append(row)
            print(f"PASS  {label}", flush=True)
        except Exception as ex:
            row["status"] = "FAIL"
            row["detail"] = str(ex)
            failed += 1
            results.append(row)
            print(f"FAIL  {label} :: {ex}", flush=True)

    stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    report = {
        "started": stamp,
        "exe": str(EXE),
        "account": LOGIN_USER,
        "passed": passed,
        "failed": failed,
        "total": len(results),
        "results": results,
        "note": (
            "本脚本覆盖侧栏页面可达性与打开时异常检测；"
            "业务闭环（保存草稿/提交/审批/办结/打印留白）仍需按 docs/使用冒烟清单.md 手工核对。"
        ),
    }
    json_path = OUT / f"smoke_nav_{stamp}.json"
    md_path = OUT / f"smoke_nav_{stamp}.md"
    json_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    lines = [
        f"# DocMgr 导航冒泡结果（{stamp}）",
        "",
        f"- 通过：**{passed}** / 失败：**{failed}** / 合计：**{len(results)}**",
        f"- 账号：`{LOGIN_USER}`",
        "",
        "| #清单 | 路径 | 菜单 | 结果 | 说明 |",
        "| --- | --- | --- | --- | --- |",
    ]
    for r in results:
        lines.append(
            f"| {r['item'] or '-'} | {r['path']} | {r['target']} | {r['status']} | {r['detail']} |"
        )
    lines.extend(
        [
            "",
            "## 仍需手工的业务闭环（清单）",
            "",
            "| # | 流程 | 说明 |",
            "| --- | --- | --- |",
            "| 1b | 草稿待办出现/提交后消失 | 需真实保存草稿与提交 |",
            "| 1 | 待办弹窗/角标 | 依赖个人偏好与待办数据 |",
            "| 2–10 | 申请→审批→办结 | 需多角色与业务数据 |",
            "| 12 | 打印签批单办结前留白 | 需打开打印预览核对 |",
            "",
        ]
    )
    md_path.write_text("\n".join(lines), encoding="utf-8")

    print(flush=True)
    print(f"结果：通过 {passed}，失败 {failed}，合计 {len(results)}", flush=True)
    print(f"报告：{md_path}", flush=True)
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
