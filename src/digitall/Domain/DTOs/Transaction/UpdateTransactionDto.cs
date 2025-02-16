﻿using System.ComponentModel.DataAnnotations;
using Domain.Enums.Transaction;
using Microsoft.AspNetCore.Http;

namespace Domain.DTOs.Transaction;

public class UpdateTransactionDto
{
    [Display(Name = "عنوان تراکنش")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد")]
    public string? Title { get; set; }

    [Display(Name = "مبلغ تراکش")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    // [MinLength(500000, ErrorMessage = "{0} نمیتواند کمتر از {1} مبلغ باشد")]
    // [MaxLength(3000000, ErrorMessage = "{0} نمیتواند بیشتر از {1} مبلغ باشد")]
    public long? Price { get; set; }

    [Display(Name = "توضیحات تراکنش")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد")]
    public string? Description { get; set; }

    [Display(Name = "نوع تراکنش")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    public TransactionType TransactionType { get; set; }

    [Display(Name = "نام صاحب حساب")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد")]
    public string AccountName { get; set; }

    [Display(Name = "زمان تراکنش")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    public DateTime TransactionTime { get; set; }

    [Display(Name = "شماره کارت")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    public long CardNumber { get; set; }

    [Display(Name = "نام بانک")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد")]
    public string? BankName { get; set; }

    [Display(Name = "عکس تراکنش")] public IFormFile? AvatarTransaction { get; set; }
}