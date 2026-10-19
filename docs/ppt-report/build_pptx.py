# -*- coding: utf-8 -*-
"""Build 50-slide leadership PPT from institute_data.yaml + real screenshots."""
from __future__ import annotations

from pathlib import Path

import yaml
from pptx import Presentation
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.util import Inches, Pt

ROOT = Path(__file__).resolve().parent
SHOT = ROOT / "screenshots"
DATA = ROOT / "institute_data.yaml"
OUT = ROOT / "河北省第三测绘院-测绘资料管理系统-建设汇报.pptx"

# Brand colors (match app teal)
TEAL = RGBColor(0x39, 0x7E, 0x7A)
TEAL_DARK = RGBColor(0x2A, 0x5F, 0x5C)
NAVY = RGBColor(0x1F, 0x2A, 0x37)
GRAY = RGBColor(0x4A, 0x55, 0x63)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
LIGHT = RGBColor(0xF4, 0xF7, 0xF7)
ACCENT = RGBColor(0xC4, 0x5C, 0x26)  # for 【待填】 callouts
SLIDE_W = Inches(13.333)
SLIDE_H = Inches(7.5)


def load_data() -> dict:
    with DATA.open(encoding="utf-8") as f:
        return yaml.safe_load(f)


def set_run(run, text, size=18, bold=False, color=NAVY, font="Microsoft YaHei"):
    run.text = text
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.color.rgb = color
    run.font.name = font


def add_bg(slide, color=LIGHT):
    shape = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SLIDE_W, SLIDE_H)
    shape.fill.solid()
    shape.fill.fore_color.rgb = color
    shape.line.fill.background()
    # send to back
    spTree = slide.shapes._spTree
    sp = shape._element
    spTree.remove(sp)
    spTree.insert(2, sp)


def add_top_bar(slide, title: str):
    bar = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SLIDE_W, Inches(0.85))
    bar.fill.solid()
    bar.fill.fore_color.rgb = TEAL
    bar.line.fill.background()
    box = slide.shapes.add_textbox(Inches(0.4), Inches(0.18), Inches(12.4), Inches(0.55))
    tf = box.text_frame
    tf.word_wrap = True
    p = tf.paragraphs[0]
    set_run(p.add_run(), title, size=24, bold=True, color=WHITE)


def add_footer(slide, page: int, total: int = 50):
    box = slide.shapes.add_textbox(Inches(0.4), Inches(7.1), Inches(12.5), Inches(0.3))
    tf = box.text_frame
    p = tf.paragraphs[0]
    set_run(p.add_run(), f"河北省第三测绘院 · 测绘资料管理系统  |  {page}/{total}", size=10, color=GRAY)


def add_bullets(slide, lines: list[str], left=0.5, top=1.2, width=12.3, height=5.5, size=18):
    box = slide.shapes.add_textbox(Inches(left), Inches(top), Inches(width), Inches(height))
    tf = box.text_frame
    tf.word_wrap = True
    for i, line in enumerate(lines):
        p = tf.paragraphs[0] if i == 0 else tf.add_paragraph()
        p.level = 0
        p.space_after = Pt(8)
        color = ACCENT if "【待填" in line else NAVY
        set_run(p.add_run(), line, size=size, color=color)


def text_slide(prs, page, title, bullets, note: str | None = None):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    add_bg(slide)
    add_top_bar(slide, title)
    add_bullets(slide, bullets)
    if note:
        box = slide.shapes.add_textbox(Inches(0.5), Inches(6.5), Inches(12.3), Inches(0.45))
        tf = box.text_frame
        tf.word_wrap = True
        set_run(tf.paragraphs[0].add_run(), note, size=12, color=GRAY)
    add_footer(slide, page)
    return slide


def shot_slide(prs, page, title, image_name: str, caption: str = ""):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    add_bg(slide, WHITE)
    add_top_bar(slide, title)
    img = SHOT / image_name
    if img.exists():
        # fit image under bar
        left = Inches(0.35)
        top = Inches(1.0)
        max_w = Inches(12.6)
        max_h = Inches(5.7)
        pic = slide.shapes.add_picture(str(img), left, top, width=max_w)
        # if too tall, rescale by height
        if pic.height > max_h:
            ratio = max_h / pic.height
            pic.width = int(pic.width * ratio)
            pic.height = int(max_h)
            pic.left = int((SLIDE_W - pic.width) / 2)
        else:
            pic.left = int((SLIDE_W - pic.width) / 2)
    else:
        add_bullets(slide, [f"（截图缺失：{image_name}）"], top=2.5)
    if caption:
        box = slide.shapes.add_textbox(Inches(0.4), Inches(6.75), Inches(12.5), Inches(0.3))
        set_run(box.text_frame.paragraphs[0].add_run(), caption, size=11, color=GRAY)
    add_footer(slide, page)
    return slide


