﻿using System.ComponentModel.DataAnnotations;
using Domain.Enums.Product;

namespace Domain.DTOs.Product;

public class AddProductDto
{
    [Display(Name = "نام محصول")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    [MaxLength(200, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد")]
    public string? ProductName { get; set; }

    [Display(Name = "قیمت محصول")]
    [Required(ErrorMessage = "لطفا {0} را وارد کنید")]
    public long Price { get; set; }

    [Display(Name = "شناسه دسته بندی")] public long CategoryId { get; set; }

    [Display(Name = "توضیحات")]
    [MaxLength(1000, ErrorMessage = "{0} نمی تواند بیشتر از {1} کاراکتر باشد")]
    public string? Description { get; set; }

    //[Display(Name = "تصویر محصول")]
    //public IFormFile? Avatar { get; set; }

    public ProductType ProductType { get; set; }
}