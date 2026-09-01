using System.Text.Json.Serialization;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;

namespace LinnworksMcp.Application.Customers;

/// <summary>
/// Customers operations.
/// </summary>
public sealed class CustomerService(ILinnworksClient client)
{
    internal const string SearchCustomersPath = "/api/Customers/SearchCustomers";
    internal const string GetCustomerByIdPath = "/api/Customers/GetCustomerById";

    public async Task<PagedResult<CustomerDto>> SearchCustomersAsync(
        string searchKeyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var request = new SearchCustomersRequest(
            SearchTerm: searchKeyword,
            PageNumber: pageNumber,
            EntriesPerPage: pageSize);

        var response = await client
            .PostAsync<SearchCustomersRequest, SearchCustomersResponse>(SearchCustomersPath, request, cancellationToken)
            .ConfigureAwait(false);

        var items = response.Customers?.Select(Project).ToList() ?? [];
        return PagedResult<CustomerDto>.Create(items, pageNumber, pageSize, totalCount: response.TotalCount);
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        var request = new GetCustomerByIdRequest(CustomerId: customerId);
        var response = await client
            .PostAsync<GetCustomerByIdRequest, CustomerResponse>(GetCustomerByIdPath, request, cancellationToken)
            .ConfigureAwait(false);

        return response is not null ? Project(response) : null;
    }

    private static CustomerDto Project(CustomerResponse c) => new(
        CustomerId: c.CustomerId ?? string.Empty,
        FullName: c.FullName ?? string.Empty,
        EmailAddress: c.EmailAddress ?? string.Empty,
        PhoneNumber: c.PhoneNumber ?? string.Empty,
        Address: c.Address ?? string.Empty,
        Town: c.Town ?? string.Empty,
        Postcode: c.Postcode ?? string.Empty,
        Country: c.Country ?? string.Empty);
}

public sealed record SearchCustomersRequest(
    [property: JsonPropertyName("SearchTerm")] string SearchTerm,
    [property: JsonPropertyName("PageNumber")] int PageNumber,
    [property: JsonPropertyName("EntriesPerPage")] int EntriesPerPage);

public sealed record SearchCustomersResponse(
    [property: JsonPropertyName("Customers")] List<CustomerResponse>? Customers,
    [property: JsonPropertyName("TotalCount")] int TotalCount);

public sealed record GetCustomerByIdRequest(
    [property: JsonPropertyName("CustomerId")] string CustomerId);

public sealed record CustomerResponse(
    [property: JsonPropertyName("CustomerId")] string? CustomerId,
    [property: JsonPropertyName("FullName")] string? FullName,
    [property: JsonPropertyName("EmailAddress")] string? EmailAddress,
    [property: JsonPropertyName("PhoneNumber")] string? PhoneNumber,
    [property: JsonPropertyName("Address")] string? Address,
    [property: JsonPropertyName("Town")] string? Town,
    [property: JsonPropertyName("Postcode")] string? Postcode,
    [property: JsonPropertyName("Country")] string? Country);

public sealed record CustomerDto(
    string CustomerId,
    string FullName,
    string EmailAddress,
    string PhoneNumber,
    string Address,
    string Town,
    string Postcode,
    string Country);
