using System.Text.Json;

namespace Request.Infrastructure.JsonConverters;

public class RequestConverter : JsonConverter<Domain.Requests.Request>
{
    public override Domain.Requests.Request? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var jsonDocument = JsonDocument.ParseValue(ref reader);
        var root = jsonDocument.RootElement;

        var id = root.GetProperty("id").GetInt64();
        var customersElement = root.GetProperty("customers");

        //var request = new Domain.Requests.Request(id, "", "", default!);

        var customers = customersElement.Deserialize<List<RequestCustomer>>(options);
        if (customers != null)
        {
            var customersField =
                typeof(Domain.Requests.Request).GetField("_customers", BindingFlags.NonPublic | BindingFlags.Instance);
            customersField?.SetValue(default, customers);
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, Domain.Requests.Request value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("id", value.Id.ToString());
        // writer.WriteString("purpose", value.Purpose);
        // writer.WriteString("channel", value.Channel);

        writer.WritePropertyName("customers");
        JsonSerializer.Serialize(writer, value.Customers, options);

        writer.WriteEndObject();
    }
}