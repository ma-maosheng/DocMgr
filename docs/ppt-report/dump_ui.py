# -*- coding: utf-8 -*-
"""Dump UIA tree of running DocMgr for debugging nav capture."""
from pywinauto import Desktop

N_MAIN = "\u6cb3\u5317\u7701\u7b2c\u4e09\u6d4b\u7ed8\u9662\u6d4b\u7ed8\u8d44\u6599\u7ba1\u7406\u7cfb\u7edf"

for w in Desktop(backend="uia").windows():
    t = w.window_text() or ""
    if N_MAIN not in t:
        continue
    print("WINDOW", t, w.rectangle())
    # dismiss tip
    try:
        b = w.child_window(title="\u77e5\u9053\u4e86", control_type="Button")
        if b.exists(timeout=0.3):
            b.click_input()
            print("dismissed tip")
    except Exception:
        pass

    # try expand 历史存档 via ExpandCollapse
    for ctrl in w.descendants():
        try:
            name = ctrl.window_text() or ""
        except Exception:
            continue
        if name != "\u5386\u53f2\u5b58\u6863":
            continue
        print("FOUND history ctrl", ctrl.element_info.control_type, ctrl.element_info.class_name)
        try:
            print("  patterns", ctrl.get_patterns())
        except Exception as ex:
            print("  patterns err", ex)
        try:
            ctrl.expand()
            print("  expand() ok")
        except Exception as ex:
            print("  expand() err", ex)
        try:
            ctrl.click_input()
            print("  click ok")
        except Exception as ex:
            print("  click err", ex)

    import time
    time.sleep(0.5)
    buttons = []
    for ctrl in w.descendants(control_type="Button"):
        try:
            buttons.append(ctrl.window_text())
        except Exception:
            pass
    print("BUTTONS after expand:")
    for b in buttons:
        if b:
            print(" ", b)
    break
else:
    print("no main window")
