namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 台账简介文案：业务范围、关联对象、写入逻辑。
    /// </summary>
    public static class LedgerIntroCopySupport
    {
        /// <summary>单份台账简介内容。</summary>
        public sealed record Content(
            string Title,
            string Scope,
            string RelatedObjects,
            string WriteLogic);

        /// <summary>按种类取简介内容。</summary>
        public static Content Get(LedgerIntroKind kind) => kind switch
        {
            LedgerIntroKind.Filing => Filing,
            LedgerIntroKind.Relocation => Relocation,
            LedgerIntroKind.Circulation => Circulation,
            LedgerIntroKind.CrossDomain => CrossDomain,
            LedgerIntroKind.HardDisk => HardDisk,
            LedgerIntroKind.OpticalDisc => OpticalDisc,
            LedgerIntroKind.TopoMap => TopoMap,
            LedgerIntroKind.AerialPhoto => AerialPhoto,
            LedgerIntroKind.OtherMap => OtherMap,
            LedgerIntroKind.OnNetAsset => OnNetAsset,
            LedgerIntroKind.HistoryArchiveBox => HistoryArchiveBox,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的台账简介种类。")
        };

        private static readonly Content Filing = new(
            Title: "立档台账简介",
            Scope: "展示年度资料已完成立档后的当前台账快照：按立档编号中的档案年度浏览模拟盒、电子袋内各立档事实，支持筛选、详情与导出。"
                + "本页不包含历史存档（地形图/航摄/其他图件），也不替代迁档、借还、出入网等履历查询。",
            RelatedObjects: "主数据：立档事实（YearlyArchiveFilingFacts）。"
                + " 关联：建档申请与登记介质明细、年度档案盒/电子立档单元、模拟份数快照；详情区可查看流转履历与出库流程节点。"
                + " 与历史资料无实体关联。",
            WriteLogic: "本页为只读查询。"
                + " 立档事实在资料立档（含存量直办）办结时写入；其后由借还、迁档、盘库、离库等业务回写生命周期、当前位置与份数等字段。"
                + " 本页本身不新建或修改台账行。");

        private static readonly Content Relocation = new(
            Title: "迁档台账简介",
            Scope: "展示年度资料「整盒/整袋迁档」产生的履历：柜位与容器编号变更、并入同项目容器、迁入空白硬盘/光盘等。"
                + " 不含历史存档迁档，也不含资料借还出库/归还流水（见流转台账）。",
            RelatedObjects: "主数据：资料流转履历（YearlyArchiveMaterialTransactions），类型为迁档（Relocation）。"
                + " 关联：立档事实、迁档单（YearlyArchiveRelocationRecords）及其明细；容器为年度档案盒或电子立档单元。"
                + " 电子迁档还可能同步回写关联硬盘/光盘台账的存放位置。",
            WriteLogic: "本页为只读查询。"
                + " 在模拟/电子介质资料迁档办理成功时写入履历；并档或迁入空盒时可能将源盒标为销号（Retired）。"
                + " 本页不发起迁档业务。");

        private static readonly Content Circulation = new(
            Title: "流转台账简介",
            Scope: "展示年度资料实物借还相关的流转履历：资料出库办结与资料归还办结。"
                + " 不含立档写入、迁档、盘库、离库及跨域类履历（立档转入网、出网转立档，见跨域流转台账）。"
                + " 另可查看尚未发生借还流转的在库容器视图。",
            RelatedObjects: "主数据：资料流转履历，类型为资料出库（Outbound）与资料归还（Return）。"
                + " 关联：立档事实、出库单/归还单及同步条目；可下钻出库申请流程节点（预订/撤回等，属流程节点而非实物流转）。",
            WriteLogic: "本页为只读查询。"
                + " 出库提档办结、归还办结时由资料流转服务写入履历，并回写立档事实生命周期与位置；电子介质可伴生回写光盘/硬盘台账。"
                + " 本页不办理借还。");

        private static readonly Content CrossDomain = new(
            Title: "跨域流转台账简介",
            Scope: "汇总跨域流转履历：①「立档转入网」（资料室 → 生产网，形成在网资产；为复制入网，原件仍在资料室，保管关系不变）；"
                + "②「出网转立档」（生产网出网办结转建档后完成资料室立档）。"
                + " 不含院内实物借还（流转台账），也不含纯介质硬盘出库与在网数据处置。",
            RelatedObjects: "主数据：资料流转履历，类型为立档转入网（NetworkInboundCopy）或出网转立档（NetworkOutboundFiling）。"
                + " 关联：立档事实；入网侧关联入网明细与在网台账；出网侧关联出网申请单。"
                + " 跨域路径列按类型显示方向（立档转入网：资料室→生产网；出网转立档：生产网→资料室）。",
            WriteLogic: "本页为只读查询。"
                + " 入网办结写入「立档转入网」履历（复制、原件仍在资料室）；出网来源建档在立档办结时写入「出网转立档」履历（替代普通立档履历）。"
                + " 本页不发起入网/出网或立档业务。");

        private static readonly Content HardDisk = new(
            Title: "硬盘台账简介",
            Scope: "展示硬盘介质的当前状态台账：介质状态、属性（空白/资料）、存放档口、持有人、是否需归还等。"
                + " 可下钻查看该盘的出入库流转流水。"
                + " 本页管载体本身，不是年度立档事实列表，也不是历史图件台账。",
            RelatedObjects: "主数据：硬盘台账（HardDiskLedgers）与硬盘介质（HardDiskMedia）一对一。"
                + " 关联：硬盘出库申请、归还、盘库登记、离库处置、征用锁；年度电子立档单元可通过介质链接占用数据盘。"
                + " 已挂资料袋的硬盘一般不再走独立硬盘离库。",
            WriteLogic: "初始登记时创建台账；出库/归还/盘库/离库办结、以及年度电子立档入袋、资料迁档换位、出网征用空盘等路径按规则回写状态与位置，并同步写入硬盘流转流水。"
                + " 状态变化、档口变化或同档口序号变化均须留痕。"
                + " 本页以查询与浏览为主，办理请走左侧硬盘子菜单。");

        private static readonly Content OpticalDisc = new(
            Title: "光盘台账简介",
            Scope: "展示数据光盘的当前状态台账：状态、存放位置、持有人、是否需归还等。"
                + " 光盘不管理空白盘、无独立借还菜单；始终作为电子立档的伴生介质。"
                + " 本页不是年度立档事实列表，也不是历史图件台账。",
            RelatedObjects: "主数据：光盘台账（OpticalDiscLedgers）与光盘介质（OpticalDiscMedia）一对一。"
                + " 关联：电子立档单元及光盘链接；资料出库/归还/迁档/盘库/离库会被动回写本台账。"
                + " 档口用途可为年度或历史数据光盘专用（由柜体档口配置决定）。",
            WriteLogic: "向光盘写入资料并完成电子立档赋码时自动建介质与台账（在库·资料）。"
                + " 随后随资料出库、归还、迁档、盘库与离库处置回写状态/位置并写光盘流转流水。"
                + " 本页为查询浏览；立档与借还请走年度资料菜单。");

        private static readonly Content TopoMap = new(
            Title: "地形图台账简介",
            Scope: "历史存档中的地形图图件台账：按分类/比例尺等维护图号、图名、图幅与盒号落柜信息。"
                + " 属于历史资料体系，不进入年度立档事实，也不出现在年度立档/迁档/流转台账中。",
            RelatedObjects: "主数据：地形图（TopoMaps）。"
                + " 关联：历史档案盒（HistoryArchiveBoxes）及盒—台账链接（HistoryArchiveBoxLedgerLinks，资料类别 TopoMap）；"
                + " 离库处置单可批量将在库行标为已离库并撤柜。"
                + " 与 YearlyArchiveFilingFacts 无关联。",
            WriteLogic: "录入、编辑与 Excel 导入时写入/更新台账行，并按盒号同步维护历史档案盒实体与链接。"
                + " 历史资料离库处置办结后更新生命周期并撤柜。"
                + " 迁档只改盒实体位置时，台账行盒号投影随链接水合，不直接改业务字段语义。");

        private static readonly Content AerialPhoto = new(
            Title: "航摄影像台账简介",
            Scope: "历史存档中的航摄胶片、像片台账：测区、比例尺、摄影日期、盒内内容与盒号等。"
                + " 属于历史资料体系，与年度资料立档/借还台账相互独立。",
            RelatedObjects: "主数据：航片（AerialPhotos）。"
                + " 关联：历史档案盒及盒—台账链接（资料类别 AerialPhoto）；历史离库处置。"
                + " 与年度立档事实、在网台账无直接关联。",
            WriteLogic: "录入、编辑与 Excel 导入维护台账行，并同步历史档案盒与链接。"
                + " 离库处置办结更新生命周期并撤柜。"
                + " 本页支持浏览、检索与导出，不办理年度资料借还。");

        private static readonly Content OtherMap = new(
            Title: "其他图件台账简介",
            Scope: "历史存档中的其他资料/图件台账：资料分类、起止年度、比例尺、资料内容与盒号等。"
                + " 属于历史资料体系，不并入年度立档台账。",
            RelatedObjects: "主数据：其他图件（OtherMaps）。"
                + " 关联：历史档案盒及盒—台账链接（资料类别 OtherMap）；历史离库处置。"
                + " 与 YearlyArchiveFilingFacts、硬盘/光盘介质台账无直接关联。",
            WriteLogic: "录入、编辑与 Excel 导入维护台账行，并同步历史档案盒与链接。"
                + " 离库处置办结更新生命周期并撤柜。"
                + " 本页为历史台账维护与查询入口。");

        private static readonly Content OnNetAsset = new(
            Title: "在网台账简介",
            Scope: "展示已入生产网的在网资产当前态：资产编号、资料名称、服务器路径、密级、生命周期（在网/曾出网/已处置等）。"
                + " 入口为「年度资料 → 出入网 → 在网数据台账」；不是年度离线立档台账，也不是历史图件台账。"
                + " 库管资料总览等视图通常不包含在网资产。",
            RelatedObjects: "主数据：在网台账（NetworkOnNetAssets）。"
                + " 关联：入网明细（办结回写 OnNetAssetId）、出网明细、在网数据处置单；跨域流转台账可展示在网资产编号。"
                + " 可追溯到来源立档事实或入网/出网明细。",
            WriteLogic: "入网办结时写入在网台账；出网办结可标记曾出网等生命周期；在网数据处置办结后按处置方式注销或销毁对应资产。"
                + " 本页以查询为主；注销或销毁请使用「在网数据处置」。");

        private static readonly Content HistoryArchiveBox = new(
            Title: "历史存档盒台账简介",
            Scope: "展示历史档案盒与杂项容器台账：资料类别、盒号或存放位置、摘要、混放关联与可处置状态。"
                + " 入口为「历史存档 → 资料台账与资料处置 → 台账」；不是地形图/航摄/其他图件/杂项的明细录入页，也不办理离库审批。"
                + " 可按「在库 / 已离库」筛选；默认在库（含办理中的离库锁定）。杂项资料以单个存放容器为一行。",
            RelatedObjects: "主数据：历史档案盒（HistoryArchiveBoxes）及盒—台账链接（HistoryArchiveBoxLedgerLinks）；杂项资料（HistoryMiscMaterials）。"
                + " 关联：地形图/航片/其他图件台账行；历史存档离库处置单。"
                + " 与年度立档事实、在网台账无直接关联。",
            WriteLogic: "本页为只读查询。"
                + " 盒实体与链接由历史图件录入/导入维护；杂项由「杂项资料」录入，离库处置办结后清空存放位置。"
                + " 离库销毁/转交请使用「资料台账与资料处置 → 处置」。");
    }
}
