﻿using System.Net;
using Api.Controllers.Base;
using Api.Filters;
using Api.Utitlities;
using Asp.Versioning;
using Application.Extensions;
using Application.Services.Implementation.Agent;
using Application.Services.Interface.Transaction;
using Domain.DTOs.Transaction;
using Domain.Enums;
using Domain.Enums.Transaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Update.Internal;

namespace Api.Controllers.Transaction;

/// <summary>
/// for transaction services 
/// </summary>
/// <param name="transactionService"></param>
[ApiVersion(1)]
public class TransactionController(ITransactionService transactionService) : BaseController
{
    /// <summary>
    /// add transaction for user
    /// </summary>
    [PermissionChecker("AddTransaction")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
    [ProducesDefaultResponseType]
    public async Task<ApiResult> AddTransaction([FromForm] AddTransactionDto transaction)
    {
        AddTransactionResult response = await transactionService.AddTransactionAsync(transaction, User.GetId());

        return response switch
        {
            AddTransactionResult.Success =>
                new ApiResult(true, ApiResultStatusCode.Success,
                    "عملیات با موفقیت انجام شد منتظر برسی کارشناسان ما باشید"),
            AddTransactionResult.Error =>
                new ApiResult(false, ApiResultStatusCode.LogicError,
                    "عملیات با خطا مواجع شد با پشتیبانی تماس بگیرید"),
            _ => new NotFoundResult()
        };
    }


    /// <summary>
    /// after admin show transaction acept transaction
    /// </summary>
    /// <param name="transaction">TransactionStatus 1 = accepted, 2 not accepted, 3 waiting</param>
    [PermissionChecker("UpdateTransactionStatus")]
    [HttpPut]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
    [ProducesDefaultResponseType]
    public async Task<ApiResult> UpdateTransactionStatus([FromBody] UpdateTransactionStatusDto transaction)
    {
        await transactionService.UpdateTransactionStatusAsync(transaction, User.GetId());
        return Ok();
    }

    /// <summary>
    /// get transaction list by filter
    /// </summary>
    /// <returns>FilterTransaction</returns>
    [PermissionChecker("FilterTransaction")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult<FilterTransactionDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
    public async Task<ApiResult<FilterTransactionDto>> FilterTransaction([FromQuery] FilterTransactionDto transaction)
    {
        FilterTransactionDto response = await transactionService.FilterTransactionAsync(transaction,User.GetId());
        return Ok(response);
    }

    
    /// <summary>
    /// get transaction by id async
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [PermissionChecker("GetTransaction")]
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult<TransactionDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
    public async Task<ApiResult<TransactionDto>> GetTransaction(long id)
    {
        return Ok(await transactionService.GetTransactionByIdAsync(id));
    }

    /// <summary>
    /// add transaction detail
    /// </summary>
    /// <returns>FilterTransaction</returns>
    [PermissionChecker("AddTransactionDetails")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
    public async Task<ApiResult> AddTransactionDetail(
        [FromBody] AddTransactionDetailDto transaction)
    {
        await transactionService.AddTransactionDetailAsync(transaction, User.GetId());
        return Ok();
    }

    /// <summary>
    /// get transaciton detail list
    /// </summary>
    /// <returns>FilterTransaction</returns>
    [PermissionChecker("GetTransactionDetails")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult<TransactionDetailDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.NotFound)]
    [ProducesDefaultResponseType]
    public async Task<ApiResult<TransactionDetailDto>> GetTransactionDetail()
    {
        return Ok(await transactionService.GetTransactionDetailsByUserIdAsync(User.GetId()));
    }

    /// <summary>
    /// add increase transaction dto
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    [PermissionChecker("IncreaseBalance")]
    [HttpPost("{userId}")]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesDefaultResponseType]
    public async Task<ApiResult> IncreaseBalance(long userId,[FromBody] AddTransactionDto transaction)
    {
        transaction.Title = "افزایش دستی موجودی";
        transaction.TransactionTime = DateTime.Now;
        transaction.TransactionType = TransactionType.ManualIncrease;
        await transactionService.IncreaseUserAsync(transaction, userId, User.GetId());
        return Ok();
    }

    /// <summary>
    /// Decrease
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    [PermissionChecker("DecreaseBalance")]
    [HttpPost("{userId}")]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesDefaultResponseType]
    public async Task<ApiResult> DecreaseBalance(long userId,[FromBody] AddTransactionDto transaction)
    {
        transaction.Title = "کاهش دستی موجودی";
        transaction.TransactionTime = DateTime.Now;
        transaction.TransactionType = TransactionType.ManualDecrease;
        await transactionService.DecreaseUserAsync(transaction, userId, User.GetId());
        return Ok();
    }
}