﻿using Domain.Entities.Telegram;

namespace Data.DefaultData;

public static class TelegramBotItems
{ 
    public static List<TelegramBot> TelegramBots = new()
    {
        new()
        {
            Description = "ربات مستر ما",
            AgentId = 100001,
            CreateDate = DateTime.Now,
            BotId = 7419690675,
            Name = "master_digitall_vpn_bot",
            Link = "https://t.me/master_digitall_vpn_bot",
            Route = "/master_digitall_vpn_bot",
            HostAddress = "https://test.samanii.com",
            Token = "7419690675:AAGpFGOAt_Nei0qQoppFct1V9NdY4MfzinE",
            PersionName = "ربات اصلی مستر",
            IsDelete = false,
            CreateBy = 1,
            Id = 1,
            SecretToken = "",
            ModifiedDate = DateTime.Now,
            ModifyBy = 1,
        }
    };
}