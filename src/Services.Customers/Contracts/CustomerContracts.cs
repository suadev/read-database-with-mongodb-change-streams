namespace Services.Customers.Contracts;

public record CustomerDto(Guid Id, string Name, string Email, string Phone);

public record CreateCustomerRequest(Guid? Id, string Name, string Email, string Phone);

public record UpdateCustomerRequest(string Name, string Email, string Phone);
