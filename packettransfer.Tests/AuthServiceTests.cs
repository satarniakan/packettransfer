using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using packettransfer.Application.DTOs;
using packettransfer.Application.Services;
using packettransfer.Domain.Identity;

namespace packettransfer.Tests;

/// <summary>
/// ورود با نام کاربری/رمز، تغییر رمز و ویرایش پروفایل.
/// </summary>
public class AuthServiceTests
{
    private sealed class Harness
    {
        public Mock<UserManager<ApplicationUser>> Users { get; } = CreateUserManager();
        public Mock<SignInManager<ApplicationUser>> SignIn { get; }
        public Mock<ILoginHistoryService> History { get; } = new();
        public Mock<IHttpContextAccessor> Http { get; } = new();

        public Harness()
        {
            SignIn = new Mock<SignInManager<ApplicationUser>>(
                Users.Object,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
                null!, null!, null!, null!);
        }

        public AuthService Build() => new(
            Users.Object,
            SignIn.Object,
            History.Object,
            Http.Object,
            NullLogger<AuthService>.Instance);

        private static Mock<UserManager<ApplicationUser>> CreateUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            return new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }
    }

    private static ApplicationUser User(string id = "u1") => new()
    {
        Id = id,
        UserName = "ali.m"
    };

    [Fact]
    public async Task Login_UnknownUser_ReturnsFailure_AndRecordsHistory()
    {
        var h = new Harness();
        h.Users.Setup(u => u.FindByEmailAsync("ghost")).ReturnsAsync((ApplicationUser?)null);
        h.Users.Setup(u => u.FindByNameAsync("ghost")).ReturnsAsync((ApplicationUser?)null);

        var result = await h.Build().LoginWithPasswordAsync("ghost", "whatever");

        Assert.False(result.Succeeded);
        h.History.Verify(x => x.RecordAsync(
            null, "ghost", false, "Password",
            It.IsAny<string?>(), It.IsAny<string?>(), "invalid-credentials"), Times.Once);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsFailure()
    {
        var h = new Harness();
        var user = User();
        h.Users.Setup(u => u.FindByNameAsync("ali.m")).ReturnsAsync(user);
        h.SignIn.Setup(s => s.PasswordSignInAsync(user, "wrong", true, true))
            .ReturnsAsync(SignInResult.Failed);

        var result = await h.Build().LoginWithPasswordAsync("ali.m", "wrong");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ChangePassword_Mismatch_ReturnsMismatch_WithoutCallingManager()
    {
        var h = new Harness();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(User());

        var result = await h.Build().ChangePasswordAsync("u1", "old-1", "aaa", "bbb");

        Assert.Equal(ChangePasswordStatus.PasswordMismatch, result.Status);
        h.Users.Verify(u => u.ChangePasswordAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_ReturnsWrongCurrentPassword()
    {
        var h = new Harness();
        var user = User();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        h.Users.Setup(u => u.ChangePasswordAsync(user, "bad-old", "new-1"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch" }));

        var result = await h.Build().ChangePasswordAsync("u1", "bad-old", "new-1", "new-1");

        Assert.Equal(ChangePasswordStatus.WrongCurrentPassword, result.Status);
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_ReturnsChangeFailed()
    {
        var h = new Harness();
        var user = User();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        h.Users.Setup(u => u.ChangePasswordAsync(user, "old-1", "123"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));

        var result = await h.Build().ChangePasswordAsync("u1", "old-1", "123", "123");

        Assert.Equal(ChangePasswordStatus.ChangeFailed, result.Status);
    }

    [Fact]
    public async Task ChangePassword_ValidFlow_ReturnsSuccess()
    {
        var h = new Harness();
        var user = User();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        h.Users.Setup(u => u.ChangePasswordAsync(user, "old-1", "new-1"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await h.Build().ChangePasswordAsync("u1", "old-1", "new-1", "new-1");

        Assert.Equal(ChangePasswordStatus.Success, result.Status);
    }

    [Fact]
    public async Task UpdateProfile_EmailTakenByOther_ReturnsEmailAlreadyExists()
    {
        var h = new Harness();
        h.Users.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(User());
        h.Users.Setup(u => u.FindByEmailAsync("taken@test.ir")).ReturnsAsync(User("u2"));

        var result = await h.Build().UpdateProfileAsync("u1", "علی", "taken@test.ir");

        Assert.Equal(ProfileUpdateStatus.EmailAlreadyExists, result.Status);
    }
}
