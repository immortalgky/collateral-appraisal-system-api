using System.Text.Json;
using Workflow.FeeAppointmentApprovals.Domain;

namespace Workflow.Tests;

/// <summary>
/// FeeAppointmentApproval.Lines is persisted as a JSON column (see FeeAppointmentApprovalConfiguration).
/// The line properties have private setters, so System.Text.Json silently skips them on
/// deserialization unless [JsonInclude] is present — which made every line read back as a default
/// Appointment line with null values (Fee lines vanished, dates went null). These tests lock the
/// [JsonInclude] fix using the same serializer options the EF value converter uses.
/// </summary>
public class FeeAppointmentApprovalLineJsonTests
{
    // Mirrors FeeAppointmentApprovalConfiguration.SerializerOptions
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public void FeeLine_Deserializes_WithTypeCodeAndAmount()
    {
        // lineType 1 == Fee
        const string json = """
        [{"id":"019e40f6-4585-7348-80f2-575e41d9214f","lineType":1,"targetId":"2fb8433e-f36b-1410-896b-006f4f934fe1","newDate":null,"rescheduleCount":null,"feeCode":"99","feeDescription":"Test","feeAmount":1000.00,"lineStatus":0,"decisionReason":null}]
        """;

        var lines = JsonSerializer.Deserialize<List<FeeAppointmentApprovalLine>>(json, Options);

        Assert.NotNull(lines);
        var line = Assert.Single(lines);
        Assert.Equal(FeeApprovalLineType.Fee, line.LineType); // would default to Appointment(0) without [JsonInclude]
        Assert.Equal("99", line.FeeCode);
        Assert.Equal("Test", line.FeeDescription);
        Assert.Equal(1000.00m, line.FeeAmount);
    }

    [Fact]
    public void AppointmentLine_Deserializes_WithNewDate()
    {
        // lineType 0 == Appointment
        const string json = """
        [{"id":"019e40f6-4585-7348-80f2-575e41d9214f","lineType":0,"targetId":"2fb8433e-f36b-1410-896b-006f4f934fe1","newDate":"2026-04-02T09:00:00","rescheduleCount":1,"feeCode":null,"feeDescription":null,"feeAmount":null,"lineStatus":0,"decisionReason":null}]
        """;

        var lines = JsonSerializer.Deserialize<List<FeeAppointmentApprovalLine>>(json, Options)!;

        var line = Assert.Single(lines);
        Assert.Equal(FeeApprovalLineType.Appointment, line.LineType);
        Assert.Equal(new DateTime(2026, 4, 2, 9, 0, 0), line.NewDate); // would be null without [JsonInclude]
        Assert.Equal(1, line.RescheduleCount);
    }

    [Fact]
    public void SerializeThenDeserialize_PreservesFeeLine()
    {
        const string json = """
        [{"id":"019e40f6-4585-7348-80f2-575e41d9214f","lineType":1,"targetId":"2fb8433e-f36b-1410-896b-006f4f934fe1","newDate":null,"rescheduleCount":null,"feeCode":"99","feeDescription":"Test","feeAmount":1000.00,"lineStatus":0,"decisionReason":null}]
        """;

        var first = JsonSerializer.Deserialize<List<FeeAppointmentApprovalLine>>(json, Options)!;
        var reserialized = JsonSerializer.Serialize(first, Options);
        var second = JsonSerializer.Deserialize<List<FeeAppointmentApprovalLine>>(reserialized, Options)!;

        var line = Assert.Single(second);
        Assert.Equal(FeeApprovalLineType.Fee, line.LineType);
        Assert.Equal("99", line.FeeCode);
        Assert.Equal(1000.00m, line.FeeAmount);
    }
}
