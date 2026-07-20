-- ============================================================
-- CA: Seed parameter.Parameters group 'VesselType' (EN=8, TH=8)
-- Feeds the Request page "Vessel Type" dropdown (collateral code 12 = Ship),
-- mirroring the existing 'VehicleType' group.
-- Idempotent: only inserts when the group is absent, so re-runs are safe.
-- ============================================================
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM parameter.Parameters WHERE [group] = N'VesselType')
BEGIN
    INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
    VALUES
        (N'VesselType', N'TH', N'EN', N'1', N'Cargo Ship', 1, 1),
        (N'VesselType', N'TH', N'TH', N'1', N'เรือบรรทุกสินค้า', 1, 1),
        (N'VesselType', N'TH', N'EN', N'2', N'Barge', 1, 2),
        (N'VesselType', N'TH', N'TH', N'2', N'เรือลำเลียง', 1, 2),
        (N'VesselType', N'TH', N'EN', N'3', N'Tugboat', 1, 3),
        (N'VesselType', N'TH', N'TH', N'3', N'เรือลากจูง', 1, 3),
        (N'VesselType', N'TH', N'EN', N'4', N'Fishing Vessel', 1, 4),
        (N'VesselType', N'TH', N'TH', N'4', N'เรือประมง', 1, 4),
        (N'VesselType', N'TH', N'EN', N'5', N'Passenger Boat', 1, 5),
        (N'VesselType', N'TH', N'TH', N'5', N'เรือโดยสาร', 1, 5),
        (N'VesselType', N'TH', N'EN', N'6', N'Tanker', 1, 6),
        (N'VesselType', N'TH', N'TH', N'6', N'เรือบรรทุกน้ำมัน', 1, 6),
        (N'VesselType', N'TH', N'EN', N'7', N'Speedboat', 1, 7),
        (N'VesselType', N'TH', N'TH', N'7', N'เรือเร็ว', 1, 7),
        (N'VesselType', N'TH', N'EN', N'8', N'Ferry', 1, 8),
        (N'VesselType', N'TH', N'TH', N'8', N'เรือข้ามฟาก', 1, 8);
END
GO
