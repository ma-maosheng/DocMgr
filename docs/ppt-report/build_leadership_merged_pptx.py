# -*- coding: utf-8 -*-
"""
将三份材料梳理合成为「院领导汇报」PPT：

  1) 河北省第三测绘院-测绘资料管理系统-建设汇报.pptx  —— 叙事总述 / 进度 / 成效
  2) 测绘资料管理系统-菜单结构图.pptx                 —— 功能导航结构
  3) 测绘资料管理系统-界面图.pptx                     —— 手作界面实况（优先于自动截图）

依赖：本机已安装 Microsoft PowerPoint + pywin32。
"""
from __future__ import annotations

import sys
from pathlib import Path

import win32com.client

ROOT = Path(__file__).resolve().parent
SRC_REPORT = ROOT / "河北省第三测绘院-测绘资料管理系统-建设汇报.pptx"
SRC_MENU = ROOT / "测绘资料管理系统-菜单结构图.pptx"
SRC_UI = ROOT / "测绘资料管理系统-界面图.pptx"
OUT = ROOT / "河北省第三测绘院-测绘资料管理系统-建设汇报-合成版.pptx"

# PowerPoint: ppLayoutBlank = 12
PP_LAYOUT_BLANK = 12
# msoTextOrientationHorizontal = 1
MSO_TEXT_ORIENT_HORIZONTAL = 1


def _rgb(r: int, g: int, b: int) -> int:
    """Office COM ForeColor.RGB 使用 0xBBGGRR 打包。"""
    return int(r) + (int(g) << 8) + (int(b) << 16)


def _require_sources() -> None:
    missing = [p for p in (SRC_REPORT, SRC_MENU, SRC_UI) if not p.exists()]
    if missing:
        raise FileNotFoundError(
            "缺少源文件：\n" + "\n".join(str(p) for p in missing)
        )


def _open_ro(app, path: Path):
    return app.Presentations.Open(str(path), WithWindow=False, ReadOnly=True)


def _copy_slides(src_prs, indices_1based: list[int], dest_prs) -> int:
    """按 1-based 页码顺序复制到 dest 末尾，返回复制页数。"""
    n = 0
    for idx in indices_1based:
        if idx < 1 or idx > src_prs.Slides.Count:
            raise IndexError(f"{src_prs.Name}: 页码 {idx} 超出范围 1..{src_prs.Slides.Count}")
        src_prs.Slides(idx).Copy()
        # 粘贴到末尾
        dest_prs.Slides.Paste(dest_prs.Slides.Count + 1)
        n += 1
    return n


def _pages(start: int, end: int) -> list[int]:
    """闭区间页码列表（1-based）。"""
    return list(range(start, end + 1))


def _add_section_slide(dest_prs, part_no: str, title: str, lines: list[str]) -> None:
    """插入一页章节分隔页（青绿底 + 白字），风格贴近建设汇报封面。"""
    slide = dest_prs.Slides.Add(dest_prs.Slides.Count + 1, PP_LAYOUT_BLANK)

    # 背景色块（TEAL_DARK #2A5F5C）
    bg = slide.Shapes.AddShape(1, 0, 0, dest_prs.PageSetup.SlideWidth, dest_prs.PageSetup.SlideHeight)
    bg.Fill.Solid()
    bg.Fill.ForeColor.RGB = _rgb(0x2A, 0x5F, 0x5C)
    bg.Line.Visible = 0

    # 底栏（TEAL #397E7A）
    bar_h = 90
    bar = slide.Shapes.AddShape(
        1,
        0,
        dest_prs.PageSetup.SlideHeight - bar_h,
        dest_prs.PageSetup.SlideWidth,
        bar_h,
    )
    bar.Fill.Solid()
    bar.Fill.ForeColor.RGB = _rgb(0x39, 0x7E, 0x7A)
    bar.Line.Visible = 0

    w = dest_prs.PageSetup.SlideWidth
    tb_part = slide.Shapes.AddTextbox(MSO_TEXT_ORIENT_HORIZONTAL, 54, 120, w - 108, 40)
    tr = tb_part.TextFrame.TextRange
    tr.Text = part_no
    tr.Font.Name = "Microsoft YaHei"
    tr.Font.Size = 18
    tr.Font.Bold = False
    tr.Font.Color.RGB = _rgb(255, 255, 255)

    tb_title = slide.Shapes.AddTextbox(MSO_TEXT_ORIENT_HORIZONTAL, 54, 170, w - 108, 70)
    tr = tb_title.TextFrame.TextRange
    tr.Text = title
    tr.Font.Name = "Microsoft YaHei"
    tr.Font.Size = 36
    tr.Font.Bold = True
    tr.Font.Color.RGB = _rgb(255, 255, 255)

    if lines:
        body = "\r".join(lines)
        tb_body = slide.Shapes.AddTextbox(MSO_TEXT_ORIENT_HORIZONTAL, 54, 270, w - 108, 220)
        tr = tb_body.TextFrame.TextRange
        tr.Text = body
        tr.Font.Name = "Microsoft YaHei"
        tr.Font.Size = 16
        tr.Font.Color.RGB = _rgb(255, 255, 255)
        tb_body.TextFrame.WordWrap = True

    tb_foot = slide.Shapes.AddTextbox(
        MSO_TEXT_ORIENT_HORIZONTAL,
        54,
        dest_prs.PageSetup.SlideHeight - 58,
        w - 108,
        28,
    )
    tr = tb_foot.TextFrame.TextRange
    tr.Text = "河北省第三测绘院 · 测绘资料管理系统 · 建设汇报（合成版）"
    tr.Font.Name = "Microsoft YaHei"
    tr.Font.Size = 12
    tr.Font.Color.RGB = _rgb(255, 255, 255)


