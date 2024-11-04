﻿using Application.Services.Interface.Account;
using Application.Services.Interface.Agent;
using Application.Services.Interface.Notification;
using Application.Static.Template;
using Domain.DTOs.Agent;
using Domain.DTOs.Notification;
using Domain.Enums.Notification;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Application.Jobs;

public class CheckAndNotifyNegativeBalanceJob(IServiceScopeFactory serviceScopeFactory) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var agentService = scope.ServiceProvider.GetRequiredService<IAgentService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        try
        {
            List<AgentDto> agents = await agentService.AgentsReachedNegativeLimit();
            
            foreach (var agent in agents)
            {
                if (agent.DisabledAccountTime is null)
                {
                    agent.DisabledAccountTime = DateTime.Now.AddHours(24);
                    await agentService.UpdateAgentAsync(agent, 1);
                }

                if (((agent.DisabledAccountTime ?? DateTime.Now) - DateTime.Now).Minutes <= 0)
                {
                    // todo : disabled account

                    await userService.DisabledAllUserAccount(agent.AgentAdminId);
                    // await notificationService.AddNotificationsAsync()
                }

                await notificationService.AddNotificationAsync(
                    NotificationTemplate.AlterForDisabledAccounts(agent.DisabledAccountTime ?? DateTime.Now,
                        agent.AgentAdminId),1);
            }
        }
        catch (Exception e)
        {
            await notificationService.AddNotificationAsync(new AddNotificationDto()
            {
                Message = $"""
                           هنگام اجرای job CheckAndNotifyNegativeBalanceJob به مشکل خوردیم.
                           {e.Message}
                           {e.Data}
                           {e.InnerException}
                           """,
                NotificationType = NotificationType.BogsReports,
                UserId = 1,
            }, 1);
        }

    }
}