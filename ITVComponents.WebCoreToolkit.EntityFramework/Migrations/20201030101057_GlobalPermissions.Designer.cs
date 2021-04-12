﻿// <auto-generated />
using System;
using ITVComponents.WebCoreToolkit.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ITVComponents.WebCoreToolkit.EntityFramework.Migrations
{
    [DbContext(typeof(SecurityContext))]
    [Migration("20201030101057_GlobalPermissions")]
    partial class GlobalPermissions
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .UseIdentityColumns()
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("ProductVersion", "5.0.0-preview.7.20365.15");

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.AuthenticationType", b =>
                {
                    b.Property<int>("AuthenticationTypeId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("AuthenticationTypeName")
                        .IsRequired()
                        .HasMaxLength(512)
                        .HasColumnType("nvarchar(512)");

                    b.HasKey("AuthenticationTypeId");

                    b.HasIndex(new[] { "AuthenticationTypeName" }, "IX_UniqueAuthenticationType")
                        .IsUnique();

                    b.ToTable("AuthenticationTypes");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.CustomUserProperty", b =>
                {
                    b.Property<int>("CustomUserPropertyId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("PropertyName")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.HasKey("CustomUserPropertyId");

                    b.HasIndex(new[] { "UserId", "PropertyName" }, "IX_UniqueProperty")
                        .IsUnique();

                    b.ToTable("UserProperties");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.DiagnosticsQuery", b =>
                {
                    b.Property<int>("DiagnosticsQueryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<bool>("AutoReturn")
                        .HasColumnType("bit");

                    b.Property<string>("DbContext")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("DiagnosticsQueryName")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<int>("PermissionId")
                        .HasColumnType("int");

                    b.Property<string>("QueryText")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("DiagnosticsQueryId");

                    b.HasIndex("PermissionId");

                    b.HasIndex(new[] { "DiagnosticsQueryName" }, "IX_DiagnosticsQueryUniqueness")
                        .IsUnique();

                    b.ToTable("DiagnosticsQueries");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.DiagnosticsQueryParameter", b =>
                {
                    b.Property<int>("DiagnosticsQueryParameterId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("DefaultValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("DiagnosticsQueryId")
                        .HasColumnType("int");

                    b.Property<string>("Format")
                        .HasMaxLength(64)
                        .HasColumnType("nvarchar(64)");

                    b.Property<bool>("Optional")
                        .HasColumnType("bit");

                    b.Property<string>("ParameterName")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<int>("ParameterType")
                        .HasColumnType("int");

                    b.HasKey("DiagnosticsQueryParameterId");

                    b.HasIndex("DiagnosticsQueryId");

                    b.ToTable("DiagnosticsQueryParameters");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.GlobalSetting", b =>
                {
                    b.Property<int>("GlobalSettingId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<bool>("JsonSetting")
                        .HasColumnType("bit");

                    b.Property<string>("SettingsKey")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("SettingsValue")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("GlobalSettingId");

                    b.HasIndex(new[] { "SettingsKey" }, "UQ_GlobalSettingsKey")
                        .IsUnique();

                    b.ToTable("GlobalSettings");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.NavigationMenu", b =>
                {
                    b.Property<int>("NavigationMenuId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<int?>("ParentId")
                        .HasColumnType("int");

                    b.Property<int?>("PermissionId")
                        .HasColumnType("int");

                    b.Property<int?>("SortOrder")
                        .HasColumnType("int");

                    b.Property<string>("SpanClass")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Url")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("UrlUniqueness")
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)")
                        .HasComputedColumnSql("case when isnull(Url,'')='' then 'MENU__'+convert(varchar(10),NavigationMenuId) else Url end persisted");

                    b.HasKey("NavigationMenuId");

                    b.HasIndex("ParentId");

                    b.HasIndex("PermissionId");

                    b.HasIndex(new[] { "UrlUniqueness" }, "IX_UniqueUrl")
                        .IsUnique();

                    b.ToTable("Navigation");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.Permission", b =>
                {
                    b.Property<int>("PermissionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("Description")
                        .HasMaxLength(2048)
                        .HasColumnType("nvarchar(2048)");

                    b.Property<bool>("IsGlobal")
                        .HasColumnType("bit");

                    b.Property<string>("PermissionName")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.HasKey("PermissionId");

                    b.HasIndex(new[] { "PermissionName" }, "IX_UniquePermissionName")
                        .IsUnique();

                    b.ToTable("Permissions");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.Role", b =>
                {
                    b.Property<int>("RoleId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("RoleName")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.HasKey("RoleId");

                    b.HasIndex(new[] { "RoleName" }, "IX_UniqueRoleName")
                        .IsUnique();

                    b.ToTable("Roles");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.RolePermission", b =>
                {
                    b.Property<int>("RolePermissionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int>("PermissionId")
                        .HasColumnType("int");

                    b.Property<int>("RoleId")
                        .HasColumnType("int");

                    b.Property<int>("TenantId")
                        .HasColumnType("int");

                    b.HasKey("RolePermissionId");

                    b.HasIndex("PermissionId");

                    b.HasIndex("TenantId");

                    b.HasIndex(new[] { "RoleId", "PermissionId", "TenantId" }, "IX_UniqueRolePermission")
                        .IsUnique();

                    b.ToTable("RolePermissions");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.SystemEvent", b =>
                {
                    b.Property<int>("SystemEventId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("Category")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<DateTime>("EventTime")
                        .HasColumnType("datetime2");

                    b.Property<int>("LogLevel")
                        .HasColumnType("int");

                    b.Property<string>("Message")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Title")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.HasKey("SystemEventId");

                    b.ToTable("SystemLog");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.Tenant", b =>
                {
                    b.Property<int>("TenantId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("DisplayName")
                        .HasMaxLength(1024)
                        .HasColumnType("nvarchar(1024)");

                    b.Property<string>("TenantName")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.HasKey("TenantId");

                    b.HasIndex(new[] { "TenantName" }, "IX_UniqueTenant")
                        .IsUnique();

                    b.ToTable("Tenants");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.TenantDiagnosticsQuery", b =>
                {
                    b.Property<int>("TenantDiagnosticsQueryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int>("DiagnosticsQueryId")
                        .HasColumnType("int");

                    b.Property<int>("TenantId")
                        .HasColumnType("int");

                    b.HasKey("TenantDiagnosticsQueryId");

                    b.HasIndex("DiagnosticsQueryId");

                    b.HasIndex(new[] { "TenantId", "DiagnosticsQueryId" }, "IX_UniqueDiagnosticsTenantLink")
                        .IsUnique();

                    b.ToTable("TenantDiagnosticsQueries");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.TenantNavigationMenu", b =>
                {
                    b.Property<int>("TenantNavigationMenuId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int>("NavigationMenuId")
                        .HasColumnType("int");

                    b.Property<int>("TenantId")
                        .HasColumnType("int");

                    b.HasKey("TenantNavigationMenuId");

                    b.HasIndex("NavigationMenuId");

                    b.HasIndex("TenantId");

                    b.ToTable("TenantNavigation");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.TenantSetting", b =>
                {
                    b.Property<int>("TenantSettingId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<bool>("JsonSetting")
                        .HasColumnType("bit");

                    b.Property<string>("SettingsKey")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("SettingsValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("TenantId")
                        .HasColumnType("int");

                    b.HasKey("TenantSettingId");

                    b.HasIndex("TenantId");

                    b.HasIndex(new[] { "SettingsKey", "TenantId" }, "UQ_SettingsKey")
                        .IsUnique();

                    b.ToTable("TenantSettings");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.User", b =>
                {
                    b.Property<int>("UserId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int>("AuthenticationTypeId")
                        .HasColumnType("int");

                    b.Property<string>("UserName")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.HasKey("UserId");

                    b.HasIndex("AuthenticationTypeId");

                    b.HasIndex(new[] { "UserName" }, "IX_UniqueUserName")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.UserRole", b =>
                {
                    b.Property<int>("UserRoleId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<int>("RoleId")
                        .HasColumnType("int");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("UserRoleId");

                    b.HasIndex("UserId");

                    b.HasIndex(new[] { "RoleId", "UserId" }, "IX_UniqueUserRole")
                        .IsUnique();

                    b.ToTable("UserRoles");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.WebPlugin", b =>
                {
                    b.Property<int>("WebPluginId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<bool>("AutoLoad")
                        .HasColumnType("bit");

                    b.Property<string>("Constructor")
                        .HasMaxLength(2048)
                        .HasColumnType("nvarchar(2048)");

                    b.Property<string>("StartupRegistrationConstructor")
                        .HasMaxLength(2048)
                        .HasColumnType("nvarchar(2048)");

                    b.Property<string>("UniqueName")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("WebPluginId");

                    b.ToTable("WebPlugins");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.WebPluginConstant", b =>
                {
                    b.Property<int>("WebPluginConstantId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .UseIdentityColumn();

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("nvarchar(128)");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("WebPluginConstantId");

                    b.HasIndex(new[] { "Name" }, "IX_UniquePluginConst")
                        .IsUnique();

                    b.ToTable("WebPluginConstants");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.CustomUserProperty", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.User", "User")
                        .WithMany("UserProperties")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.DiagnosticsQuery", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Permission", "Permission")
                        .WithMany()
                        .HasForeignKey("PermissionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.DiagnosticsQueryParameter", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.DiagnosticsQuery", "DiagnosticsQuery")
                        .WithMany("Parameters")
                        .HasForeignKey("DiagnosticsQueryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.NavigationMenu", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.NavigationMenu", "Parent")
                        .WithMany("Children")
                        .HasForeignKey("ParentId");

                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Permission", "EntryPoint")
                        .WithMany()
                        .HasForeignKey("PermissionId");
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.RolePermission", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Permission", "Permission")
                        .WithMany("RolePermissions")
                        .HasForeignKey("PermissionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Role", "Role")
                        .WithMany("RolePermissions")
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Tenant", "Tenant")
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.TenantDiagnosticsQuery", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.DiagnosticsQuery", "DiagnosticsQuery")
                        .WithMany("Tenants")
                        .HasForeignKey("DiagnosticsQueryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Tenant", "Tenant")
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.TenantNavigationMenu", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.NavigationMenu", "NavigationMenu")
                        .WithMany("Tenants")
                        .HasForeignKey("NavigationMenuId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Tenant", "Tenant")
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.TenantSetting", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Tenant", "Tenant")
                        .WithMany()
                        .HasForeignKey("TenantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.User", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.AuthenticationType", "AuthenticationType")
                        .WithMany()
                        .HasForeignKey("AuthenticationTypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ITVComponents.WebCoreToolkit.EntityFramework.Models.UserRole", b =>
                {
                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.Role", "Role")
                        .WithMany("UserRoles")
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ITVComponents.WebCoreToolkit.EntityFramework.Models.User", "User")
                        .WithMany("UserRoles")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
