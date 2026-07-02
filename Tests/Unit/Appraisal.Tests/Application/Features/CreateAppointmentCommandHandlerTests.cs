using Appraisal.Application.Features.Appointments.CreateAppointment;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Data.Outbox;
using Shared.Exceptions;
using Shared.Messaging.Events;
using Shared.Time;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Regression tests for <see cref="CreateAppointmentCommandHandler"/>.
///
/// The primary regression: <c>CreateAppointmentCommandHandler</c> must publish
/// <c>AppointmentDateChangedIntegrationEvent</c> on initial appointment creation so
/// the Workflow module can stamp <c>PendingTask.DueAt</c> immediately.
///
/// Before the fix, only <c>RescheduleAppointmentCommandHandler</c> and
/// <c>FeeAppointmentApprovalResolvedIntegrationEventHandler</c> published this event,
/// leaving <c>DueAt</c> null indefinitely after first appointment creation.
/// The consumer-level fix is validated in <c>AppointmentDateChangedConsumerTests</c>
/// (Workflow.Tests). This file owns the handler-level assertion.
/// </summary>
public class CreateAppointmentCommandHandlerTests
{
    [Fact(DisplayName = "REGRESSION: first appointment creation publishes AppointmentDateChangedIntegrationEvent with correct CorrelationId and AppointmentDate")]
    public async Task Handle_FirstAppointmentCreation_PublishesAppointmentDateChangedEvent()
    {
        // Arrange – appraisal with a single Pending admin assignment
        var requestId = Guid.NewGuid();
        var appraisal = AppraisalAggregate.Create(
            requestId: requestId,
            appraisalType: "Initial",
            priority: "Normal",
            now: DateTime.Now);
        appraisal.AssignAdmin();

        var repository = Substitute.For<IAppraisalRepository>();
        repository.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(appraisal);

        // InMemory DB — empty, so AnyAsync returns false (no existing appointment)
        var dbOptions = new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppraisalDbContext(dbOptions);

        var outbox = Substitute.For<IIntegrationEventOutbox>();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.ApplicationNow.Returns(DateTime.Now);

        var handler = new CreateAppointmentCommandHandler(repository, db, outbox, dateTimeProvider);

        var appointmentDate = new DateTime(2026, 7, 15, 9, 0, 0);
        var command = new CreateAppointmentCommand(
            AppraisalId: appraisal.Id,
            AppointmentDateTime: appointmentDate,
            AppointedBy: "test-user");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert – an appointment was returned and the event was published with the
        // correct correlation (RequestId) and date. This is the regression anchor:
        // if the outbox.Publish call is removed from the handler, this test fails.
        Assert.NotEqual(Guid.Empty, result.AppointmentId);
        outbox.Received(1).Publish(
            Arg.Is<AppointmentDateChangedIntegrationEvent>(e =>
                e.CorrelationId == requestId &&
                e.AppointmentDate == appointmentDate));
    }

    [Fact(DisplayName = "When an active appointment already exists, handler throws BadRequestException and never publishes")]
    public async Task Handle_WhenActiveAppointmentAlreadyExists_DoesNotPublishEvent()
    {
        // Arrange – appraisal with assignment, plus a pre-existing Appointed appointment in the DB
        var appraisal = AppraisalAggregate.Create(
            requestId: Guid.NewGuid(),
            appraisalType: "Initial",
            priority: "Normal",
            now: DateTime.Now);
        appraisal.AssignAdmin();
        var assignment = appraisal.Assignments.Single();

        var repository = Substitute.For<IAppraisalRepository>();
        repository.GetByIdWithAllDataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(appraisal);

        var dbOptions = new DbContextOptionsBuilder<AppraisalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppraisalDbContext(dbOptions);

        // Seed an active appointment for this assignment (Status = "Appointed" by default after Create)
        var existingAppointment = Appointment.Create(
            assignment.Id,
            new DateTime(2026, 7, 1, 9, 0, 0),
            "original-user");
        db.Appointments.Add(existingAppointment);
        await db.SaveChangesAsync();

        var outbox = Substitute.For<IIntegrationEventOutbox>();
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CreateAppointmentCommandHandler(repository, db, outbox, dateTimeProvider);

        var command = new CreateAppointmentCommand(
            AppraisalId: appraisal.Id,
            AppointmentDateTime: new DateTime(2026, 7, 20, 9, 0, 0),
            AppointedBy: "test-user");

        // Act & Assert – handler must reject the duplicate and must NOT publish the event
        await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(command, CancellationToken.None));
        outbox.DidNotReceive().Publish(Arg.Any<AppointmentDateChangedIntegrationEvent>());
    }
}
