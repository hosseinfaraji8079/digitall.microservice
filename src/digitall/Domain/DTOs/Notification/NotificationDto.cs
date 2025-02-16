﻿using System.ComponentModel.DataAnnotations;
using Domain.DTOs.Telegram;
using Domain.Entities.Account;
using Domain.Enums.Notification;

namespace Domain.DTOs.Notification;

public class NotificationDto
{
    public DateTime Expire { get; set; }
    public NotificationType NotificationType { get; set; }
    public string? Message { get; set; }
    public long? BotId { get; set; }
    public long? ChatId { get; set; }
    public long Id { get; set; }
    public string? FileAddress { get; set; }
    public List<ButtonJsonDto?>? Buttons { get; set; }
    public string? FileCaption { get; set; }
    public bool Forward { get; set; }
    public long? ForwardChatId { get; set; }
    public int? MessageId { get; set; }
    
    public NotificationDto()
    {
    }

    public NotificationDto(Entities.Notification.Notification notification)
    {
        Expire = notification.Expire;
        this.NotificationType = notification.NotificationType;
        Message = notification.Message;
        BotId = notification.User?.BotId;
        ChatId = notification.User?.ChatId;
        Id = notification.Id;
        Buttons = notification!.Buttons!;
        FileAddress = notification.FileAddress;
        FileCaption = notification.FileCaption;
        Forward = notification.Forward;
        ForwardChatId = notification.ForwarderChatId;
        MessageId = notification.MessageId;
    }
}