def _add_agenda_slide(dest_prs) -> None:
    slide = dest_prs.Slides.Add(dest_prs.Slides.Count + 1, PP_LAYOUT_BLANK)
    w = dest_prs.PageSetup.SlideWidth
    h = dest_prs.PageSetup.SlideHeight

    bg = slide.Shapes.AddShape(1, 0, 0, w, h)
    bg.Fill.Solid()
    bg.Fill.ForeColor.RGB = _rgb(0xF4, 0xF7, 0xF7)
    bg.Line.Visible = 0

    bar = slide.Shapes.AddShape(1, 0, 0, w, 60)
    bar.Fill.Solid()
    bar.Fill.ForeColor.RGB = _rgb(0x39, 0x7E, 0x7A)
    bar.Line.Visible = 0

    tb = slide.Shapes.AddTextbox(MSO_TEXT_ORIENT_HORIZONTAL, 28, 14, w - 56, 36)
    tr = tb.TextFrame.TextRange
    tr.Text = "汇报提纲（合成版）"
    tr.Font.Name = "Microsoft YaHei"
    tr.Font.Size = 24
    tr.Font.Bold = True
    tr.Font.Color.RGB = _rgb(255, 255, 255)

    items = [
        "一、建设背景、目标与系统定位",
        "二、建设进度、投入与计划节点（本院数据）",
        "三、功能导航：左侧菜单结构总览",
        "四、核心业务与界面实况（历史存档 / 年度资料 / 库房介质）",
        "五、内控治理、部署运维与操作手册",
        "六、建设成效、下一步与请示事项",
        "",
        "说明：本版由「建设汇报叙事」+「菜单结构图」+「界面图」梳理合成；",
        "界面实况收录《测绘资料管理系统-界面图》全部页面，按业务章节编排。",
    ]
    body = slide.Shapes.AddTextbox(MSO_TEXT_ORIENT_HORIZONTAL, 36, 90, w - 72, h - 140)
    tr = body.TextFrame.TextRange
    tr.Text = "\r".join(items)
    tr.Font.Name = "Microsoft YaHei"
    tr.Font.Size = 18
    tr.Font.Color.RGB = _rgb(0x1F, 0x2A, 0x37)
    body.TextFrame.WordWrap = True


