using LeaveLite.Domain.Errors;
using LeaveLite.Domain.ValueObjects;

namespace LeaveLite.Domain.UnitTests.ValueObjects;

public sealed class EmailTests
{
    [Theory]
    [InlineData("ada@leavelite.io")]
    [InlineData("first.last@example.co.uk")]
    [InlineData("a@b.c")]
    [InlineData("o'brien@example.com")]
    [InlineData("x+tag@example.org")]
    public void TryCreate_ValidAddresses_Succeeds(string input)
    {
        var created = Email.TryCreate(input, out var email);

        Assert.True(created);
        Assert.Equal(input, email!.Value);
    }

    [Fact]
    public void TryCreate_TrimsSurroundingWhitespace()
    {
        var created = Email.TryCreate("  ada@leavelite.io\t", out var email);

        Assert.True(created);
        Assert.Equal("ada@leavelite.io", email!.Value);
    }

    [Fact]
    public void TryCreate_MixedCaseAddress_LowercasesInvariantly()
    {
        var created = Email.TryCreate("Ada@LeaveLite.IO", out var email);

        Assert.True(created);
        Assert.Equal("ada@leavelite.io", email!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign.com")]
    [InlineData("a@b")] // no dot in the domain part
    [InlineData("a@.b")] // empty domain label
    [InlineData("a b@c.d")] // whitespace inside the local part
    [InlineData("a@b@c.d")] // two at-signs
    [InlineData("@b.c")] // empty local part
    [InlineData("a@")] // empty domain
    public void TryCreate_InvalidAddresses_Fails(string? input)
    {
        var created = Email.TryCreate(input, out var email);

        Assert.False(created);
        Assert.Null(email);
    }

    [Fact]
    public void TryCreate_AddressLongerThan254Characters_Fails()
    {
        var tooLong = new string('a', 250) + "@b.io";

        var created = Email.TryCreate(tooLong, out var email);

        Assert.False(created);
        Assert.Null(email);
    }

    [Fact]
    public void Create_InvalidInput_ReturnsInvalidError()
    {
        var result = Email.Create("not-an-email");

        Assert.True(result.IsError);
        Assert.Equal(EmailErrors.Invalid("not-an-email").Code, result.FirstError.Code);
    }

    [Fact]
    public void Create_ValidInput_ReturnsNormalizedEmail()
    {
        var result = Email.Create("  Ada@LeaveLite.IO ");

        Assert.False(result.IsError);
        Assert.Equal("ada@leavelite.io", result.Value.Value);
    }

    [Fact]
    public void Equality_NormalizedEqualAddresses_AreEqual()
    {
        var first = Email.TryCreate("Ada@LeaveLite.IO", out var a) ? a : null;
        var second = Email.TryCreate("ada@leavelite.io", out var b) ? b : null;

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ToString_ReturnsBareValue()
    {
        var email = Email.TryCreate("ada@leavelite.io", out var value) ? value : null;

        Assert.NotNull(email);
        Assert.Equal("ada@leavelite.io", email.ToString());
    }
}
