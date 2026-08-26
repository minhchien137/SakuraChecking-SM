-- ============================================================
-- Tạo mới 2 bảng cho trạm FPY (First Pass Yield) — chạy 1 lần.
-- SM_FQCFPY   : bảng rollup Qty/PassQty/NgQty theo WorkOrder (giống SM_FQCBP)
-- SM_FQCFPY_H : bảng lịch sử từng lần quét (giống SM_FQCBP_H)
-- FPY cho phép quét lại cùng 1 Serial Number nhiều lần — KHÔNG có ràng buộc
-- UNIQUE trên SerialNumber (khác với SM_FQCBP_H).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SM_FQCFPY')
BEGIN
    CREATE TABLE SM_FQCFPY (
        Id        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WorkOrder NVARCHAR(100) NOT NULL,
        Station   NVARCHAR(20)  NOT NULL CONSTRAINT DF_SM_FQCFPY_Station DEFAULT 'FPY',
        Qty       INT NOT NULL DEFAULT 0,
        PassQty   INT NOT NULL DEFAULT 0,
        NgQty     INT NOT NULL DEFAULT 0
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SM_FQCFPY_H')
BEGIN
    CREATE TABLE SM_FQCFPY_H (
        Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        WorkOrder     NVARCHAR(100) NOT NULL,
        Station       NVARCHAR(20)  NOT NULL CONSTRAINT DF_SM_FQCFPY_H_Station DEFAULT 'FPY',
        SerialNumber  NVARCHAR(20)  NOT NULL,
        Status        NVARCHAR(10)  NOT NULL,
        Timeline      DATETIME2     NOT NULL CONSTRAINT DF_SM_FQCFPY_H_Timeline DEFAULT SYSDATETIME(),
        Color         NVARCHAR(50)  NULL,
        NgCode        NVARCHAR(50)  NULL,
        NgReason      NVARCHAR(200) NULL,
        NgDescription NVARCHAR(500) NULL,
        Item_code     NVARCHAR(200) NULL
    );

    -- Cho phép tra cứu nhanh theo SN, KHÔNG unique — FPY cho phép trùng SN.
    CREATE INDEX IX_SM_FQCFPY_H_SerialNumber ON SM_FQCFPY_H (SerialNumber);
END
GO
