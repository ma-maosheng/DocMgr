# -*- coding: utf-8 -*-
"""Capture missing screenshots on already-running DocMgr (wxl session)."""
from __future__ import annotations

import sys
import time
from pathlib import Path

from pywinauto import Desktop

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

OUT = Path(__file__).resolve().parent / "screenshots"
MAIN = "\u6cb3\u5317\u7701\u7b2c\u4e09\u6d4b\u7ed8\u9662\u6d4b\u7ed8\u8d44\u6599\u7ba1\u7406\u7cfb\u7edf"
TIP_OK = "\u77e5\u9053\u4e86"

MISSING = [
    ("04_aerial_photo", ["\u5386\u53f2\u5b58\u6863"], "\u822a\u6444\u80f6\u7247\u3001\u50cf\u7247", -1),
    ("06_hist_box_ledger", ["\u5386\u53f2\u5b58\u6863", "\u8d44\u6599\u53f0\u8d26\u4e0e\u8d44\u6599\u5904\u7f6e"], "\u53f0\u8d26", -1),
    ("09_register_apply", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u5efa\u6863\u7533\u8bf7", -1),
    ("11_archive_filing", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u8d44\u6599\u7acb\u6863", -1),
    ("12_stock_hdd_direct", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u5b58\u91cf\u786c\u76d8\u76f4\u529e", -1),
    ("13_filing_ledger", ["\u5e74\u5ea6\u8d44\u6599", "\u5efa\u6863\u4e0e\u7acb\u6863"], "\u7acb\u6863\u53f0\u8d26", -1),
    ("15_ele_relocation", ["\u5e74\u5ea6\u8d44\u6599", "\u8fc1\u6863"], "\u7535\u5b50\u4ecb\u8d28\u8d44\u6599\u8fc1\u6863", -1),
    ("17_search_pool", ["\u5e74\u5ea6\u8d44\u6599", "\u68c0\u7d22"], "\u68c0\u7d22\u6c60", -1),
    ("19_outbound_approve", ["\u5e74\u5ea6\u8d44\u6599", "\u5b9e\u7269\u6d41\u8f6c"], "\u501f\u51fa\u5ba1\u6279", -1),
    ("20_return_apply", ["\u5e74\u5ea6\u8d44\u6599", "\u5b9e\u7269\u6d41\u8f6c"], "\u5f52\u8fd8\u7533\u8bf7", -1),
    ("21_circulation_ledger", ["\u5e74\u5ea6\u8d44\u6599", "\u5b9e\u7269\u6d41\u8f6c"], "\u6d41\u8f6c\u53f0\u8d26", -1),
    ("23_net_outbound_apply", ["\u5e74\u5ea6\u8d44\u6599", "\u51fa\u5165\u7f51"], "\u51fa\u7f51\u7533\u8bf7", -1),
    ("24_onnet_ledger", ["\u5e74\u5ea6\u8d44\u6599", "\u51fa\u5165\u7f51"], "\u5728\u7f51\u6570\u636e\u53f0\u8d26", -1),
    ("25_cross_domain", ["\u5e74\u5ea6\u8d44\u6599", "\u51fa\u5165\u7f51"], "\u8de8\u57df\u6d41\u8f6c\u53f0\u8d26", -1),
    ("27_ele_disposal", ["\u5e74\u5ea6\u8d44\u6599", "\u76d8\u5e93\u4e0e\u79bb\u5e93"], "\u7535\u5b50\u8d44\u6599\u79bb\u5e93\u5904\u7f6e", -1),
    ("29_cabinet_register", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u6863\u6848\u67dc"], "\u6863\u6848\u67dc\u767b\u8bb0", -1),
    ("31_hdd_overview", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u786c\u76d8"], "\u6982\u89c8", 0),
    ("33_hdd_outbound", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u786c\u76d8"], "\u51fa\u5e93\u7533\u8bf7", -1),
    ("34_hdd_ledger", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u786c\u76d8"], "\u786c\u76d8\u53f0\u8d26", -1),
    ("35_od_overview", ["\u5e93\u623f\u4e0e\u4ecb\u8d28", "\u5149\u76d8"], "\u6982\u89c8", 0),
    ("38_user_mgmt", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u7528\u6237\u7ba1\u7406", -1),
    ("40_todo_overview", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u5f85\u529e\u4e8b\u9879\u603b\u89c8", -1),
    ("42_session", ["\u7cfb\u7edf\u8bbe\u7f6e"], "\u5728\u7ebf\u7528\u6237\u4e0e\u767b\u5f55\u8bb0\u5f55", -1),
]


def get_main():
    for w in Desktop(backend="uia").windows():
        t = w.window_text() or ""
        if MAIN in t and w.rectangle().width() > 1000:
            return w
    raise RuntimeError("main not found")


def dismiss(win):
    for ctrl in win.descendants(control_type="Button"):
        try:
            if ctrl.window_text() == TIP_OK:
                ctrl.click_input()
                time.sleep(0.25)
        except Exception:
            pass


def ensure_expanded(win, header: str) -> None:
    # ExpandCollapse only — do not toggle-click
    for ctrl in win.descendants(control_type="Group"):
        try:
            if ctrl.window_text() == header:
                try:
                    ctrl.expand()
                except Exception:
                    # if already expanded, expand may fail; ignore
                    pass
                time.sleep(0.2)
                return
        except Exception:
            continue
    # fallback: click button once only if target children absent later
    for ctrl in win.descendants(control_type="Button"):
        try:
            if ctrl.window_text() == header:
                ctrl.click_input()
                time.sleep(0.25)
                return
        except Exception:
            continue


def find_buttons(win, title: str):
    out = []
    for ctrl in win.descendants(control_type="Button"):
        try:
            if ctrl.window_text() == title:
                out.append(ctrl)
        except Exception:
            continue
    return out


def open_path(win, path):
    for h in path:
        ensure_expanded(win, h)


def shot(win, stem):
    path = OUT / f"{stem}.png"
    try:
        win.set_focus()
    except Exception:
        pass
    time.sleep(0.4)
    win.capture_as_image().save(str(path))
    print(" saved", path.name, flush=True)


def main():
    win = get_main()
    dismiss(win)
    ok = 0
    for stem, path, target, oidx in MISSING:
        print("Capture", stem, target, flush=True)
        dismiss(win)
        open_path(win, path)
        # if still not found, try click-toggle once on last header
        matches = find_buttons(win, target)
        if not matches:
            ensure_expanded(win, path[-1])
            # last resort toggle click on last path header
            for ctrl in win.descendants(control_type="Button"):
                try:
                    if ctrl.window_text() == path[-1]:
                        ctrl.click_input()
                        time.sleep(0.3)
                        break
                except Exception:
                    pass
            matches = find_buttons(win, target)
        if not matches:
            print("  NOT FOUND", flush=True)
            continue
        idx = 0 if oidx < 0 else min(oidx, len(matches) - 1)
        matches[idx].click_input()
        time.sleep(1.0)
        dismiss(win)
        shot(win, stem)
        ok += 1
    print("ok", ok, "total png", len(list(OUT.glob("*.png"))), flush=True)


if __name__ == "__main__":
    main()
