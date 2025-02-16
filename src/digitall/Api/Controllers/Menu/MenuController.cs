﻿using System.Diagnostics;
using System.Net;
using Asp.Versioning;
using Application.Extensions;
using Application.Services.Interface.Menu;
using Domain.DTOs.Category;
using Domain.DTOs.Menu;
using Domain.Entities.Authorization;
using Domain.Entities.Category;
using Domain.Enums.Category;
using Domain.Enums;
using Domain.Enums.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Api.Filters;
using Api.Controllers.Base;
using Api.Utitlities;

namespace Api.Controllers.Menu;

[ApiVersion(1)]
public class MenuController : BaseController
{
    #region constructor

    private readonly IMenuService _menuService;

    public MenuController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    #endregion

    #region add

    /// <summary>
    /// add menu for front developer
    /// </summary>
    /// <param name="menu"></param>
    /// <returns></returns>
    [PermissionChecker("AddMenus")]
    [HttpPost]
    [ProducesDefaultResponseType]
    public async Task<ApiResult> AddMenus([FromBody] AddMenuDto menu)
    {
        AddMenuResult response = await _menuService.AddMenuAsync(menu, User.GetId());
        return response switch
        {
            AddMenuResult.Exists => new ApiResult(false, ApiResultStatusCode.Duplicate, "این منو تکراری است"),
            AddMenuResult.Success => new ApiResult(true, ApiResultStatusCode.Success, "عملیات با موفقیت انجام شد"),
            _ => Ok()
        };
    }

    #endregion

    #region get

    /// <summary>
    /// receive menus using the token sent in the header
    /// </summary>
    /// <returns>List MenuDto</returns>
    [PermissionChecker("GetMenus")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<List<MenuDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult<IEnumerable<MenuDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesDefaultResponseType]
    public async Task<ApiResult<List<MenuDto>>> GetMenus()
    {
        List<MenuDto> menus = await _menuService.GetMenuByUserId(User.GetId());
        return Ok(menus);
    }


    /// <summary>
    /// get all menu
    /// </summary>
    /// <returns>List MenuDto</returns>
    [PermissionChecker("GetAllMenus")]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<List<MenuDto>>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ApiResult), (int)HttpStatusCode.OK)]
    [ProducesDefaultResponseType]
    public async Task<ApiResult<List<MenuDto>>> GetAllMenus()
    {
        List<MenuDto> menus = await _menuService.GetMenus();
        return Ok(menus);
    }

    #endregion
}