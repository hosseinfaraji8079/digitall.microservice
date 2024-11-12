﻿using Domain.Entities.Account;
using Domain.Entities.Apple;

namespace Domain.DTOs.Apple;

public class AppleIdDto
{
    public AppleIdDto()
    {
    }
    
    
    public AppleIdDto(AppleId appleId,User? createBy = null,User? modifyBy = null)
    {
        Id = appleId.Id;
        Email = appleId.Email;
        Phone = appleId.Phone;
        Password = appleId.Password;
        BirthDay = appleId.BirthDay;
        Question1 = appleId.Question1;
        Answer1 = appleId.Answer1;
        Question2 = appleId.Question2;
        Answer2 = appleId.Answer2;
        Question3 = appleId.Question3;
        Answer3 = appleId.Answer3;
        UserId = appleId.UserId;
        OrderId = appleId.OrderId;
        CreateBy = createBy?.FirstName + "" + createBy?.LastName;
        ModifyBy = modifyBy?.FirstName + "" + modifyBy?.LastName;
        CreateDate = appleId.CreateDate;
        ModifiedDate = appleId.ModifiedDate;
        Status = appleId.UserId != null ? "not-active" : "active";
    }

    
    public long Id { get; set; }
    public string Status { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Password { get; set; }
    public DateTime? BirthDay { get; set; }
    public string? Question1 { get; set; }
    public string? Answer1 { get; set; }
    public string? Question2 { get; set; }
    public string? Answer2 { get; set; }
    public string? Question3 { get; set; }
    public string? Answer3 { get; set; }
    public long? UserId { get; set; }
    public long? OrderId { get; set; }

    public string? CreateBy { get; set; }
    
    public string? ModifyBy { get; set; }
    
    public DateTime CreateDate { get; set; }
    
    public DateTime ModifiedDate { get; set; }
}