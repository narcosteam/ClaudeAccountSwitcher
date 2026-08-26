using ClaudeAccountSwitcher;
using Xunit;

namespace ClaudeAccountSwitcher.Tests;

public class PkceTests
{
    [Fact]
    public void ComputeCodeChallenge_MatchesRfc7636TestVector()
    {
        // Test vector from RFC 7636 Appendix B.
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        var challenge = Pkce.ComputeCodeChallenge(verifier);

        Assert.Equal(expectedChallenge, challenge);
    }

    [Fact]
    public void GenerateCodeVerifier_ProducesUrlSafeStringOfSufficientLength()
    {
        var verifier = Pkce.GenerateCodeVerifier();

        Assert.True(verifier.Length >= 43, "RFC 7636 requires at least 43 characters");
        Assert.Matches("^[A-Za-z0-9_-]+$", verifier);
    }

    [Fact]
    public void GenerateState_ProducesNonEmptyUrlSafeString()
    {
        var state = Pkce.GenerateState();

        Assert.NotEmpty(state);
        Assert.Matches("^[A-Za-z0-9_-]+$", state);
    }

    [Fact]
    public void GenerateState_Produces43CharState_MatchingRealServerExpectation()
    {
        // A shorter state (e.g. 16 random bytes / 22 chars) got "Invalid
        // request format" from the real claude.ai OAuth server — confirmed
        // by comparing against a live-captured working authorize request's
        // state length (32 random bytes / 43 chars, base64url).
        var state = Pkce.GenerateState();

        Assert.Equal(43, state.Length);
    }
}
