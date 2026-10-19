测绘资料管理系统 · 院领导汇报 PPT
================================

产出文件
  河北省第三测绘院-测绘资料管理系统-建设汇报.pptx          （叙事+自动截图，50页）
  测绘资料管理系统-菜单结构图.pptx                        （左侧菜单结构）
  测绘资料管理系统-界面图.pptx                            （手作界面实况）
  河北省第三测绘院-测绘资料管理系统-建设汇报-合成版.pptx  （推荐给领导：三份梳理合成）

合成版（推荐汇报用）
  需本机安装 Microsoft PowerPoint，然后执行：
    python docs/ppt-report/build_leadership_merged_pptx.py
  结构：建设总述 → 进度投入 → 菜单导航 → 业务界面实况（手作图优先）→ 内控运维 → 成效请示

本院数据（进度/投入/上线时间）
  编辑 institute_data.yaml 中带【待填】的字段后执行：
    python docs/ppt-report/build_pptx.py
    python docs/ppt-report/build_leadership_merged_pptx.py

重新截图（需本机 Debug 版 DocMgr，账号 wxl/wxl）
  python docs/ppt-report/capture_screenshots.py
  python docs/ppt-report/capture_missing.py

说明
  - 建设汇报约 50 页；合成版收录界面图全部页面（约 120+ 页，含章节分隔）
  - 面向：院领导 / 科室内部 / 信息化专项
  - 截图来自资料管理员演示账号，列表数据随库内容变化
