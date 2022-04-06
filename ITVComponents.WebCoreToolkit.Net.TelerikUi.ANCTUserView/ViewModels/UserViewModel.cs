﻿using System;
using System.ComponentModel.DataAnnotations;

namespace ITVComponents.WebCoreToolkit.Net.TelerikUi.AspNetCoreTenantSecurityUserView.ViewModels
{
    public class UserViewModel
    {
        public string Id { get;set; }

        [MaxLength(150)]
        [Required]
        public string UserName { get; set; }

        public int? AuthenticationTypeId { get; set; }

        public bool Assigned { get; set; }
        public int? RoleId { get; set; }
        
        public string UniQUID{get; set; }
        
        public int? TenantId{get; set; }

        /*
         * IdentityUser-Properties
         */

        public virtual string NormalizedUserName { get; set; }
        /// <summary>Gets or sets the email address for this user.</summary>
        public virtual string Email { get; set; }
        /// <summary>
        /// Gets or sets the normalized email address for this user.
        /// </summary>
        public virtual string NormalizedEmail { get; set; }
        /// <summary>
        /// Gets or sets a flag indicating if a user has confirmed their email address.
        /// </summary>
        /// <value>True if the email address has been confirmed, otherwise false.</value>
        public virtual bool EmailConfirmed { get; set; }
        /// <summary>
        /// Gets or sets a salted and hashed representation of the password for this user.
        /// </summary>
        public virtual string PasswordHash { get; set; }
        /// <summary>
        /// A random value that must change whenever a users credentials change (password changed, login removed)
        /// </summary>
        public virtual string SecurityStamp { get; set; }
        /// <summary>
        /// A random value that must change whenever a user is persisted to the store
        /// </summary>
        public virtual string ConcurrencyStamp { get; set; }
        /// <summary>Gets or sets a telephone number for the user.</summary>
        public virtual string PhoneNumber { get; set; }
        /// <summary>
        /// Gets or sets a flag indicating if a user has confirmed their telephone address.
        /// </summary>
        /// <value>True if the telephone number has been confirmed, otherwise false.</value>
        public virtual bool PhoneNumberConfirmed { get; set; }
        /// <summary>
        /// Gets or sets a flag indicating if two factor authentication is enabled for this user.
        /// </summary>
        /// <value>True if 2fa is enabled, otherwise false.</value>
        public virtual bool TwoFactorEnabled { get; set; }
        /// <summary>
        /// Gets or sets the date and time, in UTC, when any user lockout ends.
        /// </summary>
        /// <remarks>A value in the past means the user is not locked out.</remarks>
        public virtual DateTimeOffset? LockoutEnd { get; set; }
        /// <summary>
        /// Gets or sets a flag indicating if the user could be locked out.
        /// </summary>
        /// <value>True if the user could be locked out, otherwise false.</value>
        public virtual bool LockoutEnabled { get; set; }
        /// <summary>
        /// Gets or sets the number of failed login attempts for the current user.
        /// </summary>
        public virtual int AccessFailedCount { get; set; }

        public bool Enabled { get; set; }
    }
}
