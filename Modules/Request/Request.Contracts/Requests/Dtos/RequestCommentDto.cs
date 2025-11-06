namespace Request.Contracts.Requests.Dtos;

public record RequestCommentDto(Guid Id, string Comment, bool IsDeleted);