def cover_slide(prs, data):
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    bg = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SLIDE_W, SLIDE_H)
    bg.fill.solid()
    bg.fill.fore_color.rgb = TEAL_DARK
    bg.line.fill.background()
    accent = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, Inches(5.8), SLIDE_W, Inches(1.7))
    accent.fill.solid()
    accent.fill.fore_color.rgb = TEAL
    accent.line.fill.background()

    box = slide.shapes.add_textbox(Inches(0.8), Inches(1.8), Inches(11.5), Inches(1))
    set_run(box.text_frame.paragraphs[0].add_run(), data["report"]["institute"], size=22, color=WHITE)

    box2 = slide.shapes.add_textbox(Inches(0.8), Inches(2.6), Inches(11.5), Inches(1.2))
    p = box2.text_frame.paragraphs[0]
    set_run(p.add_run(), data["report"]["system_name"] + "建设汇报", size=40, bold=True, color=WHITE)

    box3 = slide.shapes.add_textbox(Inches(0.8), Inches(4.0), Inches(11.5), Inches(0.8))
    set_run(
        box3.text_frame.paragraphs[0].add_run(),
        "面向院领导 / 科室内部 / 信息化专项  ·  标准版（含真实界面）",
        size=16,
        color=WHITE,
    )

    box4 = slide.shapes.add_textbox(Inches(0.8), Inches(6.15), Inches(11.5), Inches(1))
    tf = box4.text_frame
    set_run(tf.paragraphs[0].add_run(), f"版本 {data['report']['version']}    {data['report']['report_date']}", size=14, color=WHITE)
    p2 = tf.add_paragraph()
    set_run(p2.add_run(), "模拟盒 · 电子袋 ｜ 柜体 · 档口 · 盒袋 ｜ 申请 · 审批 · 签批办结", size=14, color=WHITE)
    return slide


