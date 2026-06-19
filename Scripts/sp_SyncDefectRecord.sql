-- ============================================================
-- MIGRATION: chạy 1 lần duy nhất để thêm cột Item_code
-- (bỏ qua nếu cột đã tồn tại)
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('SM_FQCBP_H') AND name = 'Item_code'
)
    ALTER TABLE SM_FQCBP_H ADD Item_code NVARCHAR(200) NULL;
GO


-- ============================================================
-- BACKFILL cho dữ liệu cũ (chưa có Item_code)
-- Điền thủ công theo WorkOrder / ngày
-- Ví dụ: toàn bộ NG ngày 17/6 thuộc WO NM/MO/02414 → RM15A-1000NW
-- ============================================================
/*
UPDATE SM_FQCBP_H
SET    Item_code = 'RM15A-1000NW'       -- thay đúng item code
WHERE  Status  = 'NG'
  AND  Item_code IS NULL
  AND  WorkOrder LIKE '%NM/MO/02414%'   -- thay đúng WO
  AND  FORMAT(Timeline, 'yyyyMMdd') = '20260617';
*/


-- ============================================================
-- [1] sp_SyncDefectRecord
-- Upsert 1 dòng vào SVN_Defect_Record
-- ============================================================
CREATE OR ALTER PROCEDURE sp_SyncDefectRecord
    @ItemCode    NVARCHAR(200),
    @NgCode      NVARCHAR(50),
    @Date        NVARCHAR(8)   = NULL,   -- yyyyMMdd, NULL = hôm nay
    @Operation   NVARCHAR(200) = NULL    -- NULL → tự tìm trong SVN_target
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @INSDatetime NVARCHAR(8) =
        ISNULL(@Date, FORMAT(GETDATE(), 'yyyyMMdd'));

    IF @Operation IS NULL
    BEGIN
        SELECT TOP 1 @Operation = Operation
        FROM   SVN_target
        WHERE  Date_time = @INSDatetime
          AND  Operation LIKE '%' + @ItemCode + '%';
    END

    MERGE SVN_Defect_Record AS target
    USING (
        SELECT @ItemCode    AS Item_code,
               @NgCode      AS Defect_Code,
               @INSDatetime AS INSDatetime
    ) AS source
        ON  target.Item_code   = source.Item_code
        AND target.Defect_Code = source.Defect_Code
        AND target.INSDatetime = source.INSDatetime
    WHEN MATCHED THEN
        UPDATE SET
            Qty_NG    = ISNULL(target.Qty_NG, 0) + 1,
            Operation = ISNULL(@Operation, target.Operation)
    WHEN NOT MATCHED THEN
        INSERT (Item_code, Defect_Code, Qty_NG, INSDatetime, Operation, Employer_code, Employer_name)
        VALUES (@ItemCode, @NgCode, 1, @INSDatetime, @Operation, NULL, NULL);

    SELECT *
    FROM   SVN_Defect_Record
    WHERE  Item_code   = @ItemCode
      AND  Defect_Code = @NgCode
      AND  INSDatetime = @INSDatetime;
END;
GO


