# -*- coding: utf-8 -*-
"""Launch DocMgr, login as archive admin, capture real UI screenshots."""
from __future__ import annotations

import subprocess
import sys
import time
from pathlib import Path

from pywinauto import Application, Desktop
from pywinauto.keyboard import send_keys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

ROOT = Path(__file__).resolve().parents[2]
EXE = ROOT / "bin" / "Debug" / "net8.0-windows" / "DocMgr.exe"
OUT = Path(__file__).resolve().parent / "screenshots"
OUT.mkdir(parents=True, exist_ok=True)

LOGIN_USER = "wxl"
LOGIN_PASS = "wxl"

MAIN = "\u6cb3\u5317\u7701\u7b2c\u4e09\u6d4b\u7ed8\u9662\u6d4b\u7ed8\u8d44\u6599\u7ba1\u7406\u7cfb\u7edf"
LOGIN_BTN = "\u767b \u5f55"
TIP_OK = "\u77e5\u9053\u4e86"

# path expanders (click in order) then target button; overview_index for duplicate titles
# (stem, path_headers..., target, overview_index)
CAPTURES = [
    ("03_topo_map", ["\u5386\u53f2\u5b58\u6863"], "\u5730\u5f62\u56fe\u56fe\u4ef6", -1),
    ("04_aerial_photo", ["\u5386\u53f2\u5b58\u6863"], "\u822a\u6444\u80f6\u7247\u3001\u50cf\u7247", -1),
    ("05_other_map", ["\u5386\u53f2\u5b58\u6863"], "\u5176\u4ed6\u8d44\u6599", -1),
    ("06_hist_box_ledger", ["\u5386\u53f2\u5b58\u6863", "\u8d44\u6599\u53f0\u8d26\u4e0e\u8d44\u6599\u5904\u7f6e"], "\u53f0\u8d26", -1),
    ("07_hist_disposal", ["\u5386\u53f2\u5b58\u6863", "\u8d44\u6599\u53f0\u8d26\u4e0e\u8d44\u6599\u5904\u7f6e"], "\u5904\u7f6e", -1),
    ("08_project_info", ["\u5e74\u5ea6\u8d44\u6599"], "\u9879\u76ee\u4fe1\u606f", -1),
    ("09_register_apply", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u5efa\u6863\u7533\u8bf7", -1),
    ("10_register_approve", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u5efa\u6863\u5ba1\u6279", -1),
    ("11_archive_filing", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u8d44\u6599\u7acb\u6863", -1),
    ("12_stock_hdd_direct", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u5b58\u91cf\u786c\u76d8\u76f4\u529e", -1),
    ("13_filing_ledger", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u7acb\u6863\u53f0\u8d26", -1),
    ("14_sim_relocation", ["\u5e74\u5ea6\u8d44\u6599", "\u8fc1\u6863"], "\u6a21\u62df\u4ecb\u8d28\u8d44\u6599\u8fc1\u6863", -1),
    ("15_ele_relocation", ["\u5e74\u5ea6\u8d44\u6599", "\u8fc1\u6863"], "\u7535\u5b50\u4ecb\u8d28\u8d44\u6599\u8fc1\u6863", -1),
    ("16_search_combined", ["\u5e74\u5ea6\u8d44\u6599", "\u68c0\u7d22"], "\u8d44\u6599\u68c0\u7d22(\u7efc\u5408\u6a21\u5f0f)", -1),
    ("17_search_pool", ["\u5e74\u5ea6\u8d44\u6599", "\u68c0\u7d22"], "\u68c0\u7d22\u6c60", -1),
    ("18_outbound_apply", ["\u5e74\u5ea6\u8d44\u6599", "\u5b9e\u7269\u6d41\u8f6c"], "\u501f\u51fa\u7533\u8bf7", -1),
    ("19_outbound_approve", ["\u5e74\u5ea6\u8d44\u6599", "\u5b9e\u7269\u6d41\u8f6c"], "\u501f\u51fa\u5ba1\u6279", -1),
    ("20_return_apply", ["\u5e74\u5ea6\u8d44\u6599", "\u5b9e\u7269\u6d41\u8f6c"], "\u5f52\u8fd8\u7533\u8bf7", -1),
    ("21_circulation_ledger", ["\u5e74\u5ea6\u8d44\u6599", "\u5b9e\u7269\u6d41\u8f6c"], "\u6d41\u8f6c\u53f0\u8d26", -1),
    ("22_net_inbound_apply", ["\u5e74\u5ea6\u8d44\u6599", "\u51fa\u5165\u7f51"], "\u5165\u7f51\u7533\u8bf7", -1),
    ("23_net_outbound_apply", ["\u5e74\u5ea6\u8d44\u6599", "\u51fa\u5165\u7f51"], "\u51fa\u7f51\u7533\u8bf7", -1),
    ("24_onnet_ledger", ["\u5e74\u5ea6\u8d44\u6599", "\u51fa\u5165\u7f51"], "\u5728\u7f51\u6570\u636e\u53f0\u8d26", -1),
    ("25_cross_domain", ["\u5e74\u5ea6\u8d44\u6599", "\u51fa\u5165\u7f51"], "\u8de8\u57df\u6d41\u8f6c\u53f0\u8d26", -1),
    ("26_sim_inventory", ["\u5e74\u5ea6\u8d44\u6599", "\u76d8\u5e93\u4e0e\u79bb\u5e93"], "\u6a21\u62df\u8d44\u6599\u76d8\u5e93", -1),
    ("27_ele_disposal", ["\u5e74\u5ea6\u8d44\u6599", "\u76d8\u5e93\u4e0e\u79bb\u5e93"], "\u7535\u5b50\u8d44\u6599\u79bb\u5e93\u5904\u7f6e", -1),
    ("28_stock_overview", ["\u5e93\u623f\u4e0e\u4ecb\u8d28"], "\u5e93\u7ba1\u8d44\u6599\u603b\u89c8", -1),
    ("29_cabinet_register", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u6863\u6848\u67dc"], "\u6863\u6848\u67dc\u767b\u8bb0", -1),
    ("30_cabinet_search", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u6863\u6848\u67dc"], "\u5f00\u67dc\u67e5\u770b", -1),
    ("31_hdd_overview", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u786c\u76d8"], "\u6982\u89c8", 0),
    ("32_hdd_register", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u786c\u76d8"], "\u521d\u59cb\u767b\u8bb0", -1),
    ("33_hdd_outbound", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u786c\u76d8"], "\u51fa\u5e93\u7533\u8bf7", -1),
    ("34_hdd_ledger", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u786c\u76d8"], "\u786c\u76d8\u53f0\u8d26", -1),
    ("35_od_overview", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u5149\u76d8"], "\u6982\u89c8", 0),
    ("36_od_ledger", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u5149\u76d8"], "\u5149\u76d8\u53f0\u8d26", -1),
    ("37_user_pref", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u4e2a\u4eba\u8bbe\u7f6e", -1),
    ("38_user_mgmt", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u7528\u6237\u7ba1\u7406", -1),
    ("39_approval_wf", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u5ba1\u6838\u5ba1\u6279", -1),
    ("40_todo_overview", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u5f85\u529e\u4e8b\u9879\u603b\u89c8", -1),
    ("41_db_log", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u6570\u636e\u5e93\u64cd\u4f5c\u65e5\u5fd7", -1),
    ("42_session", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u5728\u7ebf\u7528\u6237\u4e0e\u767b\u5f55\u8bb0\u5f55", -1),
    ("43_help", ["\u5e2e\u52a9"], "\u64cd\u4f5c\u624b\u518c", -1),
]


def shot(win, stem: str) -> None:
    path = OUT / f"{stem}.png"
    try:
        win.set_focus()
    except Exception:
        pass
    time.sleep(0.4)
    img = win.capture_as_image()
    img.save(str(path))
    print(f"  saved {path.name} ({img.size[0]}x{img.size[1]})", flush=True)


def has_button(win, title: str) -> bool:
    try:
        for ctrl in win.descendants(control_type="Button"):
            if ctrl.window_text() == title:
                return True
    except Exception:
        pass
    return False


def find_buttons(win, title: str):
    out = []
    for ctrl in win.descendants(control_type="Button"):
        try:
            if ctrl.window_text() == title:
                out.append(ctrl)
        except Exception:
            continue
    return out


def click_title(win, title: str, index: int = 0) -> bool:
    matches = find_buttons(win, title)
    if not matches:
        # try Group header
        for ctrl in win.descendants(control_type="Group"):
            try:
                if ctrl.window_text() == title:
                    try:
                        ctrl.expand()
                    except Exception:
                        ctrl.click_input()
                    return True
            except Exception:
                continue
        return False
    if index >= len(matches):
        index = len(matches) - 1
    matches[index].click_input()
    return True


def dismiss_overlay(win) -> None:
    for _ in range(6):
        if click_title(win, TIP_OK):
            time.sleep(0.3)
            continue
        clicked = False
        for w in Desktop(backend="uia").windows():
            if click_title(w, TIP_OK) or click_title(w, "\u786e\u5b9a") or click_title(w, "\u662f"):
                clicked = True
                time.sleep(0.25)
        if not clicked:
            break


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
    shot(win, "01_login")
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
        # Prefer Button click (accordion header)
        if not click_title(win, header, 0):
            # Group expand fallback
            for ctrl in win.descendants(control_type="Group"):
                try:
                    if ctrl.window_text() == header:
                        try:
                            ctrl.expand()
                        except Exception:
                            ctrl.click_input()
                        break
                except Exception:
                    continue
        time.sleep(0.28)


def main() -> int:
    print("EXE", EXE, flush=True)
    subprocess.run(["taskkill", "/IM", "DocMgr.exe", "/F"], capture_output=True)
    time.sleep(1.5)
    Application(backend="uia").start(str(EXE))
    time.sleep(4.0)

    login_win = wait_login_window(120)
    print("login ok", login_win.rectangle(), flush=True)
    main_win = do_login(login_win)
    print("main ok", main_win.rectangle(), flush=True)
    dismiss_overlay(main_win)
    shot(main_win, "02_home")

    ok = 0
    for stem, path, target, oidx in CAPTURES:
        print("Capture", stem, "->", target, flush=True)
        dismiss_overlay(main_win)
        open_path(main_win, path)
        idx = 0 if oidx < 0 else oidx
        if click_title(main_win, target, idx):
            time.sleep(1.0)
            dismiss_overlay(main_win)
            shot(main_win, stem)
            ok += 1
        else:
            print("  NOT FOUND", target, flush=True)

    print("captured_pages", ok, "png_total", len(list(OUT.glob("*.png"))), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
