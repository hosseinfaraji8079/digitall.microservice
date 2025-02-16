﻿using Data.DefaultData;
using Data.SeedsData;
using Domain.Common;
using Domain.DTOs.Notification;
using Domain.DTOs.Telegram;
using Domain.Entities.Agent;
using Domain.Entities.Apple;
using Domain.Entities.Authorization;
using Domain.Entities.Category;
using Domain.Entities.Country;
using Domain.Entities.Marzban;
using Domain.Entities.Menu;
using Domain.Entities.Notification;
using Domain.Entities.Order;
using Domain.Entities.Product;
using Domain.Entities.Registry;
using Domain.Entities.Sanaei;
using Domain.Entities.Server;
using Domain.Entities.Subscription;
using Domain.Entities.Telegram;
using Domain.Entities.Transaction;
using Domain.Entities.Vpn;
using Domain.Entities.Wireguard;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using User = Domain.Entities.Account.User;

namespace Data.Context;

public class DigitallDbContext : DbContext
{
    public DigitallDbContext(DbContextOptions<DigitallDbContext> options) : base(options)
    {
    }

    #region db set

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<Menu> Menu { get; set; }
    public DbSet<RoleMenus> RoleMenus { get; set; }
    public DbSet<Agent> Agent { get; set; }
    public DbSet<Category> Category { get; set; }
    public DbSet<Product> Product { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<TransactionDetail> TransactionDetail { get; set; }
    public DbSet<Server> Server { get; set; }
    public DbSet<Subscribtion> Subscriptions { get; set; }
    public DbSet<Vpn> Vpn { get; set; }
    public DbSet<Order> Order { get; set; }
    public DbSet<OrderDetail> OrderDetail { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<VpnCountry> VpnCountry { get; set; }
    public DbSet<Inbound> Inbounds { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<MarzbanServer> MarzbanServers { get; set; }
    public DbSet<MarzbanVpn> MarzbanVpns { get; set; }
    public DbSet<MarzbanUser> MarzbanUsers { get; set; }
    public DbSet<MarzbanVpnTemplate> MarzbanVpnTemplates { get; set; }
    public DbSet<TelegramBot> TelegramBots { get; set; }
    public DbSet<Notification> Notification { get; set; }
    public DbSet<AgentOptions> AgentOptions { get; set; }
    public DbSet<AgentRequest> AgentRequest { get; set; }
    public DbSet<AgentsIncomesDetail> AgentsIncomesDetail { get; set; }
    public DbSet<TelegramButtons> TelegramButtons { get; set; }
    public DbSet<TelegramTopic> TelegramTopics { get; set; }
    public DbSet<TelegramGroup> TelegramGroups { get; set; }
    public DbSet<TelegramGroupTopics> TelegramGroupTopics { get; set; }
    public DbSet<WireguardServer> WireguardServers { get; set; }
    public DbSet<WireguardVpn> WireguardVpn { get; set; }
    public DbSet<WireguardVpnTemplate> WireguardVpnTemplates { get; set; }
    public DbSet<Peer> Peers { get; set; }
    public DbSet<AppleId> AppleId { get; set; }
    public DbSet<AppleIdType> AppleIdTypes { get; set; }
    public DbSet<Registry> Registries { get; set; }
    public DbSet<RegistrationOptions> RegistrationOptions { get; set; }

    #endregion

    #region properties

    /// <summary>
    /// set auto when update or delete
    /// </summary>
    public long UserId { get; set; }

    #endregion

    #region config

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreateDate = DateTime.Now;
                    entry.Entity.CreateBy = UserId;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedDate = DateTime.Now;
                    entry.Entity.ModifyBy = UserId;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreateDate = DateTime.Now;
                    entry.Entity.CreateBy = UserId;
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedDate = DateTime.Now;
                    entry.Entity.ModifyBy = UserId;
                    break;
            }
        }

        return base.SaveChanges();
    }

    #endregion

    #region flounet api

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(s => s.GetForeignKeys()))
        {
            if (!relationship.IsSelfReferencing())
            {
                relationship.DeleteBehavior = DeleteBehavior.Cascade;
            }
        }

        #region seeds data

        modelBuilder.AddData(AgentItems.Agents);
        modelBuilder.AddData(UserItems.Users);
        modelBuilder.AddData(RoleItems.Roles);
        modelBuilder.AddData(MenuItems.Menu);
        modelBuilder.AddData(UserRoleItems.UserRoles);
        modelBuilder.AddData(RoleMenuItems.RoleMenus);
        modelBuilder.AddData(PermissionsItems.Permissions);
        modelBuilder.AddData(RolePermissionItems.RolePermissions);
        modelBuilder.AddData(TransactionDetailItems.TransactionDetails);
        modelBuilder.AddData(TelegramBotItems.TelegramBots);
        modelBuilder.AddData(TelegramButtonItems.TelegramButtons);
        modelBuilder.AddData(TelegramGroupItems.TelegramGroups);
        modelBuilder.AddData(TelegramTopicItems.TelegramTopics);
        modelBuilder.AddData(TelegramGroupTopicItems.TelegramGroupTopics);

        #endregion

        #region agent id started

        //modelBuilder.Entity<Agent>(b =>
        //{
        //    b.ToTable("Agent");
        //    b.Property<long>(x => x.Id).ValueGeneratedOnAdd().UseIdentityColumn(10000, 1);
        //});

        #endregion

        base.OnModelCreating(modelBuilder);
    }

    #endregion
}