﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using AspNetIdentityDependencyInjectionSample.DataLayer.Context;
using AspNetIdentityDependencyInjectionSample.DomainClasses;
using AspNetIdentityDependencyInjectionSample.ServiceLayer.Contracts;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.DataProtection;

namespace AspNetIdentityDependencyInjectionSample.ServiceLayer
{
    public class ApplicationUserManager
        : UserManager<ApplicationUser, int>, IApplicationUserManager
    {
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly IApplicationRoleManager _roleManager;
        private readonly IUserStore<ApplicationUser, int> _store;
        private readonly IUnitOfWork _uow;
        private readonly IDbSet<ApplicationUser> _users;
        private readonly IIdentity _identity;
        private ApplicationUser _user;

        public ApplicationUserManager(IUserStore<ApplicationUser, int> store,
            IUnitOfWork uow,
            IIdentity identity,
            IApplicationRoleManager roleManager,
            IDataProtectionProvider dataProtectionProvider,
            IIdentityMessageService smsService,
            IIdentityMessageService emailService)
            : base(store)
        {
            _store = store;
            _uow = uow;
            _identity = identity;
            _users = _uow.Set<ApplicationUser>();
            _roleManager = roleManager;
            _dataProtectionProvider = dataProtectionProvider;
            this.SmsService = smsService;
            this.EmailService = emailService;

            createApplicationUserManager();
        }

        public ApplicationUser FindById(int userId)
        {
            return _users.Find(userId);
        }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(ApplicationUser applicationUser)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await CreateIdentityAsync(applicationUser, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }

        public Task<List<ApplicationUser>> GetAllUsersAsync()
        {
            return this.Users.ToListAsync();
        }

        public ApplicationUser GetCurrentUser()
        {
            return _user ?? (_user = this.FindById(GetCurrentUserId()));
        }

        public async Task<ApplicationUser> GetCurrentUserAsync()
        {
            return _user ?? (_user = await this.FindByIdAsync(GetCurrentUserId()));
        }

        public int GetCurrentUserId()
        {
            return _identity.GetUserId<int>();
        }

        public async Task<bool> HasPassword(int userId)
        {
            var user = await FindByIdAsync(userId);
            return user != null && user.PasswordHash != null;
        }

        public async Task<bool> HasPhoneNumber(int userId)
        {
            var user = await FindByIdAsync(userId);
            return user != null && user.PhoneNumber != null;
        }

        public Func<CookieValidateIdentityContext, Task> OnValidateIdentity()
        {
            return SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, ApplicationUser, int>(
                         validateInterval: TimeSpan.FromMinutes(30),
                         regenerateIdentityCallback: (manager, user) => generateUserIdentityAsync(manager, user),
                         getUserIdCallback: id => id.GetUserId<int>());
        }

        public void SeedDatabase()
        {
            const string name = "admin@example.com";
            const string password = "Admin@123456";
            const string roleName = "Admin";

            //Create Role Admin if it does not exist
            var role = _roleManager.FindRoleByName(roleName);
            if (role == null)
            {
                role = new CustomRole(roleName);
                var roleresult = _roleManager.CreateRole(role);
            }

            var user = this.FindByName(name);
            if (user == null)
            {
                user = new ApplicationUser { UserName = name, Email = name };
                var result = this.Create(user, password);
                result = this.SetLockoutEnabled(user.Id, false);
            }

            // Add user admin to Role Admin if not already added
            var rolesForUser = this.GetRoles(user.Id);
            if (!rolesForUser.Contains(role.Name))
            {
                var result = this.AddToRole(user.Id, role.Name);
            }
        }

        private void createApplicationUserManager()
        {
            // Configure validation logic for usernames
            this.UserValidator = new UserValidator<ApplicationUser, int>(this)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };

            // Configure validation logic for passwords
            this.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = true,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
            };

            // Configure user lockout defaults
            this.UserLockoutEnabledByDefault = true;
            this.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(5);
            this.MaxFailedAccessAttemptsBeforeLockout = 5;

            // Register two factor authentication providers. This application uses Phone and Emails as a step of receiving a code for verifying the user
            // You can write your own provider and plug in here.
            this.RegisterTwoFactorProvider("PhoneCode", new PhoneNumberTokenProvider<ApplicationUser, int>
            {
                MessageFormat = "Your security code is: {0}"
            });
            this.RegisterTwoFactorProvider("EmailCode", new EmailTokenProvider<ApplicationUser, int>
            {
                Subject = "SecurityCode",
                BodyFormat = "Your security code is {0}"
            });

            if (_dataProtectionProvider != null)
            {
                var dataProtector = _dataProtectionProvider.Create("ASP.NET Identity");
                this.UserTokenProvider = new DataProtectorTokenProvider<ApplicationUser, int>(dataProtector);
            }
        }

        private async Task<ClaimsIdentity> generateUserIdentityAsync(ApplicationUserManager manager, ApplicationUser applicationUser)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(applicationUser, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }
}