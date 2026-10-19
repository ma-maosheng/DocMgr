# -*- coding: utf-8 -*-
"""
为「合成版1」第四部分界面拷贝页添加核心设计思路说明。

策略（尽量不遮蔽原有信息）：
  1) 将页内全部形状按顶部对齐均匀缩放，腾出底部讲解带
  2) 底部增加青绿强调条 + 半透明浅底「设计思路」说明

输入：河北省第三测绘院-测绘资料管理系统-建设汇报-合成版1.pptx
输出：河北省第三测绘院-测绘资料管理系统-建设汇报-合成版1-设计说明.pptx
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

import win32com.client

ROOT = Path(__file__).resolve().parent
SRC = ROOT / "河北省第三测绘院-测绘资料管理系统-建设汇报-合成版1.pptx"
OUT = ROOT / "河北省第三测绘院-测绘资料管理系统-建设汇报-合成版1-设计说明.pptx"

# 第四部分范围（1-based，含界面与其间的叙事页）
PART4_FIRST = 19
PART4_LAST = 110  # 111 起为第五部分

# 不处理：章节分隔页、建设汇报叙事页（非界面拷贝）
SKIP_TITLES = {
    "第四部分",
    "第四部分 · A",
    "第四部分 · B",
    "第四部分 · C",
    "核心业务与界面实况",
    "历史存档管理",
    "三、历史存档管理",
    "年度资料全生命周期",
    "四、年度资料全生命周期",
    "双轨立档：模拟盒 + 电子袋",
    "实物流转：借出与归还",
    "出入网与在网数据",
    "盘库与离库处置",
    "库房与介质",
    "五、库房与介质",
}

# 精确标题 → 设计思路（1～2 句，领导可读）
NOTES_EXACT: dict[str, str] = {
    # 封面
    "": "开篇定调：以真实界面承载业务，而不是概念图；后续按历史存档→年度资料→库房介质展开。",
    # 历史存档
    "历史存档.地形图图件": "设计思路：历史图件按「分类—检索—维护」组织，保留传统台账习惯，同时纳入统一生命周期与柜位管理。",
    "历史存档.航摄胶片、像片": "设计思路：航片按测区/比例尺/摄影期等专业维度管理，支持盒内内容与数量登记，便于长周期盘点与处置。",
    "历史存档.其他资料": "设计思路：其他图件用「起止年度+资料内容」表达跨年存量，避免硬套年度项目口径造成失真。",
    "历史存档.资料台账与资料处置.台账": "设计思路：先「盒台账」后「处置」，以档案盒为管理单元，台账条数与混放关系一次看清。",
    "历史存档.资料台账与资料处置.处置": "设计思路：离库必须走审批+实物撤柜确认，销毁/转交等处置方式留痕，防止「账销物还在」。",
    # 年度资料总览/项目
    "年度资料": "设计思路：年度资料是系统主战场——以项目为入口，把建档、立档、流转、出入网、盘库串成一条可审计闭环。",
    "年度资料.项目信息": "设计思路：项目是资料归集主键；后续建档/立档/台账均挂靠项目与年度，保证「一本账」口径一致。",
    "年度资料.建档与立档": "设计思路：建档解决「准不准进库」，立档解决「落到哪、什么载体」；两段分离，权限与责任更清晰。",
    # 建档
    "年度资料.建档与立档.建档申请": "设计思路：申请侧只填业务必要项，审批与签字在后续节点完成；减少一次性表单过重导致的填报阻力。",
    "年度资料.建档与立档.建档申请.申请信息录入": "设计思路：模拟/电子介质分栏录入，密级、份数、来源一次采集，为立档落位与借还份数控制打底。",
    "年度资料.建档与立档.建档审批": "设计思路：线上推进节点、线下亲笔签为主；系统录入签字人与日期，兼顾效率与内控习惯。",
    # 立档
    "年度资料.建档与立档.资料立档（模拟介质）": "设计思路：模拟资料以「档案盒→标准/混用档口」落位，立档即绑定柜体位置，检索与开柜同源。",
    "年度资料.建档与立档.资料立档（电子介质-1）": "设计思路：电子资料以「硬盘袋/光盘袋→磁盘柜」落位，支持空盘立档、并档等库房实际操作。",
    "年度资料.建档与立档.资料立档（电子介质-2）": "设计思路：电子立档强调存储路径与数据量，形成「袋—盘—目录」可追溯链条，避免只见盘不见数。",
    "年度资料.建档与立档.资料立档（电子介质-3）": "设计思路：多盘/并档场景可视化确认目标袋与盘号，防止错并、漏并导致台账分裂。",
    "年度资料.建档与立档.资料立档": "设计思路：待立档/已立档分屏对照；模拟盒与电子袋同页呈现，体现双轨并行、口径统一。",
    "年度资料.建档与立档.立档台账": "设计思路：立档台账是「已入库真相源」，后续借还、迁档、盘库、离库都回指这里，避免多头记账。",
    "年度资料.建档与立档.存量硬盘直办立档": "设计思路：存量硬盘走直办快车道，跳过完整建档审批但仍写立档事实，兼顾历史清理效率与可追溯。",
    "年度资料.建档与立档.存量文本直办立档": "设计思路：存量文本支持表格式/结构化录入两种入口，降低大批量补录成本，同时约束必填字段质量。",
    # 迁档
    "年度资料.迁档（模拟资料）": "设计思路：迁档=位置变更业务，保留迁前迁后容器与档口，形成移库履历，而不是直接改台账字段。",
    "年度资料.迁档（电子资料）": "设计思路：电子迁档可联动源盘处置（留用/低格等），把「数据搬家」与「介质命运」一次决策清楚。",
    "年度资料.迁档（迁档台账）": "设计思路：迁档台账供审计与对账，回答「何时、何人、从哪到哪」，支撑柜位调整可复核。",
    # 检索
    "年度资料.检索.综合模式": "设计思路：综合检索跨模拟/电子一张网查询，先找得到，再决定借出或出入网，减少「两套系统各搜一遍」。",
    "年度资料.检索.电子资料检索模式": "设计思路：电子检索突出路径、目录/文件明细与载体，服务精确到「要哪一段数据」的办理场景。",
    "年度资料.检索.模拟资料检索模式": "设计思路：模拟检索突出盒号、档口与份数可用性，服务提档/复制决策前的实物可行性判断。",
    "年度资料.检索.检索池": "设计思路：检索池是「待办购物车」——多条件检索结果可暂存，批量转入借出/出入网，降低重复勾选成本。",
    # 实物流转
    "年度资料.实物流转": "设计思路：借还是高频主流程；申请—审批—交接—签批—办结五段清晰，状态机驱动待办与逾期提醒。",
    "年度资料.实物流转.借出申请": "设计思路：借出申请按介质与用途分流（提档/复制等），强制关联检索结果或立档事实，杜绝无根借出。",
    "年度资料.实物流转.借出审批": "设计思路：审批节点可配置；密级与去向决定签批深度，系统只固化「谁何时同意」，亲笔签仍以纸质为准。",
    "年度资料.实物流转.借出前后比较": "设计思路：用前后视图对照提档/复制对容器与份数的不同影响，让办理人一眼看见「借出到底改了什么」。",
    "年度资料.实物流转.归还申请（正常归还）": "设计思路：完好归还走短路径，快速核销借出占用；归还目标盒可指定，支撑「物归原位或合理归位」。",
    "年度资料.实物流转.归还申请（非正常归还）": "设计思路：灭失/缺损单独分流，要求更完整说明与更严审批，防止非正常情况被「正常归还」冲销。",
    "年度资料.实物流转.流转台账": "设计思路：流转台账沉淀借还全链路，既是催还依据，也是迎检时「来龙去脉」的证据链。",
    # 出入网
    "年度资料.出入网.入网申请": "设计思路：入网把「库内资料→服务器路径」做成审批业务，路径落点受服务器路径设置约束，责任可追。",
    "年度资料.出入网.入网审批": "设计思路：入网审批关注密级、数据量与目标路径合规；办结后生成/更新在网台账，而不是只留申请单。",
    "年度资料.出入网.出网申请": "设计思路：出网明确目的地类别与接收单位，可联动后续建档，使「出网—建档」跨域链条不断档。",
    "年度资料.出入网.出网审批": "设计思路：出网审批与交接确认分离，确保「批了」不等于「交了」；附件与路径补录在办结前闭环。",
    "年度资料.出入网.在网数据台账": "设计思路：在网台账回答「网上现在有什么」；版本、路径、来源入/出网可回溯，支撑盘点与处置。",
    "年度资料.出入网.在网数据处置": "设计思路：在网数据销号/处置必须单独审批，避免管理员私下删目录导致账网不一致。",
    "年度资料.出入网.跨域流转台账": "设计思路：跨域台账串联出网转建档等任务链，让跨业务进度可盯办，而不是靠人口头同步。",
    # 盘库离库
    "年度资料.盘库与离库.模拟资料盘库": "设计思路：盘库登记丢失/拟销份数，先固化账实差异，再决定补登或进入离库，而不是直接改库存。",
    "年度资料.盘库与离库.电子资料盘库": "设计思路：电子盘库核对袋/盘状态与位置，发现损坏、错位及时锁定，避免带病继续借出。",
    "年度资料.盘库与离库.模拟资料离库处置": "设计思路：离库=正式退出库房生命周期；要求签批、现场照片与撤柜确认，满足销毁/转交合规。",
    "年度资料.盘库与离库.电子资料离库处置": "设计思路：电子离库可勾选低格留盘等实物动作，把「数据处置」与「介质处置」策略写进同一单据。",
    # 库房档案柜
    "库房与介质.档案柜.档案柜登记": "设计思路：柜体是库房数字化底座；先登记规格与布局，后谈盒袋落位，避免「有资料无柜图」。",
    "库房与介质.档案柜.档案柜登记.新增档案柜": "设计思路：新增柜体向导化采集面/层/列与画布位置，支撑开柜平面图与库房总览一致渲染。",
    "库房与介质.档案柜.开柜操作（标准滑道档案柜）": "设计思路：开柜所见即档口库存；点击档口即可查看盒袋，缩短「凭记忆找资料」路径。",
    "库房与介质.档案柜.开柜操作（防磁磁盘柜）": "设计思路：磁盘柜开柜强调介质格口占用与空位，服务空盘领取、归还入格与电子立档选位。",
    "库房与介质.档案柜.开柜操作.标识图例": "设计思路：用统一图例表达占用/空闲/用途，降低培训成本，保证不同柜型「同一套读法」。",
    "库房与介质.档案柜.开柜操作.设置档口用途": "设计思路：档口用途（年度/历史/混用/空白硬盘等）可配置，从制度上防止错柜错格混放。",
    "库房与介质.档案柜": "设计思路：档案柜模块把「登记—开柜—用途」收束为一套库房操作语言，与立档落位、迁档目标同源。",
    # 硬盘
    "库房与介质.硬盘.硬盘概览": "设计思路：硬盘概览用 KPI+状态分布做库房驾驶舱，先看全局健康度，再下钻单盘业务。",
    "库房与介质.硬盘.批量登记": "设计思路：批量登记面向到货入库高峰，表格化采集编号/序列号，减少逐盘点选的重复劳动。",
    "库房与介质.硬盘.交互式登记": "设计思路：交互式登记适合现场逐盘核验，边扫边确认，降低批量导入时的错号风险。",
    "库房与介质.硬盘.出库申请": "设计思路：硬盘出库独立成单，可关联资料业务；去向类别与是否归还决定后续催还与台账状态。",
    "库房与介质.硬盘.出库申请的待办事项": "设计思路：出库待办接入统一待办中心，审批人与资料室同一入口处理，避免「业务办了、待办还挂着」。",
    "库房与介质.硬盘.出库办理": "设计思路：办理侧重实物交接与状态切换；打印、签字、上传、办结分步确认，防止跳步导致账实漂移。",
    "库房与介质.硬盘.出库申请及办理业务流程": "设计思路：流程页把线上申请与线下签批、交接、附件、办结画成一条链，统一培训口径与内控检查点。",
    "库房与介质.硬盘.出库业务对应的数据库同步更新": "设计思路：强调「界面操作=多表一致性更新」：介质状态、位置、流水、锁同一事务落地，杜绝半成功。",
    "库房与介质.硬盘.归还申请": "设计思路：归还申请与出库对称设计，明确归还目标空白档口，保证盘回到正确格位而非「随手一搁」。",
    "库房与介质.硬盘.归还办理": "设计思路：归还办理核验盘号与状态后释放占用，流水留痕，闭环「借出必有归」的管理预期。",
    "库房与介质.硬盘.盘库登记": "设计思路：硬盘盘库支持损坏/丢失/正常等登记类型，差异先入盘库单，再决定迁档或离库，流程可审。",
    "库房与介质.硬盘.离库处置": "设计思路：硬盘离库走正式处置单，照片与签批齐备后才变更介质生命周期，满足资产与保密双重要求。",
    "库房与介质.硬盘.离库登记": "设计思路：离库登记固化处置前状态与位置快照，事后可回答「当时盘在哪、什么状态被处置」。",
    "库房与介质.硬盘": "设计思路：硬盘域覆盖登记→出库→归还→盘库→离库全寿命，与资料立档用盘、出网带盘场景衔接。",
    # 光盘
    "库房与介质.光盘": "设计思路：光盘以台账+流转流水管理，随电子立档关联；体量小于硬盘但同样要求位置与状态可查。",
}

# 前缀兜底（当标题有细微编辑差异时）
NOTES_PREFIX: list[tuple[str, str]] = [
    ("历史存档.地形图", NOTES_EXACT["历史存档.地形图图件"]),
    ("历史存档.航摄", NOTES_EXACT["历史存档.航摄胶片、像片"]),
    ("历史存档.其他", NOTES_EXACT["历史存档.其他资料"]),
    ("历史存档.资料台账与资料处置.台账", NOTES_EXACT["历史存档.资料台账与资料处置.台账"]),
    ("历史存档.资料台账与资料处置.处置", NOTES_EXACT["历史存档.资料台账与资料处置.处置"]),
    ("年度资料.建档与立档.建档审批", NOTES_EXACT["年度资料.建档与立档.建档审批"]),
    ("年度资料.建档与立档.建档申请", NOTES_EXACT["年度资料.建档与立档.建档申请"]),
    ("年度资料.建档与立档.资料立档（模拟", NOTES_EXACT["年度资料.建档与立档.资料立档（模拟介质）"]),
    ("年度资料.建档与立档.资料立档（电子", NOTES_EXACT["年度资料.建档与立档.资料立档（电子介质-1）"]),
    ("年度资料.建档与立档.资料立档", NOTES_EXACT["年度资料.建档与立档.资料立档"]),
    ("年度资料.建档与立档.立档台账", NOTES_EXACT["年度资料.建档与立档.立档台账"]),
    ("年度资料.建档与立档.存量硬盘", NOTES_EXACT["年度资料.建档与立档.存量硬盘直办立档"]),
    ("年度资料.建档与立档.存量文本", NOTES_EXACT["年度资料.建档与立档.存量文本直办立档"]),
    ("年度资料.迁档（模拟", NOTES_EXACT["年度资料.迁档（模拟资料）"]),
    ("年度资料.迁档（电子", NOTES_EXACT["年度资料.迁档（电子资料）"]),
    ("年度资料.迁档", NOTES_EXACT["年度资料.迁档（迁档台账）"]),
    ("年度资料.检索.综合", NOTES_EXACT["年度资料.检索.综合模式"]),
    ("年度资料.检索.电子", NOTES_EXACT["年度资料.检索.电子资料检索模式"]),
    ("年度资料.检索.模拟", NOTES_EXACT["年度资料.检索.模拟资料检索模式"]),
    ("年度资料.检索.检索池", NOTES_EXACT["年度资料.检索.检索池"]),
    ("年度资料.实物流转.借出申请", NOTES_EXACT["年度资料.实物流转.借出申请"]),
    ("年度资料.实物流转.借出审批", NOTES_EXACT["年度资料.实物流转.借出审批"]),
    ("年度资料.实物流转.借出前后", NOTES_EXACT["年度资料.实物流转.借出前后比较"]),
    ("年度资料.实物流转.归还申请（正常", NOTES_EXACT["年度资料.实物流转.归还申请（正常归还）"]),
    ("年度资料.实物流转.归还申请（非正常", NOTES_EXACT["年度资料.实物流转.归还申请（非正常归还）"]),
    ("年度资料.实物流转.流转台账", NOTES_EXACT["年度资料.实物流转.流转台账"]),
    ("年度资料.实物流转", NOTES_EXACT["年度资料.实物流转"]),
    ("年度资料.出入网.入网申请", NOTES_EXACT["年度资料.出入网.入网申请"]),
    ("年度资料.出入网.入网审批", NOTES_EXACT["年度资料.出入网.入网审批"]),
    ("年度资料.出入网.出网申请", NOTES_EXACT["年度资料.出入网.出网申请"]),
    ("年度资料.出入网.出网审批", NOTES_EXACT["年度资料.出入网.出网审批"]),
    ("年度资料.出入网.在网数据台账", NOTES_EXACT["年度资料.出入网.在网数据台账"]),
    ("年度资料.出入网.在网数据处置", NOTES_EXACT["年度资料.出入网.在网数据处置"]),
    ("年度资料.出入网.跨域", NOTES_EXACT["年度资料.出入网.跨域流转台账"]),
    ("年度资料.盘库与离库.模拟资料盘库", NOTES_EXACT["年度资料.盘库与离库.模拟资料盘库"]),
    ("年度资料.盘库与离库.电子资料盘库", NOTES_EXACT["年度资料.盘库与离库.电子资料盘库"]),
    ("年度资料.盘库与离库.模拟资料离库", NOTES_EXACT["年度资料.盘库与离库.模拟资料离库处置"]),
    ("年度资料.盘库与离库.电子资料离库", NOTES_EXACT["年度资料.盘库与离库.电子资料离库处置"]),
    ("库房与介质.档案柜.档案柜登记.新增", NOTES_EXACT["库房与介质.档案柜.档案柜登记.新增档案柜"]),
    ("库房与介质.档案柜.档案柜登记", NOTES_EXACT["库房与介质.档案柜.档案柜登记"]),
    ("库房与介质.档案柜.开柜操作（标准", NOTES_EXACT["库房与介质.档案柜.开柜操作（标准滑道档案柜）"]),
    ("库房与介质.档案柜.开柜操作（防磁", NOTES_EXACT["库房与介质.档案柜.开柜操作（防磁磁盘柜）"]),
    ("库房与介质.档案柜.开柜操作.标识", NOTES_EXACT["库房与介质.档案柜.开柜操作.标识图例"]),
    ("库房与介质.档案柜.开柜操作.设置", NOTES_EXACT["库房与介质.档案柜.开柜操作.设置档口用途"]),
    ("库房与介质.档案柜", NOTES_EXACT["库房与介质.档案柜"]),
    ("库房与介质.硬盘.硬盘概览", NOTES_EXACT["库房与介质.硬盘.硬盘概览"]),
    ("库房与介质.硬盘.批量登记", NOTES_EXACT["库房与介质.硬盘.批量登记"]),
    ("库房与介质.硬盘.交互式登记", NOTES_EXACT["库房与介质.硬盘.交互式登记"]),
    ("库房与介质.硬盘.出库申请的待办", NOTES_EXACT["库房与介质.硬盘.出库申请的待办事项"]),
    ("库房与介质.硬盘.出库申请及办理", NOTES_EXACT["库房与介质.硬盘.出库申请及办理业务流程"]),
    ("库房与介质.硬盘.出库业务对应", NOTES_EXACT["库房与介质.硬盘.出库业务对应的数据库同步更新"]),
    ("库房与介质.硬盘.出库申请", NOTES_EXACT["库房与介质.硬盘.出库申请"]),
    ("库房与介质.硬盘.出库办理", NOTES_EXACT["库房与介质.硬盘.出库办理"]),
    ("库房与介质.硬盘.归还申请", NOTES_EXACT["库房与介质.硬盘.归还申请"]),
    ("库房与介质.硬盘.归还办理", NOTES_EXACT["库房与介质.硬盘.归还办理"]),
    ("库房与介质.硬盘.盘库", NOTES_EXACT["库房与介质.硬盘.盘库登记"]),
    ("库房与介质.硬盘.离库处置", NOTES_EXACT["库房与介质.硬盘.离库处置"]),
    ("库房与介质.硬盘.离库登记", NOTES_EXACT["库房与介质.硬盘.离库登记"]),
    ("库房与介质.硬盘", NOTES_EXACT["库房与介质.硬盘"]),
    ("库房与介质.光盘", NOTES_EXACT["库房与介质.光盘"]),
    ("年度资料.项目信息", NOTES_EXACT["年度资料.项目信息"]),
    ("年度资料.建档与立档", NOTES_EXACT["年度资料.建档与立档"]),
    ("年度资料", NOTES_EXACT["年度资料"]),
]

FALLBACK_NOTE = "设计思路：本页展示真实办理界面；核心是把业务规则固化在操作路径中，使申请、审批、交接、台账同一口径可追溯。"


def _rgb(r: int, g: int, b: int) -> int:
    return int(r) + (int(g) << 8) + (int(b) << 16)


def _slide_title(slide) -> str:
    """取最像标题的文本：优先顶部居中短文本，其次最长的「域.功能」样式。"""
    candidates: list[tuple[float, str]] = []
    for i in range(1, slide.Shapes.Count + 1):
        sh = slide.Shapes(i)
        if sh.HasTextFrame == 0:
            continue
        text = " ".join(str(sh.TextFrame.TextRange.Text).split())
        if not text or len(text) > 80:
            continue
        # 跳过脚注类
        if "河北省第三测绘院" in text and "建设汇报" in text:
            continue
        score = 0.0
        if "." in text and not text.startswith("设计"):
            score += 50
        if sh.Top < 50:
            score += 30
        score += max(0, 40 - abs(len(text) - 20) * 0.5)
        candidates.append((score, text))
    if not candidates:
        return ""
    candidates.sort(key=lambda x: x[0], reverse=True)
    return candidates[0][1]


def _resolve_note(title: str) -> str:
    t = title.strip()
    if t in NOTES_EXACT:
        return NOTES_EXACT[t]
    for prefix, note in NOTES_PREFIX:
        if t.startswith(prefix):
            return note
    # 无标题（封面类）
    if not t:
        return NOTES_EXACT[""]
    return FALLBACK_NOTE


def _should_annotate(slide, index_1based: int) -> bool:
    if index_1based < PART4_FIRST or index_1based > PART4_LAST:
        return False
    title = _slide_title(slide)
    # 章节/叙事页：通常无图或标题在 SKIP 列表
    has_pic = False
    for i in range(1, slide.Shapes.Count + 1):
        # msoPicture = 13, msoLinkedPicture = 11
        t = slide.Shapes(i).Type
        if t in (13, 11):
            has_pic = True
            break
    if title in SKIP_TITLES:
        return False
    # 叙事纯文字页（建设汇报风格大标题）也跳过
    if not has_pic and title in {
        "三、历史存档管理",
        "四、年度资料全生命周期",
        "双轨立档：模拟盒 + 电子袋",
        "实物流转：借出与归还",
        "出入网与在网数据",
        "盘库与离库处置",
        "五、库房与介质",
    }:
        return False
    # 有图或带「域.功能」命名的界面页
    if has_pic or ("." in title and not title.startswith("第")):
        return True
    # 流程说明页（无图但属于界面图拷贝）
    if title.startswith("库房与介质.") or title.startswith("年度资料.") or title.startswith("历史存档."):
        return True
    return False


def _shrink_content_for_band(slide, band_h_pt: float, side_pad_pt: float = 10.0) -> None:
    """将现有形状缩放到顶部区域，为底部讲解带留空。"""
    sw = float(slide.Parent.PageSetup.SlideWidth)
    sh = float(slide.Parent.PageSetup.SlideHeight)
    target_w = sw - 2 * side_pad_pt
    target_h = sh - band_h_pt - 8.0
    if slide.Shapes.Count == 0:
        return

    # 计算包围盒
    min_l = min_t = 1e18
    max_r = max_b = -1e18
    shapes = []
    for i in range(1, slide.Shapes.Count + 1):
        sh_obj = slide.Shapes(i)
        shapes.append(sh_obj)
        min_l = min(min_l, float(sh_obj.Left))
        min_t = min(min_t, float(sh_obj.Top))
        max_r = max(max_r, float(sh_obj.Left) + float(sh_obj.Width))
        max_b = max(max_b, float(sh_obj.Top) + float(sh_obj.Height))

    content_w = max(max_r - min_l, 1.0)
    content_h = max(max_b - min_t, 1.0)
    scale = min(target_w / content_w, target_h / content_h, 1.0)
    # 略微再收一点，避免贴边
    scale *= 0.98

    new_w = content_w * scale
    new_h = content_h * scale
    offset_x = (sw - new_w) / 2.0 - min_l * scale
    # 顶部稍留空，标题不顶边
    offset_y = 6.0 - min_t * scale

    for sh_obj in shapes:
        left = float(sh_obj.Left) * scale + offset_x
        top = float(sh_obj.Top) * scale + offset_y
        width = float(sh_obj.Width) * scale
        height = float(sh_obj.Height) * scale
        try:
            sh_obj.Left = left
            sh_obj.Top = top
            sh_obj.Width = width
            sh_obj.Height = height
        except Exception:
            # 部分组合/表可能锁尺寸，尽量只平移
            try:
                sh_obj.Left = left
                sh_obj.Top = top
            except Exception:
                pass


def _add_design_band(slide, note: str, band_h_pt: float) -> None:
    sw = float(slide.Parent.PageSetup.SlideWidth)
    sh = float(slide.Parent.PageSetup.SlideHeight)
    top = sh - band_h_pt

    # 浅底
    bg = slide.Shapes.AddShape(1, 0, top, sw, band_h_pt)  # msoShapeRectangle
    bg.Fill.Solid()
    bg.Fill.ForeColor.RGB = _rgb(0xF4, 0xF7, 0xF7)
    bg.Line.Visible = 0

    # 顶部分隔线（青绿细条）
    line = slide.Shapes.AddShape(1, 0, top, sw, 3.0)
    line.Fill.Solid()
    line.Fill.ForeColor.RGB = _rgb(0x39, 0x7E, 0x7A)
    line.Line.Visible = 0

    # 左侧色块标签
    tag_w = 78.0
    tag = slide.Shapes.AddShape(1, 0, top + 3.0, tag_w, band_h_pt - 3.0)
    tag.Fill.Solid()
    tag.Fill.ForeColor.RGB = _rgb(0x2A, 0x5F, 0x5C)
    tag.Line.Visible = 0
    tag.TextFrame.TextRange.Text = "设计\r思路"
    tag.TextFrame.TextRange.Font.Name = "Microsoft YaHei"
    tag.TextFrame.TextRange.Font.Size = 11
    tag.TextFrame.TextRange.Font.Bold = True
    tag.TextFrame.TextRange.Font.Color.RGB = _rgb(255, 255, 255)
    tag.TextFrame.MarginLeft = 4
    tag.TextFrame.MarginRight = 4
    tag.TextFrame.MarginTop = 8
    tag.TextFrame.WordWrap = True
    try:
        tag.TextFrame.VerticalAnchor = 3  # msoAnchorMiddle
    except Exception:
        pass

    # 说明文字
    box = slide.Shapes.AddTextbox(1, tag_w + 10, top + 8, sw - tag_w - 24, band_h_pt - 14)
    tr = box.TextFrame.TextRange
    # 去掉可能重复的「设计思路：」前缀，标签已表达
    body = note
    body = re.sub(r"^设计思路[：:]\s*", "", body)
    tr.Text = body
    tr.Font.Name = "Microsoft YaHei"
    tr.Font.Size = 12
    tr.Font.Color.RGB = _rgb(0x1F, 0x2A, 0x37)
    box.TextFrame.WordWrap = True
    try:
        box.TextFrame.VerticalAnchor = 3
    except Exception:
        pass


def build() -> Path:
    if not SRC.exists():
        raise FileNotFoundError(SRC)
    if OUT.exists():
        try:
            OUT.unlink()
        except OSError as exc:
            raise RuntimeError(f"请先关闭输出文件：{OUT}") from exc

    app = win32com.client.Dispatch("PowerPoint.Application")
    try:
        app.Visible = True
    except Exception:
        pass

    prs = None
    try:
        # 复制源再改，保留合成版1不动
        prs = app.Presentations.Open(str(SRC), WithWindow=False, ReadOnly=False)
        prs.SaveAs(str(OUT))
        prs.Close()
        prs = app.Presentations.Open(str(OUT), WithWindow=False, ReadOnly=False)

        band_h = 58.0  # points ≈ 0.8 inch
        annotated = 0
        skipped = 0
        details: list[str] = []

        for idx in range(PART4_FIRST, min(PART4_LAST, prs.Slides.Count) + 1):
            slide = prs.Slides(idx)
            title = _slide_title(slide)
            if not _should_annotate(slide, idx):
                skipped += 1
                details.append(f"{idx:03d} SKIP | {title or '(无标题)'}")
                continue
            note = _resolve_note(title)
            _shrink_content_for_band(slide, band_h_pt=band_h)
            _add_design_band(slide, note, band_h_pt=band_h)
            annotated += 1
            details.append(f"{idx:03d} OK   | {title or '(无标题)'} | {note[:40]}…")

        prs.Save()
        log_path = ROOT / "annotate_part4_design_notes.log.txt"
        log_path.write_text("\n".join(details), encoding="utf-8")
        print(f"Wrote {OUT}")
        print(f"annotated={annotated} skipped={skipped} log={log_path.name}")
        return OUT
    finally:
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
