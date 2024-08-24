﻿using Application.Services.Interface.Notification;
using Application.Services.Interface.Telegram;
using Domain.DTOs.Notification;
using Domain.DTOs.Telegram;
using Domain.Entities.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Application.Jobs;

public class SendTelegramNotificationJob : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public SendTelegramNotificationJob(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();

            var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            List<NotificationDto> notifications = await notificationService.GetNotificationsAsync();

            foreach (var notification in notifications)
            {
                if (notification.ChatId is not null && notification.BotId is not null)
                {
                    TelegramBotDto? bot = await telegramService.GetTelegramBotByBotIdAsync(notification.BotId ?? 0);

                    var options = new TelegramBotClientOptions(bot.Token!);

                    var botClient = new TelegramBotClient(options);

                    IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();
                    if (notification.Buttons is not null)
                    {
                        for (int i = 0; i < notification.Buttons.Count; i++)
                        {
                            ButtonJsonDto? button_1 = notification.Buttons[i]!;
                            ButtonJsonDto? button_2 =
                                (i + 1 < notification.Buttons.Count) ? notification.Buttons[i + 1]! : null;

                            if (button_1 is not null)
                            {
                                List<InlineKeyboardButton> key = new()
                                {
                                    InlineKeyboardButton.WithCallbackData(button_1.Text, button_1.CallbackQuery),
                                };

                                if (button_2 is not null)
                                    key.Add(
                                        InlineKeyboardButton.WithCallbackData(button_2.Text, button_2.CallbackQuery));

                                keys.Add(key);
                            }

                            i++;
                        }
                    }

                    if (!string.IsNullOrEmpty(notification.FileAddress))
                    {
                        using (var stream = new FileStream(notification.FileAddress, FileMode.Open, FileAccess.Read,
                                   FileShare.Read))
                        {
                            var inputOnlineFile =
                                new InputFileStream(stream, Path.GetFileName(notification.FileAddress));

                            await botClient.SendPhotoAsync(
                                chatId: notification.ChatId,
                                photo: inputOnlineFile,
                                caption: "فایل ارسالی",
                                cancellationToken: default
                            );
                        }
                    }

                    await botClient.SendTextMessageAsync(
                        chatId: notification.ChatId,
                        text: notification.Message ?? "",
                        replyMarkup: new InlineKeyboardMarkup(keys)
                    );

                    await notificationService.UpdateSendNotification(notification.Id);

                    Thread.Sleep(500);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}