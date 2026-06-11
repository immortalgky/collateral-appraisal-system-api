namespace Auth.Application.Configurations;

public class LdapConfiguration
{
    public const string SectionName = "Ldap";

    public bool Enabled { get; set; }
    public string Server { get; set; } = string.Empty;
    public int Port { get; set; } = 636;
    public bool UseSsl { get; set; } = true;

    // When true, the app binds to AD as its own process/app-pool Windows identity (Kerberos/Negotiate)
    // instead of an explicit service account — so no BindDn/BindPassword is stored in configuration.
    public bool UseIntegratedAuth { get; set; }

    // NetBIOS domain (e.g. "LHB") used to qualify the user during the Negotiate password-validation bind.
    public string Domain { get; set; } = string.Empty;

    public string BaseDn { get; set; } = string.Empty;
    public string BindDn { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty;
    public string SearchFilter { get; set; } = "(sAMAccountName={0})";
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
