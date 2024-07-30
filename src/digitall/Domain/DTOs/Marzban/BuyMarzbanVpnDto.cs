﻿using System.ComponentModel.DataAnnotations;
using Domain.Entities.Marzban;
using Domain.Exceptions;

namespace Domain.DTOs.Marzban;

public class BuyMarzbanVpnDto
{
    [Display(Name = "تعداد")] public int Count { get; set; }

    [MaxLength(50)] [Required] public string? Title { get; set; }
    public long MarzbanVpnId { get; set; }
    public int TotalGb { get; set; } = 1;
    public int TotalDay { get; set; } = 1;
    public long? MarzbanVpnTemplateId { get; set; } = null;

    public long CountingPrice(MarzbanVpn vpn)
    {
        if (TotalDay > vpn.DayMax || TotalDay < vpn.DayMin)
            throw new BadRequestException("نمیتواند اینقدر روز برای vpn باشد");

        if (TotalGb > vpn.GbMax || TotalGb < vpn.GbMin)
            throw new BadRequestException("نمیتواند اینقدر گیگ برای vpn باشد");

        long price = (TotalGb * vpn.GbPrice) + (TotalDay * vpn.DayPrice);

        return price;
    }

    public long CountingPrice(GetMarzbanVpnDto? vpn)
    {
        if (TotalDay > vpn.DayMax || TotalDay < vpn.DayMin)
            throw new BadRequestException("نمیتواند اینقدر روز برای vpn باشد");

        if (TotalGb > vpn.GbMax || TotalGb < vpn.GbMin)
            throw new BadRequestException("نمیتواند اینقدر گیگ برای vpn باشد");

        long price = (TotalGb * vpn.GbPrice) + (TotalDay * vpn.DayPrice);

        return price;
    }
}