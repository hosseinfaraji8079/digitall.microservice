﻿using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Runtime.InteropServices.JavaScript;
using System.Web;
using Application.Extensions;
using Application.Helper;
using Application.Services.Interface.Notification;
using Application.Services.Interface.Telegram;
using Application.Sessions;
using Application.Static.Template;
using Application.Utilities;
using Data.Migrations;
using Domain.DTOs.Agent;
using Domain.DTOs.Marzban;
using Domain.DTOs.Notification;
using Domain.DTOs.Telegram;
using Domain.DTOs.Transaction;
using Domain.Entities.Agent;
using Domain.Entities.Marzban;
using Domain.Entities.Telegram;
using Domain.Enums.Agent;
using Domain.Enums.Marzban;
using Domain.Enums.Transaction;
using Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = Telegram.Bot.Types.File;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using User = Domain.Entities.Account.User;

namespace Application.Services.Implementation.Telegram;

public class BotService(
    ITelegramService telegramService,
    INotificationService notificationService,
    IWebHostEnvironment _env) : IBotService
{
    private static ConcurrentDictionary<long, TelegramMarzbanVpnSession>? userSessions =
        new ConcurrentDictionary<long, TelegramMarzbanVpnSession>();

    public async Task SendUserForLoginToWebAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            string? password = await telegramService.ResetUserPasswordAsync(chatId);

            #region message

            string information = $@"
👤 نام کاربری شما :{chatId}       
       🔐 کلمه عبور جدید: {password}
";

            #endregion

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: information,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task<Message> StartLinkAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long agentId = 0;
        try
        {
            if (message.Text != null && message.Text.StartsWith("/start"))
            {
                Int64.TryParse((message.Text.Substring(6)), out agentId);
            }

            AgentOptionDto? agentOptions = await telegramService.StartTelegramBotAsync(new StartTelegramBotDto()
            {
                AgentCode = agentId,
                ChatId = message.Chat.Id,
                FirstName = message.From.FirstName,
                LastName = message.From.LastName,
                BotId = botClient.BotId,
                TelegramUsername = message.From.Username
            });

            IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

            keys.Add(new List<InlineKeyboardButton>()
            {
                InlineKeyboardButton.WithCallbackData("تست رایگان 😎", "test_free"),
                InlineKeyboardButton.WithCallbackData("خرید اشتراک 🔒", "subscribe")
            });

            keys.Add(new List<InlineKeyboardButton>()
            {
                InlineKeyboardButton.WithCallbackData("سرویس های من 🎁", "my_services"),
            });

            keys.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("در خواست نمایندگی ♻️", "agent_request"),
                InlineKeyboardButton.WithCallbackData("کیف پول + شارژ 🏦", "wallet")
            });

            keys.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("کلمه عبور و نام کاربری سایت 🔒",
                    "web_information")
            });

            keys.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("همکاری در فروش 🤝",
                    "invitation_link")
            });

            InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keys);

            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: agentOptions!.WelcomeMessage ?? "خوش آمدید",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            return await botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendMainMenuAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken, string? title = null)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        bool isAgent = await telegramService.IsAgentAsyncByChatIdAsync(chatId);

        if (isAgent)
            await SendAgentMenuForAdmin(botClient, chatId, cancellationToken);
        else
            await DeleteMenu(botClient, callbackQuery.Message, cancellationToken);

        BotSessions
            .users_Sessions?
            .AddOrUpdate(chatId, new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.None),
                (key, old)
                    => old = new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.None));

        IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

        keys.Add(new List<InlineKeyboardButton>()
        {
            InlineKeyboardButton.WithCallbackData("تست رایگان 😎", "test_free"),
            InlineKeyboardButton.WithCallbackData("خرید اشتراک 🔒", "subscribe")
        });

        keys.Add(new List<InlineKeyboardButton>()
        {
            InlineKeyboardButton.WithCallbackData("سرویس های من 🎁", "my_services"),
            InlineKeyboardButton.WithCallbackData("پشتیبانی",
                "send_message")
        });

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("در خواست نمایندگی ♻️", "agent_request"),
            InlineKeyboardButton.WithCallbackData("کیف پول + شارژ 🏦", "wallet")
        });

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("کلمه عبور و نام کاربری سایت 🔒",
                "web_information")
        });

        if (isAgent)
            keys.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("همکاری در فروش 🤝",
                    "invitation_link")
            });


        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }

        InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keys);

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: title ?? "به منو اصلی بازگشتید 🏠",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    public async Task<Message> SendMainMenuAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken,
        string? title = null)
    {
        long chatId = message.Chat.Id;

        bool isAgent = await telegramService.IsAgentAsyncByChatIdAsync(chatId);

        if (isAgent)
            await SendAgentMenuForAdmin(botClient, chatId, cancellationToken);
        else
            await DeleteMenu(botClient, message, cancellationToken);

        BotSessions
            .users_Sessions?
            .AddOrUpdate(chatId, new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.None),
                (key, old)
                    => old = new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.None));

        IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

        keys.Add(new List<InlineKeyboardButton>()
        {
            InlineKeyboardButton.WithCallbackData("تست رایگان 😎", "test_free"),
            InlineKeyboardButton.WithCallbackData("خرید اشتراک 🔒", "subscribe")
        });

        keys.Add(new List<InlineKeyboardButton>()
        {
            InlineKeyboardButton.WithCallbackData("سرویس های من 🎁", "my_services"),
        });

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("در خواست نمایندگی ♻️", "agent_request"),
            InlineKeyboardButton.WithCallbackData("کیف پول + شارژ 🏦", "wallet")
        });

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("کلمه عبور و نام کاربری سایت 🔒",
                "web_information")
        });

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("همکاری در فروش 🤝",
                "invitation_link")
        });

        if (message.MessageId != 0)
        {
            await botClient.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
        }

        InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keys);

        return await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: title ?? "به منو اصلی بازگشتید 🏠",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    public async Task SendListVpnsAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

        List<MarzbanVpnBotDto> vpns = await telegramService.GetMarzbanVpnsAsync();

        foreach (MarzbanVpnBotDto vpn in vpns)
        {
            List<InlineKeyboardButton> button = new()
            {
                InlineKeyboardButton.WithCallbackData(vpn.Title, "vpn_template?id=" + vpn.Id)
            };
            keys.Add(button);
        }

        List<InlineKeyboardButton> home = new()
        {
            InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
        };

        keys.Add(home);

        InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keys);

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "اشتراک خود را انتخاب نمایید. 📌",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }
    }

    public async Task SendListVpnsHaveTestAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

        List<MarzbanVpnTestDto> vpns = await telegramService.GetListMarzbanVpnTestAsync();

        foreach (MarzbanVpnTestDto vpn in vpns)
        {
            List<InlineKeyboardButton> button = new()
            {
                InlineKeyboardButton.WithCallbackData(vpn.Title, "createtestsub?id=" + vpn.Id)
            };
            keys.Add(button);
        }

        List<InlineKeyboardButton> home = new()
        {
            InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
        };

        keys.Add(home);

        InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keys);

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: " موقعیت سرویس را انتخاب نمایید. 📌",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }
    }

    public async Task SendTestSubscibeAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        long id = 0;

        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');
        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int64.TryParse(queryParameters["id"], out id);
        }

        MarzbanUserInformationDto user =
            await telegramService
                .GetMarzbanTestVpnsAsync(id, callbackQuery!.Message!.Chat.Id);

        MarzbanVpnDto? vpn = await telegramService
            .GetMarzbanVpnInformationByIdAsync(id, chatId);

        byte[] QrImage = await GenerateQrCode
            .GetQrCodeAsync(user.Subscription_Url);

        string caption = $@"
✅ سرویس با موفقیت ایجاد شد

