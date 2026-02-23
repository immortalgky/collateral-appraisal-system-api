using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Data.Extensions;

public interface IOwnedEntityConfiguration<TOwner, TOwned> where TOwner : class where TOwned : class
{
    void Configure(OwnedNavigationBuilder<TOwner, TOwned> builder);
}