def build() -> Path:
    _require_sources()
    if OUT.exists():
        try:
            OUT.unlink()
        except OSError as exc:
            raise RuntimeError(f"无法覆盖输出文件（请先关闭）：{OUT}") from exc

    app = win32com.client.Dispatch("PowerPoint.Application")
    # 部分环境不允许改 Visible；失败则忽略
    try:
        app.Visible = True
    except Exception:
        pass

    report = menu = ui = dest = None
    try:
        report = _open_ro(app, SRC_REPORT)
        menu = _open_ro(app, SRC_MENU)
        ui = _open_ro(app, SRC_UI)

        dest = app.Presentations.Add(WithWindow=False)
        # 宽屏 16:9（与源一致）
        dest.PageSetup.SlideWidth = report.PageSetup.SlideWidth
        dest.PageSetup.SlideHeight = report.PageSetup.SlideHeight

        # —— 封面 ——
        _copy_slides(report, [1], dest)
        # Presentations.Add 会自带 1 页空白，删掉以免多一页
        if dest.Slides.Count >= 2:
            dest.Slides(1).Delete()

        # —— 提纲 ——
        _add_agenda_slide(dest)

        # —— 一、建设总述 ——
        _add_section_slide(
            dest,
            "第一部分",
            "建设背景、目标与定位",
            [
                "为什么建：资料室全链路可追溯、模拟/电子一本账、柜体档口可视",
                "建成什么：内网桌面一体化工作台，服务申请—审批—交接—办结",
                "谁在用：部门资料员 · 资料管理员 · 系统管理员 · 网管负责人",
            ],
        )
        _copy_slides(report, [3, 5, 7, 9], dest)  # 背景、目标、角色、四大域

        # —— 二、进度与投入 ——
        _add_section_slide(
            dest,
            "第二部分",
            "建设进度与投入计划",
            [
                "进度、阶段、已完成/进行中事项（本院数据）",
                "投入概算与计划上线节点（待填项见 institute_data.yaml）",
            ],
        )
        _copy_slides(report, [11, 12], dest)

        # —— 三、菜单结构 ——
        _add_section_slide(
            dest,
            "第三部分",
            "功能导航：菜单结构",
            [
                "与主窗口左侧导航一致，五大一级模块",
                "便于领导快速把握系统覆盖范围与入口层级",
            ],
        )
        # 菜单结构全书（略封面，避免与汇报封面重复）
        _copy_slides(menu, _pages(2, 8), dest)

        # 界面图收录范围（与源文件页序一致，按业务域重排后全部纳入）
        ui_total = int(ui.Slides.Count)
        ui_used: list[int] = []

        def take_ui(indices: list[int]) -> None:
            ui_used.extend(indices)
            _copy_slides(ui, indices, dest)

        # —— 界面实况开篇（界面图封面） ——
        _add_section_slide(
            dest,
            "第四部分",
            "核心业务与界面实况",
            [
                "以下界面页完整收录《测绘资料管理系统-界面图》全部内容",
                "编排顺序：历史存档 → 年度资料 → 库房与介质 → 系统设置与手册",
            ],
        )
        take_ui([1])

        # —— 四·A 历史存档（界面图 87–94） ——
        _add_section_slide(
            dest,
            "第四部分 · A",
            "历史存档管理",
            [
                "地形图 / 航片 / 其他资料台账",
                "离库处置与柜体档口联动",
            ],
        )
        _copy_slides(report, [13], dest)
        take_ui(_pages(87, 94))

        # —— 四·B 年度资料（界面图 39–86） ——
        _add_section_slide(
            dest,
            "第四部分 · B",
            "年度资料全生命周期",
            [
                "建档—立档—检索—借还—出入网—盘库—离库",
                "模拟盒 + 电子袋双轨立档，同一口径",
            ],
        )
        _copy_slides(report, [17, 21], dest)
        take_ui(_pages(39, 56))  # 总览 / 项目 / 建档立档 / 存量直办
        take_ui(_pages(57, 59))  # 迁档
        take_ui(_pages(60, 63))  # 检索
        _copy_slides(report, [25], dest)
        take_ui(_pages(64, 74))  # 实物流转
        _copy_slides(report, [29], dest)
        take_ui(_pages(75, 82))  # 出入网
        _copy_slides(report, [33], dest)
        take_ui(_pages(83, 86))  # 盘库离库

        # —— 四·C 库房与介质（界面图 15–38） ——
        _add_section_slide(
            dest,
            "第四部分 · C",
            "库房与介质",
            [
                "档案柜登记与开柜档口",
                "硬盘全链路（登记 / 出库 / 归还 / 盘库 / 离库）",
                "光盘概览与台账",
            ],
        )
        _copy_slides(report, [36], dest)
        take_ui(_pages(15, 38))

        # —— 五、内控与运维（界面图 4–14 系统设置 + 2–3 操作手册） ——
        _add_section_slide(
            dest,
            "第五部分",
            "内控治理与部署运维",
            [
                "审批规则、待办驱动、用户权限与操作审计",
                "内网桌面部署、备份还原、内置操作手册",
            ],
        )
        _copy_slides(report, [42], dest)
        take_ui(_pages(4, 14))
        _copy_slides(report, [46], dest)
        take_ui(_pages(2, 3))

        # —— 六、成效与请示 ——
        _add_section_slide(
            dest,
            "第六部分",
            "建设成效与请示事项",
            [
                "闭环能力、内控留痕、角色分离",
                "下一步计划与需领导审定事项",
            ],
        )
        _copy_slides(report, [48, 49, 50], dest)

        # 校验：界面图每一页都必须用到且不重复
        missing = sorted(set(_pages(1, ui_total)) - set(ui_used))
        dupes = sorted({x for x in ui_used if ui_used.count(x) > 1})
        if missing or dupes or len(ui_used) != ui_total:
            raise RuntimeError(
                f"界面图收录不完整：源={ui_total} 已用={len(ui_used)} "
                f"缺失={missing} 重复={dupes}"
            )

        dest.SaveAs(str(OUT))
        total = dest.Slides.Count
        print(f"Wrote {OUT}")
        print(f"slides={total}  ui_slides_included={len(ui_used)}/{ui_total}")
        return OUT
    finally:
        for prs in (dest, report, menu, ui):
            if prs is not None:
                try:
                    prs.Close()
                except Exception:
                    pass
        try:
            app.Quit()
        except Exception:
            pass


if __name__ == "__main__":
    try:
        build()
    except Exception as exc:
        print("ERROR:", exc, file=sys.stderr)
        sys.exit(1)
