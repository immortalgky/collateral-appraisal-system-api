-- ============================================================
-- Seed: Pricing Analysis Reference Parameters
-- Tables: parameter.PricingParameterRoomTypes
--         parameter.PricingParameterJobPositions
--         parameter.PricingParameterTaxBrackets
--         parameter.PricingParameterAssumptionTypes
--         parameter.PricingParameterAssumptionMethods
--
-- Idempotent: skips entire block if RoomType '00' already exists.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM parameter.PricingParameterRoomTypes WHERE Code = '00')
BEGIN

    -- Room Types (31 entries)
    INSERT INTO parameter.PricingParameterRoomTypes (Code, Name, DisplaySeq) VALUES
    ('00', 'Standard Room',       0),
    ('01', 'Superior Room',       1),
    ('02', 'Deluxe Room',         2),
    ('03', 'Premier Room',        3),
    ('04', 'Executive Room',      4),
    ('05', 'Studio Room',         5),
    ('06', 'Suite',               6),
    ('07', 'Junior Suite',        7),
    ('08', 'Executive Suite',     8),
    ('09', 'Presidential Suite',  9),
    ('10', 'Family Room',        10),
    ('11', 'Connecting Room',    11),
    ('12', 'Adjoining Room',     12),
    ('13', 'Twin Room',          13),
    ('14', 'Double Room',        14),
    ('15', 'Single Room',        15),
    ('16', 'Triple Room',        16),
    ('17', 'Quad Room',          17),
    ('18', 'King Room',          18),
    ('19', 'Queen Room',         19),
    ('20', 'Accessible Room',    20),
    ('21', 'Ocean View Room',    21),
    ('22', 'City View Room',     22),
    ('23', 'Garden View Room',   23),
    ('24', 'Pool View Room',     24),
    ('25', 'Beachfront Room',    25),
    ('26', 'Villa',              26),
    ('27', 'Bungalow',           27),
    ('28', 'Penthouse',          28),
    ('29', 'Loft Room',          29),
    ('99', 'Others',             30);

END;

-- ---------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM parameter.PricingParameterJobPositions WHERE Code = '00')
BEGIN

    -- Job Positions (31 entries)
    INSERT INTO parameter.PricingParameterJobPositions (Code, Name, DisplaySeq) VALUES
    ('00', 'General Manager',              0),
    ('01', 'Assistant Manager',            1),
    ('02', 'Front Office Manager',         2),
    ('03', 'Front Desk Agent',             3),
    ('04', 'Concierge',                    4),
    ('05', 'Reservation Agent',            5),
    ('06', 'Bell Attendant',               6),
    ('07', 'Doorman',                      7),
    ('08', 'Housekeeping Manager',         8),
    ('09', 'Room Attendant',               9),
    ('10', 'Laundry Attendant',           10),
    ('11', 'Maintenance Technician',      11),
    ('12', 'Security Officer',            12),
    ('13', 'Food and Beverage Manager',   13),
    ('14', 'Restaurant Manager',          14),
    ('15', 'Executive Chef',              15),
    ('16', 'Sous Chef',                   16),
    ('17', 'Cook',                        17),
    ('18', 'Kitchen Assistant',           18),
    ('19', 'Server',                      19),
    ('20', 'Bartender',                   20),
    ('21', 'Room Service Attendant',      21),
    ('22', 'Sales Manager',               22),
    ('23', 'Marketing Manager',           23),
    ('24', 'Human Resources Manager',     24),
    ('25', 'Accountant',                  25),
    ('26', 'Purchasing Officer',          26),
    ('27', 'Spa Therapist',               27),
    ('28', 'Valet',                       28),
    ('29', 'Night Auditor',               29),
    ('99', 'Other',                       30);

END;

-- ---------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM parameter.PricingParameterTaxBrackets WHERE Tier = 1)
BEGIN

    -- Property Tax Brackets (4 tiers)
    INSERT INTO parameter.PricingParameterTaxBrackets (Tier, TaxRate, MinValue, MaxValue) VALUES
    (1, 0.0030,  0,   50000000),
    (2, 0.0040,  50000001,   200000000),
    (3, 0.0050,  200000001,  1000000000),
    (4, 0.0060, 1000000001,       5000000000),
    (5, 0.0070, 5000000001,       NULL);