def build():
    data = load_data()
    prs = Presentation()
    prs.slide_width = SLIDE_W
    prs.slide_height = SLIDE_H

    r, s, p, inv, pilot = data["report"], data["schedule"], data["progress"], data["investment"], data["pilot"]

    # 1
    cover_slide(prs, data)
    # 2
    text_slide(
        prs,
        2,
        "汇报提纲",
        [
            "一、建设背景与目标",
            "二、建设进度、投入与计划上线（本院数据）",
            "三、系统总体架构与用户角色",
            "四、核心业务能力（历史存档 / 年度资料 / 库房介质 / 出入网）",
            "五、内控治理与部署运维",
            "六、建设成效、下一步与请示事项",
            "说明：约半数为系统真实界面截图（资料管理员账号演示环境）",
        ],
    )
    # 3
    text_slide(
        prs,
        3,
        "一、建设背景",
        [
            "资料室业务链条长：建档、立档、借还、出入网、盘库、离库需全程可追溯",
            "模拟资料（档案盒）与电子资料（硬盘/光盘）长期存在“两套账”风险",
            "柜体档口、介质位置、审批签批、待办催办缺少一体化工具支撑",
            "迎检与内控要求：审批留痕、打印单据、附件扫描、操作审计可核查",
            "建设方向：内网桌面一体化工作台，服务资料室日常办理与院内协同",
        ],
    )
    # 4 shot login
    shot_slide(prs, 4, "界面实况｜用户登录", "01_login.png", "登录窗：院名品牌 + 版本号 + 账号认证（内网桌面端）")
    # 5
    text_slide(
        prs,
        5,
        "建设目标",
        [
            "一本账：年度资料与历史存档统一可查、口径一致",
            "一条链：申请 → 审批 → 实物交接 → 签批上传 → 办结闭环",
            "一库房：柜体 · 档口 · 盒袋 · 硬盘/光盘位置可视可控",
            "可审计：待办驱动、打印单据、附件留存、操作日志齐全",
            "可落地：内网多终端共用数据库，覆盖升级不伤业务库",
        ],
    )
    # 6 home
    shot_slide(prs, 6, "界面实况｜系统首页工作台", "02_home.png", "登录后首页：待办角标引导办理，左侧按四大业务域导航")
    # 7
    text_slide(
        prs,
        7,
        "系统定位与使用角色",
        [
            "定位：河北省第三测绘院测绘资料管理系统（DocMgr）内网桌面客户端",
            "部门资料员：提交各类申请（建档、借出、归还、出入网、硬盘出库等）",
            "资料管理员：审批、立档、盘库、离库、开柜、交接办结（资料室）",
            "系统管理员：用户/部门/角色、高级数据、操作日志等运维（不代办业务）",
            "网管负责人：维护服务器路径设置（出入网落点）",
            "说明：领导节点以纸质亲笔签为主，资料室在系统录入签字人姓名与日期",
        ],
    )
    # 8
    shot_slide(prs, 8, "界面实况｜个人设置与待办偏好", "37_user_pref.png", "个人设置：待办弹窗/角标/刷新策略，保障漏办可提醒")
    # 9
    text_slide(
        prs,
        9,
        "总体功能架构（四大域）",
        [
            "① 历史存档：地形图、航片、其他资料台账与离库处置",
            "② 年度资料：建档立档、迁档、检索、借还、出入网、盘库离库",
            "③ 库房与介质：库管总览、档案柜、硬盘全链路、光盘台账",
            "④ 系统治理：审批规则、用户权限、待办、日志、会话、手册与备份",
            "首页理念：模拟盒 · 电子袋 ｜ 柜体 · 档口 · 盒袋 ｜ 申请 · 审批 · 签批办结",
        ],
    )
    # 10
    shot_slide(prs, 10, "界面实况｜库管资料总览", "28_stock_overview.png", "库房视角总览：盒/袋与存储位置集中查看")

    # 11 progress
    done = p.get("done_items") or []
    doing = p.get("doing_items") or []
    todo = p.get("todo_items") or []
    text_slide(
        prs,
        11,
        "二、建设进度（本院数据）",
        [
            f"总体进度：{p.get('overall_percent', '【待填】')}",
            f"当前阶段：{p.get('phase', '【待填】')}",
            "已完成：",
            *[f"  · {x}" for x in done],
            "进行中：",
            *[f"  · {x}" for x in doing],
            "待开展：",
            *[f"  · {x}" for x in todo],
        ],
        note="橙色【待填】项请在 docs/ppt-report/institute_data.yaml 填写后重新生成 PPT",
    )
    # 12 investment & schedule
    text_slide(
        prs,
        12,
        "投入概算与计划节点（本院数据）",
        [
            f"启动时间：{s.get('kickoff')}",
            f"设计定稿：{s.get('design_complete')}",
            f"开发主体完成：{s.get('develop_complete')}",
            f"试运行开始：{s.get('pilot_start')}",
            f"计划正式上线：{s.get('go_live')}",
            f"计划验收：{s.get('acceptance')}",
            f"人力投入：{inv.get('labor')}　硬件：{inv.get('hardware')}",
            f"软件/许可：{inv.get('software')}　总投入：{inv.get('total')}",
            f"经费说明：{inv.get('remark')}",
            f"试点科室：{pilot.get('departments')}　试运行规模：{pilot.get('users')}",
            f"数据量级：{pilot.get('data_volume')}",
        ],
    )
    # 13
    text_slide(
        prs,
        13,
        "三、历史存档管理",
        [
            "覆盖：地形图图件、航摄胶片/像片、其他资料",
            "支持按盒管理、台账查询、Excel 导入与离库处置",
            "处置方式含销毁/转交/其他，办结前确认实物撤柜",
            "与标准档案柜档口用途联动（历史专用 / 混用）",
            "价值：长周期存量资料与年度资料同一平台管理",
        ],
    )
    # 14-16 shots
    shot_slide(prs, 14, "界面实况｜地形图图件", "03_topo_map.png")
    shot_slide(prs, 15, "界面实况｜航摄胶片、像片", "04_aerial_photo.png")
    shot_slide(prs, 16, "界面实况｜历史存档台账与处置", "06_hist_box_ledger.png", "历史盒台账；处置见 07_hist_disposal.png")

    # 17 lifecycle
    text_slide(
        prs,
        17,
        "四、年度资料全生命周期",
        [
            "建档申请 → 建档审批 → 实物交接/签批 → 办结",
            "→ 资料立档（模拟入盒落柜 / 电子入袋落盘柜）",
            "→ 检索 / 检索池 → 借出 → 归还 → 迁档 → 盘库 → 离库处置",
            "提效路径：存量硬盘直办、存档文本直办（可跳过完整建档审批）",
            "全程形成流转台账，支持撤回、逾期提醒与强制作废（按规则）",
        ],
    )
    shot_slide(prs, 18, "界面实况｜建档申请", "09_register_apply.png")
    shot_slide(prs, 19, "界面实况｜建档审批（签字录入）", "10_register_approve.png", "资料室录入各部门签字人；领导线下亲笔签")
    shot_slide(prs, 20, "界面实况｜资料立档", "11_archive_filing.png")

    # 21 dual track
    text_slide(
        prs,
        21,
        "双轨立档：模拟盒 + 电子袋",
        [
            "模拟介质：档案盒 → 标准/混用档口，支撑纸质与模拟资料落位",
            "电子介质：硬盘袋/光盘袋 → 磁盘柜档口，支持空盘立档、并档等",
            "同一项目与台账口径，避免纸质与电子“两套账”",
            "立档后进入检索、借还、迁档、盘库、离库统一闭环",
        ],
    )
    shot_slide(prs, 22, "界面实况｜立档台账 / 存量硬盘直办", "13_filing_ledger.png", "另见存量硬盘直办 12_stock_hdd_direct.png")
    shot_slide(prs, 23, "界面实况｜资料检索（综合模式）", "16_search_combined.png")
    shot_slide(prs, 24, "界面实况｜检索池", "17_search_pool.png", "检索结果可入池，支撑后续借出/出入网等批量办理")

    # 25 borrow
    text_slide(
        prs,
        25,
        "实物流转：借出与归还",
        [
            "借出：申请 → 审批（多级签字）→ 实物交接 → 签批上传 → 办结",
            "归还：完好归还与灭失分流；灭失须更完整审批与说明",
            "流转台账记录借还全过程，便于催还与审计",
            "与容器状态、份数、密级核实等业务规则联动",
        ],
    )
    shot_slide(prs, 26, "界面实况｜借出申请", "18_outbound_apply.png")
    shot_slide(prs, 27, "界面实况｜借出审批", "19_outbound_approve.png")
    shot_slide(prs, 28, "界面实况｜归还申请 / 流转台账", "20_return_apply.png", "流转台账见 21_circulation_ledger.png")

    # 29 network
    text_slide(
        prs,
        29,
        "出入网与在网数据",
        [
            "入网 / 出网：申请—审批—交接—路径/目录补录—办结",
            "在网数据台账：掌握已入网资料状态与版本信息",
            "在网数据处置：对在网资产走正式审批与办结",
            "跨域流转台账：串联出网转建档等跨业务链条",
            "与服务器路径设置联动，规范入网落点与责任",
        ],
    )
    shot_slide(prs, 30, "界面实况｜入网申请", "22_net_inbound_apply.png")
    shot_slide(prs, 31, "界面实况｜出网申请 / 在网台账", "23_net_outbound_apply.png", "在网台账 24、跨域台账 25")
    shot_slide(prs, 32, "界面实况｜跨域流转台账", "25_cross_domain.png")

    # 33 inventory
    text_slide(
        prs,
        33,
        "盘库与离库处置",
        [
            "盘库：对在库模拟/电子资料进行核对登记，支撑账实一致",
            "离库：销毁/转交等正式审批，上传签批与现场照片（按规则）",
            "可联动撤柜确认、低格留盘确认等实物动作勾选",
            "管理价值：从“能借出”升级为“盘得准、处置有据”",
        ],
    )
    shot_slide(prs, 34, "界面实况｜模拟资料盘库", "26_sim_inventory.png")
    shot_slide(prs, 35, "界面实况｜电子资料离库处置", "27_ele_disposal.png")

    # 36 facility
    text_slide(
        prs,
        36,
        "五、库房与介质",
        [
            "档案柜：柜体登记、开柜查看、档口用途（年度/历史/混用/空白硬盘等）",
            "硬盘：概览 KPI、初始登记、出库/归还、盘库、离库、台账",
            "光盘：概览与台账，随资料业务联动",
            "库管总览：盒袋与位置集中可视，减少“靠记忆找资料”",
        ],
    )
    shot_slide(prs, 37, "界面实况｜档案柜登记", "29_cabinet_register.png")
    shot_slide(prs, 38, "界面实况｜开柜查看", "30_cabinet_search.png")
    shot_slide(prs, 39, "界面实况｜硬盘概览", "31_hdd_overview.png")
    shot_slide(prs, 40, "界面实况｜硬盘台账 / 出库申请", "34_hdd_ledger.png", "出库申请见 33_hdd_outbound.png；初始登记见 32")
    shot_slide(prs, 41, "界面实况｜光盘概览与台账", "35_od_overview.png", "光盘台账见 36_od_ledger.png")

    # 42 governance
    text_slide(
        prs,
        42,
        "六、内控治理能力",
        [
            "审批规则可配置：部门负责人、资料室、生产科、分管领导等节点",
            "打印审批单/交接单；办结前签字栏留白，办结后可预填",
            "附件：签批扫描、资料/处置/硬盘照片；支持高拍仪直拍",
            "待办角标/弹窗/总览驱动办理；逾期设置可配",
            "数据库操作日志、在线用户与登录记录，满足审计与运维",
        ],
    )
    shot_slide(prs, 43, "界面实况｜审核审批规则", "39_approval_wf.png")
    shot_slide(prs, 44, "界面实况｜待办事项总览", "40_todo_overview.png")
    shot_slide(prs, 45, "界面实况｜用户 / 会话 / 操作日志", "38_user_mgmt.png", "会话 42_session.png；日志 41_db_log.png")

    # 46 deploy
    text_slide(
        prs,
        46,
        "部署与运维方式",
        [
            "形态：Windows 桌面客户端（WPF），非互联网公开系统",
            "数据：内网数据库多终端共用；升级前全员退出",
            "升级：只换程序文件，不覆盖业务库；支持自动迁移前备份",
            "交付：覆盖发布脚本 / 自包含安装包 / 可选安装向导",
            "运维：高级数据管理备份还原；操作手册内置（版本与库路径可见）",
        ],
    )
    shot_slide(prs, 47, "界面实况｜操作手册", "43_help.png", f"当前演示版本：{r.get('version')}")

    # 48 benefits
    text_slide(
        prs,
        48,
        "建设成效（可按试运行数据再充实）",
        [
            "业务闭环已覆盖：建档—立档—借还—出入网—盘库—离库",
            "历史存档、柜体档口、硬盘/光盘与年度资料同一工作台",
            "审批、打印、附件、待办、日志齐全，满足内控与迎检留痕",
            "角色权限清晰：申请侧与资料室办理侧分离，减少越权代办",
            f"试运行：{pilot.get('departments')}；数据量级：{pilot.get('data_volume')}",
        ],
    )
    # 49 next
    text_slide(
        prs,
        49,
        "下一步计划与请示事项",
        [
            f"按计划节点推进上线：{s.get('go_live')}（请审定）",
            "扩大试点科室培训，固化《操作规程》与角色分工",
            "完成历史台账/存量数据导入与盘点核对",
            "根据试运行反馈优化审批节点、逾期提醒与报表通报",
            "请示：是否同意按上述计划进入试运行/正式上线阶段",
            "请示：建设投入与保障条件是否按本院数据口径予以确认",
        ],
    )
    # 50 thanks
    slide = prs.slides.add_slide(prs.slide_layouts[6])
    bg = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, 0, SLIDE_W, SLIDE_H)
    bg.fill.solid()
    bg.fill.fore_color.rgb = TEAL_DARK
    bg.line.fill.background()
    box = slide.shapes.add_textbox(Inches(1), Inches(2.5), Inches(11), Inches(1.2))
    set_run(box.text_frame.paragraphs[0].add_run(), "敬请批评指正", size=40, bold=True, color=WHITE)
    box2 = slide.shapes.add_textbox(Inches(1), Inches(4.0), Inches(11), Inches(1.5))
    tf = box2.text_frame
    set_run(tf.paragraphs[0].add_run(), "河北省第三测绘院 · 测绘资料管理系统", size=18, color=WHITE)
    p = tf.add_paragraph()
    set_run(p.add_run(), "数据填写：docs/ppt-report/institute_data.yaml", size=14, color=WHITE)
    p = tf.add_paragraph()
    set_run(p.add_run(), "重新生成：python docs/ppt-report/build_pptx.py", size=14, color=WHITE)
    add_footer(slide, 50)

    # Fix page numbers: cover has no footer with 1 — add manually? cover skipped footer.
    # Re-number not critical; we added footers with explicit page nums.

    prs.save(OUT)
    print("Wrote", OUT, "slides=", len(prs.slides))


if __name__ == "__main__":
    build()
