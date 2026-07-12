namespace Parameter.Dealers.Features.GetDealers;

public record GetDealersResult(List<DealerDto> Dealers);

public record DealerDto(string DealerCode, string DealerName);
