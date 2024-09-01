﻿
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Web;
using Api.Filters;
using Application.Extensions;
using Application.Helper;
using Application.Services.Interface.Telegram;
using Application.Sessions;
using Application.Utilities;
using Domain.DTOs.Transaction;
using Domain.Enums.Transaction;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Types;
using User = Domain.Entities.Account.User;

namespace Api.Controllers.Telegram;

[ServiceFilter(typeof(ExceptionHandler))]
public class BotHookController(
    IServiceProvider serviceProvider,
    IBotService botService,
    ITelegramService telegramService,
    IMemoryCache memoryCache
) : ControllerBase
{
    private ITelegramBotClient? _botClient;
    private string? _token;

    [HttpPost("{botName}")]
    public async Task Post(string botName,
        [FromBody] Update update,
        CancellationToken cancellationToken)

    {
        try
        {
            string? token = await telegramService.GetTelegramBotAsyncByName(botName);

            _token = token;

            _botClient = new TelegramBotClient(token);

            await HandleUpdateAsync(update, new CancellationToken());
            await Task.CompletedTask;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <summary>
    /// update
    /// </summary>
    /// <param name="update"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task HandleUpdateAsync(Update update,
        CancellationToken cancellationToken)

    {
        var handler = update switch
        {
            { Message: { } message } => BotOnMessageReceived(message, cancellationToken),
            { EditedMessage: { } message } => BotOnMessageReceived(message, cancellationToken),
            { CallbackQuery: { } callbackQuery } => BotOnCallbackQueryReceived(callbackQuery,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(update), update, null)
        };

        await handler;
    }

    private async Task BotOnMessageReceived(Message message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (message.Photo is not { } photo & message.Text is not { } messageText)
                return;

            if (memoryCache.TryGetValue(message.Chat.Id, out TelegramMarzbanVpnSession? user))
            {
            }
            else
            {
                memoryCache.Set(message.Chat.Id,new TelegramMarzbanVpnSession(),TimeSpan.FromMinutes(10));
                user = memoryCache.Get(message.Chat.Id) as TelegramMarzbanVpnSession;
            }


            if (user is not null)
            {
                switch (user.State)
                {
                    case TelegramMarzbanVpnSessionState.AwaitingSendMessageForUser:
                        await botService.SendMessageForUserAsync(_botClient, message, cancellationToken);
                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendTicketMessage:
                        await botService.SendTicketAsync(_botClient, message, cancellationToken);
                        user.State = TelegramMarzbanVpnSessionState.None;
                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendSpecialPercent:
                        long specialPercent = -1;
                        Int64.TryParse(message.Text, out specialPercent);
                        if (specialPercent <= 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.⚠️
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        await telegramService.UpdateAgentSpecialPercent(message.Chat.Id, user.ChildAgentId,
                            specialPercent);

                        await _botClient!.SendTextMessageAsync(
                            chatId: message!.Chat.Id,
                            text: """
                                   ✅ عملیات با موفقیت انجام شد
                                  """,
                            cancellationToken: cancellationToken);

                        user.State = TelegramMarzbanVpnSessionState.None;
                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendMinimalAmountForUser:
                        long minimalAmountForUser = -1;
                        Int64.TryParse(message.Text, out minimalAmountForUser);
                        if (minimalAmountForUser <= 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.⚠️
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        if (user.MinimalAmountForUser > user.MaximumAmountForUser)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      نمیتواند سقف پرداخت کوچیک تر⚠️
                                      از کف پرداخت باشد
                                      """,
                                cancellationToken: cancellationToken);
                        }

                        user.MinimalAmountForUser = minimalAmountForUser;
                        user.State = TelegramMarzbanVpnSessionState.None;


                        await telegramService.UpdateUserPaymentAsync(message.Chat.Id,
                            user.MinimalAmountForUser, user.MaximumAmountForUser);

                        await _botClient!.SendTextMessageAsync(
                            chatId: message!.Chat.Id,
                            text: """
                                   ✅ عملیات با موفقیت انجام شد
                                  """,
                            cancellationToken: cancellationToken);
                        break;

                    case TelegramMarzbanVpnSessionState.AwaitingSendMaximumAmountForUser:
                        long MaximumAmountForUser = -1;
                        Int64.TryParse(message.Text, out MaximumAmountForUser);
                        if (MaximumAmountForUser <= 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.⚠️
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        user.MaximumAmountForUser = MaximumAmountForUser;
                        user.State = TelegramMarzbanVpnSessionState.AwaitingSendMinimalAmountForUser;

                        await _botClient!.SendTextMessageAsync(message!.Chat.Id,
                            "\u26a0\ufe0fلطفا کف پرداخت کاربر ها را ارسال کنید",
                            cancellationToken: cancellationToken);

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendMinimalAmountForAgent:
                        long MinimalAmountForAgent = -1;
                        Int64.TryParse(message.Text, out MinimalAmountForAgent);
                        if (MinimalAmountForAgent <= 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.⚠️
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        if (user.MinimalAmountForAgent > user.MaximumAmountForAgent)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      نمیتواند سقف پرداخت کوچیک تر⚠️
                                      از کف پرداخت باشد
                                      """,
                                cancellationToken: cancellationToken);
                        }

                        user.MinimalAmountForAgent = MinimalAmountForAgent;
                        user.State = TelegramMarzbanVpnSessionState.None;


                        await telegramService.UpdateAgentPaymentAsync(message.Chat.Id,
                            user.MinimalAmountForAgent, user.MaximumAmountForAgent);

                        await _botClient!.SendTextMessageAsync(
                            chatId: message!.Chat.Id,
                            text: """
                                   ✅ عملیات با موفقیت انجام شد
                                  """,
                            cancellationToken: cancellationToken);
                        break;

                    case TelegramMarzbanVpnSessionState.AwaitingSendMaximumAmountForAgent:
                        long MaximumAmountForAgent = -1;
                        Int64.TryParse(message.Text, out MaximumAmountForAgent);
                        if (MaximumAmountForAgent <= 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.⚠️
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        user.MaximumAmountForAgent = MaximumAmountForAgent;
                        user.State = TelegramMarzbanVpnSessionState.AwaitingSendMinimalAmountForAgent;

                        await _botClient!.SendTextMessageAsync(message!.Chat.Id,
                            "\u26a0\ufe0fلطفا کف پرداخت نماینده را ارسال کنید",
                            cancellationToken: cancellationToken);

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSearchUserByChatId:
                        long userChatId = 0;
                        Int64.TryParse(message.Text, out userChatId);
                        if (userChatId == 0 || userChatId <= 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        await botService.ManagementUserAsync(_botClient, message!.Chat.Id, userChatId,
                            cancellationToken);

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendDescriptionForDecrease:
                        string description_decrease = message!.Text;

                        AddTransactionDto transaction_decrease = new()
                        {
                            Description = description_decrease,
                            CardNumber = "",
                            Price = user.DecreasePrice,
                            Title = "کاهش دستی موحودی",
                            AccountName = "",
                            BankName = null,
                            TransactionType = TransactionType.Decrease,
                            TransactionTime = DateTime.Now,
                        };

                        await telegramService.DecreaseUserAsync(message.Chat.Id, user.UserChatId,
                            transaction_decrease);

                        user.State = TelegramMarzbanVpnSessionState.None;
                        await _botClient!.SendTextMessageAsync(
                            chatId: message!.Chat.Id,
                            text: $"""
                                     مبلغ {user.DecreasePrice:N0}
                                      از حساب کاربر کم شد و به حساب  شما واریز شد.
                                   """,
                            cancellationToken: cancellationToken);
                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendPriceForDecrease:
                        long decrease_price = 0;
                        Int64.TryParse(message.Text, out decrease_price);

                        if (decrease_price == 0 || decrease_price <= 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        User? userChild =
                            await telegramService.GetUserByChatIdAsync(chatId: user.UserChatId);

                        if (userChild.Balance >= decrease_price)
                        {
                            user.DecreasePrice = decrease_price;
                            user.State = TelegramMarzbanVpnSessionState.AwaitingSendDescriptionForDecrease;

                            await _botClient!.SendTextMessageAsync(message.Chat.Id,
                                "توضیحات تراکنش را ارسال کنید !",
                                cancellationToken: cancellationToken);
                            break;
                        }

                        await _botClient!.SendTextMessageAsync(message.Chat.Id,
                            "مقدار که برای کاهش موجودی کاربر درخواست داده اید بیشتر از موجودی حساب کاربراست!",
                            cancellationToken: cancellationToken);

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendDescriptionForIncrease:
                        string description = message!.Text;

                        AddTransactionDto transaction = new()
                        {
                            Description = description,
                            CardNumber = "",
                            Price = user.IncreasePrice,
                            Title = "افزایش دستی موجودی",
                            AccountName = "",
                            BankName = null,
                            TransactionType = TransactionType.Increase,
                            TransactionTime = DateTime.Now,
                        };

                        await telegramService.IncreaseUserAsync(message.Chat.Id, user.UserChatId,
                            transaction);

                        user.State = TelegramMarzbanVpnSessionState.None;
                        await _botClient!.SendTextMessageAsync(
                            chatId: message!.Chat.Id,
                            text: $"""
                                     مبلغ {user.IncreasePrice:N0}
                                      از حساب شما کم شد و به حساب کاربر شما واریز شد.
                                   """,
                            cancellationToken: cancellationToken);
                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendPriceForIncrease:
                        long increace_price = 0;
                        Int64.TryParse(message.Text, out increace_price);

                        if (increace_price == 0 || increace_price <= 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        User? user_current =
                            await telegramService.GetUserByChatIdAsync(chatId: message.Chat.Id);

                        if (user_current.Balance >= increace_price)
                        {
                            user.IncreasePrice = increace_price;
                            user.State = TelegramMarzbanVpnSessionState.AwaitingSendDescriptionForIncrease;

                            await _botClient!.SendTextMessageAsync(message.Chat.Id,
                                "توضیحات تراکنش را ارسال کنید !",
                                cancellationToken: cancellationToken);
                            break;
                        }

                        await _botClient!.SendTextMessageAsync(message.Chat.Id,
                            "مقدار که برای افزایش موجودی کاربر درخواست داده اید بیشتر از موجودی حساب شما است!",
                            cancellationToken: cancellationToken);

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendPhone:
                        string? phone = message.Text;

                        if (phone!.Length != 11)
                        {
                            throw new ApplicationException("لطفا شماره تماس درست را ارسال کنید");
                        }

                        user.Phone = phone;
                        user.State =
                            TelegramMarzbanVpnSessionState.AwaitingSendDescriptionForAddAgentRequest;

                        await _botClient!.SendTextMessageAsync(chatId: message.Chat.Id,
                            "لطفا توضیحات درخواست خود را ارسال کنید");

                        break;

                    case TelegramMarzbanVpnSessionState.AwaitingSendEnglishBrandName:
                        string? englishBrandName = message!.Text;
                        if (englishBrandName!.Length <= 2 || string.IsNullOrEmpty(englishBrandName))
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                        نام نمایندگی را به صورت صحیح ارسال کنید!
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        if (!EnglishText.IsValidUsername(englishBrandName))
                            throw new ApplicationException(
                                "\u26a0\ufe0f نام کاربری باید بدون کاراکترهای اضافه مانند @، فاصله، خط تیره باشد.\n" +
                                "\u26a0\ufe0f نام کاربری باید انگلیسی باشد.\n" +
                                "\u2705 نام کاربری های صحیح : ali12 | mahdi | ws1_ksdf\n" +
                                "\u274c نام کاربری های نادرست : ali_ | tele@ | _mahdi | محسن"
                            );

                        user.EnglishBrandName = message.Text;
                        bool updateBrandNamesResponse = await telegramService.UpdateAgentBrandNames(message.Chat.Id,
                            user.PersionBrandName, user.EnglishBrandName);

                        user.State = TelegramMarzbanVpnSessionState.None;
                        if (!updateBrandNamesResponse)
                            throw new ApplicationException("مشکلی در تغییر نام نمایندگی رخ داده.");

                        await _botClient!.SendTextMessageAsync(chatId: message.Chat.Id,
                            text: "عملیات با موفقیت انجام شد", cancellationToken: cancellationToken);

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendPersianBrandName:
                        string? persionBrandName = message!.Text;
                        if (persionBrandName.Length <= 2 || string.IsNullOrEmpty(persionBrandName))
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                        نام نمایندگی را به صورت صحیح ارسال کنید!
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        user.PersionBrandName = message.Text;
                        user.State = TelegramMarzbanVpnSessionState.AwaitingSendEnglishBrandName;

                        await _botClient!.SendTextMessageAsync(
                            chatId: message.Chat.Id,
                            text: "لطفا نام نمایندگی خود را به صورت انگیلیسی ارسال کنید",
                            cancellationToken: cancellationToken
                        );
                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendUserPercent:
                        int userPercent = 0;
                        Int32.TryParse(message?.Text, out userPercent);

                        if (userPercent == 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        user.UserPercent = userPercent;
                        bool update_user_percent =
                            await telegramService.UpdateUserPercentAsync(message.Chat.Id, userPercent);
                        user.State = TelegramMarzbanVpnSessionState.None;
                        if (update_user_percent)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      درصد کاربر های شما با موفقیت تغییر کرد
                                      """,
                                cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                        مشکلی در ثبت اطلاعات رخ داد
                                      """,
                                cancellationToken: cancellationToken);
                        }

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendAgentPercent:
                        int agentPercent = 0;
                        Int32.TryParse(message?.Text, out agentPercent);
                        if (agentPercent == 0)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      لطفا فرمت درست را ارسال کنید.
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        user.AgentPercent = agentPercent;
                        bool update_agent_percent =
                            await telegramService.UpdateAgentPercentAsync(message.Chat.Id, agentPercent);
                        user.State = TelegramMarzbanVpnSessionState.None;
                        if (update_agent_percent)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      درصد نماینده های شما با موفقیت تغییر کرد
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }
                        else
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                        مشکلی در ثبت اطلاعات رخ داد
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendCardHolderName:
                        string? HolderName = message.Text;

                        user.State = TelegramMarzbanVpnSessionState.AwaitingSendCardHolderName;
                        if (HolderName.Trim().Length <= 4 | string.IsNullOrEmpty(HolderName))
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                        نام صاحب کارت را به صورت صحیح ارسال کنید!
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        user.State = TelegramMarzbanVpnSessionState.None;
                        user.CardHolderName = message.Text;
                        BotSessions
                            .users_Sessions?
                            .AddOrUpdate(message.Chat.Id,
                                user, (key, old) => old = user);
                        bool response =
                            await telegramService.EditeAgentTransactionDetailAsync(message.Chat.Id, user);
                        if (response)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      اطلاعات پرداخت شما با موقیت ثبت شد
                                      """,
                                cancellationToken: cancellationToken);
                        }
                        else
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      مشکلی در ثبت اطلاعات رخ داد
                                      """,
                                cancellationToken: cancellationToken);
                        }

                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendCardNumber:
                        long cardNumber = 0;
                        Int64.TryParse(message?.Text, out cardNumber);

                        if (cardNumber == 0 | message.Text.Length != 16)
                        {
                            await _botClient!.SendTextMessageAsync(
                                chatId: message!.Chat.Id,
                                text: """
                                      فرمت ارسالی کارت اشتباه است!
                                      : فرمت درست
                                       6037696975758585
                                      """,
                                cancellationToken: cancellationToken);
                            break;
                        }

                        user.State = TelegramMarzbanVpnSessionState.AwaitingSendCardHolderName;
                        user.CardNumber = message.Text;
                        await botService.EditeAgentCardHolderNameInformationAsync(_botClient, message,
                            cancellationToken,user);
                        BotSessions
                            .users_Sessions?
                            .AddOrUpdate(message.Chat.Id,
                                user, (key, old) => old = user);
                        break;

                    case TelegramMarzbanVpnSessionState.AwaitingSendDescriptionForAddAgentRequest:
                        await botService.RequestForAgentAsync(_botClient, message, cancellationToken,user);
                        user.State = TelegramMarzbanVpnSessionState.None;
                        user.RequestDescription = message!.Text;
                        BotSessions
                            .users_Sessions?
                            .AddOrUpdate(message.Chat.Id,
                                user, (key, old) => old = user);
                        break;

                    case TelegramMarzbanVpnSessionState.AwaitingSendAppendDaysForService:
                        int day = 0;
                        Int32.TryParse(message?.Text, out day);
                        user.Date = day;
                        await botService.SendFactorAppendDaysAsync(_botClient, message, cancellationToken,user);
                        user.State = TelegramMarzbanVpnSessionState.None;
                        BotSessions
                            .users_Sessions?
                            .AddOrUpdate(message.Chat.Id,
                                user, (key, old) => old = user);
                        break;

                    case TelegramMarzbanVpnSessionState.AwaitingSendAppendGbForService:
                        int gig = 0;
                        Int32.TryParse(message?.Text, out gig);
                        user.Gb = gig;
                        await botService.SendFactorAppendGbAsync(_botClient, message, cancellationToken,user);
                        user.State = TelegramMarzbanVpnSessionState.None;
                        BotSessions
                            .users_Sessions?
                            .AddOrUpdate(message.Chat.Id,
                                user, (key, old) => old = user);
                        break;

                    case TelegramMarzbanVpnSessionState.AwaitingGb:
                        int gb = 0;
                        Int32.TryParse(message?.Text, out gb);
                        user.Gb = gb;
                        BotSessions
                            .users_Sessions?
                            .AddOrUpdate(message.Chat.Id,
                                user, (key, old) => old = user);
                        await botService.SendDaysPriceAsync(_botClient, message, cancellationToken,user);
                        break;

                    case TelegramMarzbanVpnSessionState.AwaitingDate:
                        int date = 0;
                        Int32.TryParse(message?.Text, out date);
                        user.Date = date;
                        BotSessions
                            .users_Sessions?
                            .AddOrUpdate(message.Chat.Id,
                                user, (key, old) => old = user);

                        await botService.SendCustomFactorVpnAsync(_botClient, message, cancellationToken,user);
                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendPrice:
                        int price = 0;
                        Int32.TryParse(message?.Text, out price);
                        user.Price = price;

                        BotSessions
                            .users_Sessions?
                            .AddOrUpdate(message.Chat.Id,
                                user, (key, old) => old = user);

                        await botService.SendCardNumberAndDetailAsync(_botClient, message, cancellationToken,user);
                        break;
                    case TelegramMarzbanVpnSessionState.AwaitingSendTransactionImage:
                        await botService.AddTransactionAsync(_botClient, message, cancellationToken,user);
                        break;
                }
            }

            bool? started = message?.Text?.StartsWith("/start");

            Message action = new Message();
            if (started != null && started == true)
            {
                action = message?.Text?.Split(" ")[0] switch
                {
                    "/start" => await botService.StartLinkAsync(_botClient, message, cancellationToken,user),
                };
            }
            else
            {
                action = message?.Text switch
                {
                    "ثبت | تغییر نام نمایندگی \ud83d\udc65" => await botService.UpdateAgentPersionBrandNameAsync(
                        _botClient,
                        message,
                        cancellationToken,user),
                    "تغییر درصد نماینده" => await botService.UpdateAgentPercentAsync(_botClient,
                        message,
                        cancellationToken,user),
                    "تغییر درصد کاربر" => await botService.UpdateUserPercentAsync(_botClient,
                        message,
                        cancellationToken,user),
                    "مدیریت پنل نمایندگی \u270f\ufe0f" => await botService.SendAgentInformationMenuAsync(_botClient,
                        message,
                        cancellationToken),
                    "آمار نمایندگی \ud83d\udcca" => await botService.SendAgentInformationAsync(_botClient,
                        message,
                        cancellationToken),
                    "\ud83c\udfe0 بازگشت به منو اصلی" => await botService.SendMainMenuAsync(_botClient, message,
                        cancellationToken,user),
                    "ثبت | تغییر شماره کارت \ud83d\udcb3" => await botService.EditeAgentCardNumberInformationAsync(
                        _botClient,
                        message, cancellationToken,user),
                    "مشاهده اطلاعات پرداخت \ud83d\udcb0" => await botService.SendAgentTransactionPaymentDetailAsync(
                        _botClient,
                        message, cancellationToken),
                    "جستجو کاربر \ud83d\udd0d" => await botService.SearchUserByChatAsync(_botClient,
                        message, cancellationToken,user),
                    "\ud83d\udd22 پرداخت نمایندگی" => await botService.ChangeAgentPaymentOptionAsync(_botClient,
                        message, cancellationToken,user),
                    "\ud83d\udd22 پرداخت کاربری" => await botService.ChangeUserPaymentOptionAsync(_botClient,
                        message, cancellationToken,user),
                    "مدیریت نماینده ها \ud83d\udc65" => await botService.SendListAgentsAsync(_botClient,
                        message, cancellationToken),
                    _ => new Message()
                };
            }

            Message sentMessage = action;
        }
        catch (Exception e)
        {
            await _botClient!.SendTextMessageAsync(message.Chat.Id, e.Message, cancellationToken: cancellationToken);
        }
    }

    private async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        string data = callbackQuery.Data.Split('?')[0];
        
        if (memoryCache.TryGetValue(callbackQuery.Message.Chat.Id, out TelegramMarzbanVpnSession? user))
        {
        }
        else
        {
            memoryCache.Set(callbackQuery.Message.Chat.Id,new TelegramMarzbanVpnSession(),TimeSpan.FromMinutes(10));
            user = memoryCache.Get(callbackQuery.Message.Chat.Id) as TelegramMarzbanVpnSession;
        }

        switch (data)
        {
            case "subscribe":
                await botService.SendListVpnsAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "test_free":
                await botService.SendListVpnsHaveTestAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "back_to_main":
                await botService.SendMainMenuAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "vpn_template":
                await botService.SendListVpnsTemplateAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "factor_subscribe":
                await botService.SendFactorVpnAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "buy_subscribe":
                await botService.SendSubscriptionAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "custom_subscribe":
                await botService.SendGbPriceAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "my_services":
                await botService.SendListServicesAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "subscribe_info":
                await botService.SendSubscribeInformationAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "createtestsub":
                await botService.SendTestSubscibeAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "subscription_link":
                await botService.SendSubscribeAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "get_traffic":
                await botService.SendConfigsAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "wallet":
                await botService.SendUserInformationAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "inventory_increase":
                await botService.SendTransactionDetailsAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "send_transaction_image":
                await botService.AwaitingForTransactionImageAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "web_information":
                await botService.SendUserForLoginToWebAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "accept_gb":
                await botService.AcceptAppendGbAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "accept_date":
                await botService.AcceptAppendDaysAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "append_date":
                await botService.SendDaysPriceForAppendDaysAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "active_service":
                await botService.ActiveMarzbanUserAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "disabled_service":
                await botService.DisabledMarzbanUserAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "delete_service":
                await botService.DeleteMarzbanUserAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "revoke_sub":
                await botService.RevokeSubscribeAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "invitation_link":
                await botService.SendTelegramInviteLinkAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "accept_agent_request":
                await botService.AcceptAgentRequestAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "reject_agent_request":
                await botService.RejectAgentRequestAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "action_card":
                await botService.ChangeStateCardToCard(_botClient, callbackQuery, cancellationToken);
                break;
            case "user_management":
                long id = 0;
                string callbackData = callbackQuery.Data;
                int questionMarkIndex = callbackData.IndexOf('?');

                if (questionMarkIndex >= 0)
                {
                    string? query = callbackData?.Substring(questionMarkIndex);
                    NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                    Int64.TryParse(queryParameters["Id"], out id);
                }

                await botService.ManagementUserAsync(_botClient!, callbackQuery.Message!.Chat.Id, id,
                    cancellationToken);
                break;
            case "renewal_service":
                await _botClient!.SendTextMessageAsync(
                    chatId: callbackQuery!.Message!.Chat.Id,
                    text: callbackQuery.Data,
                    cancellationToken: cancellationToken);
                break;
            case "increase_by_agent":
                await botService.IncreaseUserByAgentAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "decrease_by_agent":
                await botService.DecreaseUserByAgentAsync(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "update_trans":
                await botService.UpdateTransactionStatusAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "transactions":
                await botService.SendTransactionsAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "blocked_user":
                await botService.BlockUserAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "on_blocked_user":
                await botService.OnBlockUserAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "agent_management":
                await botService.SendChildAgentInformation(_botClient, callbackQuery, cancellationToken);
                break;
            case "change_agent_percent":
                await botService.SendMessageForUpdateSpecialPercent(_botClient, callbackQuery, cancellationToken,user);
                break;
            case "send_message":
                await botService.SendTicketMenuAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "default_question":
                await botService.SendDefaultQuestionAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "sendvpntemplate":
                await botService.SendMarzbanVpnTemplatesGbAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "added_agent":
                await botService.AddAgentAsync(_botClient, callbackQuery, cancellationToken);
                break;
            case "send_message_user":
                long userChatId = 0;
                string callbackData2 = callbackQuery.Data;
                int questionMarkIndex2 = callbackData2.IndexOf('?');
                if (questionMarkIndex2 >= 0)
                {
                    string? query = callbackData2?.Substring(questionMarkIndex2);
                    NameValueCollection queryParameters = HttpUtility.ParseQueryString(query);
                    Int64.TryParse(queryParameters["id"], out userChatId);
                }

                user!.State = TelegramMarzbanVpnSessionState.AwaitingSendMessageForUser;
                user.UserChatId = userChatId;

                await _botClient!.SendTextMessageAsync(
                    callbackQuery.Message?.Chat.Id,
                    "لطفا پیغام خود را ارسال کنید!",
                    cancellationToken: cancellationToken);
                break;

            case "send_message_agent":
                
                user!.State = TelegramMarzbanVpnSessionState.AwaitingSendTicketMessage;

                await _botClient!.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: "لطفا پیغام خود را ارسال کنید",
                    cancellationToken: cancellationToken);
                break;
            case "agent_request":
                long chatId = callbackQuery!.Message!.Chat.Id;

                bool have = await telegramService.HaveRequestForAgentAsync(callbackQuery!.Message!.Chat.Id);
                if (have)
                {
                    await _botClient!.SendTextMessageAsync(
                        chatId: chatId,
                        text: "شما قبلا درخواستی برای نمایندگی ثبت کردید",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    user!.State = TelegramMarzbanVpnSessionState.AwaitingSendPhone;

                    await _botClient!.SendTextMessageAsync(
                        chatId: chatId,
                        text: "لطفا شماره تلفن خود را برای ثبت نمایندگی ارسال کنید",
                        cancellationToken: cancellationToken);
                }

                break;
            default:
                break;
        }
    }
}