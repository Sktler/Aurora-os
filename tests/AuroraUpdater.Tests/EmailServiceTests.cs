using Aurora.App.Services;
using MailKit.Security;
using Xunit;

namespace AuroraUpdater.Tests
{
    public class EmailServiceTests
    {
        [Fact]
        public void TryParseRecipients_AcceptsCommaAndSemicolonSeparatedAddresses()
        {
            Assert.True(EmailService.TryParseRecipients("a@x.com; b@y.org, c@z.net", out var list, out var error));
            Assert.Equal(3, list.Count);
            Assert.Equal("", error);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("not-an-address")]
        [InlineData("ok@x.com, nope")]
        public void TryParseRecipients_RejectsInvalidInput(string? raw)
        {
            Assert.False(EmailService.TryParseRecipients(raw, out _, out var error));
            Assert.NotEmpty(error);
        }

        [Fact]
        public void TrimBody_TruncatesLongBodies()
        {
            var trimmed = EmailService.TrimBody(new string('a', 20000));
            Assert.EndsWith("[truncated]", trimmed);
            Assert.True(trimmed.Length < 7000);
            Assert.Equal("hi", EmailService.TrimBody("  hi  "));
        }

        [Fact]
        public void FormatSummary_IsSingleLineAndFlagsUnread()
        {
            var line = EmailService.FormatSummary("7", "Bob\r\nInjected", "Subject\nline", DateTimeOffset.UtcNow, true);
            Assert.DoesNotContain('\n', line);
            Assert.Contains("[7] UNREAD", line);
        }

        [Fact]
        public void Security_FollowsPort()
        {
            Assert.Equal(SecureSocketOptions.SslOnConnect, EmailService.ImapSecurity(993));
            Assert.Equal(SecureSocketOptions.StartTls, EmailService.ImapSecurity(143));
            Assert.Equal(SecureSocketOptions.SslOnConnect, EmailService.SmtpSecurity(465));
            Assert.Equal(SecureSocketOptions.StartTls, EmailService.SmtpSecurity(587));
        }

        [Fact]
        public void SuggestServers_KnowsCommonProvidersOnly()
        {
            Assert.Equal("imap.gmail.com", EmailService.SuggestServers("me@Gmail.com")?.ImapHost);
            Assert.Null(EmailService.SuggestServers("me@company.example"));
            Assert.Null(EmailService.SuggestServers("no-at-sign"));
        }

        [Fact]
        public void IsEmailTool_MatchesOnlyEmailTools()
        {
            Assert.True(EmailTools.IsEmailTool("email_send"));
            Assert.True(EmailTools.IsEmailTool("email_read"));
            Assert.False(EmailTools.IsEmailTool("web_search"));
        }
    }
}
