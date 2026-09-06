using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Locks the rule that a machinery record may not hold a value it contradicts: no registration
/// number on a machine that is not registered, no quotation number on one that is not being
/// procured. The form disables both boxes in exactly those states, and this is what keeps the
/// stored row agreeing with what the screen shows — on every write path, not just the form's.
///
/// Deliberately NOT part of the rule: IsPriceCertified. Certifying a price is the appraiser's
/// judgement, and the invariant that used to infer it from these same two fields was removed.
/// </summary>
public class MachineryInapplicableFieldsTests
{
    private const string UnderProcurement = "2";
    private const string Installed = "1";

    private static MachineryAppraisalDetail Machine() =>
        MachineryAppraisalDetail.Create(Guid.NewGuid());

    [Fact]
    public void Update_KeepsTheRegistrationNumberOfARegisteredMachine()
    {
        var machine = Machine();

        machine.Update(registrationStatus: true, registrationNumber: "ร.2/1-123");

        Assert.Equal("ร.2/1-123", machine.RegistrationNumber);
    }

    [Fact]
    public void Update_ClearsTheRegistrationNumberWhenTheMachineIsNotRegistered()
    {
        var machine = Machine();
        machine.Update(registrationStatus: true, registrationNumber: "ร.2/1-123");

        machine.Update(registrationStatus: false);

        Assert.Null(machine.RegistrationNumber);
    }

    [Fact]
    public void Update_RefusesARegistrationNumberOnAnUnregisteredMachineEvenWhenAsked()
    {
        var machine = Machine();

        // The two arrive together, so there is no "the status was already false" ordering to lean
        // on — the rule has to hold on whatever the record ends up saying.
        machine.Update(registrationStatus: false, registrationNumber: "ร.2/1-123");

        Assert.Null(machine.RegistrationNumber);
    }

    [Fact]
    public void Update_KeepsTheInvoiceWhileTheMachineIsUnderProcurement()
    {
        var machine = Machine();

        machine.Update(installationStatus: UnderProcurement, invoiceNumber: "INV-9");

        Assert.Equal("INV-9", machine.InvoiceNumber);
    }

    [Fact]
    public void Update_ClearsTheInvoiceOnceTheMachineIsInstalled()
    {
        var machine = Machine();
        machine.Update(installationStatus: UnderProcurement, invoiceNumber: "INV-9");

        machine.Update(installationStatus: Installed);

        Assert.Null(machine.InvoiceNumber);
    }

    [Fact]
    public void Update_LeavesPriceCertificationToTheAppraiser()
    {
        var machine = Machine();

        // Unregistered and under procurement: what the removed invariant used to force to false.
        machine.Update(registrationStatus: false, installationStatus: UnderProcurement,
            isPriceCertified: true);

        Assert.True(machine.IsPriceCertified);
    }
}
