namespace AuthService.Tests.Domain;

using AuthService.Domain.Entities;
using FluentAssertions;
using Xunit;

public class UserTests
{
    [Fact]
    public void Create_should_generate_valid_user_with_hashed_password()
    {
        // Act
        var user = User.Create("test@example.com", "hashed_password", "Test User");

        // Assert
        user.Email.Should().Be("test@example.com");
        user.PasswordHash.Should().Be("hashed_password");
        user.FullName.Should().Be("Test User");
        user.IsEmailConfirmed.Should().BeFalse();
        user.IsActive.Should().BeTrue();
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void ConfirmEmail_should_set_IsEmailConfirmed_to_true()
    {
        var user = User.Create("test@example.com", "hash", "Test");
        user.ConfirmEmail();
        user.IsEmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public void Suspend_should_deactivate_account()
    {
        var user = User.Create("test@example.com", "hash", "Test");
        user.Suspend();
        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SetRefreshToken_should_store_token_and_expiry()
    {
        var user = User.Create("test@example.com", "hash", "Test");
        var token    = "refresh_token_value";
        var expiry   = DateTime.UtcNow.AddDays(30);

        user.SetRefreshToken(token, expiry);

        user.RefreshToken.Should().Be(token);
        user.RefreshTokenExpiresAt.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RefreshToken_should_be_expired_when_expiry_is_in_past()
    {
        var user = User.Create("test@example.com", "hash", "Test");
        user.SetRefreshToken("token", DateTime.UtcNow.AddMinutes(-1));
        user.RefreshTokenExpiresAt.Should().BeBefore(DateTime.UtcNow);
    }
}

public class WatchlistItemTests
{
    [Fact]
    public void Create_should_build_item_with_correct_fields()
    {
        var matchId = Guid.NewGuid();

        var item = WatchlistItem.Create(
            Guid.NewGuid(),
            matchId,
            "US Product",
            "VN Product",
            alertAboveScore: 80,
            alertBelowScore: 20
        );

        item.MatchId.Should().Be(matchId);
        item.UsProductName.Should().Be("US Product");
        item.VnProductName.Should().Be("VN Product");
        item.AlertAboveScore.Should().Be(80);
        item.AlertBelowScore.Should().Be(20);
        item.IsMuted.Should().BeFalse();
        item.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetMuted_toggle_should_update_IsMuted()
    {
        var item = WatchlistItem.Create(Guid.NewGuid(), Guid.NewGuid(), "US", "VN");

        item.SetMuted(true);
        item.IsMuted.Should().BeTrue();

        item.SetMuted(false);
        item.IsMuted.Should().BeFalse();
    }
}

public class AlertThresholdTests
{
    [Fact]
    public void Create_should_build_threshold_with_defaults()
    {
        var threshold = AlertThreshold.Create(
            Guid.NewGuid(),
            Common.Domain.Enums.DeliveryChannel.Email,
            minScore: 60,
            deliveryTarget: "user@example.com",
            minMargin: 10m
        );

        threshold.MinScoreThreshold.Should().Be(60);
        threshold.MinMarginPct.Should().Be(10m);
        threshold.DeliveryTarget.Should().Be("user@example.com");
        threshold.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UpdateThresholds_should_modify_all_fields()
    {
        var t = AlertThreshold.Create(
            Guid.NewGuid(),
            Common.Domain.Enums.DeliveryChannel.Email,
            minScore: 50, deliveryTarget: "a@b.com"
        );

        t.UpdateThresholds(minScore: 75, minMargin: 15m, minDelta: 10m);

        t.MinScoreThreshold.Should().Be(75);
        t.MinMarginPct.Should().Be(15m);
        t.MinScoreDelta.Should().Be(10m);
    }

    [Fact]
    public void Deactivate_should_set_IsActive_false()
    {
        var t = AlertThreshold.Create(
            Guid.NewGuid(),
            Common.Domain.Enums.DeliveryChannel.Telegram,
            minScore: 50, deliveryTarget: "123456"
        );

        t.Deactivate();
        t.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Reactivate_should_set_IsActive_true()
    {
        var t = AlertThreshold.Create(
            Guid.NewGuid(),
            Common.Domain.Enums.DeliveryChannel.InApp,
            minScore: 50, deliveryTarget: "target"
        );
        t.Deactivate();
        t.Reactivate();
        t.IsActive.Should().BeTrue();
    }
}