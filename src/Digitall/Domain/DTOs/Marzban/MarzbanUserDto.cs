﻿using Domain.Entities.Marzban;
using Newtonsoft.Json;

namespace Domain.DTOs.Marzban;

public class MarzbanUserDto
{
    public long Id { get; set; }
    [JsonProperty("expire")] public long? Expire { get; set; }

    [JsonProperty("data_limit")] public long? Data_Limit { get; set; } = 0;

    [JsonProperty("data_limit_reset_strategy")]
    public string Data_Limit_Reset_Strategy { get; set; }

    [JsonProperty("note")] public string Note { get; set; }

    [JsonProperty("sub_updated_at")] public DateTime? Sub_Updated_At { get; set; }

    [JsonProperty("sub_last_user_agent")] public string? Sub_Last_User_Agent { get; set; }

    [JsonProperty("online_at")] public DateTime? Online_At { get; set; }

    [JsonProperty("on_hold_expire_duration")]
    public string? OnHoldExpireDuration { get; set; }

    [JsonProperty("on_hold_timeout")] public string? OnHoldTimeout { get; set; }

    [JsonProperty("username")] public string? Username { get; set; }

    [JsonProperty("status")] public string? Status { get; set; }

    [JsonProperty("used_traffic")] public long? Used_Traffic { get; set; }

    [JsonProperty("lifetime_used_traffic")]
    public long? Lifetime_Used_Traffic { get; set; }

    [JsonProperty("created_at")] public DateTime? Created_At { get; set; }

    [JsonProperty("links")] public List<string?> Links { get; set; } = new();

    [JsonProperty("subscription_url")] public string? Subscription_Url { get; set; }
    
    public long? MarzbanVpnId { get; set; }

    public MarzbanUserDto()
    {
    }

    public MarzbanUserDto(MarzbanUser? marzbanUser)
    {
        Id = marzbanUser.Id;
        Username = marzbanUser.Username;
        Expire = marzbanUser.Expire;
        Data_Limit = marzbanUser.Data_Limit;
        Sub_Updated_At = marzbanUser.Sub_Updated_At;
        Sub_Last_User_Agent = marzbanUser.Sub_Last_User_Agent;
        Online_At = marzbanUser.Online_At;
        OnHoldExpireDuration = marzbanUser.OnHoldExpireDuration;
        OnHoldTimeout = marzbanUser.OnHoldTimeout;
        Username = marzbanUser.Username;
        Status = marzbanUser.Status;
        Used_Traffic = marzbanUser.Used_Traffic;
        Lifetime_Used_Traffic = marzbanUser.Lifetime_Used_Traffic;
        Created_At = marzbanUser.Created_At;
        Links = marzbanUser.Links;
        Subscription_Url = marzbanUser.Subscription_Url;
        MarzbanVpnId = marzbanUser.MarzbanVpnId;
    }
}