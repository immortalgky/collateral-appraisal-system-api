namespace Auth.Application.Configurations;

public class LdapConfiguration
{
    public const string SectionName = "Ldap";

    public bool Enabled { get; set; }
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 636;
    public bool UseSsl { get; set; } = true;
    public string BaseDn { get; set; } = string.Empty;
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty;
    public string SearchFilter { get; set; } = "(sAMAccountName={0})";
    public bool FallbackToLocalAuth { get; set; } = true;
    public int ConnectionTimeoutSeconds { get; set; } = 5;
    public LdapAttributeMapping Attributes { get; set; } = new();
}

public class LdapAttributeMapping
{
    public string Username { get; set; } = "sAMAccountName";
    public string Email { get; set; } = "mail";
    public string FirstName { get; set; } = "givenName";
    public string LastName { get; set; } = "sn";
    public string Department { get; set; } = "department";
    public string Position { get; set; } = "title";
}
