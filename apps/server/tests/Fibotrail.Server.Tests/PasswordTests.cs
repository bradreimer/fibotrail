using Fibotrail.Server.Auth;

namespace Fibotrail.Server.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new(TestApp.FastCost);

    [Fact]
    public void A_hash_is_an_argon2id_string_that_does_not_hold_the_password()
    {
        var hash = _hasher.Hash(TestApp.Password);

        Assert.StartsWith("$argon2id$v=19$m=64,t=1,p=1$", hash, StringComparison.Ordinal);
        Assert.DoesNotContain(TestApp.Password, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void The_default_cost_is_the_owasp_minimum()
    {
        var hash = new PasswordHasher(Argon2Cost.Default).Hash(TestApp.Password);

        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
        Assert.True(PasswordHasher.Verify(TestApp.Password, hash));
    }

    [Fact]
    public void The_right_password_verifies_and_a_wrong_one_does_not()
    {
        var hash = _hasher.Hash(TestApp.Password);

        Assert.True(PasswordHasher.Verify(TestApp.Password, hash));
        Assert.False(PasswordHasher.Verify(TestApp.Password + "!", hash));
        Assert.False(PasswordHasher.Verify(string.Empty, hash));
    }

    [Fact]
    public void Two_hashes_of_one_password_differ_because_each_has_its_own_salt()
    {
        Assert.NotEqual(_hasher.Hash(TestApp.Password), _hasher.Hash(TestApp.Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("correct horse battery")]
    [InlineData("$argon2i$v=19$m=64,t=1,p=1$c2FsdHNhbHQ$aGFzaGhhc2hoYXNoaGFzaA")]
    [InlineData("$argon2id$v=19$m=64,t=1$c2FsdHNhbHQ$aGFzaGhhc2hoYXNoaGFzaA")]
    [InlineData("$argon2id$v=19$m=999999999,t=1,p=1$c2FsdHNhbHQ$aGFzaGhhc2hoYXNoaGFzaA")]
    [InlineData("$argon2id$v=19$m=64,t=1,p=1$!!!$aGFzaGhhc2hoYXNoaGFzaA")]
    public void A_malformed_hash_never_matches(string stored)
    {
        Assert.False(PasswordHasher.Verify(TestApp.Password, stored));
    }
}

public class PasswordPolicyTests
{
    [Fact]
    public void Eleven_characters_are_too_few()
    {
        Assert.Equal(PasswordPolicy.TooShort, PasswordPolicy.Check("elevenchars", "elevenchars"));
        Assert.Equal(PasswordPolicy.TooShort, PasswordPolicy.Check(null, null));
    }

    [Theory]
    [InlineData("twelve chars")]
    [InlineData("aaaaaaaaaaaa")]
    [InlineData("123456789012")]
    public void Twelve_characters_pass_with_no_other_rule(string password)
    {
        Assert.Null(PasswordPolicy.Check(password, password));
    }

    [Fact]
    public void The_two_entries_must_match()
    {
        Assert.Equal(PasswordPolicy.Mismatch, PasswordPolicy.Check("twelve chars", "twelve charz"));
    }

    [Fact]
    public void A_password_over_the_ceiling_is_rejected()
    {
        var password = new string('a', PasswordPolicy.MaximumLength + 1);

        Assert.Equal(PasswordPolicy.TooLong, PasswordPolicy.Check(password, password));
    }
}
