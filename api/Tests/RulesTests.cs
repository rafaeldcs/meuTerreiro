using Terreiro.Domain;
using Xunit;
namespace Terreiro.Tests;
public sealed class RulesTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
    private static Cleaning Valid() => new()
    {
        Id = "test", StartsAt = Now.AddHours(1), EndsAt = Now.AddHours(2), Status = "Published",
        PublicationVersion = 2, Assignments = [new Assignment { UserId = "m1" }],
        Tasks = [new CleaningTask { Title = "Organizar" }]
    };
    [Theory]
    [InlineData(" JOAO.1 ", "joao.1")]
    [InlineData("maria_silva", "maria_silva")]
    public void Normalizes_login(string input, string expected) => Assert.Equal(expected, Rules.Login(input));
    [Theory]
    [InlineData("a")][InlineData("../admin")][InlineData("nome com espaço")][InlineData("<script>")]
    public void Rejects_invalid_login(string input) => Assert.Throws<RuleException>(() => Rules.Login(input));
    [Fact] public void Short_password_is_rejected() => Assert.Throws<RuleException>(() => Rules.Password("short"));
    [Fact] public void Long_passphrase_is_allowed() => Rules.Password("Uma frase exclusiva de teste");
    [Theory][InlineData(0)][InlineData(-1)][InlineData(501)]
    public void Team_target_is_bounded(int target) => Assert.Throws<RuleException>(() => Rules.Plan("Team", target, Now.AddHours(1), Now.AddHours(2), Now));
    [Fact] public void General_does_not_require_target() => Rules.Plan("General", null, Now.AddHours(1), Now.AddHours(2), Now);
    [Fact] public void Past_start_is_rejected() => Assert.Throws<RuleException>(() => Rules.Plan("Team", 4, Now.AddHours(-1), Now.AddHours(2), Now));
    [Fact] public void Inverted_dates_are_rejected() => Assert.Throws<RuleException>(() => Rules.Plan("Team", 4, Now.AddHours(2), Now.AddHours(1), Now));
    [Fact] public void Assigned_member_can_confirm() => Rules.CanRespond(Valid(), "m1", 2, "Confirmed", Now);
    [Fact] public void Confirmation_validation_does_not_record_presence()
    {
        var item = Valid(); Rules.CanRespond(item, "m1", 2, "Confirmed", Now);
        Assert.Equal("Unverified", item.Assignments[0].Participation);
        Assert.False(item.Tasks[0].Verified);
    }
    [Fact] public void Outsider_cannot_respond() => Assert.Throws<RuleException>(() => Rules.CanRespond(Valid(), "other", 2, "Confirmed", Now));
    [Fact] public void Old_publication_cannot_be_confirmed() => Assert.Throws<RuleException>(() => Rules.CanRespond(Valid(), "m1", 1, "Confirmed", Now));
    [Fact] public void Cancelled_activity_cannot_be_confirmed()
    {
        var item = Valid(); item.Status = "Cancelled";
        Assert.Throws<RuleException>(() => Rules.CanRespond(item, "m1", 2, "Confirmed", Now));
    }
    [Fact] public void Only_explicit_response_values_are_accepted() => Assert.Throws<RuleException>(() => Rules.CanRespond(Valid(), "m1", 2, "Present", Now));
    [Fact] public void Unverified_required_task_blocks_completion() => Assert.Throws<RuleException>(() => Rules.CanClose(Valid(), Now.AddHours(3)));
    [Fact] public void Future_cleaning_cannot_be_completed()
    {
        var item = Valid(); item.Tasks[0].Verified = true;
        Assert.Throws<RuleException>(() => Rules.CanClose(item, Now));
    }
    [Fact] public void Verified_tasks_allow_completion_without_faking_presence()
    {
        var item = Valid(); item.Tasks[0].Verified = true;
        Rules.CanClose(item, Now.AddHours(3)); Assert.Equal("Unverified", item.Assignments[0].Participation);
    }
    [Theory]
    [InlineData("http://fcm.googleapis.com/x")][InlineData("https://fcm.googleapis.com.evil.test/x")]
    [InlineData("https://localhost/x")][InlineData("https://127.0.0.1/x")][InlineData("https://fcm.googleapis.com:444/x")]
    [InlineData("https://user@fcm.googleapis.com/x")][InlineData("https://169.254.169.254/latest/meta-data/")]
    public void Push_cannot_target_arbitrary_or_internal_urls(string url) => Assert.False(Rules.ValidPushEndpoint(url));
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc")][InlineData("https://web.push.apple.com/abc")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc")]
    public void Approved_push_transport_is_allowed(string url) => Assert.True(Rules.ValidPushEndpoint(url));
    [Fact] public void Desktop_is_not_mobile() => Assert.False(Rules.IsMobile("Mozilla/5.0 Windows NT 10.0"));
    [Fact] public void Hash_is_deterministic_and_does_not_contain_original()
    {
        Assert.Equal(Rules.Hash("one"), Rules.Hash("one"));
        Assert.NotEqual(Rules.Hash("one"), Rules.Hash("two"));
        Assert.Equal(64, Rules.Hash("one").Length);
    }
}
