namespace AirwayHVAC.SmsAi.Infrastructure.Cosmos.Models;

public sealed class TenantDocument
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    // 31/10/2026: real tenant documents in Cosmos use "tenantName", not
    // "companyName" (tenant 3: "Arctic Air HVAC Services"). Without this
    // field the app saw an empty CompanyName and the greeting fell back
    // to the generic wording, even though the tenant has a name.
    public string TenantName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string ServiceArea { get; set; } = string.Empty;

    public string Timezone { get; set; } = string.Empty;

    public BusinessHoursDocument BusinessHours { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
}
