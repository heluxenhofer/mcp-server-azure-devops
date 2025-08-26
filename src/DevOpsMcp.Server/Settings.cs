namespace DevOpsMcp.Server
{
    /// <summary>
    /// Application settings including Azure AD configuration.
    /// </summary>
    public class Settings
    {
        /// <summary>
        /// Gets or sets the Azure Active Directory configuration.
        /// </summary>
        public AzureAd AzureAd { get; set; }
    }

    public class AzureAd
    {
        /// <summary>
        /// Gets or sets the Azure AD client ID.
        /// </summary>
        public required string ClientId { get; set; }
        /// <summary>
        /// Gets or sets the Azure AD client secret.
        /// </summary>
        public required string ClientSecret { get; set; }
        /// <summary>
        /// Gets or sets the Azure AD tenant ID.
        /// </summary>
        public required string TenantId { get; set; }
        /// <summary>
        /// Gets the Azure AD authority URL.
        /// </summary>
        public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";
    }
}