👤 نام کاربری سرویس: {user.Username.TrimEnd()}
🌿 نام سرویس: {vpn.Name.TrimEnd()}
⏳ مدت زمان: {vpn.Test_Days} روز
🗜 حجم سرویس: {vpn.Test_TotalGb} مگابایت
لینک اتصال:
{user.Subscription_Url.TrimEnd()}
";
        using (var Qr = new MemoryStream(QrImage))
        {
            await botClient.SendPhotoAsync(
                chatId: callbackQuery.Message.Chat.Id,
                photo: new InputFileStream(Qr, user.Username),
                caption: caption,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendListVpnsTemplateAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

            long id = 0;
            long subscribeId = 0;
            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');
            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["id"], out id);
                Int64.TryParse(queryParameters["subscribeId"], out subscribeId);
            }

            List<MarzbanVpnTemplateDto>
                templates = await telegramService.GetMarzbanVpnTemplatesByVpnIdAsync(id, chatId);

            var groupedTemplates = templates.GroupBy(x => x.Days);
            
            foreach (var group in groupedTemplates)
            {
                var firstTemplate = group.First();
                
                string text = group.Key switch
                {
                    31 => "یک ماه",
                    61 => "دو ماه",
                    91 => "سه ماه",
                    121 => "چهار ماه",
                    151 => "پنج ماه",
                    181 => "شش ماه",
                    211 => "هفت ماه",
                    241 => "هشت ماه",
                    271 => "نه ماه",
                    301 => "ده ماه",
                    331 => "یازده ماه",
                    361 => "یک سال",
                    _ => firstTemplate.Days + " روزه "
                };


                List<InlineKeyboardButton> button = new()
                {
                    InlineKeyboardButton.WithCallbackData(text,
                        "sendvpntemplate?id=" + firstTemplate.Id + "&vpnId=" + id + "&subscribeId=" + subscribeId +
                        "&days=" + firstTemplate.Days)
                };

                keys.Add(button);
            }
            
            List<InlineKeyboardButton> custom = new()
            {
                // InlineKeyboardButton.WithCallbackData("\ud83d\udecd حجم و زمان دلخواه",
                //     "custom_subscribe?vpnId=" + id + "&subscribeId" + subscribeId)
            };

            List<InlineKeyboardButton> home = new()
            {
                InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
            };

            keys.Add(custom);
            keys.Add(home);

            InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keys);

            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: " نوع سرویس خود را انتخاب کنید. 📌",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);

            if (callbackQuery.Message.MessageId != 0)
            {
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            }
        }
        catch (Exception e)
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
            await SendMainMenuAsync(botClient, callbackQuery, cancellationToken);
        }
    }

    public async Task SendFactorVpnAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        long id = 0;
        long vpnId = 0;
        long subscribeId = 0;
        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');

        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int64.TryParse(queryParameters["id"], out id);
            Int64.TryParse(queryParameters["vpnId"], out vpnId);
            Int64.TryParse(queryParameters["subscribeId"], out subscribeId);
        }

        BuyMarzbanVpnDto buy = new();

        buy.MarzbanVpnTemplateId = id;
        buy.MarzbanVpnId = vpnId;
        buy.Count = 1;

        IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

        List<InlineKeyboardButton> bu = new()
        {
            InlineKeyboardButton.WithCallbackData("پرداخت و دریافت سرویس", $"buy_subscribe" +
                                                                           $"?templateId={id}" +
                                                                           $"&marzbanvpnid={vpnId}" +
                                                                           $"&subscribeId={subscribeId}")
        };
        List<InlineKeyboardButton> home = new()
        {
            InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
        };

        keys.Add(bu);
        keys.Add(home);

        InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keys);

        SubscribeFactorBotDto sub = await telegramService.SendFactorSubscribeAsync(buy, chatId);

        KeyValuePair<long, TelegramMarzbanVpnSession>? userSesstion = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        TelegramMarzbanVpnSession? uservalue = userSesstion?.Value;

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: uservalue?.UserSubscribeId != null ? sub.GetRenewalInfo() : sub.GetInfo(),
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }
    }

    public async Task SendSubscriptionAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            long templateId = 0;
            long marzbanvpnid = 0;
            int days = 0;
            int gb = 0;
            long subscribeId = 0;

            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');
            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["templateId"], out templateId);
                Int64.TryParse(queryParameters["marzbanvpnid"], out marzbanvpnid);
                Int32.TryParse(queryParameters["days"], out days);
                Int32.TryParse(queryParameters["gb"], out gb);
                Int64.TryParse(queryParameters["subscribeId"], out subscribeId);
            }

            KeyValuePair<long, TelegramMarzbanVpnSession>? userSesstion = BotSessions
                .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

            TelegramMarzbanVpnSession? uservalue = userSesstion?.Value;

            BuyMarzbanVpnDto buy = new();

            buy.MarzbanVpnId = marzbanvpnid;
            buy.MarzbanVpnTemplateId = templateId;
            buy.Count = 1;
            buy.TotalDay = days;
            buy.TotalGb = gb;
            buy.MarzbanUserId = uservalue?.UserSubscribeId;

            MarzbanVpnTemplateDto? template = null;

            if (templateId != 0)
                template = await telegramService.GetMarzbanTemplateByIdAsync(templateId);

            List<MarzbanUser> marzbanUsers = await telegramService.BuySubscribeAsync(buy, chatId);

            foreach (MarzbanUser user in marzbanUsers)
            {
                byte[] QrImage = await GenerateQrCode
                    .GetQrCodeAsync(user?.Subscription_Url);

                string caption = $@"
✅ سرویس با موفقیت ایجاد شد

👤 نام کاربری سرویس: {user.Username.TrimEnd()}
🌿 نام سرویس: {template?.Title ?? "خرید اشتراک"}
⏳ مدت زمان: {template?.Days ?? days} روز
👥 حجم سرویس: {((template?.Gb ?? gb) > 200 ? "نامحدود" : (template?.Gb ?? gb) + "گیگ")}
لینک اتصال:
{user.Subscription_Url.TrimEnd()}
";
                using (var Qr = new MemoryStream(QrImage))
                {
                    await botClient.SendPhotoAsync(
                        chatId: callbackQuery.Message.Chat.Id,
                        photo: new InputFileStream(Qr, user.Subscription_Url),
                        caption: caption,
                        cancellationToken: cancellationToken);
                }
            }

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId, new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.None),
                    (key, old)
                        => old = new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.None));


            await SendMainMenuAsync(botClient, callbackQuery, cancellationToken);
        }
        catch (Exception e)
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
            await SendMainMenuAsync(botClient, callbackQuery, cancellationToken);
        }
    }

    public async Task SendListServicesAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;


        int page = 1;
        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');

        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int32.TryParse(queryParameters["page"], out page);
        }

        FilterMarzbanUser filter = new FilterMarzbanUser();
        User? user = await telegramService.GetUserByChatIdAsync(chatId);
        filter.UserId = user.Id;
        filter.Page = page;
        FilterMarzbanUser users = await telegramService.FilterMarzbanUsersList(filter);

        IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

        foreach (var us in users.Entities)
        {
            List<InlineKeyboardButton> key = new()
            {
                InlineKeyboardButton.WithCallbackData(us.Username, $"subscribe_info?id={us.Id}&vpnId={us.MarzbanVpnId}")
            };
            keys.Add(key);
        }

        List<InlineKeyboardButton> beforAfter = new();

        if (page != 1)
            beforAfter.Add(InlineKeyboardButton.WithCallbackData("قبلی",
                $"my_services?page={page - 1}"));
        if (page * filter.TakeEntity < filter.AllEntitiesCount)
            beforAfter.Add(InlineKeyboardButton.WithCallbackData("بعدی",
                $"my_services?page={page + 1}"));

        List<InlineKeyboardButton> home = new()
        {
            InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
        };

        keys.Add(beforAfter);
        keys.Add(home);

        InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(keys);

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text:
            "\ud83d\udecd اشتراک های خریداری شده توسط شما\n\n\u26a0\ufe0fبرای مشاهده اطلاعات و مدیریت روی نام کاربری کلیک کنید",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }
    }

    public async Task SendSubscribeInformationAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        long id = 0;
        int vpnId = 0;
        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');

        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int64.TryParse(queryParameters["id"], out id);
            Int32.TryParse(queryParameters["vpnId"], out vpnId);
        }

        SubescribeStatus.ServiceStatus? status = await telegramService.GetMarzbanUserByChatIdAsync(id, chatId);

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        TelegramMarzbanVpnSession? uservalue = user?.Value;

        if (uservalue is null)
            uservalue = new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.None, UserSubscribeId: id);

        uservalue.UserSubscribeId = id;

        BotSessions
            .users_Sessions?
            .AddOrUpdate(chatId, uservalue,
                (key, old)
                    => old = uservalue);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("تغییر لینک ⚙️", $"revoke_sub"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("دریافت ترافیک 🌍", $"get_traffic?id={id}"),
                InlineKeyboardButton.WithCallbackData("لینک اشتراک 🔗", $"subscription_link?id={id}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("تمدید سرویس 💊", $"vpn_template" +
                                                                        $"?id={vpnId}&" +
                                                                        $"subscribeId={id}"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("خرید حجم اضافه ➕",
                    $"custom_subscribe?vpnId={vpnId}&appendGb=true"),
                InlineKeyboardButton.WithCallbackData("خرید زمان اضافه ⌛️",
                    $"append_date?vpnId={vpnId}&subscribeId={id}"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("حذف سرویس ❌", $"delete_service")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("فعال کردن 🤞", "active_service"),
                InlineKeyboardButton.WithCallbackData(" غیر فعال کردن ❌", "disabled_service"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("بازگشت به لیست سرویس‌ها 🏠", "my_services")
            }
        });

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: status.GetInfo(),
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }
    }

    public async Task SendConfigsAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        int id = 0;
        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');

        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int32.TryParse(queryParameters["id"], out id);
        }

        List<string> links = await telegramService.GetMarzbanSubscibtionLiknsAsync(id, chatId);

        foreach (string link in links)
        {
            byte[] QrImage = await GenerateQrCode
                .GetQrCodeAsync(link);

            using (var Qr = new MemoryStream(QrImage))
            {
                await botClient.SendPhotoAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    photo: new InputFileStream(Qr, link),
                    caption: link,
                    cancellationToken: cancellationToken);
            }
        }
    }

    public async Task SendSubscribeAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        int id = 0;
        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');

        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int32.TryParse(queryParameters["id"], out id);
        }

        string? subscribe = await telegramService.GetSubscibetionAsync(id, chatId);

        byte[] QrImage = await GenerateQrCode
            .GetQrCodeAsync(subscribe);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("بازگشت 🌍", $"subscribe_info?id={id}"),
            },
        });

        using (var Qr = new MemoryStream(QrImage))
        {
            await botClient.SendPhotoAsync(
                chatId: callbackQuery.Message.Chat.Id,
                photo: new InputFileStream(Qr, subscribe),
                caption: subscribe,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendGbPriceAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            int vpnId = 0;
            bool appendGb = false;
            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');
            long subscribeId = 0;

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Boolean.TryParse(queryParameters["appendGb"], out appendGb);
                Int32.TryParse(queryParameters["vpnId"], out vpnId);
                Int64.TryParse(queryParameters["subscribeId"], out subscribeId);
            }

            MarzbanVpnDto? vpn = await telegramService.GetMarzbanVpnInformationByIdAsync(vpnId, chatId);

            KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
                .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

            TelegramMarzbanVpnSession? uservalue = user?.Value;
            if (uservalue is null)
                uservalue = new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.None);

            uservalue.VpnId = vpnId;
            if (!appendGb)
            {
                uservalue.State = TelegramMarzbanVpnSessionState.AwaitingGb;

                BotSessions
                    .users_Sessions?
                    .AddOrUpdate(chatId,
                        new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingGb, vpnId: vpnId),
                        (key, old)
                            => old = old);
            }
            else
            {
                uservalue.State = TelegramMarzbanVpnSessionState.AwaitingSendAppendGbForService;

                BotSessions
                    .users_Sessions?
                    .AddOrUpdate(chatId,
                        new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendAppendGbForService,
                            vpnId: vpnId),
                        (key, old)
                            => old = old);
            }

            string deatils = $@"📌 حجم درخواستی خود را ارسال کنید.
