using System.Text.Json;

namespace Request.Data.JsonConverters;

public class RequestConverter : JsonConverter<Requests.Models.Request>
{
    public override Requests.Models.Request? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var jsonDocument = JsonDocument.ParseValue(ref reader);
        var root = jsonDocument.RootElement;

        var id = root.GetProperty("id").GetInt64();
        var customersElement = root.GetProperty("customers");

        //var request = new Requests.Models.Request(id, "", "", default!);

        var customers = customersElement.Deserialize<List<RequestCustomer>>(options);
        if (customers != null)
        {
            var customersField =
                typeof(Requests.Models.Request).GetField("_customers", BindingFlags.NonPublic | BindingFlags.Instance);
            customersField?.SetValue(default, customers);
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, Requests.Models.Request value, JsonSerializerOptions options)
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