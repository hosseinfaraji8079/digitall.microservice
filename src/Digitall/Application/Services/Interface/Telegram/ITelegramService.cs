﻿using Domain.DTOs.Account;
using Domain.DTOs.Marzban;
using Domain.DTOs.Telegram;
using Domain.Entities.Account;
using Domain.Entities.Marzban;

namespace Application.Services.Interface.Telegram;

public interface ITelegramService : IAsyncDisposable
{
    /// <summary>
    /// add telegram bot summery
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<AddTelegramBotDto> AddTelegramBotAsync(AddTelegramBotDto bot, long userId);

    /// <summary>
    /// get all bot for started
    /// </summary>
    /// <returns></returns>
    Task<List<TelegramBotDto>?> GetAllTelegramBotAsync();

    /// <summary>
    /// get telegram bot by name async
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    Task<string?> GetTelegramBotAsyncByName(string name);

    /// <summary>
    /// get user by chat id
    /// </summary>
    /// <param name="chatId"></param>
    /// <returns></returns>
    Task<User?> GetUserByChatIdAsync(long chatId);

    /// <summary>
    /// get list vpn have test account
    /// </summary>
    /// <returns></returns>
    Task<List<MarzbanVpnTestDto>> GetListMarzbanVpnTestAsync();

    /// <summary>
    /// get list vpn have test account
    /// </summary>
    /// <returns>MarzbanUserInformationDto</returns>
    Task<List<MarzbanVpnBotDto>> GetMarzbanVpnsAsync();
    
    
    /// <summary>
    /// generate test file and send 
    /// </summary>
    /// <param name="vpnId">for create user in vpn</param>
    /// <param name="chatId">for get user</param>
    /// <returns>MarzbanUserInformationDto</returns>
    Task<MarzbanUserInformationDto> GetMarzbanTestVpnsAsync(long vpnId, long chatId);

    /// <summary>
    /// get marzban vpm by vpnid
    /// </summary>
    /// <param name="vpnId"></param>
    /// <returns></returns>
    Task<GetMarzbanVpnDto?> GetMarzbanVpnInformationByIdAsync(long vpnId);

    /// <summary>
    /// get marzban template by vpn id
    /// </summary>
    /// <param name="vpnId"></param>
    /// <returns></returns>
    Task<List<MarzbanVpnTemplateDto>> GetMarzbanVpnTemplatesByVpnIdAsync(long vpnId);

    Task StartTelegramBot(StartTelegramBotDto start);
}