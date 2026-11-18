using System;
using System.Reflection;
using ITVComponents.Security;
using ITVComponents.WebCoreToolkit.Cookies;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Cookies;
using ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ITVComponents.WebCoreToolkit.EntityFramework.TenantSecurity.Test
{
    /// <summary>
    /// A cookie that points to a server cookie which is gone counts as missing - it never hands out the encrypted
    /// placeholder as its content ("Failed to unpack UserScope: unsupported compression method", §121).
    /// </summary>
    [TestClass]
    public class DbCookieServiceTest
    {
        private CookieContext db = null!;

        [TestInitialize]
        public void Setup()
        {
            db = new CookieContext(new DbContextOptionsBuilder<CookieContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        }

        [TestCleanup]
        public void Cleanup() => db.Dispose();

        [TestMethod]
        public void A_Placeholder_Without_Its_Server_Cookie_Counts_As_Missing()
        {
            var tag = CookieBufferHelper.CreateBufferCookieTag("SC", null, 1, out _, out _);

            Assert.IsFalse(Service(tag).TryGetCookie("UserScope", out var value));
            Assert.IsNull(value, "the encrypted placeholder is no content");
        }

        [TestMethod]
        public void A_Placeholder_With_Its_Server_Cookie_Gives_The_Content_And_A_Plain_Cookie_Itself()
        {
            var tag = CookieBufferHelper.CreateBufferCookieTag("SC", null, 1, out var validity, out var guid);
            db.ServerCookies.Add(new ServerCookie { Key = guid, ValidThrough = validity, Content = "the scope" });
            db.SaveChanges();

            Assert.IsTrue(Service(tag).TryGetCookie("UserScope", out var content));
            Assert.AreEqual("the scope", content);
            Assert.IsTrue(Service("plain").TryGetCookie("UserScope", out var plain));
            Assert.AreEqual("plain", plain);
        }

        [TestMethod]
        public void A_Server_Cookie_Lives_Its_Validity_Whatever_The_Time_Zone_Of_The_Server()
        {
            // Valid for 30 minutes, written in UTC. Compared against local time, it was gone at once on a server east of
            // UTC (and lived hours longer on one west of it). The check holds on any machine whose zone is not UTC.
            var tag = CookieBufferHelper.CreateBufferCookieTag("SC", new CookieOptions { MaxAge = TimeSpan.FromMinutes(30) }, 1, out var validity, out var guid);
            db.ServerCookies.Add(new ServerCookie { Key = guid, ValidThrough = validity, Content = "the scope" });
            db.SaveChanges();

            Assert.IsTrue(Service(tag).TryGetCookie("UserScope", out var content), $"valid through {validity:O} UTC, local offset {TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)}");
            Assert.AreEqual("the scope", content);
        }

        private DbCookieService Service(string cookieValue)
        {
            var http = new DefaultHttpContext();
            http.Request.Headers.Cookie = "UserScope=" + cookieValue;
            var accessor = new HttpContextAccessor { HttpContext = http };
            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddOptions();
            services.AddSingleton<IHttpContextAccessor>(accessor);
            services.AddSingleton(SystemContext.For(db));
            var provider = services.BuildServiceProvider();
            http.RequestServices = provider;
            return new DbCookieService(accessor, provider, Microsoft.Extensions.Options.Options.Create(new Shared.Options.ServerCookieOptions()));
        }

        public sealed class CookieContext : DbContext
        {
            public CookieContext(DbContextOptions<CookieContext> options) : base(options)
            {
            }

            public DbSet<ServerCookie> ServerCookies { get; set; } = null!;

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<ServerCookie>().HasKey(c => new { c.Key, c.ValidThrough });
            }
        }

        /// <summary>
        /// The full system context for a service that only uses <c>ServerCookies</c> and <c>SaveChanges</c>.
        /// </summary>
        public class SystemContext : DispatchProxy
        {
            private CookieContext inner = null!;

            internal static ICoreSystemContext For(CookieContext context)
            {
                var proxy = Create<ICoreSystemContext, SystemContext>();
                ((SystemContext)(object)proxy).inner = context;
                return proxy;
            }

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                return targetMethod?.Name switch
                {
                    "get_ServerCookies" => inner.ServerCookies,
                    "SaveChanges" => inner.SaveChanges(),
                    _ => throw new NotSupportedException($"{targetMethod?.Name} is not part of this test.")
                };
            }
        }
    }
}
