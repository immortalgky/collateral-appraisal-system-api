using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 10 — Property Tax By Tiered Brackets (4 bracket arrays).</summary>
public sealed record Method10Detail
{
    [JsonPropertyName("propertyTax")]
    public PropertyTaxDetail PropertyTax { get; init; } = new();

    public sealed record PropertyTaxDetail
    {
        /// <summary>Land price per bracket (array of 4 bracket values).</summary>
        [JsonPropertyName("landPrices")]
        public decimal[] LandPrices { get; init; } = [];

        /// <summary>Total property price per bracket.</summary>
        [JsonPropertyName("totalPropertyPrice")]
        public decimal[] TotalPropertyPrice { get; init; } = [];

        /// <summary>Total property tax per bracket.</summary>
        [JsonPropertyName("totalPropertyTax")]
        public decimal[] TotalPropertyTax { get; init; } = [];

        /// <summary>Effective tax rate per bracket.</summary>
        [JsonPropertyName("totalPropertyTaxRates")]
        public decimal[] TotalPropertyTaxRates { get; init; } = [];

    }
    
    [JsonPropertyName("startIn")]
    public int StartIn { get; init; }
}