END;

-- ---------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM parameter.PricingParameterAssumptionTypes WHERE Code = 'I00')
BEGIN

    -- Assumption Types
    INSERT INTO parameter.PricingParameterAssumptionTypes (Code, Name, Category, DisplaySeq) VALUES
    -- Income
    ('I00', 'Room Income',                       'income',    0),
    ('I01', 'Room Rental Income',                'income',    1),
    ('I02', 'Average Rental Rate',               'income',    2),
    ('I03', 'Energy Income',                     'income',    3),
    ('I04', 'Utility Income',                    'income',    4),
    ('I05', 'Food and Beverage Income',          'income',    5),
    ('I06', 'Other Income',                      'income',    6),
    -- Expenses
    ('E00', 'Administration Fee',                            'expenses',  7),
    ('E01', 'Advertising and Promotion Costs',               'expenses',  8),
    ('E02', 'Common Utility Fees',                           'expenses',  9),
    ('E03', 'Contingency Expenses',                          'expenses', 10),
    ('E04', 'Cost of Income from Utilities',                 'expenses', 11),
    ('E05', 'Energy Cost',                                   'expenses', 12),
    ('E06', 'Fire Insurance Premium',                        'expenses', 13),
    ('E07', 'Food and beverage expenses',                    'expenses', 14),
    ('E08', 'Marketing and Promotion Costs',                 'expenses', 15),
    ('E09', 'Operational and Administrative expenses',       'expenses', 16),
    ('E10', 'Other Expenses',                                'expenses', 17),
    ('E11', 'Project Management Compensation',               'expenses', 18),
    ('E12', 'Property Tax',                                  'expenses', 19),
    ('E13', 'Repair and Maintenance Costs',                  'expenses', 20),
    ('E14', 'Reserve Funds for Building Improvements',       'expenses', 21),
    ('E15', 'Room Cost',                                     'expenses', 22),
    ('E16', 'Salary and Benefits',                           'expenses', 23),
    ('E17', 'Sales and Marketing Expenses',                  'expenses', 24),
    ('E18', 'Utility Expenses',                              'expenses', 25),
    ('E19', 'Other Costs',                                   'expenses', 26),
    ('E20', 'Reserve for Asset Maintenance (FF&E / Cap Ex)', 'expenses', 27),
    -- Other
    ('M99', 'Miscellaneous',                                 'other',    28);

END;

-- ---------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM parameter.PricingParameterAssumptionMethods WHERE AssumptionType = 'I00')
BEGIN

    -- Assumption → Method matrix
    INSERT INTO parameter.PricingParameterAssumptionMethods (AssumptionType, MethodTypeCode) VALUES
    ('I00', '06'), ('I00', '04'),
    ('I01', '01'), ('I01', '02'), ('I01', '03'), ('I01', '04'), ('I01', '05'),
    ('I02', '06'),
    ('I03', '13'),
    ('I04', '13'), ('I04', '14'),
    ('I05', '13'), ('I05', '14'),
    ('I06', '13'), ('I06', '14'),
    ('E00', '13'), ('E00', '14'),
    ('E01', '13'), ('E01', '14'),
    ('E02', '13'), ('E02', '14'),
    ('E03', '13'), ('E03', '14'),
    ('E04', '13'), ('E04', '14'),
    ('E05', '11'),
    ('E06', '12'), ('E06', '14'),
    ('E07', '08'), ('E07', '13'),
    ('E08', '13'), ('E08', '14'),
    ('E09', '09'), ('E09', '13'), ('E09', '14'),
    ('E10', '13'), ('E10', '14'),
    ('E11', '13'), ('E11', '14'),
    ('E12', '10'), ('E12', '14'),
    ('E13', '13'), ('E13', '14'),
    ('E14', '13'), ('E14', '14'),
    ('E15', '07'), ('E15', '13'),
    ('E16', '09'), ('E16', '14'),
    ('E17', '13'), ('E17', '14'),
    ('E18', '13'), ('E18', '14'),
    ('E19', '13'), ('E19', '14'),
    ('E20', '13'), ('E20', '14'),
    ('M99', '13'), ('M99', '14');

END;
