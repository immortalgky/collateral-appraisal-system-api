using Dapper;

namespace Appraisal.Application.Features.Appointments.GetAppointmentHistory;

public class GetAppointmentHistoryQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetAppointmentHistoryQuery, GetAppointmentHistoryResult>
{
    public async Task<GetAppointmentHistoryResult> Handle(
        GetAppointmentHistoryQuery query,
        CancellationToken cancellationToken)
    {
        // Single UNION ALL query:
        //   Part 1 — appointment history events for ALL appointments of the appraisal (full
        //            timeline; an appraisal can have more than one appointment after a
        //            cancel-then-recreate).
        //   Part 2 — fee events (FeeAdded / FeeApproved / FeeRejected) for user-added fee items
        //            on the same assignment chain.
        //
        // Appointment resolution: Appointments.AssignmentId → AppraisalAssignments.Id
        //   (where AppraisalId = @AppraisalId).
        //
        // NewDate computation for Rescheduled rows uses LEAD() PARTITIONed BY AppointmentId to find
        // the PreviousAppointmentDateTime of the next-newer history row of the SAME appointment
        // (so pairing never bleeds across appointments). If there is no newer row the appointment's
        // current AppointmentDateTime is the new date.
        //
        // Actor name: actor fields now store the bank code (= AspNetUsers.UserName). Both branches
        // use a defensive dual join (UserName OR Id-as-Guid via TRY_CONVERT) so legacy rows that
        // stored a Guid before the ApprovedBy/ChangedBy code migration still resolve to a name.
        const string sql = """
            -- ── Part 1: Appointment history events ──────────────────────────────────────
            SELECT
                h.EventType,
                h.Title,
                h.OldDate,
                h.NewDate,
                NULL          AS FeeCode,
                NULL          AS FeeDescription,
                NULL          AS Amount,
                h.Status,
                h.ActorCode,
                COALESCE(u.FirstName + ' ' + u.LastName, h.ActorCode) AS ActorName,
                h.Reason,
                h.OccurredAt
            FROM (
                SELECT
                    ah.Id,
                    CASE ah.ChangeType
                        WHEN 'Rescheduled'        THEN 'AppointmentRescheduled'
                        WHEN 'Cancelled'          THEN 'AppointmentCancelled'
                        WHEN 'RescheduleRejected' THEN 'AppointmentRejected'
                        WHEN 'StatusChanged'      THEN
                            CASE
                                WHEN (ah.ChangeReason LIKE 'Approved%' OR ah.ChangeReason LIKE 'Appointed%') THEN 'AppointmentApproved'
                                ELSE NULL   -- skip Completed and other internal transitions
                            END
                        ELSE NULL
                    END                                                     AS EventType,
                    CASE ah.ChangeType
                        WHEN 'Rescheduled'        THEN 'Appointment Rescheduled'
                        WHEN 'Cancelled'          THEN 'Appointment Cancelled'
                        WHEN 'RescheduleRejected' THEN 'Reschedule Rejected'
                        WHEN 'StatusChanged'      THEN
                            CASE
                                WHEN (ah.ChangeReason LIKE 'Approved%' OR ah.ChangeReason LIKE 'Appointed%') THEN 'Appointment Approved'
                                ELSE NULL
                            END
                        ELSE NULL
                    END                                                     AS Title,
                    CASE ah.ChangeType
                        WHEN 'Rescheduled' THEN ah.PreviousAppointmentDateTime
                        ELSE NULL
                    END                                                     AS OldDate,
                    CASE ah.ChangeType
                        WHEN 'Rescheduled' THEN
                            COALESCE(
                                LEAD(ah.PreviousAppointmentDateTime)
                                    OVER (PARTITION BY ah.AppointmentId ORDER BY ah.ChangedAt),
                                ap.AppointmentDateTime
                            )
                        ELSE NULL
                    END                                                     AS NewDate,
                    CASE
                        WHEN ah.ChangeType = 'Rescheduled'                             THEN 'Pending'
                        WHEN ah.ChangeType = 'Cancelled'                               THEN 'Cancelled'
                        WHEN ah.ChangeType = 'RescheduleRejected'                      THEN 'Rejected'
                        WHEN ah.ChangeType = 'StatusChanged'
                             AND (ah.ChangeReason LIKE 'Approved%' OR ah.ChangeReason LIKE 'Appointed%')                      THEN 'Approved'
                        ELSE NULL
                    END                                                     AS Status,
                    ah.ChangedBy                                            AS ActorCode,
                    ah.ChangeReason                                         AS Reason,
                    ah.ChangedAt                                            AS OccurredAt
                FROM appraisal.AppointmentHistory ah
                INNER JOIN appraisal.Appointments ap ON ap.Id = ah.AppointmentId
                INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = ap.AssignmentId
                WHERE aa.AppraisalId = @AppraisalId
            ) h
            -- ChangedBy may hold either a bank code (UserName) or a user Guid (Id), depending on
            -- the caller; match either so the name resolves regardless. The literal 'system'
            -- (auto-apply) matches neither and falls back to the raw ActorCode.
            LEFT JOIN auth.AspNetUsers u
                ON u.UserName = h.ActorCode
                OR u.Id = TRY_CONVERT(UNIQUEIDENTIFIER, h.ActorCode)
            WHERE h.EventType IS NOT NULL   -- discard skipped rows (e.g. Completed)

            UNION ALL

            -- ── Part 2a: FeeAdded events (one per user-added fee item) ─────────────────
            SELECT
                'FeeAdded'          AS EventType,
                'Fee Added'         AS Title,
                NULL                AS OldDate,
                NULL                AS NewDate,
                fi.FeeCode,
                fi.FeeDescription,
                fi.FeeAmount        AS Amount,
                'Pending'           AS Status,
                NULL                AS ActorCode,
                NULL                AS ActorName,
                NULL                AS Reason,
                fi.CreatedAt        AS OccurredAt
            FROM appraisal.AppraisalFeeItems fi
            INNER JOIN appraisal.AppraisalFees af ON af.Id = fi.AppraisalFeeId
            INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = af.AssignmentId
            WHERE aa.AppraisalId = @AppraisalId
              AND fi.Source = 'User'

            UNION ALL

            -- ── Part 2b: FeeApproved / FeeRejected events ─────────────────────────────
            SELECT
                CASE fi.ApprovalStatus
                    WHEN 'Approved' THEN 'FeeApproved'
                    WHEN 'Rejected' THEN 'FeeRejected'
                    ELSE NULL
                END                 AS EventType,
                CASE fi.ApprovalStatus
                    WHEN 'Approved' THEN 'Fee Approved'
                    WHEN 'Rejected' THEN 'Fee Rejected'
                    ELSE NULL
                END                 AS Title,
                NULL                AS OldDate,
                NULL                AS NewDate,
                fi.FeeCode,
                fi.FeeDescription,
                fi.FeeAmount        AS Amount,
                fi.ApprovalStatus   AS Status,
                fi.ApprovedBy       AS ActorCode,
                COALESCE(u2.FirstName + ' ' + u2.LastName, fi.ApprovedBy) AS ActorName,
                fi.RejectionReason  AS Reason,
                fi.ApprovedAt       AS OccurredAt
            FROM appraisal.AppraisalFeeItems fi
            INNER JOIN appraisal.AppraisalFees af ON af.Id = fi.AppraisalFeeId
            INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = af.AssignmentId
            LEFT JOIN auth.AspNetUsers u2
                ON u2.UserName = fi.ApprovedBy
                OR u2.Id = TRY_CONVERT(UNIQUEIDENTIFIER, fi.ApprovedBy)
            WHERE aa.AppraisalId = @AppraisalId
              AND fi.Source = 'User'
              AND fi.ApprovalStatus IN ('Approved', 'Rejected')
              AND fi.ApprovedAt IS NOT NULL

            ORDER BY OccurredAt DESC
            """;

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        var rows = await connectionFactory.QueryAsync<AppointmentHistoryEventDto>(sql, parameters);

        return new GetAppointmentHistoryResult(rows.ToList().AsReadOnly());
    }
}
