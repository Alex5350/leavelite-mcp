using ErrorOr;
using NSubstitute;
using LeaveLite.Application.Abstractions;
using LeaveLite.Application.Common;
using LeaveLite.Application.Employees;
using LeaveLite.Domain.Enums;
using LeaveLite.Domain.Policies;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Application.UnitTests.Employees;

public sealed class RegisterEmployeeTests : IAsyncDisposable
{
    private readonly ApplicationTestHost _host = new();

    private readonly AccrualPolicy _vacationPolicy = TestData.VacationMonthly();

    private RegisterEmployeeCommand Command(
        string email = "new.hire@leavelite.io",
        EmploymentType employmentType = EmploymentType.FullTime,
        AccrualPolicyId? policyId = null,
        DateOnly? hiredOn = null)
        => new(
            "  New Hire  ",
            email,
            employmentType,
            TestData.TeamId,
            TeamRole.Member,
            hiredOn ?? new DateOnly(2026, 6, 1),
            policyId ?? _vacationPolicy.Id);

    private Task<ErrorOr<EmployeeId>> Handle(RegisterEmployeeCommand command)
        => _host.Handler<ICommandHandler<RegisterEmployeeCommand, EmployeeId>>().Handle(command, TestContext.Current.CancellationToken);

    [Fact]
    public async Task HappyPath_RegistersEmployeeWithNormalizedData()
    {
        _host.Policies.GetByIdAsync(_vacationPolicy.Id, Arg.Any<CancellationToken>()).Returns(_vacationPolicy);
        _host.Employees.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((Domain.Employees.Employee?)null);

        var result = await Handle(Command());

        Assert.False(result.IsError);
        Assert.NotEqual(default, result.Value);
        await _host.Employees.Received(1).AddAsync(
            Arg.Is<Domain.Employees.Employee>(employee =>
                employee.FullName == "New Hire"
                && employee.Email.Value == "new.hire@leavelite.io"
                && employee.EmploymentType == EmploymentType.FullTime),
            Arg.Any<CancellationToken>());
        await _host.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateEmail_ReturnsConflict()
    {
        var existing = TestData.Employee("Existing Hire", "new.hire@leavelite.io");
        _host.Policies.GetByIdAsync(_vacationPolicy.Id, Arg.Any<CancellationToken>()).Returns(_vacationPolicy);
        _host.Employees.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(existing);

        var result = await Handle(Command());

        Assert.True(result.IsError);
        Assert.Equal("Employee.DuplicateEmail", result.FirstError.Code);
        await _host.Employees.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
        await _host.UnitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ContractorWithFullTimePolicy_ReturnsPolicyNotEligible()
    {
        _host.Policies.GetByIdAsync(_vacationPolicy.Id, Arg.Any<CancellationToken>()).Returns(_vacationPolicy);

        var result = await Handle(Command(employmentType: EmploymentType.Contractor));

        Assert.True(result.IsError);
        Assert.Equal("Employee.PolicyNotEligible", result.FirstError.Code);
        await _host.Employees.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ContractorWithContractorPolicy_RegistersSuccessfully()
    {
        var contractorSick = TestData.SickUpfront(employmentType: EmploymentType.Contractor);
        _host.Policies.GetByIdAsync(contractorSick.Id, Arg.Any<CancellationToken>()).Returns(contractorSick);
        _host.Employees.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((Domain.Employees.Employee?)null);

        var result = await Handle(Command(email: "contractor@leavelite.io", employmentType: EmploymentType.Contractor, policyId: contractorSick.Id));

        Assert.False(result.IsError);
        await _host.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownPolicy_ReturnsNotFound()
    {
        _host.Policies.GetByIdAsync(Arg.Any<AccrualPolicyId>(), Arg.Any<CancellationToken>()).Returns((AccrualPolicy?)null);

        var result = await Handle(Command(policyId: AccrualPolicyId.New()));

        Assert.True(result.IsError);
        Assert.Equal("AccrualPolicy.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task HireDateInTheFuture_ReturnsValidationError()
    {
        var result = await Handle(Command(hiredOn: ApplicationTestHost.Today.AddDays(1)));

        Assert.True(result.IsError);
        Assert.Equal("HiredOn", result.FirstError.Code);
        await _host.Employees.DidNotReceiveWithAnyArgs().AddAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InvalidEmailFormat_ReturnsValidationError()
    {
        var result = await Handle(Command(email: "not-an-email"));

        Assert.True(result.IsError);
        Assert.Equal("Email", result.FirstError.Code);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
