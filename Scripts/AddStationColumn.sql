-- ============================================================
-- MIGRATION: chạy 1 lần duy nhất để thêm cột Station
-- Mục đích: 1 bảng lịch sử dùng chung cho nhiều trạm FQC
-- (FQCBP hiện tại, FQC02, FQC04, ...) — phân biệt bằng cột Station.
-- Dữ liệu cũ (trước khi có cột này) mặc định là 'FQCBP'.
-- (bỏ qua nếu cột đã tồn tại)
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('SM_FQCBP_H') AND name = 'Station'
)
    ALTER TABLE SM_FQCBP_H ADD Station NVARCHAR(20) NOT NULL CONSTRAINT DF_SM_FQCBP_H_Station DEFAULT 'FQCBP';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('SM_FQCBP') AND name = 'Station'
)
    ALTER TABLE SM_FQCBP ADD Station NVARCHAR(20) NOT NULL CONSTRAINT DF_SM_FQCBP_Station DEFAULT 'FQCBP';
GO
