# Modifications / 修改说明

本项目是基于 [Moonfair/ASP_Assistant](https://github.com/Moonfair/ASP_Assistant) 修改的独立衍生版本，不是原作者发布的版本。

- 上游基线：`93b2ba8514f6591a67e4cbf5a6169dee7d065bf6`。
- 修改日期：2026-11-09。
- 当前发行版本：`v1.2.0`；仓库 `bingyunhe/asp-assistant-arknights`，同步更新程序版本、图标、截图、文档与发行说明，保留既有仓库历史及许可证。
- 保留上游许可原文于 `LICENSE.upstream`，保留原有版权、来源和第三方归属声明。修改部分使用 Apache 官方标准 2.0 正文。
- 本次以清理后的源码快照建立新仓库；不包含本机 Git 历史、日志、个人配置或用户数据。

## 主要改动

- 程序图标：按用户选定的鸿雪画框图片替换 EXE 图标与共享窗口/标题栏图标，保留原图白色背景与纵横比；ICO 包含 16～256 像素的九档尺寸，WPF 使用嵌入程序集的 PNG 资源。未更改业务功能或版本号。
- v1.2.0（2026-11-09，设置与三主题）：标题栏原亮暗按钮改为齿轮设置面板，集中放置辅助深浅色、三款正式名称的游戏主题以及多队伍追踪；保留原 `light-theme.json`，新增 `game-theme.json` 和 `multiple-lineups.json` 保存选择，默认卫戍协议终端、单队追踪。启动加载偏好，切换游戏主题立即传给商店识别；个人配置排除提交。
- 三主题识别：可露希尔保留原色识别，乌萨斯保留已验证的去蓝色增强，默认终端使用更低的名称区域。对终端真实漏读的卡牌补读精确名称行，长名字保留宽裁切、短名字可去掉右侧雪花纹理。使用现有 MaaFramework 的 [`only_rec`](https://github.com/MaaXYZ/MaaFramework/blob/main/docs/en_us/3.1-PipelineProtocol.md#ocr) 能力，没有新增依赖或模型；关闭商店后不补读。新增四张终端脱敏实拍与缩放、留边回归，并验证两张“地灵”各有独立 ID；完整名称纠正仅限实测的“斯卡菜→斯卡蒂”“复泰→复奏”。
- 本轮涉及：`GameShopTheme.cs`、`SettingsManager.cs`、`IOcrEngine.cs`、`MaaOcrEngine.cs`、`ShopReader.cs`、`App.xaml.cs`、`SidePanelWindow.xaml/.cs`、`TrackingView.xaml`、设置/商店/WPF 测试、Ban 测试引擎接口补全、四张 `shop-terminal-*.png`、`.gitignore` 和说明文档。

- 未发布的本地改动（2026-11-09）：移除装备“专属”筛选和分类，只保留全部、普通、转职，70 件装备数据不删减；适配用户提供的红褐色商店新皮肤。一次 OCR 输入保留原色名称栏，并加入交错排列的红通道增强名称区域，压低蓝色冻结干扰；小尺寸截图先放大，映射回原名称坐标、去重并保留同名多卡。局部纠正已复现的“卫成推荐信”“野繫／野紫”“云边”误读，独立读取“收起”并排除商店店主区域，避免菜单误高亮。
- 本轮涉及：`Equipment.cs`、`EquipmentBrowseViewModel.cs`、`ShopReader.cs`、`ShopReaderTests.cs`、`RegulationDataTests.cs`、`ThemeAndHighlightTests.cs`、四张 `shop-frontier-*.png`、`SHOP_SAMPLES.md` 和 `README.md`。没有更换游戏数据、增加依赖、自动发布或重启游戏/辅助。

- v1.1.1：统一项目展示名为 `ASP Assistant · Arknights`，更新 README、窗口/标题栏、文件属性、NOTICE 页首和发行标题；保留原项目名称、历史修改归属与版权声明，程序逻辑与 v1.1.0 相同。

- 更新卫戍协议·规程·序列 I 的干员、装备、盟约数据与缺失图标。
- 干员列表压缩显示头像和盟约，悬停展示普通/精锐详情。
- 修正五个页面宽度、筛选布局与悬停延迟；滚动时保留筛选展开状态。
- 本局可用盟约高亮与清除高亮；支持黑白主题及主题记忆。
- 多队伍合并追踪；追踪干员复用缩略头像、盟约与悬停详情。
- 装备名称置顶、合并普通/精锐数值并高亮、显示无精锐及转职盟约，修复等阶“全部”。
- 更新服务仅关联本仓库的 GitHub Releases；移除原作者 OSS 与 Pages 下载发布依赖。
- 重写“确认本局信息”界面的 ban 位识别：改为单张未滚动截图读数，盟约禁用状态按图标亮绿/灰色判定，不再按“最右 N 个”猜测；OCR 把相邻盟约名合并成一行（如“伊比利亚维多利亚”）时按字符位置拆开；识别改为双向——先用像素找出图标网格与每个槽位的亮/灰状态，再把 OCR 名字吸附到最近的槽位（同名多行按纵向距离判定），没读到名字的图标作为“未命名图标”上报而不是静默丢弃；识别失败不再自动滚屏重试；单实例运行并限制同一界面只触发一次识别。
- 补全盟约“绝技”（bondId `suntShip`，REGULAR，激活阈值 2）：它是无棋子归属的 noSelect 盟约，但会出现在 ban 位界面，因此 `covenants.json` 收录、`fetch_spdatabase.py` 的 `BOND_BAN` 不再排除它。
- 装备卡片图标由 72 改为 56 像素；筛选区改版：胶囊式“筛选”开关带已选数量徽标、分组标题加细分隔线、芯片选中态改为浅底＋彩色描边（浅色主题用橙色 `FilterSelected*`，深色主题用蓝色），并修复深色主题下等阶“全部”芯片文字不显示的问题。
- 自动更新加固：下载后校验 Release 里的 `SHA256SUMS.txt`（缺失或不匹配一律中止）；更新脚本改为“同盘 staging → 旧目录改名备份 → staging 改名就位 → 启动新版 → 清理备份”，任一步失败自动回滚并重启旧版；脚本改为 UTF-8 with BOM 写出（原实现用无 BOM UTF-8，Windows PowerShell 5.1 按 ANSI 解码会让中文路径/提示乱码并直接失败）；失败原因写入日志并显示在更新横幅上（原来被静默吞掉）。新增 `ASPA_UPDATE_FEED`（覆盖更新源）与 `ASPA_UPDATE_SILENT`（更新脚本不弹框）两个仅用于本地验证的环境变量。
- 筛选增强：核心盟约／其他盟约／装备「类型」各加「同时选中」复选框（勾选后行内由 OR 改为 AND）；干员与装备的等阶改为默认多选；筛选结果为空时显示提示文案（AND 模式下为“没有同时满足所选条件的…”）；干员页「截屏录入」左侧新增「重置」按钮，一键恢复全部筛选状态。另修复词条／触发时机两行空芯片在深色主题下不显示文字的问题。
- 装备页筛选新增「类型」（攻击/防御/生存/技力/经济/盟约/其他，数据来自手工维护的 `data/equipment_categories.json`，一件装备可属多类）；「类型」支持多选，选中多个时装备命中任一即显示；原「类型」栏（普通/转职）改名为「装备类别」。装备卡片在名称下方显示所属类别小标签。游戏数据源没有装备分类字段，分类由本项目自行判定，键缺失的装备归入「其他」。
- 补充数据、视图模型、WPF 布局与主题回归验证；更新说明、排除规则及发布包中的许可证。
- 2026-11-09 商店追踪：干员与装备统一进入识别，裁出名称栏放大后单次 OCR；处理合并名称、同名多卡与装备名称包含关系，按名称位置显示标记，修复漏检后残留与缩放后位置不更新。金光只作为视觉干扰，不推断角色三连或精锐。
- 游戏最小化联动收起辅助和覆盖层，恢复时重新定位，并保留用户先前手动最小化的意图；窗口轮询避免并发重入。
- 新增六张脱敏商店样本的真实 PaddleOCR 回归、四格和分辨率合成回归、商店状态与最小化状态验证。其他商店皮肤、原生超宽屏及实际游戏窗口联动仍需实机验证。
- 2026-11-09 敌人识别：改读新版右侧“敌方领袖”区域，支持断行首领和合并/重复特训类型；敌人页增加独立识别/重试、忙碌与部分识别状态，未知内容保留 OCR 文字并提示资料未收录。取消易混淆“假想敌”及短类型的泛化近似匹配，防止错误展示其他敌人资料。启动时等待两类敌人资料就绪。
- 自动本局分析与手动敌人识别串行，共用异步截图入口，临时隐藏并恢复辅助窗口及覆盖层，避免内部贴边窗口遮挡敌人区域；不修改盟约亮灰判断、禁用计算及商店识别。补充实拍原生 OCR、未知内容、空结果、状态清理和黑白主题界面测试。
- 2026-11-09 录入稳定性修复：前台及截图前后窗口位置校验，防止读取覆盖游戏的聊天窗口；开局自动触发要求标题及两个盟约区域，取消“漏读五秒重新触发”，改以明确对局画面重置下一次开局检测。最小化/失焦/缩放只暂停，手动成功录入也锁定当前开局。覆盖层不获取焦点。
- “截屏录入”及快捷键统一接入完整信息分析，支持无开局标题的局内信息面板。移除自动滚动重试，完整结果才发布 Ban、盟约和敌人更新；重试和失败保留上次结果。盟约读名局部修正实拍中的“伊利亚”漏字，亮灰判定和禁用计算规则保持不变。
- 商店星号刷新忽略小于 6 个界面像素的位移，减少 OCR 边界微动。新增局内信息页和脱敏对局标记实拍回归，原生 OCR 测试串行，避免第三方原生库首次加载的进程级字典竞争。

## 修改的上游文件

v1.2.0 发布同步更新版本配置、`README.md`、`RELEASE_NOTES.md`、`MODIFICATIONS.md` 及 `docs/images/` 的六张测试界面截图；README 使用与程序一致的鸿雪画框 PNG。

程序图标本轮新增或修改：`data/icons/aspa_logo.ico`、`data/icons/aspa_logo.png`、`src/ASPAssistant.App/Resources/Logo.xaml` 和 `src/ASPAssistant.App/ASPAssistant.App.csproj`。

v1.1.1 展示名称调整：`README.md`、`RELEASE_NOTES.md`、`MODIFICATIONS.md`、`NOTICE`、`.github/workflows/release.yml`、`src/ASPAssistant.App/ASPAssistant.App.csproj` 和 `src/ASPAssistant.App/Windows/SidePanelWindow.xaml`。

v1.1.0 发布调整：`src/ASPAssistant.App/ASPAssistant.App.csproj`、`src/ASPAssistant.Core/Services/UpdateService.cs`、`README.md`、`RELEASE_NOTES.md` 和 `MODIFICATIONS.md`。

录入稳定性本轮新增或修改：`src/ASPAssistant.Core/Services/InformationScreenState.cs`、`InformationScreenDetector.cs`、`ScreenCaptureService.cs`、`OcrScannerService.cs`、`BanScreenAnalyzer.cs`、`BanCovenantReader.cs`、`src/ASPAssistant.Core/ViewModels/EnemyViewModel.cs`、`src/ASPAssistant.Core/Interop/User32.cs`、`src/ASPAssistant.App/App.xaml.cs`、`src/ASPAssistant.App/Views/OperatorBrowseView.xaml.cs`、`src/ASPAssistant.App/Windows/OverlayWindow.xaml`、`OverlayWindow.xaml.cs`、`src/ASPAssistant.Tests/Services/InformationScreenStateTests.cs`、`InformationRecordingTests.cs`、`NativeOcrCollection.cs`、`ShopReaderTests.cs`、`EnemyRecognizerTests.cs`、`src/ASPAssistant.Tests/ViewModels/EnemyViewModelTests.cs`、`src/ASPAssistant.Tests/Views/ThemeAndHighlightTests.cs`、`src/ASPAssistant.Tests/TestData/info-ingame-1920x1080.png`、`gameplay-markers-1920x1080.png` 和 `README.md`。

敌人流程本轮涉及：`src/ASPAssistant.Core/Services/EnemyRecognizer.cs`、`BossDataService.cs`、`EnemyTypeDataService.cs`、`BanScreenAnalyzer.cs`（仅截图入口）、`src/ASPAssistant.Core/Models/EnemyResult.cs`、`src/ASPAssistant.Core/GameModes/GarrisonProtocol/GarrisonOcrStrategy.cs`（仅敌人区域）、`src/ASPAssistant.Core/ViewModels/EnemyViewModel.cs`、`src/ASPAssistant.App/App.xaml.cs`、`src/ASPAssistant.App/Windows/SidePanelWindow.xaml.cs`、`src/ASPAssistant.App/Views/EnemyView.xaml`、`EnemyView.xaml.cs`、`src/ASPAssistant.Tests/Services/EnemyRecognizerTests.cs`、`src/ASPAssistant.Tests/ViewModels/EnemyViewModelTests.cs`、`src/ASPAssistant.Tests/Views/ThemeAndHighlightTests.cs` 和 `README.md`。

本轮新增或修改：`src/ASPAssistant.Core/Services/ShopReader.cs`、`OcrScannerService.cs`、`ShopStateStabilizer.cs`、`WindowTrackerService.cs`、`src/ASPAssistant.Core/Models/ShopItem.cs`、`src/ASPAssistant.App/App.xaml.cs`、`src/ASPAssistant.App/Windows/OverlayWindow.xaml.cs`、`SidePanelWindow.xaml.cs`、`src/ASPAssistant.Tests/Services/ShopReaderTests.cs`、`ShopStateStabilizerTests.cs`、`src/ASPAssistant.Tests/Views/ThemeAndHighlightTests.cs`、`src/ASPAssistant.Tests/TestData/shop-*.png`、`src/ASPAssistant.Tests/TestData/SHOP_SAMPLES.md` 和 `README.md`。

以下文件均相对上述上游基线作过修改。源码和构建文件还包含文件内变更声明，JSON 根对象包含 `_modificationNotice` 元数据。

- `README.md` — 重写本衍生版本说明及上游署名。
- `LICENSE` — 改用 Apache 官方标准 2.0 正文；上游许可原文逐字保存至 `LICENSE.upstream`，没有删除上游许可和版权声明。
- `.github/workflows/release.yml`
- `.gitignore`
- `data/covenants.json`
- `data/equipment.json`
- `data/equipment_categories.json`（新增：装备功能分类，手工维护，抓取脚本不覆盖）
- `data/fetch_spdatabase.py`
- `data/operators.json`
- `src/ASPAssistant.App/ASPAssistant.App.csproj`
- `src/ASPAssistant.App/App.xaml`
- `src/ASPAssistant.App/App.xaml.cs`
- `src/ASPAssistant.App/Controls/EquipmentCard.xaml`
- `src/ASPAssistant.App/Controls/EquipmentCard.xaml.cs`
- `src/ASPAssistant.App/Controls/OperatorCard.xaml`
- `src/ASPAssistant.App/Controls/OperatorCard.xaml.cs`
- `src/ASPAssistant.App/Resources/Styles.xaml`
- `src/ASPAssistant.App/Views/EnemyView.xaml`
- `src/ASPAssistant.App/Views/EquipmentBrowseView.xaml`
- `src/ASPAssistant.App/Views/EquipmentBrowseView.xaml.cs`
- `src/ASPAssistant.App/Views/LineupListView.xaml`
- `src/ASPAssistant.App/Views/OperatorBrowseView.xaml`
- `src/ASPAssistant.App/Views/OperatorBrowseView.xaml.cs`
- `src/ASPAssistant.App/Views/TrackingView.xaml`
- `src/ASPAssistant.App/Views/TrackingView.xaml.cs`
- `src/ASPAssistant.App/Windows/InputDialog.xaml`
- `src/ASPAssistant.App/Windows/LineupEditorWindow.xaml`
- `src/ASPAssistant.App/Windows/LineupEditorWindow.xaml.cs`
- `src/ASPAssistant.App/Windows/SidePanelWindow.xaml`
- `src/ASPAssistant.App/Windows/SidePanelWindow.xaml.cs`
- `src/ASPAssistant.App/Windows/SimplePickerDialog.xaml`
- `src/ASPAssistant.App/Windows/UpdateDialog.xaml`
- `src/ASPAssistant.Core/Data/SettingsManager.cs`
- `src/ASPAssistant.Core/GameModes/GarrisonProtocol/GarrisonOcrStrategy.cs`
- `src/ASPAssistant.Core/Models/Equipment.cs`
- `src/ASPAssistant.Core/Services/BanScreenAnalyzer.cs`
- `src/ASPAssistant.Core/Services/OcrScannerService.cs`
- `src/ASPAssistant.Core/Services/UpdateService.cs`
- `src/ASPAssistant.Core/ViewModels/EquipmentBrowseViewModel.cs`
- `src/ASPAssistant.Core/ViewModels/OperatorBrowseViewModel.cs`
- `src/ASPAssistant.Core/ViewModels/TrackingViewModel.cs`
- `src/ASPAssistant.Tests/ASPAssistant.Tests.csproj`
- `src/ASPAssistant.Tests/Data/SettingsManagerTests.cs`
- `src/ASPAssistant.Tests/Services/WindowTrackerServiceTests.cs`
- `src/ASPAssistant.Tests/ViewModels/OperatorBrowseViewModelTests.cs`
- `src/ASPAssistant.Tests/ViewModels/TrackingViewModelTests.cs`

## 新增文件

- `NOTICE`、`MODIFICATIONS.md`：上游归属与修改说明。
- `LICENSE.upstream`：与上游原 `LICENSE` 内容逐字相同的副本。
- `RELEASE_NOTES.md`：首版独立运行包的下载、使用、功能、限制与许可证说明。
- `.gitattributes`：固定文本换行，按二进制处理图标和 OCR 模型。
- `docs/images/`：使用测试数据生成的界面截图，不包含实际游戏或个人桌面画面。
- `data/icons/equipment/一次性促销券.png`
- `data/icons/equipment/不退者之盾.png`
- `data/icons/equipment/不退者力场.png`
- `data/icons/equipment/不退者卫护系统.png`
- `data/icons/equipment/乌萨斯呼吸器.png`
- `data/icons/equipment/人事部记录.png`
- `data/icons/equipment/伊比利亚骨刺刀.png`
- `data/icons/equipment/佣兵名牌.png`
- `data/icons/equipment/动能加速器.png`
- `data/icons/equipment/动能单手弩.png`
- `data/icons/equipment/动能轰击弩.png`
- `data/icons/equipment/卫戍推荐信.png`
- `data/icons/equipment/可靠怀表.png`
- `data/icons/equipment/多功能补剂.png`
- `data/icons/equipment/天马之耀.png`
- `data/icons/equipment/定期存折.png`
- `data/icons/equipment/投机骰子.png`
- `data/icons/equipment/汲取者之喙.png`
- `data/icons/equipment/汲取者之心.png`
- `data/icons/equipment/汲取者之镰.png`
- `data/icons/equipment/灵巧涡轮手套.png`
- `data/icons/equipment/瓶装烈性反应.png`
- `data/icons/equipment/电流维式重锤.png`
- `data/icons/equipment/终极维式重锤.png`
- `data/icons/equipment/聚合急救箱.png`
- `data/icons/equipment/莱塔尼亚小鼓.png`
- `data/icons/equipment/虹彩活血饮料.png`
- `data/icons/equipment/雪祀之衣.png`
- `data/icons/equipment/驮兽储蓄罐.png`
- `data/icons/equipment/驱逐者瓦斯.png`
- `data/icons/equipment/驱逐者警棍.png`
- `data/icons/equipment/驱逐者防暴装备.png`
- `data/icons/operators/Miss.Christine.png`
- `data/icons/operators/Mon3tr.png`
- `data/icons/operators/W.png`
- `data/icons/operators/乌啾.png`
- `data/icons/operators/乌有.png`
- `data/icons/operators/云迹.png`
- `data/icons/operators/伊桑.png`
- `data/icons/operators/克莱门莎.png`
- `data/icons/operators/冻河.png`
- `data/icons/operators/卡达.png`
- `data/icons/operators/历阵锐枪芬.png`
- `data/icons/operators/可露希尔.png`
- `data/icons/operators/吉星.png`
- `data/icons/operators/塔露拉.png`
- `data/icons/operators/复奏.png`
- `data/icons/operators/夏栎.png`
- `data/icons/operators/夜莺.png`
- `data/icons/operators/奥尔佳.png`
- `data/icons/operators/奥斯塔.png`
- `data/icons/operators/安哲拉.png`
- `data/icons/operators/帕拉斯.png`
- `data/icons/operators/怒潮凛冬.png`
- `data/icons/operators/慕斯.png`
- `data/icons/operators/承曦格雷伊.png`
- `data/icons/operators/折光.png`
- `data/icons/operators/拜松.png`
- `data/icons/operators/摆渡人.png`
- `data/icons/operators/斥罪.png`
- `data/icons/operators/旅骨.png`
- `data/icons/operators/星极.png`
- `data/icons/operators/星源.png`
- `data/icons/operators/机械师.png`
- `data/icons/operators/杰克.png`
- `data/icons/operators/极光.png`
- `data/icons/operators/棘刺.png`
- `data/icons/operators/森蚺.png`
- `data/icons/operators/水灯心.png`
- `data/icons/operators/海沫.png`
- `data/icons/operators/涤火杰西卡.png`
- `data/icons/operators/淬羽赫默.png`
- `data/icons/operators/渡桥.png`
- `data/icons/operators/温蒂.png`
- `data/icons/operators/灰喉.png`
- `data/icons/operators/烈夏.png`
- `data/icons/operators/白铁.png`
- `data/icons/operators/真理.png`
- `data/icons/operators/真言.png`
- `data/icons/operators/矩.png`
- `data/icons/operators/维伊.png`
- `data/icons/operators/罗比菈塔.png`
- `data/icons/operators/老鲤.png`
- `data/icons/operators/艾丽妮.png`
- `data/icons/operators/艾雅法拉.png`
- `data/icons/operators/蓝毒.png`
- `data/icons/operators/薇薇安娜.png`
- `data/icons/operators/褐果.png`
- `data/icons/operators/贝洛内.png`
- `data/icons/operators/赫德雷.png`
- `data/icons/operators/赫拉格.png`
- `data/icons/operators/车尔尼.png`
- `data/icons/operators/逻各斯.png`
- `data/icons/operators/遥.png`
- `data/icons/operators/酒神.png`
- `data/icons/operators/铸铁.png`
- `data/icons/operators/阿米娅.png`
- `data/icons/operators/陨星.png`
- `data/icons/operators/霍尔海雅.png`
- `data/icons/operators/霜叶.png`
- `data/icons/operators/青枳.png`
- `data/icons/operators/风絮.png`
- `data/icons/operators/鸿雪.png`
- `data/icons/operators/黍.png`
- `data/icons/operators/黑键.png`
- `src/ASPAssistant.App/Converters/CovenantHighlightConverter.cs`
- `src/ASPAssistant.App/Converters/TrackedOperatorConverter.cs`
- `src/ASPAssistant.App/Resources/Themes/Dark.xaml`
- `src/ASPAssistant.App/Resources/Themes/Light.xaml`
- `src/ASPAssistant.App/ThemeManager.cs`
- `src/ASPAssistant.Core/Services/BanCovenantReader.cs`
- `src/ASPAssistant.Core/Services/BanIconBrightness.cs`
- `src/ASPAssistant.Core/Services/BanIconGrid.cs`
- `src/ASPAssistant.App/Program.cs`（启动入口、Windows 版本检查与单实例互斥）
- `src/ASPAssistant.Tests/Data/RegulationDataTests.cs`
- `src/ASPAssistant.Tests/Data/EquipmentCategoryTests.cs`
- `src/ASPAssistant.Tests/Services/UpdateServiceTests.cs`
- `src/ASPAssistant.Tests/Services/BanCovenantReaderTests.cs`
- `src/ASPAssistant.Tests/TestData/ban-sample-1920x1080.png`
- `src/ASPAssistant.Tests/Views/ThemeAndHighlightTests.cs`
