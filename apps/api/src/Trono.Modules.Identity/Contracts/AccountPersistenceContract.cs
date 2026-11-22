namespace Trono.Modules.Identity.Contracts;

// Shared database contract for ownership FKs. Consumers may reference only the account ID.
public static class AccountPersistenceContract
{
    public const string Schema = "identity";
    public const string Table = "AspNetUsers";
}