🔔قیمت هر گیگ حجم {vpn?.GbPrice ?? 0} تومان می باشد.
🔔 حداقل حجم {vpn?.GbMin ?? 0} گیگابایت و حداکثر {vpn?.GbMax ?? 0} گیگابایت می باشد.";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
                }
            });

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: deatils,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendDaysPriceAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);
        var uservalue = user.Value.Value;

        MarzbanVpnDto? vpn = await telegramService.GetMarzbanVpnInformationByIdAsync(uservalue.VpnId ?? 0, chatId);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
            }
        });

        if (uservalue.Gb > vpn?.GbMax | uservalue.Gb < vpn?.GbMin | uservalue.Gb == 0)
        {
            uservalue.State = TelegramMarzbanVpnSessionState.AwaitingGb;
            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId, uservalue,
                    (key, old)
                        => old = uservalue);

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text:
                $"\u274c حجم نامعتبر است.\n\ud83d\udd14 حداقل حجم {vpn.GbMin} گیگابایت و حداکثر {vpn.GbMax} گیگابایت می باشد",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        else
        {
            uservalue.State = TelegramMarzbanVpnSessionState.AwaitingDate;

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId, uservalue,
                    (key, old)
                        => old = uservalue);

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text:
                $"\u231b\ufe0f زمان سرویس خود را انتخاب نمایید \n\ud83d\udccc تعرفه هر روز  : {vpn?.DayPrice}  تومان\n\u26a0\ufe0f حداقل زمان {vpn?.DayMin} روز  و حداکثر {vpn?.DayMax} روز  می توانید تهیه کنید",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendCustomFactorVpnAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        var uservalue = user.Value.Value;

        MarzbanVpnDto? vpn = await telegramService.GetMarzbanVpnInformationByIdAsync(uservalue.VpnId ?? 0, chatId);
        User? mainUser = await telegramService.GetUserByChatIdAsync(chatId);


        if (uservalue.Date > vpn?.DayMax | uservalue.Date < vpn?.DayMin | uservalue.Date == 0)
        {
            uservalue.State = TelegramMarzbanVpnSessionState.AwaitingDate;
            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId, uservalue,
                    (key, old)
                        => old = uservalue);

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
                }
            });

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text:
                $"\u274c زمان ارسال شده نامعتبر است . زمان باید بین {vpn.DayMin} روز تا {vpn.DayMax} روز باشد",
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        else
        {
            uservalue.State = TelegramMarzbanVpnSessionState.AwaitingFactor;

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId, uservalue,
                    (key, old)
                        => old = uservalue);

            string payment = uservalue.UserSubscribeId == null ? "دریافت سرویس" : $"تمدید سرویس";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData($"پرداخت و {payment}", $"buy_subscribe" +
                        $"?marzbanvpnid={uservalue.VpnId}" +
                        $"&gb={uservalue.Gb}" +
                        $"&days={uservalue.Date}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
                }
            });

            SubscribeFactorBotDto sub = new()
            {
                Days = uservalue.Date ?? 0,
                Gb = uservalue.Gb ?? 0,
                Balance = mainUser.Balance,
                Count = 1,
                Price = ((uservalue.Gb * vpn.GbPrice) + (uservalue.Date * vpn.DayPrice)) * 1 ?? 0,
                Title = "خرید اشتراک"
            };

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: uservalue.UserSubscribeId != null ? sub.GetRenewalInfo() : sub.GetInfo(),
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);

            if (message.MessageId != 0)
            {
                await botClient!.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
            }
        }
    }

    public async Task SendUserInformationAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        User? user = await telegramService.GetUserByChatIdAsync(chatId);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("\ud83d\udcb0 افزایش موجودی", "inventory_increase")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
            }
        });

        string information = @$"🗂 اطلاعات حساب کاربری شما :

👤 نام: {user.UserFullName()}
📱 شماره تماس :🔴 {user.Mobile ?? " ارسال نشده است "} 🔴
💰 موجودی: {user.Balance} تومان";


        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: information,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }
    }

    public async Task SendTransactionDetailsAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            TransactionDetailDto? transactionDetail = await telegramService.GetTransactionDetailAsync(chatId);

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId, new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendPrice),
                    (key, old)
                        => old = new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendPrice));

            KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
                .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

            TelegramMarzbanVpnSession uservalue = user.Value.Value;

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
                }
            });

            if (transactionDetail is null)
                throw new ApplicationException("پرداخت کارت به کارت برای شما غیر فعال است.");

            bool isAgent = await telegramService.IsAgentAsyncByChatIdAsync(chatId);

            string information = "";

            if (isAgent)
                information =
                    $"\ud83d\udcb8 مبلغ را به تومان وارد کنید:\n\u2705 حداقل مبلغ {transactionDetail.MinimalAmountForAgent} حداکثر مبلغ {transactionDetail.MaximumAmountForAgent} تومان می باشد";
            else
                information =
                    $"\ud83d\udcb8 مبلغ را به تومان وارد کنید:\n\u2705 حداقل مبلغ {transactionDetail.MinimalAmountForUser} حداکثر مبلغ {transactionDetail.MaximumAmountForUser} تومان می باشد";

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId, uservalue,
                    (key, old)
                        => old = uservalue);

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: information,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendCardNumberAndDetailAsync(ITelegramBotClient? botClient, Message? message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        TransactionDetailDto? transactionDetail = await telegramService.GetTransactionDetailAsync(chatId);

        var uservalue = user.Value.Value;

        bool isAgent = await telegramService.IsAgentAsyncByChatIdAsync(chatId);

        if (isAgent)
        {
            if (transactionDetail!.MaximumAmountForAgent < uservalue.Price |
                transactionDetail.MinimalAmountForAgent > uservalue.Price)
            {
                string exText =
                    $"\u274c خطا \n\ud83d\udcac مبلغ باید حداقل {transactionDetail.MinimalAmountForAgent} تومان و حداکثر {transactionDetail.MaximumAmountForAgent} تومان باشد";

                await Task.CompletedTask;
                throw new AppException(exText);
            }
        }
        else
        {
            if (transactionDetail!.MaximumAmountForUser < uservalue.Price |
                transactionDetail.MinimalAmountForUser > uservalue.Price)
            {
                string exText =
                    $"\u274c خطا \n\ud83d\udcac مبلغ باید حداقل {transactionDetail.MinimalAmountForUser} تومان و حداکثر {transactionDetail.MaximumAmountForUser} تومان باشد";

                await Task.CompletedTask;
                throw new AppException(exText);
            }
        }

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ پرداخت کردم  | ارسال رسید", "send_transaction_image")
            }
        });

        string text =
            $@"برای افزایش موجودی، مبلغ {uservalue.Price:No}  تومان  را به شماره‌ی حساب زیر واریز کنید 👇🏻
        
        ==================== 
        {transactionDetail.CardNumber}
        {transactionDetail.CardHolderName}
        ====================

