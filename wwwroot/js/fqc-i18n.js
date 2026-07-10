// ══════════════════════════════════════════════════════════
// FQC i18n — dictionary key/value cho 2 ngôn ngữ (zh/en)
// Dùng chung cho toàn bộ Views/FQC/*.cshtml
// ══════════════════════════════════════════════════════════
(function (global) {
    'use strict';

    const STORAGE_KEY = 'fqcLang';

    const DICT = {
        // ── Common ──────────────────────────────────────────
        labelWorkOrder:        { zh: '工单',                         en: 'Work Order' },
        labelSerialNumber:     { zh: '序列号',                       en: 'Serial Number' },
        labelStatus:           { zh: '状态',                         en: 'Status' },
        labelColor:            { zh: '颜色',                         en: 'Color' },
        labelStation:          { zh: '工位',                         en: 'Station' },
        optionAllDash:         { zh: '-- 全部 --',                   en: '-- All --' },
        backToCheck:           { zh: '← 返回检查',                    en: '← Back to Check' },
        backToScan:            { zh: '← 扫描',                       en: '← Scan' },

        // ── Scan page (FQCBP / FQC02 / FQC04) ────────────────
        ngDialogTitle:         { zh: 'NG — 选择不良代码',              en: 'NG — Select Defect Code' },
        ngSearchPlaceholder:   { zh: '按代码或名称搜索...',             en: 'Search by code or name...' },
        ngSectionDefectCode:   { zh: '不良代码',                      en: 'Defect Code' },
        ngReasonLoading:       { zh: '正在加载…',                     en: 'Loading…' },
        ngReasonEmpty:         { zh: '未找到',                        en: 'Not found' },
        ngSelectedLabel:       { zh: '已选择:',                       en: 'Selected:' },
        ngSectionDescription:  { zh: '描述',                          en: 'Description' },
        ngDescOptional:        { zh: '（可选）',                      en: '(optional)' },
        ngDescPlaceholder:     { zh: '请输入更多描述...',              en: 'Additional details (optional)...' },
        btnSkip:               { zh: '跳过',                          en: 'Skip' },
        btnConfirmNg:          { zh: '✕ 确认不良',                    en: '✕ Confirm NG' },
        ngHintNumberKeys:      { zh: '数字键',                        en: 'Number keys' },
        ngHintSelect:          { zh: '从列表中选择',                   en: 'select from the list' },
        ngHintEnter:           { zh: '回车',                          en: 'Enter' },
        ngHintConfirm:         { zh: '确认',                          en: 'confirm' },
        ngHintEsc:             { zh: '退出',                          en: 'Esc' },
        ngHintSkipLower:       { zh: '跳过',                          en: 'skip' },

        woAutoFillPlaceholder: { zh: '自动填充',                       en: 'Auto-filled from serial...' },
        snPlaceholder:         { zh: '请输入序列号',                    en: 'Scan Serial Number...' },
        snErrorMsg:            { zh: '⚠ 序列号无效 — 必须为15位字符',    en: '⚠ Invalid SN — must be 15 characters' },
        statusPlaceholder:     { zh: '扫描状态',                        en: 'Scan PASS or NG...' },
        statusErrorMsg:        { zh: '⚠ 状态无效 — 必须为 PASS 或 NG',   en: '⚠ Invalid status — must be PASS or NG' },
        hintNextField:         { zh: '下一栏',                          en: 'Next field' },
        hintClearReset:        { zh: '清除并重置',                      en: 'Clear & reset' },
        woNotEntered:          { zh: '未输入',                          en: 'Not Entered' },
        labelQtyInspection:    { zh: '检验数量',                        en: 'Qty Inspection' },
        circleNotScanned:      { zh: '未扫描',                          en: 'Not scanned' },
        woStatusLooking:       { zh: '查找工单中…',                     en: 'Looking up WO…' },
        woStatusNotFound:      { zh: '✕ 找不到工单',                    en: '✕ WO not found' },
        toastWoNotFoundTitle:  { zh: '未找到工单',                       en: 'WO Not Found' },
        toastWoNotFoundMsg:    { zh: '找不到对应工单。',                  en: 'WO not found in Odoo.' },
        toastDuplicateTitle:   { zh: '序列号重复',                       en: 'Duplicate SN' },
        toastDuplicateMsg:     { zh: '已检查，请扫描其他。',               en: 'Already scanned.' },
        toastStationBlockedTitle: { zh: '工位管控',                     en: 'Station Control' },
        toastStationBlockedMsg:   { zh: '未在以下工位输入：',             en: 'Not yet input at:' },

        // ── History pages (FQCBPH / FQC02H / FQC04H) ─────────
        historyTitleGeneral:  { zh: 'FQC 检查历史',                     en: 'FQC Inspection History' },
        historySubGeneral:    { zh: '所有FQC序列号检查结果',              en: 'All FQC Serial Number Check Results' },
        historyTitle02:       { zh: 'FQC02 检查历史',                    en: 'FQC02 Inspection History' },
        historySub02:         { zh: '半成品 FQC02 工位序列号检查结果',     en: 'Semi-finished FQC02 Station Serial Number Check Results' },
        historyTitle04:       { zh: 'FQC04 检查历史',                    en: 'FQC04 Inspection History' },
        historySub04:         { zh: '半成品 FQC04 工位序列号检查结果',     en: 'Semi-finished FQC04 Station Serial Number Check Results' },
        statTotal:            { zh: '总数',                             en: 'TOTAL' },
        statPass:             { zh: '通过',                             en: 'PASS' },
        statNg:               { zh: '不良',                             en: 'NG' },
        statPassRate:         { zh: '通过率',                           en: 'PASS RATE' },
        filterPlaceholderWo:  { zh: '搜索工单',                          en: 'Search WO...' },
        filterPlaceholderSN:  { zh: '搜索序列号',                        en: 'Search SN...' },
        filterLabelNgCode:    { zh: '不良代码/NGCode',                    en: 'NG Code' },
        filterPlaceholderNgCode: { zh: '筛选不良代码…',                   en: 'Filter Defect Code…' },
        filterLabelDateFrom:  { zh: '开始日期',                          en: 'From Date' },
        filterLabelDateTo:    { zh: '结束日期',                          en: 'To Date' },
        btnFilter:            { zh: '筛选',                             en: 'Filter' },
        btnReset:             { zh: '重置',                             en: 'Reset' },
        btnExport:            { zh: '↓ 导出',                           en: '↓ Export' },
        tableColTime:         { zh: '时间 (UTC+8)',                     en: 'TIME (UTC+8)' },
        tableColWO:           { zh: '工单',                             en: 'WORK ORDER' },
        tableColStation:      { zh: '工位',                             en: 'STATION' },
        tableColSN:           { zh: '序列号',                           en: 'SERIAL NUMBER' },
        tableColColor:        { zh: '颜色',                             en: 'COLOR' },
        tableColStatus:       { zh: '状态',                             en: 'STATUS' },
        tableColNgCode:       { zh: '不良代码',                         en: 'NG Code' },
        tableColReason:       { zh: 'NG 原因',                          en: 'REASON' },
        tableColDesc:         { zh: '描述',                             en: 'DESCRIPTION' },
        emptyRecords:         { zh: '📭 未找到记录',                     en: '📭 No records found' },
        noRecordsShort:       { zh: '未找到记录',                        en: 'No records found' },
        showingLabel:         { zh: '显示',                             en: 'Showing' },
        recordsLabel:         { zh: '条记录',                           en: 'records' },

        // ── Dashboard (FQCBPR) ────────────────────────────────
        rptTitle:             { zh: '📊 FQC 报告',                       en: '📊 FQC Report' },
        rptSub:               { zh: 'FQC 质量报告（最终质量控制）',        en: 'FQC Quality Report' },
        rptExport:            { zh: '导出 Excel',                        en: 'Export Excel' },
        rptHistory:           { zh: '📋 历史记录',                       en: '📋 History' },
        rptFilterTitle:       { zh: '🔍 筛选',                           en: '🔍 Filter' },
        rptFilterWo:          { zh: 'Work Order',                       en: 'Work Order' },
        rptFilterFrom:        { zh: '开始日期',                          en: 'From' },
        rptFilterTo:          { zh: '结束日期',                          en: 'To' },
        rptFilterNgCode:      { zh: '不良代码',                          en: 'NG Code' },
        rptFilterStation:     { zh: '工位',                             en: 'Station' },
        rptFilterColor:       { zh: '颜色',                             en: 'Color' },
        rptAllStations:       { zh: '全部工位',                          en: 'All Stations' },
        rptAllColors:         { zh: '全部颜色',                          en: 'All Colors' },
        rptApply:             { zh: '▶ 应用',                           en: '▶ Apply' },
        rptResetFilter:       { zh: '↺ 重置',                           en: '↺ Reset' },
        kpiTotal:             { zh: '总扫描',                            en: 'Total Scanned' },
        kpiTotalSub:          { zh: '总扫描数',                          en: 'Total scans' },
        kpiPass:              { zh: '合格',                             en: 'Pass' },
        kpiNg:                { zh: '不良',                             en: 'NG' },
        kpiWo:                { zh: '工单',                             en: 'Work Orders' },
        kpiWoSub:             { zh: '唯一工单数',                        en: 'Unique WOs' },
        kpiNgCode:            { zh: '不良代码',                          en: 'NG Codes' },
        kpiNgCodeSub:         { zh: '唯一不良类型',                       en: 'Unique NG types' },
        chartTrendTitle:      { zh: '📈 每日 NG / PASS 趋势',             en: '📈 Daily NG / PASS Trend' },
        chartTrendSub:        { zh: '每日 NG / PASS 数量变化',            en: 'Daily NG / PASS over time' },
        toggleAllWo:          { zh: '全部工单',                          en: 'All WO' },
        toggleByWo:           { zh: '按工单',                            en: 'By WO' },
        chartDonutTitle:      { zh: '🍩 合格率',                         en: '🍩 PASS / NG Ratio' },
        chartDonutSub:        { zh: '整体合格率',                        en: 'Overall pass rate' },
        chartWoBarTitle:      { zh: '📦 按工单的合格 / 不良数量',          en: '📦 PASS / NG by Work Order' },
        chartWoBarSub:        { zh: '不良最多的工单',                     en: 'Top WOs with highest NG' },
        chartParetoTitle:     { zh: '🔴 不良代码分析（帕累托）',            en: '🔴 NG Code Analysis (Pareto)' },
        chartParetoSub:       { zh: '数量 + 累计百分比（按不良代码）',       en: 'Count + cumulative % by NG code' },
        chartWoTrendTitle:    { zh: '📉 各工单不良趋势（前5个工单）',        en: '📉 NG Trend by Work Order (Top 5)' },
        chartWoTrendSub:      { zh: '每个工单每日不良数量',                 en: 'Daily NG count per WO' },
        chartStackedTitle:    { zh: '🧱 各工单不良代码分布',                en: '🧱 NG Code Breakdown by Work Order' },
        chartStackedSub:      { zh: '堆叠图 – 每个工单的缺陷分布',           en: 'Stacked – defect distribution per WO' },
        chartRankTitle:       { zh: '🏆 不良代码排行榜',                   en: '🏆 NG Code Ranking' },
        chartRankSub:         { zh: '不良原因排行（带进度条）',              en: 'NG reason ranking with progress bar' },
        chartHeatmapTitle:    { zh: '🌡️ 按小时 × 星期的不良热力图',         en: '🌡️ NG Heatmap by Hour × Day' },
        chartHeatmapSub:      { zh: '查找不良较高的班次',                   en: 'Identify high NG shifts' },
        chartDetailTitle:     { zh: '📋 按工单不良明细',                    en: '📋 NG Detail by Work Order' },
        chartDetailSub:       { zh: '不良明细（按工单分组）',                en: 'NG Details grouped by Work Order' },
        detailSearchPlaceholder: { zh: '🔍 搜索工单 / SN / 代码...',        en: '🔍 Search WO / SN / Code...' },
        detailAllWo:          { zh: '全部工单',                            en: 'All WO' },
        exportingLabel:       { zh: '正在导出...',                         en: 'Exporting...' },
        pleaseLoadDataFirst:  { zh: '请先加载数据后再导出！',                 en: 'Please load data before exporting!' },
        exportFailedMsg:      { zh: '导出 Excel 失败，请重试。',              en: 'Excel export failed. Please try again.' },
    };

    function getLang() {
        return localStorage.getItem(STORAGE_KEY) || 'zh';
    }

    function t(key) {
        const entry = DICT[key];
        if (!entry) return key;
        const lang = getLang();
        return entry[lang] ?? entry.zh ?? key;
    }

    function apply(root) {
        root = root || document;
        const lang = getLang();

        root.querySelectorAll('[data-i18n]').forEach(function (el) {
            el.textContent = t(el.getAttribute('data-i18n'));
        });
        root.querySelectorAll('[data-i18n-placeholder]').forEach(function (el) {
            el.setAttribute('placeholder', t(el.getAttribute('data-i18n-placeholder')));
        });
        root.querySelectorAll('[data-i18n-html]').forEach(function (el) {
            el.innerHTML = t(el.getAttribute('data-i18n-html'));
        });

        document.querySelectorAll('.lang-flag').forEach(function (f) {
            f.classList.toggle('active', f.getAttribute('data-lang') === lang);
        });
    }

    function setLang(lang) {
        localStorage.setItem(STORAGE_KEY, lang);
        apply(document);
        document.dispatchEvent(new CustomEvent('fqc-lang-changed', { detail: { lang: lang } }));
    }

    document.addEventListener('DOMContentLoaded', function () { apply(document); });

    global.fqcI18n = { t: t, apply: apply, setLang: setLang, getLang: getLang };
})(window);
