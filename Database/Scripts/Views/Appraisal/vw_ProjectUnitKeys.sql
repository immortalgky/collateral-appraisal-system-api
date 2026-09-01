-- One row per (project unit, key the unit answers to).
--
-- A block project's unit has to be findable from what AS400 wrote about the collateral, and the
-- value it wrote can land in any of three columns because the data arrived three ways: projects
-- migrated from the legacy system recorded the unit under RoomNumber, newly appraised ones record
-- CondoRegistrationNumber, and a house in a development carries neither and is known only by its
-- HouseNumber. Any of the three can also hold a comma list, because one collateral can cover
-- several rooms.
--
-- KeyRank orders the columns rather than branching on them, so a caller picks the strongest key that
-- matched and keeps working as the mix shifts: registration first, then room, then house number,
-- and last the plot number.
--
-- PlotNumber (rank 3) is the PlanNo a house in a development is named by, and it is the ONLY key a
-- village unit has when the project is appraised before house numbers are issued. It is ranked last
-- and callers are expected to admit it only where the key is trustworthy: plot numbers are short
-- ("12", "P-001") and would otherwise collide with numbers parsed out of AS400's free-text name and
-- address fields. The export views therefore accept rank 3 only for a unit ticket, which is a key
-- we minted and handed over rather than one we guessed at.
--
-- The empty-string guard is load-bearing. A blank key would match every unit whose column is blank
-- and price a collateral from an unrelated room.
CREATE OR ALTER VIEW appraisal.vw_ProjectUnitKeys
AS
SELECT
    u.Id        AS ProjectUnitId,
    u.ProjectId,
    u.LandArea,
    s.KeyRank,
    LTRIM(RTRIM(p.value)) AS UnitKey
FROM appraisal.ProjectUnits u
CROSS APPLY (VALUES (0, u.CondoRegistrationNumber),
                    (1, u.RoomNumber),
                    (2, u.HouseNumber),
                    (3, u.PlotNumber)) AS s(KeyRank, Raw)
CROSS APPLY STRING_SPLIT(ISNULL(s.Raw, ''), ',') p
WHERE LTRIM(RTRIM(p.value)) <> '';
