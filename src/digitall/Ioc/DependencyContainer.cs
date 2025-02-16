﻿using Data.Repositories.Account;
using Data.Repositories.Agent;
using Data.Repositories.Authorization;
using Data.Repositories.Base;
using Data.Repositories.Agent;
using Data.Repositories.Category;
using Data.Repositories.Menu;
using Data.Repositories.Server;
using Data.Repositories.Transaction;
using Domain.IRepositories.Account;
using Domain.IRepositories.Agent;
using Domain.IRepositories.Authorization;
using Domain.IRepositories.Base;
using Domain.IRepositories.Category;
using Domain.IRepositories.Menu;
using Domain.IRepositories.Server;
using Domain.IRepositories.Transaction;
using Microsoft.Extensions.DependencyInjection;
using Domain.IRepositories.Product;
using Data.Repositories.Product;
using Data.Repositories.Country;
using Data.Repositories.Marzban;
using Data.Repositories.Order;
using Data.Repositories.Sanaei;
using Domain.Entities.Marzban;
using Domain.IRepositories.Country;
using Domain.IRepositories.Marzban;
using Domain.IRepositories.Order;
using Domain.IRepositories.Sanaei;
using Domain.IRepositories.Vpn;
using Application.Services.Interface.Order;
using Application.Services.Interface.Category;
using Application.Services.Implementation.Country;
using Application.Services.Interface.Menu;
using Application.Services.Interface.Authorization;
using Application.Services.Implementation.Server;
using Application.Services.Implementation.Account;
using Application.Services.Interface.Account;
using Application.Services.Interface.Notification;
using Application.Services.Implementation.Sanaei;
using Application.Services.Implementation.Transaction;
using Application.Services.Interface.Product;
using Application.Services.Interface.Vpn;
using Application.Services.Implementation.Product;
using Application.Services.Interface.Country;
using Application.Services.Implementation.Order;
using Application.Services.Interface.Server;
using Application.Services.Interface.Agent;
using Application.Services.Interface.Sanaei;
using Application.Services.Interface.Transaction;
using Application.Services.Implementation.Category;
using Application.Services.Interface.Marzban;
using Application.Services.Implementation.Menu;
using Application.Services.Implementation.Agent;
using Application.Senders.Sms;
using Application.Services.Implementation.Apple;
using Application.Services.Implementation.Authorization;
using Application.Services.Implementation.Docker;
using Application.Services.Implementation.Vpn;
using Application.Services.Implementation.Marzban;
using Application.Services.Implementation.Notification;
using Application.Services.Implementation.Registry;
using Application.Services.Implementation.Telegram;
using Application.Services.Implementation.Wireguard;
using Application.Services.Interface.Apple;
using Application.Services.Interface.Docker;
using Application.Services.Interface.Registry;
using Application.Services.Interface.Telegram;
using Application.Services.Interface.Wireguard;
using Data.Migrations;
using Data.Repositories;
using Data.Repositories.Apple;
using Data.Repositories.Notification;
using Data.Repositories.Registry;
using Data.Repositories.Telegram;
using Data.Repositories.Vpn;
using Domain.IRepositories.Apple;
using Domain.IRepositories.Notification;
using Domain.IRepositories.Registry;
using Domain.IRepositories.Telegram;
using Domain.IRepositories.Wireguard;

namespace Ioc;

public static class DependencyContainer
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        #region repository

        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IMenuRepository, MenuRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ITransactionDetailRepository, TransactionDetailRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRoleMenuRepository, RoleMenuRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IVpnRepository, VpnRepository>();
        services.AddScoped<IOrderDetailRepository, OrderDetailRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<ICountryRepository, CountryRepository>();
        services.AddScoped<IVpnCountryRepository, VpnCountryRepository>();
        services.AddScoped<IInboundRepository, InboundRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IMarzbanServerRepository, MarzbanServerRepository>();
        services.AddScoped<IMarzbanVpnRepository, MarzbanVpnRepository>();
        services.AddScoped<IMarzbanUserRepository, MarzbanUserRepository>();
        services.AddScoped<IMarzbanVpnTemplatesRepository, MarzbanVpnTemplatesRepository>();
        services.AddScoped<ITelegramBotRepository, TelegramBotRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAgentOptionRepository, AgentOptionRepository>();
        services.AddScoped<IAgentRequestRepository, AgentRequestRepository>();
        services.AddScoped<IAgentsIncomesDetailRepository, AgentsIncomesDetailRepository>();
        services.AddScoped<ITelegramGroupTopicRepository, TelegramGroupTopicRepository>();
        services.AddScoped<IWireguardVpnRepository, WireguardVpnRepository>();
        services.AddScoped<IWireguardServerRepository, WireguardServerRepository>();
        services.AddScoped<IPeerRepository, PeerRepository>();
        services.AddScoped<IWireguardVpnTemplateRepository, WireguardVpnTemplateRepository>();
        services.AddScoped<ITelegramUserRepository, TelegramUserRepository>();
        services.AddScoped<IAppleIdRepository, AppleIdRepository>();
        services.AddScoped<IAppleIdTypeRepository, AppleIdTypeRepository>();
        services.AddScoped<IRegistryRepository, RegistryRepository>();
        services.AddScoped<IRegistrationOptionsRepository, RegistrationOptionsRepository>();
        
        #endregion

        #region services

        services.AddMemoryCache();

        services.AddScoped(typeof(ISendNotificationService<>), typeof(SendSmsService<>));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IAuthorizeService, AuthorizeService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IServerService, ServerService>();
        services.AddScoped<IAuthorizeService, AuthorizeService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ISanaeiService, SanaeiService>();
        services.AddScoped<IVpnService, VpnService>();
        services.AddScoped<ICountryService, CountryService>();
        services.AddScoped<IMarzbanService, MarzbanServies>();
        services.AddScoped<ITelegramService, TelegramService>();
        services.AddScoped<IDockerService, DockerService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IWireguardServices, WireguardService>();
        services.AddScoped<IAppleService, AppleService>();
        services.AddScoped<IRegistryService, RegistryService>();
        
        #endregion

        return services;
    }
}