‼️مبلغ باید همان مبلغی که در بالا ذکر شده واریز نمایید.
‼️امکان برداشت وجه از کیف پول نیست.
‼️مسئولیت واریز اشتباهی با شماست.
🔝بعد از پرداخت  دکمه پرداخت کردم را زده سپس تصویر رسید را ارسال نمایید
💵بعد از تایید پرداختتون توسط ادمین کیف پول شما شارژ خواهد شد و در صورتی که سفارشی داشته باشین انجام خواهد شد";

        User? current_user = await telegramService.GetUserByChatIdAsync(chatId);
        AgentDto? agent = await telegramService.GetAgentByChatIdAsync(chatId);

        if (string.IsNullOrEmpty(transactionDetail.CardNumber) || !current_user.CardToCardPayment)
        {
            if (!current_user.CardToCardPayment)
            {
                await notificationService.AddNotificationAsync(
                    NotificationTemplate
                        .ErrorForAddTransactionNotification(agent.AgentAdminId, current_user.TelegramUsername,
                            current_user.ChatId ?? 0, uservalue.Price, true), current_user.Id
                );
            }
            else
            {
                await notificationService.AddNotificationAsync(
                    NotificationTemplate
                        .ErrorForAddTransactionNotification(agent.AgentAdminId, current_user.TelegramUsername,
                            current_user.ChatId ?? 0, uservalue.Price), current_user.Id
                );
            }

            text = "پرداخت غیر فعال است";
            uservalue.State = TelegramMarzbanVpnSessionState.None;
            throw new AppException(text);
        }

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: text,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        await Task.CompletedTask;
    }

    public async Task WatingForTransactionImageAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        var uservalue = user.Value.Value;

        uservalue.State = TelegramMarzbanVpnSessionState.AwaitingSendTransactionImage;

        BotSessions
            .users_Sessions?
            .AddOrUpdate(chatId, uservalue,
                (key, old)
                    => old = uservalue);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
            }
        });

        string text = "🖼 تصویر رسید خود را ارسال نمایید";

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: text,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    public async Task AddTransactionAsync(ITelegramBotClient? botClient, Message? message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        if (message.Type == MessageType.Photo)
        {
            KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
                .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

            TelegramMarzbanVpnSession uservalue = user?.Value;

            uservalue.State = TelegramMarzbanVpnSessionState.None;

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId, uservalue,
                    (key, old)
                        => old = uservalue);

            var file = await botClient!.GetFileAsync(message.Photo![^1].FileId, cancellationToken: cancellationToken);
            using var memoryStream = new MemoryStream();

            await botClient!.DownloadFileAsync(file.FilePath!, memoryStream, cancellationToken);

            memoryStream.Seek(0, SeekOrigin.Begin); // R

            IFormFile formFile =
                new FormFile(
                    memoryStream,
                    0,
                    memoryStream.Length,
                    file.FileId, $"{file.FileId}.jpg");


            AddTransactionDto transaction = new()
            {
                AccountName = message.From?.FirstName ?? "بدون اسم",
                TransactionTime = DateTime.Now,
                TransactionType = TransactionType.Increase,
                AvatarTransaction = formFile,
                Price = uservalue.Price,
                Title = "افزایش موجودی"
            };

            CallbackQuery callbackQuery = new CallbackQuery()
            {
                Data = "back_to_main",
                Message = message
            };

            await telegramService.AddTransactionAsync(transaction, chatId);
            await SendMainMenuAsync(botClient, callbackQuery, cancellationToken,
                "\ud83d\ude80 رسید پرداخت  شما ارسال شد پس از تایید توسط مدیریت مبلغ به کیف پول شما واریز خواهد شد");
        }
        else
        {
            string text = "🖼 لطفا عکس ارسال کنید";

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendFactorAppendGbAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        TelegramMarzbanVpnSession uservalue = user?.Value;

        MarzbanVpnDto? vpn = await telegramService.GetMarzbanVpnInformationByIdAsync(uservalue.VpnId ?? 0, chatId);

        SubscribeFactorBotDto sub = new SubscribeFactorBotDto()
        {
            Gb = uservalue.Gb ?? 0,
            Price = vpn.GbPrice
        };

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("تایید و دریافت حجم اضاف", "accept_gb")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
            }
        });

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: sub.GetAppendGbInfo(),
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (message.MessageId != 0)
        {
            await botClient!.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
        }
    }

    public async Task SendFactorAppendDaysAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        TelegramMarzbanVpnSession uservalue = user?.Value;

        MarzbanVpnDto? vpn = await telegramService.GetMarzbanVpnInformationByIdAsync(uservalue.VpnId ?? 0, chatId);

        SubscribeFactorBotDto sub = new SubscribeFactorBotDto()
        {
            Days = uservalue.Date ?? 0,
            Price = vpn.DayPrice
        };

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("تایید و دریافت روز اضاف", "accept_date")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
            }
        });

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: sub.GetAppendDayInfo(),
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (message.MessageId != 0)
        {
            await botClient!.DeleteMessageAsync(chatId, message.MessageId, cancellationToken);
        }
    }

    public async Task AcceptAppendGbAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        TelegramMarzbanVpnSession uservalue = user?.Value;

        MarzbanVpnDto marzbanVpn = await telegramService
            .GetMarzbanVpnInformationByIdAsync(uservalue.VpnId ?? 0, chatId);

        BuyMarzbanVpnDto buy = new();

        buy.MarzbanVpnId = uservalue.VpnId ?? 0;
        buy.Count = 1;
        buy.TotalGb = uservalue.Gb ?? 0;
        buy.MarzbanUserId = uservalue?.UserSubscribeId;

        List<MarzbanUser> marzbanUsers = await telegramService.BuySubscribeAsync(buy, chatId);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("بازگشت به سروی ",
                    $"subscribe_info?id={uservalue.UserSubscribeId}&vpnId={uservalue.VpnId}")
            },
        });

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: "\u2705 افزایش حجم برای سرویس شما با موفقیت صورت گرفت\n\n" +
                  $"\u25ab\ufe0fحجم اضافه : {buy.TotalGb} گیگ\n\n" +
                  $"\u25ab\ufe0fمبلغ افزایش حجم : {uservalue.Gb * marzbanVpn.GbPrice} تومان",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }
    }

    public async Task AcceptAppendDaysAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        TelegramMarzbanVpnSession uservalue = user?.Value;

        MarzbanVpnDto marzbanVpn = await telegramService
            .GetMarzbanVpnInformationByIdAsync(uservalue.VpnId ?? 0, chatId);

        BuyMarzbanVpnDto buy = new();

        buy.MarzbanVpnId = uservalue.VpnId ?? 0;
        buy.Count = 1;
        buy.TotalDay = uservalue.Date ?? 0;
        buy.MarzbanUserId = uservalue?.UserSubscribeId;

        await telegramService.BuySubscribeAsync(buy, chatId);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("بازگشت به سرور ",
                    $"subscribe_info?id={uservalue.UserSubscribeId}&vpnId={uservalue.VpnId}")
            },
        });

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: "\u2705 افزایش روز برای سرویس شما با موفقیت صورت گرفت\n\n" +
                  $"\u25ab\ufe0fروز اضافه : {buy.TotalDay}\n\n" +
                  $"\u25ab\ufe0fمبلغ افزایش حجم : {uservalue.Date * marzbanVpn.DayPrice} تومان",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);

        if (callbackQuery.Message.MessageId != 0)
        {
            await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
        }
    }

    public async Task SendDaysPriceForAppendDaysAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        int vpnId = 0;
        long subscribeId = 0;

        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');

        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int32.TryParse(queryParameters["vpnId"], out vpnId);
            Int64.TryParse(queryParameters["subscribeId"], out subscribeId);
        }

        KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
            .users_Sessions?.SingleOrDefault(x => x.Key == chatId);

        TelegramMarzbanVpnSession? uservalue = user?.Value;

        if (uservalue is null)
            uservalue = new(TelegramMarzbanVpnSessionState.AwaitingSendAppendDaysForService, vpnId: vpnId,
                UserSubscribeId: subscribeId);

        uservalue.VpnId = vpnId;
        uservalue.UserSubscribeId = subscribeId;

        BotSessions
            .users_Sessions?
            .AddOrUpdate(chatId, uservalue,
                (key, old)
                    => old = uservalue);

        MarzbanVpnDto? vpn = await telegramService.GetMarzbanVpnInformationByIdAsync(uservalue.VpnId ?? 0, chatId);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
            }
        });

        uservalue.State = TelegramMarzbanVpnSessionState.AwaitingSendAppendDaysForService;

        BotSessions
            .users_Sessions?
            .AddOrUpdate(chatId, uservalue,
                (key, old)
                    => old = uservalue);

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text:
            $"\u231b\ufe0f زمان سرویس خود را انتخاب نمایید \n\ud83d\udccc تعرفه هر روز  : {vpn?.DayPrice}  تومان\n\u26a0\ufe0f حداقل زمان {vpn?.DayMin} روز  و حداکثر {vpn?.DayMax} روز  می توانید تهیه کنید",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken);
    }

    public async Task ActiveMarzbanUserAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        try
        {
            KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
                .users_Sessions?.SingleOrDefault(x => x.Key == chatId);
            TelegramMarzbanVpnSession? uservalue = user?.Value;

            if (uservalue.UserSubscribeId is null)
            {
                throw new NotFoundException("با عرض پوزش سرور پاسخگو نمیباشد");
            }

            await telegramService.ChangeMarzbanUserStatusAsync(MarzbanUserStatus.active, uservalue.UserSubscribeId ?? 0,
                chatId);

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: "سرویس شما با موفقیت فعال شد",
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task DisabledMarzbanUserAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        try
        {
            KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
                .users_Sessions?.SingleOrDefault(x => x.Key == chatId);
            TelegramMarzbanVpnSession? uservalue = user?.Value;

            if (uservalue.UserSubscribeId is null)
            {
                throw new NotFoundException("با عرض پوزش سرور پاسخگو نمیباشد");
            }

            await telegramService.ChangeMarzbanUserStatusAsync(MarzbanUserStatus.disabled,
                uservalue.UserSubscribeId ?? 0,
                chatId);

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: "سرویس شما با موفقیت غیر فعال شد",
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            // await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
            await SendMainMenuAsync(botClient!, callbackQuery, cancellationToken);
        }
    }

    public async Task RequestForAgentAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;
        try
        {
            KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions.users_Sessions!
                .SingleOrDefault(x => x.Key == message.Chat.Id);

            if (user.Value.Value is null)
                throw new ApplicationException("لطفا دوباره درخواست ارسال کنید");

            await telegramService.AddRequestAgentAsync(message.Text ?? "", user.Value.Value.Phone, chatId);

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: "درخواست شما با موفثیت ثبت شد منتظر برسی کارشناسان ما باشید",
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task DeleteMarzbanUserAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery.Message!.Chat.Id;
        try
        {
            KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
                .users_Sessions?.SingleOrDefault(x => x.Key == chatId);
            TelegramMarzbanVpnSession? uservalue = user?.Value;

            if (uservalue is null)
            {
                throw new NotFoundException("با عرض پوزش دوباره تلاش کنید");
            }

            await telegramService.DeleteMarzbanUserAsync(uservalue.UserSubscribeId ?? 0, chatId);

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: "سرویس شما با موفقیت حذف شد",
                cancellationToken: cancellationToken);

            await SendListServicesAsync(botClient!, callbackQuery, cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);

            await SendListServicesAsync(botClient!, callbackQuery, cancellationToken);
        }
    }

    public async Task RevokeSubscribeAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery.Message!.Chat.Id;
        try
        {
            KeyValuePair<long, TelegramMarzbanVpnSession>? user = BotSessions
                .users_Sessions?.SingleOrDefault(x => x.Key == chatId);
            TelegramMarzbanVpnSession? uservalue = user?.Value;

            if (uservalue is null)
            {
                throw new NotFoundException("با عرض پوزش دوباره تلاش کنید");
            }

            string sub = await telegramService.RevokeMarzbanUserAsync(uservalue.UserSubscribeId ?? 0, chatId);

            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: $"\u2705 کانفیگ شما با موفقیت بروزرسانی گردید.\nاشتراک شما : {sub}",
                cancellationToken: cancellationToken);

            await SendListServicesAsync(botClient!, callbackQuery, cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);

            await SendListServicesAsync(botClient!, callbackQuery, cancellationToken);
        }
    }

    public async Task SendTelegramInviteLinkAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        var userInfo = await botClient!.GetMeAsync(cancellationToken: cancellationToken);
        TelegramLinkDto? link = await telegramService.GetAgentBotLinkAsync(callbackQuery!.Message!.Chat.Id);
        await botClient!.SendTextMessageAsync(
            callbackQuery!.Message!.Chat.Id,
            $"با استفاده از لینک میتونید از دعوت دوستان خودتون به ربات کسب درآمد کنید.\n\n👇👇👇👇👇👇👇👇👇👇\n\n🔗 {link.GenerateLink(userInfo.Username)}",
            cancellationToken: cancellationToken
        );
        await Task.CompletedTask;
    }

    public async Task SendAgentMenuForAdmin(ITelegramBotClient botClient, long chatId,
        CancellationToken cancellationToken)
    {
        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "مدیریت پنل نمایندگی \u270f\ufe0f", "آمار نمایندگی \ud83d\udcca" },
            new KeyboardButton[] { "جستجو کاربر \ud83d\udd0d", "ارسال پیام ✉️" },
        })
        {
            ResizeKeyboard = true // تنظیم اندازه کیبورد
        };

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "لطفاً یک گزینه را انتخاب کنید:",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken
        );
    }

    public async Task<Message> SendAgentInformationMenuAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        try
        {
            bool isAgent = await telegramService.IsAgentAsyncByChatIdAsync(message.Chat.Id);

            if (!isAgent)
                throw new AppException("شما نماینده نیستید");

            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "مدیریت نماینده ها \ud83d\udc65" },
                new KeyboardButton[] { "ثبت | تغییر شماره کارت \ud83d\udcb3", "ثبت | تغییر نام نمایندگی \ud83d\udc65" },
                new KeyboardButton[] { "مشاهده اطلاعات پرداخت \ud83d\udcb0" },
                new KeyboardButton[] { "تغییر درصد کاربر", "تغییر درصد نماینده" },
                new KeyboardButton[] { "\ud83d\udd22 پرداخت نمایندگی", "\ud83d\udd22 پرداخت کاربری" },
                new KeyboardButton[] { "\ud83c\udfe0 بازگشت به منو اصلی" }
            })
            {
                ResizeKeyboard = true
            };

            return await botClient!.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "مدیریت پنل نمایندگی:",
                cancellationToken: cancellationToken,
                replyMarkup: keyboard
            );
        }
        catch (Exception e)
        {
            return await botClient!.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: e.Message,
                cancellationToken: cancellationToken
            );
        }
    }

    public async Task<Message> EditeAgentCardNumberInformationAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message.Chat.Id;
        try
        {
            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId,
                    new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendCardNumber),
                    (key, old)
                        => old = new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendCardNumber));

            return await botClient!.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: """
                      لطفا شماره کارت 16 رقمی خود را ارسال کنید!
                      فرمت درست 6037696975758585
                      """,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<Message> EditeAgentCardHolderNameInformationAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message.Chat.Id;
        try
        {
            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId,
                    new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendCardHolderName),
                    (key, old)
                        => old = new TelegramMarzbanVpnSession(
                            TelegramMarzbanVpnSessionState.AwaitingSendCardHolderName));

            return await botClient!.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: """
                      لطفا نام صاحب
                       شماره کارت را
                       به صورت دقیق
                        وارد کنید!
                      """,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<Message> SendAgentTransactionPaymentDetailAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message.Chat.Id;
        try
        {
            TransactionDetailDto? transaction = await telegramService.GetAgentTransactionDetailAsync(chatId);
            if (transaction is null)
                throw new ApplicationException("اطلاعات پرداخت ثبت نشده");

            string text = $"💳 شماره کارت: {transaction.CardNumber ?? "ثبت نشده"} \n" +
                          $"👤 نام صاحب کارت: {transaction.CardHolderName ?? "ثبت نشده"}\n" +
                          $"📈 درصد سود پیش‌ فرض از کاربر عادی: %{transaction.AgentPercent}\n" +
                          $"📊 درصد سود پیش‌ فرض از نماینده: %{transaction.UserPercent}\n" +
                          $"💰 سقف پرداخت نماینده: {transaction.MaximumAmountForAgent:N0}\n" +
                          $"💵 کف پرداخت نماینده: {transaction.MinimalAmountForAgent:N0}\n" +
                          $"💰 سقف پرداخت کاربر: {transaction.MaximumAmountForUser:N0}\n" +
                          $"💵 کف پرداخت کاربر: {transaction.MinimalAmountForUser:N0}\n";


            return await botClient!.SendTextMessageAsync(chatId,
                text, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            return await botClient!.SendTextMessageAsync(chatId, e.Message, cancellationToken: cancellationToken);
        }
    }

    public async Task<Message> SendAgentInformationAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        try
        {
            long chatId = message.Chat.Id;
            AgentInformationDto agentInformation = await telegramService.GetAgentInformationAsync(chatId);
            return await botClient!.SendTextMessageAsync(
                chatId,
                agentInformation.Information_Text(),
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<Message> UpdateAgentPercentAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        try
        {
            long chatId = message.Chat.Id;

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId,
                    new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendAgentPercent),
                    (key, old)
                        => old = new TelegramMarzbanVpnSession(
                            TelegramMarzbanVpnSessionState.AwaitingSendAgentPercent));

            AgentInformationDto agentInformation = await telegramService.GetAgentInformationAsync(chatId);

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
                }
            });

            return await botClient!.SendTextMessageAsync(
                chatId,
                $"""
                 درصد صود فروش از نماینده ها : {agentInformation.AgentPercent}
                  درصد صود خود را ارسال کنید
                 """,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<Message> UpdateUserPercentAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        try
        {
            long chatId = message.Chat.Id;

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId,
                    new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendUserPercent),
                    (key, old)
                        => old = new TelegramMarzbanVpnSession(
                            TelegramMarzbanVpnSessionState.AwaitingSendUserPercent));

            AgentInformationDto agentInformation = await telegramService.GetAgentInformationAsync(chatId);

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
                }
            });

            return await botClient!.SendTextMessageAsync(
                chatId,
                $"""
                 درصد صود فروش از کاربر ها : {agentInformation.UserPercent}
                  درصد صود خود را ارسال کنید
                 """,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<Message> UpdateAgentPersionBrandNameAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        try
        {
            long chatId = message.Chat.Id;

            BotSessions
                .users_Sessions?
                .AddOrUpdate(chatId,
                    new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendPersianBrandName),
                    (key, old)
                        => old = new TelegramMarzbanVpnSession(
                            TelegramMarzbanVpnSessionState.AwaitingSendPersianBrandName));

            AgentInformationDto agentInformation = await telegramService.GetAgentInformationAsync(chatId);

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
                }
            });

            return await botClient!.SendTextMessageAsync(
                chatId,
                $"""
                        نام فعلی فارسی نمایندگی : {agentInformation.PersianBrandName ?? "ثبت نشده است"}
                         نام فعلی انگیلیسی نمایندگی :{agentInformation.BrandName ?? "ثبت نشده است"}
                         لطفا نام فارسی نمایندگی را ارسال کنید!
                 """,
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task AcceptAgentRequestAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            long Id = 0;

            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["Id"], out Id);
            }

            await telegramService.ChangeAgentRequestAsync(chatId, new()
            {
                Id = Id,
                AgentRequestStatus = "accept"
            });

            await botClient!.SendTextMessageAsync(
                chatId,
                $"""عملیات با موفقیت انجام شد""",
                replyMarkup: null,
                cancellationToken: cancellationToken);

            if (callbackQuery.Message.MessageId != 0)
            {
                await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            }
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId,
                $"""عملیات با مشکل مواجه شد""",
                replyMarkup: null,
                cancellationToken: cancellationToken);
        }
    }

    public async Task RejectAgentRequestAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            long id = 0;

            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["Id"], out id);
            }

            await telegramService.ChangeAgentRequestAsync(chatId, new()
            {
                Id = id,
                AgentRequestStatus = "reject"
            });

            await botClient!.SendTextMessageAsync(
                chatId,
                $"""عملیات با موفقیت انجام شد""",
                replyMarkup: null,
                cancellationToken: cancellationToken);

            if (callbackQuery.Message.MessageId != 0)
            {
                await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            }
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId,
                e.Message,
                replyMarkup: null,
                cancellationToken: cancellationToken);
        }
    }

    public async Task ChangeStateCardToCard(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            long id = 0;
            bool action = false;
            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["Id"], out id);
                bool.TryParse(queryParameters["action"], out action);
            }

            bool response = await telegramService.ActionForCardToCardAsync(chatId, id, action);

            string? message = "";

            if (response)
                message = "شماره کارت فعال شد";
            else
                message = "شماره کارت غیر فعال شد";

            await botClient!.SendTextMessageAsync(
                chatId,
                text: message,
                replyMarkup: null,
                cancellationToken:
                cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId,
                e.Message,
                replyMarkup: null,
                cancellationToken: cancellationToken);
            await Task.CompletedTask;
        }
    }

    public async Task ManagementUserAsync(ITelegramBotClient botClient, long chatId, long userId,
        CancellationToken cancellationToken)
    {
        IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("افزایش موجودی \u2795", $"increase_by_agent?id={userId}"),
            InlineKeyboardButton.WithCallbackData("کاهش موجودی \u2796", $"decrease_by_agent?id={userId}"),
        });

        UserInformationDto information = await telegramService.GetUserInformationAsync(chatId, userId);

        bool isAgent = await telegramService.IsAgentAsyncByChatIdAsync(information.ChatId ?? 0);


        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("رفع مسدودی کاربر\u2705", $"on_blocked_user?id={userId}"),
            InlineKeyboardButton.WithCallbackData("مسدود کردن کاربر\u274c", $"blocked_user?id={userId}")
        });

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("ارسال پیام \ud83d\udcac", $"send_message_user?id={userId}"),
        });

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("فعال کردن شماره کارت \u2705",
                $"action_card?id={userId}&action={true}"),
            InlineKeyboardButton.WithCallbackData("غیر فعال کردن شماره کارت  \u274c",
                $"action_card?id={userId}&action={false}")
        });

        if (isAgent)
        {
            AgentDto? admin = await telegramService.GetAgentByAdminChatIdAsync(information.ChatId ?? 0);
            keys.Add(new()
            {
                InlineKeyboardButton.WithCallbackData("تغییر در صد نماینده  \u2699\ufe0f",
                    $"change_agent_percent?id={admin!.Id}")
            });
            information.IsAgent = true;
            information.SpecialPercent = (admin.SpecialPercent != 0 && admin?.SpecialPercent != null)
                ? admin.SpecialPercent
                : admin?.AgentPercent;
        }

        keys.Add(new()
        {
            InlineKeyboardButton.WithCallbackData("\ud83c\udfe0 بازگشت به منو اصلی", "back_to_main")
        });

        await botClient.SendTextMessageAsync(
            chatId,
            information.GetInformation(),
            replyMarkup: new InlineKeyboardMarkup(keys),
            cancellationToken: cancellationToken);

        await Task.CompletedTask;
    }


    public async Task IncreaseUserByAgentAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        long id = 0;
        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');

        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int64.TryParse(queryParameters["Id"], out id);
        }

        BotSessions
            .users_Sessions?
            .AddOrUpdate(chatId,
                new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendPriceForIncrease),
                (key, old)
                    => old = new TelegramMarzbanVpnSession(
                        TelegramMarzbanVpnSessionState.AwaitingSendPriceForIncrease));

        TelegramMarzbanVpnSession? user_value = BotSessions
            .users_Sessions!
            .SingleOrDefault(x => x.Key == chatId).Value;

        user_value.UserChatId = id;

        await botClient!
            .SendTextMessageAsync(
                chatId: chatId,
                "لطفا مبلغ ارسالی برای شارج را ارسال کنید!",
                cancellationToken: cancellationToken);

        await Task.CompletedTask;
    }

    public async Task DecreaseUserByAgentAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;

        long id = 0;
        string callbackData = callbackQuery.Data;
        int questionMarkIndex = callbackData.IndexOf('?');

        if (questionMarkIndex >= 0)
        {
            string? query = callbackData?.Substring(questionMarkIndex);
            NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
            Int64.TryParse(queryParameters["Id"], out id);
        }

        BotSessions
            .users_Sessions?
            .AddOrUpdate(chatId,
                new TelegramMarzbanVpnSession(TelegramMarzbanVpnSessionState.AwaitingSendPriceForDecrease),
                (key, old)
                    => old = new TelegramMarzbanVpnSession(
                        TelegramMarzbanVpnSessionState.AwaitingSendPriceForDecrease));

        TelegramMarzbanVpnSession? user_value = BotSessions
            .users_Sessions!
            .SingleOrDefault(x => x.Key == chatId).Value;

        user_value.UserChatId = id;

        await botClient!
            .SendTextMessageAsync(
                chatId: chatId,
                "لطفا مبلغ ارسالی برای کسر موجودی را ارسال کنید!",
                cancellationToken: cancellationToken);

        await Task.CompletedTask;
    }

    public async Task<Message> SearchUserByChatAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        TelegramMarzbanVpnSession? user_value = BotSessions
            .users_Sessions!
            .SingleOrDefault(x => x.Key == chatId).Value;

        user_value.State = TelegramMarzbanVpnSessionState.AwaitingSearchUserByChatId;

        return await botClient!.SendTextMessageAsync(
            chatId: chatId,
            "ایدی عددی کاربر را ارسال کنید.",
            cancellationToken: cancellationToken);
    }

    public async Task UpdateTransactionStatusAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            long id = 0;
            TransactionStatus status = TransactionStatus.Waiting;

            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["id"], out id);
                Enum.TryParse(queryParameters["status"], true, out status);
            }

            await telegramService.UpdateTransactionAsync(new UpdateTransactionStatusDto(status, id), chatId);

            string typeTransaction = status == TransactionStatus.Accepted ? "قبول" : "رد";
            await botClient!.SendTextMessageAsync(
                chatId,
                $"تراکنش {typeTransaction}  شد",
                replyMarkup: null,
                cancellationToken: cancellationToken);
            await Task.CompletedTask;
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId,
                e.Message,
                replyMarkup: null,
                cancellationToken: cancellationToken);
            await Task.CompletedTask;
        }
    }

    public async Task BlockUserAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            long id = 0;

            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["id"], out id);
            }

            await telegramService.BlockUserAsync(chatId, id, true);
            await botClient!.SendTextMessageAsync(
                chatId,
                "کاربر غیر فعال شد.",
                replyMarkup: null,
                cancellationToken: cancellationToken);
            await Task.CompletedTask;
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId,
                e.Message,
                replyMarkup: null,
                cancellationToken: cancellationToken);
            await Task.CompletedTask;
        }
    }

    public async Task OnBlockUserAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery!.Message!.Chat.Id;
        try
        {
            long id = 0;

            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["id"], out id);
            }

            await telegramService.BlockUserAsync(chatId, id, false);
            await botClient!.SendTextMessageAsync(
                chatId,
                "کاربر فعال شد.",
                replyMarkup: null,
                cancellationToken: cancellationToken);
            await Task.CompletedTask;
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId,
                e.Message,
                replyMarkup: null,
                cancellationToken: cancellationToken);
            await Task.CompletedTask;
        }
    }

    public async Task<Message> ChangeAgentPaymentOptionAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        TelegramMarzbanVpnSession? user_value = BotSessions
            .users_Sessions!
            .SingleOrDefault(x => x.Key == chatId).Value;

        user_value.State = TelegramMarzbanVpnSessionState.AwaitingSendMaximumAmountForAgent;

        TransactionDetailDto? transactionDetail = await telegramService.GetAgentTransactionDetailAsync(chatId);
        string text =
            "💰 سقف پرداخت: " + transactionDetail?.MaximumAmountForAgent.ToString("N0") + " تومان\n" +
            "💵 کف پرداخت: " + transactionDetail?.MinimalAmountForAgent.ToString("N0") + " تومان\n" +
            "📤 لطفاً سقف پرداخت نماینده را ارسال کنید";

        return await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: text,
            cancellationToken: cancellationToken);
    }

    public async Task<Message> ChangeUserPaymentOptionAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;

        TelegramMarzbanVpnSession? user_value = BotSessions
            .users_Sessions!
            .SingleOrDefault(x => x.Key == chatId).Value;

        user_value.State = TelegramMarzbanVpnSessionState.AwaitingSendMaximumAmountForUser;

        TransactionDetailDto? transactionDetail = await telegramService.GetAgentTransactionDetailAsync(chatId);
        string text =
            "💰 سقف پرداخت: " + transactionDetail?.MaximumAmountForUser.ToString("N0") + " تومان\n" +
            "💵 کف پرداخت: " + transactionDetail?.MinimalAmountForUser.ToString("N0") + " تومان\n" +
            "📤 لطفاً سقف پرداخت کاربر را ارسال کنید";

        return await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text: text,
            cancellationToken: cancellationToken);
    }

    public async Task<Message> SendListAgentsAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message.Chat.Id;
        try
        {
            FilterAgentDto agents = await telegramService.FilterAgentsAsync(chatId);

            IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

            foreach (var agent in agents.Entities)
            {
                string? text = (!string.IsNullOrEmpty(agent?.PersianBrandName)
                    ? agent.PersianBrandName
                    : (!string.IsNullOrEmpty(agent?.BrandName) ? agent?.BrandName : "نام ثبت نشده"));

                List<InlineKeyboardButton> key = new()
                {
                    InlineKeyboardButton.WithCallbackData(
                        text,
                        $"agent_management?id={agent?.Id}")
                };
                keys.Add(key);
            }

            return await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: """
                      یکی از نماینده هایه لیست زیر را انتخاب کنید !
                      """,
                replyMarkup: new InlineKeyboardMarkup(keys),
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            return await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendChildAgentInformation(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery.Message!.Chat.Id;
        try
        {
            long id = 0;

            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["id"], out id);
            }

            if (callbackQuery.Message.MessageId != 0)
            {
                await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            }

            AgentInformationDto information = await telegramService.GetAgentInformationByIdAsync(chatId, id);
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: information.Information_Text(),
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendMessageForUpdateSpecialPercent(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery.Message!.Chat.Id;
        try
        {
            long id = 0;

            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');

            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["id"], out id);
            }

            if (callbackQuery.Message.MessageId != 0)
            {
                await botClient!.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
            }

            TelegramMarzbanVpnSession? user_value = BotSessions
                .users_Sessions!
                .SingleOrDefault(x => x.Key == chatId).Value;

            user_value.State = TelegramMarzbanVpnSessionState.AwaitingSendSpecialPercent;
            user_value.ChildAgentId = id;

            await botClient!.SendTextMessageAsync(chatId: chatId,
                text: "لطفا درصدی که در نظر دارید از این نماینده سود بگیرید را ارسال کنید",
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message,
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendTicketMenuAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery.Message!.Chat.Id;

        IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

        keys.Add(new List<InlineKeyboardButton>()
        {
            InlineKeyboardButton.WithCallbackData(" \u2709\ufe0f ارسال پیام به پشتیبانی", "send_message_agent"),
            InlineKeyboardButton.WithCallbackData("سوالات متداول \u2753", "default_question")
        });

        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text:
            "\u260e\ufe0f  در دکمه زیر ( سوالات متداول ) سوالات پرتکرار شما آمده است. روی دکمه زیر کلیک کنید در صورت نیافتن سوال خود روی دکمه پشتیبانی کلیک کنید",
            replyMarkup: new InlineKeyboardMarkup(keys),
            cancellationToken: cancellationToken);
    }

    public async Task SendDefaultQuestionAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery.Message!.Chat.Id;
        await botClient!.SendTextMessageAsync(
            chatId: chatId,
            text:
            """
            💡 سوالات متداول ⁉️

            1️⃣ فیلترشکن شما آیپی ثابته؟ میتونم برای صرافی های ارز دیجیتال استفاده کنم؟

            ✅ به دلیل وضعیت نت و محدودیت های کشور سرویس ما مناسب ترید نیست و فقط لوکیشن‌ ثابته.

            2️⃣ اگه قبل از منقضی شدن اکانت، تمدیدش کنم روزهای باقی مانده می سوزد؟

            ✅ خیر، روزهای باقیمونده اکانت موقع تمدید حساب میشن و اگه مثلا 5 روز قبل از منقضی شدن اکانت 1 ماهه خودتون اون رو تمدید کنید 5 روز باقیمونده + 30 روز تمدید میشه.

            3️⃣ اگه به یک اکانت بیشتر از حد مجاز متصل شیم چه اتفاقی میافته؟

            ✅ در این صورت حجم سرویس شما زود تمام خواهد شد.

            4️⃣ فیلترشکن شما از چه نوعیه؟

            ✅ فیلترشکن های ما v2ray است و پروتکل‌های مختلفی رو ساپورت میکنیم تا حتی تو دورانی که اینترنت اختلال داره بدون مشکل و افت سرعت بتونید از سرویستون استفاده کنید.

            5️⃣ فیلترشکن از کدوم کشور است؟

            ✅ سرور فیلترشکن ما از کشور  آلمان است

            6️⃣ چطور باید از این فیلترشکن استفاده کنم؟

            ✅ برای آموزش استفاده از برنامه، روی دکمه «📚 آموزش» بزنید.

            7️⃣ فیلترشکن وصل نمیشه، چیکار کنم؟

            ✅ به همراه یک عکس از پیغام خطایی که میگیرید به پشتیبانی مراجعه کنید.

            8️⃣ فیلترشکن شما تضمینی هست که همیشه مواقع متصل بشه؟

            ✅ به دلیل قابل پیش‌بینی نبودن وضعیت نت کشور، امکان دادن تضمین نیست فقط می‌تونیم تضمین کنیم که تمام تلاشمون رو برای ارائه سرویس هر چه بهتر انجام بدیم.

            9️⃣ امکان بازگشت وجه دارید؟

            ✅ امکان بازگشت وجه در صورت حل نشدن مشکل از سمت ما وجود دارد.

            💡 در صورتی که جواب سوالتون رو نگرفتید میتونید به «پشتیبانی» مراجعه کنید.
            """,
            cancellationToken: cancellationToken);
    }

    public Task SendTicketGroupingAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task<Message> SendTicketAsync(ITelegramBotClient botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message.Chat.Id;
        try
        {
            AgentDto? agent = await telegramService.GetAgentByChatIdAsync(chatId);
            User? user = await telegramService.GetUserByChatIdAsync(chatId);

            IFormFile? formFile = null;
            File? file = null;

            string? fileId = message?.Photo?[^1].FileId;

            if (fileId != null && await botClient.GetFileAsync(
                    fileId ?? null,
                    cancellationToken: cancellationToken) != null)
            {
                file = await botClient.GetFileAsync(
                    message.Photo?[^1].FileId ?? null,
                    cancellationToken: cancellationToken);

                using var memoryStream = new MemoryStream();

                await botClient!.DownloadFileAsync(file.FilePath!, memoryStream, cancellationToken);

                memoryStream.Seek(0, SeekOrigin.Begin);

                formFile =
                    new FormFile(
                        memoryStream,
                        0,
                        memoryStream.Length,
                        file.FileId, $"{file.FileId}.jpg");

                formFile.AddImageToServer(formFile.FileName,
                    PathExtension.TicketAvatarOriginServer(_env)
                    , 100, 100,
                    PathExtension.TicketAvatarThumbServer(_env));
            }

            await notificationService.AddNotificationAsync(
                NotificationTemplate.SendTicketForAgentAsync(
                    agent.AgentAdminId,
                    message?.Caption ?? message?.Text,
                    user?.ChatId ?? 0,
                    user?.TelegramUsername ?? "NOUSERNAME",
                    DateTime.Now,
                    file is not null ? PathExtension.TicketAvatarOriginServer(_env) + formFile.FileName : null
                ), user!.Id);

            return await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: "پیغام شما با موفقیت برای نماینده ارسال شد.",
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            return await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: e.Message + "مشکلی در ثبت پیام رخ داد.",
                cancellationToken: cancellationToken);
        }
    }

    public async Task SendMarzbanVpnTemplatesGbAsync(ITelegramBotClient? botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        long chatId = callbackQuery.Message!.Chat.Id;
        try
        {
            IList<List<InlineKeyboardButton>> keys = new List<List<InlineKeyboardButton>>();

            long id = 0;
            long vpnId = 0;
            int days = 0;
            long subscribeId = 0;
            string callbackData = callbackQuery.Data;
            int questionMarkIndex = callbackData.IndexOf('?');
            if (questionMarkIndex >= 0)
            {
                string? query = callbackData?.Substring(questionMarkIndex);
                NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                Int64.TryParse(queryParameters["id"], out id);
                Int64.TryParse(queryParameters["vpnId"], out vpnId);
                Int64.TryParse(queryParameters["subscribeId"], out subscribeId);
                Int32.TryParse(queryParameters["days"], out days);
            }

            List<MarzbanVpnTemplateDto> templates =
                await telegramService.SendTemplatesGroupingByDays(chatId, vpnId, days);

            templates = templates.OrderBy(x => x.Gb).ToList();
            
            foreach (var template in templates)
            {
                keys.Add(new()
                {
                    InlineKeyboardButton.WithCallbackData($"{template.Gb} گیگ {template.Price:N0} تومان",
                        "factor_subscribe?id=" + template.Id + "&vpnId=" + vpnId + "&subscribeId=" + subscribeId)
                });
            }

            keys.Add(
                new List<InlineKeyboardButton>()
                {
                    InlineKeyboardButton.WithCallbackData("بازگشت", $"vpn_template?id={vpnId}")
                });

            await botClient!.SendTextMessageAsync(chatId, "یکی از آیتم هایه زیر را انتخاب کنید",
                replyMarkup: new InlineKeyboardMarkup(keys),
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task SendMessageForUserAsync(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        long chatId = message!.Chat.Id;
        try
        {
            TelegramMarzbanVpnSession? user_value = BotSessions
                .users_Sessions!
                .SingleOrDefault(x => x.Key == chatId).Value;
            
            IFormFile? formFile = null;
            File? file = null;

            string? fileId = message?.Photo?[^1].FileId;

            if (fileId != null && await botClient.GetFileAsync(
                    fileId ?? null,
                    cancellationToken: cancellationToken) != null)
            {
                file = await botClient.GetFileAsync(
                    message.Photo?[^1].FileId ?? null,
                    cancellationToken: cancellationToken);

                using var memoryStream = new MemoryStream();

                await botClient!.DownloadFileAsync(file.FilePath!, memoryStream, cancellationToken);

                memoryStream.Seek(0, SeekOrigin.Begin);

                formFile =
                    new FormFile(
                        memoryStream,
                        0,
                        memoryStream.Length,
                        file.FileId, $"{file.FileId}.jpg");

                formFile.AddImageToServer(formFile.FileName,
                    PathExtension.TicketAvatarOriginServer(_env)
                    , 100, 100,
                    PathExtension.TicketAvatarThumbServer(_env));
            }

            User? user = await telegramService.GetUserByChatIdAsync(user_value.UserChatId); 
            
            await notificationService.AddNotificationAsync(
                NotificationTemplate.SendTicketForUserAsync(
                    user!.Id,
                    user.ChatId ?? 0,
                    message?.Caption ?? message?.Text,
                    DateTime.Now,
                    file is not null ? PathExtension.TicketAvatarOriginServer(_env) + formFile.FileName : null
                ), user!.Id);
            
             await botClient!.SendTextMessageAsync(
                chatId: chatId,
                text: "پیغام شما با موفقیت برای نماینده ارسال شد.",
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async Task DeleteMenu(ITelegramBotClient? botClient, Message message,
        CancellationToken cancellationToken)
    {
        await botClient!.EditMessageReplyMarkupAsync(
            chatId: message.Chat.Id,
            messageId: message.MessageId,
            replyMarkup: null, // حذف تمام دکمه‌ها
            cancellationToken: cancellationToken
        );
    }
}