-- ============================================================
-- [2] sp_FullSyncDefectByDate
-- Full MERGE (INSERT + UPDATE) từ SM_FQCBP_H → SVN_Defect_Record
-- Điều kiện: SM_FQCBP_H.Item_code phải đã được điền
--   - Tự động: backend điền sau mỗi NG scan (từ phiên bản này)
--   - Thủ công: dùng script BACKFILL ở trên cho dữ liệu cũ
-- ============================================================
CREATE OR ALTER PROCEDURE sp_FullSyncDefectByDate
    @Date NVARCHAR(8) = NULL   -- yyyyMMdd, NULL = hôm nay
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @INSDatetime NVARCHAR(8) =
        ISNULL(@Date, FORMAT(GETDATE(), 'yyyyMMdd'));

    -- ── Diagnostic: xem dữ liệu thô trong SM_FQCBP_H ──────────
    SELECT
        h.Item_code,
        h.NgCode      AS Defect_Code,
        COUNT(*)      AS Qty_thực_tế,
        CASE WHEN h.Item_code IS NULL THEN '⚠ Item_code trống — gọi API /FQC/backfillDefect?date=' + @INSDatetime
             ELSE 'OK'
        END AS Ghi_chu
    FROM  SM_FQCBP_H h
    WHERE h.Status = 'NG'
      AND CONVERT(VARCHAR(8), h.Timeline, 112) = @INSDatetime
    GROUP BY h.Item_code, h.NgCode
    ORDER BY h.Item_code, h.NgCode;

    -- ── MERGE: chỉ xử lý dòng đã có Item_code ────────────────
    ;WITH NgData AS (
        SELECT
            h.Item_code,
            h.NgCode                                 AS Defect_Code,
            CONVERT(VARCHAR(8), h.Timeline, 112)     AS ScanDate,
            COUNT(*)                                 AS TotalCount
        FROM  SM_FQCBP_H h
        WHERE h.Status    = 'NG'
          AND h.Item_code IS NOT NULL
          AND CONVERT(VARCHAR(8), h.Timeline, 112) = @INSDatetime
        GROUP BY h.Item_code, h.NgCode, CONVERT(VARCHAR(8), h.Timeline, 112)
    )
    MERGE SVN_Defect_Record AS target
    USING NgData AS source
        ON  target.Item_code   = source.Item_code
        AND target.Defect_Code = source.Defect_Code
        AND target.INSDatetime = source.ScanDate
    WHEN MATCHED THEN
        UPDATE SET
            Qty_NG    = source.TotalCount,
            Operation = ISNULL(
                            target.Operation,
                            (SELECT TOP 1 Operation FROM SVN_target
                             WHERE  Date_time = source.ScanDate
                               AND  Operation LIKE '%' + source.Item_code + '%')
                        )
    WHEN NOT MATCHED THEN
        INSERT (Item_code, Defect_Code, Qty_NG, INSDatetime, Operation, Employer_code, Employer_name)
        VALUES (
            source.Item_code,
            source.Defect_Code,
            source.TotalCount,
            source.ScanDate,
            (SELECT TOP 1 Operation FROM SVN_target
             WHERE  Date_time = source.ScanDate
               AND  Operation LIKE '%' + source.Item_code + '%'),
            NULL, NULL
        );

    -- ── Kết quả sau sync ──────────────────────────────────────
    PRINT 'Sync xong cho ngày: ' + @INSDatetime;

    SELECT
        dr.*,
        nc.Qty_thực_tế AS [Qty_trong_FQCBP_H]
    FROM SVN_Defect_Record dr
    LEFT JOIN (
        SELECT Item_code, NgCode, COUNT(*) AS Qty_thực_tế
        FROM   SM_FQCBP_H
        WHERE  Status = 'NG'
          AND  CONVERT(VARCHAR(8), Timeline, 112) = @INSDatetime
        GROUP BY Item_code, NgCode
    ) nc ON dr.Item_code    = nc.Item_code
         AND dr.Defect_Code = nc.NgCode
    WHERE dr.INSDatetime = @INSDatetime
    ORDER BY dr.Item_code, dr.Defect_Code;
END;
GO


-- ============================================================
-- Script test
-- ============================================================

-- [Test 1] Upsert 1 dòng hôm nay
EXEC sp_SyncDefectRecord @ItemCode = 'RM15A-1000NW', @NgCode = 'D001';

-- [Test 2] Upsert 1 dòng ngày cụ thể
EXEC sp_SyncDefectRecord @ItemCode = 'RM15A-1000NW', @NgCode = 'D001', @Date = '20260617';

-- [Test 3] Full sync hôm nay
EXEC sp_FullSyncDefectByDate;

-- [Test 4] Full sync ngày 17/6 (sau khi đã BACKFILL Item_code ở trên)
EXEC sp_FullSyncDefectByDate @Date = '20260617';

-- Kiểm tra nhanh SM_FQCBP_H ngày 17/6
SELECT WorkOrder, SerialNumber, NgCode, Item_code, Timeline
FROM   SM_FQCBP_H
WHERE  Status = 'NG' AND FORMAT(Timeline, 'yyyyMMdd') = '20260617'
ORDER BY Timeline;
