# -*- coding: utf-8 -*-
import sys
import time
from pywinauto import Desktop

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

MAIN = "\u6cb3\u5317\u7701\u7b2c\u4e09\u6d4b\u7ed8\u9662\u6d4b\u7ed8\u8d44\u6599\u7ba1\u7406\u7cfb\u7edf"
win = None
for w in Desktop(backend="uia").windows():
    if MAIN in (w.window_text() or "") and w.rectangle().width() > 1000:
        win = w
        break
if not win:
    raise SystemExit("no main")

# dismiss tip
for ctrl in win.descendants(control_type="Button"):
    if ctrl.window_text() == "\u77e5\u9053\u4e86":
        ctrl.click_input()
        time.sleep(0.3)

print("Groups:")
for ctrl in win.descendants(control_type="Group"):
    t = ctrl.window_text()
    if t:
        print(" G:", t)

print("Click 历史存档 button")
for ctrl in win.descendants(control_type="Button"):
    if ctrl.window_text() == "\u5386\u53f2\u5b58\u6863":
        ctrl.click_input()
        break
time.sleep(0.5)

print("Groups after click:")
for ctrl in win.descendants(control_type="Group"):
    t = ctrl.window_text()
    if t:
        print(" G:", t)

print("Buttons containing 地形 or 台账:")
for ctrl in win.descendants(control_type="Button"):
    t = ctrl.window_text() or ""
    if "\u5730\u5f62" in t or "\u53f0\u8d26" in t or "\u822a\u6444" in t:
        print(" B:", t)
